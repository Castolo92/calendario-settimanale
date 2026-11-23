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
    }

    /// <summary>Restituisce null se l'accesso è riuscito, altrimenti il messaggio di errore.</summary>
    public async Task<string?> AccediAsync(string email, string password)
    {
        var risposta = await PostAuthAsync("token?grant_type=password", new { email = email.Trim(), password });
        if (!risposta.IsSuccessStatusCode)
        {
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
