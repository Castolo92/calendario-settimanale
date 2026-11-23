using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;

namespace CalendarioSettimanale.Data;

public record SupabaseConfig(string Url, string Chiave)
{
    public bool Configurato => (Url.StartsWith("https://") || Url.StartsWith("http://localhost")) && Chiave.Length > 20;
}

/// <summary>
/// Accesso con email e password tramite Supabase Auth.
/// I token restano nel localStorage del dispositivo, così l'accesso si fa una volta sola
/// (anche sulla TV) e poi viene rinnovato automaticamente.
/// </summary>
public class Sessione(SupabaseConfig config, HttpClient http, IJSRuntime js)
{
    private const string ChiaveStorage = "calendario.sessione";
    private readonly SemaphoreSlim blocco = new(1, 1);
    private DatiSessione? dati;
    private bool inizializzata;

    public event Action? Cambiata;

    public bool Collegato => dati is not null;
    public string? Email => dati?.Email;

    /// <summary>Vero dopo un link di invito o di recupero: l'utente deve scegliere una password.</summary>
    public bool DeveImpostarePassword { get; private set; }

    /// <summary>Messaggio da mostrare se il link ricevuto via email non era valido.</summary>
    public string? ErroreLink { get; private set; }

    public async Task InizializzaAsync()
    {
        if (inizializzata) return;
        inizializzata = true;
        try
        {
            var json = await js.InvokeAsync<string?>("localStorage.getItem", ChiaveStorage);
            if (!string.IsNullOrEmpty(json))
                dati = JsonSerializer.Deserialize<DatiSessione>(json);
        }
        catch
        {
            dati = null; // storage non disponibile o dati rovinati: si rifà l'accesso
        }

        await GestisciLinkEmailAsync();
    }

    /// <summary>
    /// I link nelle email di Supabase (invito, recupero password, magic link) riportano al sito con
    /// i token nella parte dopo "#". Qui li leggiamo, colleghiamo l'utente e puliamo l'indirizzo.
    /// </summary>
    private async Task GestisciLinkEmailAsync()
    {
        string hash;
        try
        {
            hash = await js.InvokeAsync<string>("calendario.leggiHash");
        }
        catch
        {
            return;
        }
        if (string.IsNullOrEmpty(hash) || !(hash.Contains("access_token=") || hash.Contains("error=")))
            return;

        var parametri = hash.TrimStart('#').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .ToDictionary(p => p[0], p => p.Length > 1 ? Uri.UnescapeDataString(p[1].Replace('+', ' ')) : "");
        try { await js.InvokeVoidAsync("calendario.pulisciHash"); } catch { }

        if (parametri.ContainsKey("error"))
        {
            ErroreLink = parametri.GetValueOrDefault("error_code") == "otp_expired"
                ? "Il link dell'email è scaduto o è già stato usato. Chiedine uno nuovo con \"Password dimenticata?\"."
                : "Il link dell'email non è valido: " + parametri.GetValueOrDefault("error_description", "");
            return;
        }

        var token = new RispostaToken
        {
            AccessToken = parametri.GetValueOrDefault("access_token"),
            RefreshToken = parametri.GetValueOrDefault("refresh_token"),
            ExpiresAt = long.TryParse(parametri.GetValueOrDefault("expires_at"), out var at) ? at : null,
            ExpiresIn = long.TryParse(parametri.GetValueOrDefault("expires_in"), out var inn) ? inn : null,
        };
        if (token.AccessToken is null) return;

        // L'email non è nel link: la chiediamo a Supabase
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{config.Url}/auth/v1/user");
            req.Headers.Add("apikey", config.Chiave);
            req.Headers.Authorization = new("Bearer", token.AccessToken);
            using var r = await http.SendAsync(req);
            if (r.IsSuccessStatusCode) token.User = await r.Content.ReadFromJsonAsync<Utente>();
        }
        catch { /* l'email serve solo per mostrarla */ }

