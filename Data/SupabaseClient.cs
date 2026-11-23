using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CalendarioSettimanale.Data;

/// <summary>Errore mostrato all'utente.</summary>
public class ErroreSupabase(string messaggio) : Exception(messaggio);

/// <summary>
/// Client minimo per le API REST di Supabase (database tramite PostgREST e Storage).
/// Ogni chiamata usa il token dell'utente collegato: le regole di sicurezza (RLS)
/// su Supabase permettono lettura e scrittura solo agli utenti autenticati.
/// </summary>
public class SupabaseClient
{
    public const string Bucket = "immagini";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly SupabaseConfig config;
    private readonly HttpClient http;
    private readonly Sessione sessione;

    public SupabaseClient(SupabaseConfig config, HttpClient http, Sessione sessione)
    {
        this.config = config;
        this.http = http;
        this.sessione = sessione;
        Attivita.BaseUrlImmagini = $"{config.Url}/storage/v1/object/public/{Bucket}";
    }

    // ---------- Database ----------

    public async Task<List<T>> SelectAsync<T>(string tabellaEQuery)
    {
        using var risposta = await InviaAsync(HttpMethod.Get, $"rest/v1/{tabellaEQuery}");
        return await risposta.Content.ReadFromJsonAsync<List<T>>(Json) ?? [];
    }

    public async Task<T> InsertAsync<T>(string tabella, T riga)
    {
        using var risposta = await InviaAsync(HttpMethod.Post, $"rest/v1/{tabella}",
            JsonContent.Create(riga, options: Json), "return=representation");
        var righe = await risposta.Content.ReadFromJsonAsync<List<T>>(Json);
        return righe is { Count: > 0 } ? righe[0] : riga;
    }

    public async Task InsertManyAsync<T>(string tabella, IReadOnlyCollection<T> righe)
    {
        if (righe.Count == 0) return;
        using var _ = await InviaAsync(HttpMethod.Post, $"rest/v1/{tabella}",
            JsonContent.Create(righe, options: Json), "return=minimal");
    }

    /// <summary>Inserisce o, se esiste già una riga con la stessa chiave <paramref name="onConflict"/>, la aggiorna.</summary>
    public async Task UpsertAsync<T>(string tabella, string onConflict, T riga)
    {
        using var _ = await InviaAsync(HttpMethod.Post, $"rest/v1/{tabella}?on_conflict={onConflict}",
            JsonContent.Create(riga, options: Json), "resolution=merge-duplicates,return=minimal");
    }

    public async Task UpdateAsync(string tabellaEFiltro, object valori)
    {
        using var _ = await InviaAsync(HttpMethod.Patch, $"rest/v1/{tabellaEFiltro}",
            JsonContent.Create(valori, options: Json), "return=minimal");
    }

    public async Task DeleteAsync(string tabellaEFiltro)
    {
        using var _ = await InviaAsync(HttpMethod.Delete, $"rest/v1/{tabellaEFiltro}");
    }

    // ---------- Storage ----------

    public async Task CaricaFileAsync(string nome, byte[] contenuto, string contentType)
    {
        var corpo = new ByteArrayContent(contenuto);
        corpo.Headers.ContentType = new(contentType);
        using var _ = await InviaAsync(HttpMethod.Post, $"storage/v1/object/{Bucket}/{Uri.EscapeDataString(nome)}", corpo);
    }

    public async Task EliminaFileAsync(string nome)
    {
        try
        {
            using var _ = await InviaAsync(HttpMethod.Delete, $"storage/v1/object/{Bucket}/{Uri.EscapeDataString(nome)}");
        }
        catch (ErroreSupabase)
        {
            // Un'immagine rimasta orfana non è un problema per l'utente
        }
    }

    // ---------- Comune ----------

    private async Task<HttpResponseMessage> InviaAsync(HttpMethod metodo, string percorso, HttpContent? corpo = null, string? prefer = null)
    {
        var token = await sessione.GetTokenAsync()
            ?? throw new ErroreSupabase("Devi accedere di nuovo.");

        var req = new HttpRequestMessage(metodo, $"{config.Url}/{percorso}") { Content = corpo };
        req.Headers.Add("apikey", config.Chiave);
        req.Headers.Authorization = new("Bearer", token);
        if (prefer is not null) req.Headers.Add("Prefer", prefer);

        HttpResponseMessage risposta;
        try
        {
            risposta = await http.SendAsync(req);
        }
        catch (HttpRequestException)
        {
            throw new ErroreSupabase("Connessione non riuscita. Controlla internet e riprova.");
        }

        if (risposta.IsSuccessStatusCode) return risposta;

        var dettaglio = await risposta.Content.ReadAsStringAsync();
        var stato = risposta.StatusCode;
        risposta.Dispose();

        if (stato == HttpStatusCode.Unauthorized)
        {
            await sessione.ScadutaAsync();
            throw new ErroreSupabase("Devi accedere di nuovo.");
        }
        throw new ErroreSupabase($"Operazione non riuscita ({(int)stato}). {Riassunto(dettaglio)}".Trim());
    }

    private static string Riassunto(string testo)
    {
        try
        {
            using var doc = JsonDocument.Parse(testo);
            foreach (var campo in new[] { "message", "msg", "error_description", "error" })
                if (doc.RootElement.TryGetProperty(campo, out var v) && v.ValueKind == JsonValueKind.String)
                    return v.GetString() ?? "";
        }
        catch (JsonException) { }
        return testo.Length > 200 ? testo[..200] : testo;
    }
}