        var tipo = parametri.GetValueOrDefault("type");
        DeveImpostarePassword = tipo is "recovery" or "invite" or "signup";
        await SalvaAsync(token);
    }

    /// <summary>Imposta la password dell'utente collegato. Restituisce null se riuscito, altrimenti l'errore.</summary>
    public async Task<string?> ImpostaPasswordAsync(string password)
    {
        var token = await GetTokenAsync();
        if (token is null) return "Il collegamento è scaduto: chiedi un nuovo link con \"Password dimenticata?\".";

        using var req = new HttpRequestMessage(HttpMethod.Put, $"{config.Url}/auth/v1/user")
        {
            Content = JsonContent.Create(new { password })
        };
        req.Headers.Add("apikey", config.Chiave);
        req.Headers.Authorization = new("Bearer", token);
        using var risposta = await http.SendAsync(req);
        if (!risposta.IsSuccessStatusCode)
        {
            var dettaglio = await risposta.Content.ReadAsStringAsync();
            if (dettaglio.Contains("weak_password")) return "Password troppo debole: usa almeno 6 caratteri, meglio se con lettere e numeri.";
            if (dettaglio.Contains("same_password")) return "È uguale alla password attuale: scegline una diversa.";
            return $"Non sono riuscito a salvare la password ({(int)risposta.StatusCode}).";
        }
        DeveImpostarePassword = false;
        Cambiata?.Invoke();
        return null;
    }

    /// <summary>Invia l'email per scegliere una nuova password; il link riporta a <paramref name="indirizzoSito"/>.</summary>
    public async Task<string?> RecuperaPasswordAsync(string email, string indirizzoSito)
    {
        using var risposta = await PostAuthAsync($"recover?redirect_to={Uri.EscapeDataString(indirizzoSito)}", new { email = email.Trim() });
        if (risposta.IsSuccessStatusCode) return null;
        return (int)risposta.StatusCode == 429
            ? "Limite di email raggiunto: Supabase ne invia al massimo 2 all'ora. Riprova più tardi, oppure chiedi a chi gestisce l'app di impostarti la password dalla dashboard di Supabase."
            : $"Invio non riuscito ({(int)risposta.StatusCode}).";
    }

    public void ChiudiErroreLink() => ErroreLink = null;

    /// <summary>Restituisce null se l'accesso è riuscito, altrimenti il messaggio di errore.</summary>
    public async Task<string?> AccediAsync(string email, string password)
    {
        var risposta = await PostAuthAsync("token?grant_type=password", new { email = email.Trim(), password });
        if (!risposta.IsSuccessStatusCode)
        {
            var dettaglio = await risposta.Content.ReadAsStringAsync();
            if (dettaglio.Contains("email_not_confirmed"))
                return "Questa email non è ancora confermata: confermala dalla dashboard di Supabase (Authentication → Users).";
            return (int)risposta.StatusCode is 400 or 401
                ? "Email o password non corretti."
                : $"Accesso non riuscito ({(int)risposta.StatusCode}).";
        }
        await SalvaAsync(await risposta.Content.ReadFromJsonAsync<RispostaToken>());
        return null;
    }

    public async Task EsciAsync()
    {
        var token = dati?.AccessToken;
        DeveImpostarePassword = false;
        await SalvaAsync(null);
        if (token is null) return;
        try
        {
            await PostAuthAsync("logout", new { }, token);
        }
        catch { /* non importa: in locale la sessione è già chiusa */ }
    }

    /// <summary>Token valido per le chiamate, rinnovato se sta per scadere. Null se non collegato.</summary>
    public async Task<string?> GetTokenAsync()
    {
        await blocco.WaitAsync();
        try
        {
            if (dati is null) return null;
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() < dati.ScadeIl - 60) return dati.AccessToken;

            var risposta = await PostAuthAsync("token?grant_type=refresh_token", new { refresh_token = dati.RefreshToken });
            if (!risposta.IsSuccessStatusCode)
            {
                // Rinnovo rifiutato (es. utente disattivato): bisogna rifare l'accesso
                if ((int)risposta.StatusCode is 400 or 401 or 403) await SalvaAsync(null);
                else throw new HttpRequestException("Impossibile rinnovare l'accesso, riprova più tardi.");
                return null;
            }
            await SalvaAsync(await risposta.Content.ReadFromJsonAsync<RispostaToken>());
            return dati?.AccessToken;
        }
        finally
        {
            blocco.Release();
        }
    }

    /// <summary>Chiamato quando il server rifiuta il token: la sessione non è più valida.</summary>
    public Task ScadutaAsync() => SalvaAsync(null);

    private async Task SalvaAsync(RispostaToken? token)
    {
        dati = token?.AccessToken is null ? null : new DatiSessione(
            token.AccessToken,
            token.RefreshToken ?? "",
            token.ExpiresAt ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (token.ExpiresIn ?? 3600),
            token.User?.Email);
        try
        {
            if (dati is null) await js.InvokeVoidAsync("localStorage.removeItem", ChiaveStorage);
            else await js.InvokeVoidAsync("localStorage.setItem", ChiaveStorage, JsonSerializer.Serialize(dati));
        }
        catch { /* storage non disponibile: la sessione dura finché la pagina resta aperta */ }
        Cambiata?.Invoke();
    }

    private Task<HttpResponseMessage> PostAuthAsync(string percorso, object corpo, string? bearer = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"{config.Url}/auth/v1/{percorso}")
        {
            Content = JsonContent.Create(corpo)
        };
        req.Headers.Add("apikey", config.Chiave);
        if (bearer is not null) req.Headers.Authorization = new("Bearer", bearer);
        return http.SendAsync(req);
    }

    private record DatiSessione(string AccessToken, string RefreshToken, long ScadeIl, string? Email);

    private class RispostaToken
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
        [JsonPropertyName("expires_in")] public long? ExpiresIn { get; set; }
        [JsonPropertyName("expires_at")] public long? ExpiresAt { get; set; }
        [JsonPropertyName("user")] public Utente? User { get; set; }
    }

    private class Utente
    {
        [JsonPropertyName("email")] public string? Email { get; set; }
    }
}
