namespace CalendarioSettimanale.Data;

public class CalendarioService(SupabaseClient db)
{
    public static DateOnly InizioSettimana(DateOnly data)
    {
        int diff = ((int)data.DayOfWeek + 6) % 7; // lunedì = 0
        return data.AddDays(-diff);
    }

    public static DateOnly Oggi => DateOnly.FromDateTime(DateTime.Now);

    private static string D(DateOnly d) => d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    // ---------- Attività ----------

    public Task<List<Attivita>> GetAttivitaAsync() =>
        db.SelectAsync<Attivita>("attivita?select=id,nome,colore,immagine&order=nome.asc");

    public async Task SalvaAttivitaAsync(Attivita input, byte[]? nuovaImmagine, string? contentType, bool rimuoviImmagine)
    {
        var vecchiaImmagine = input.Immagine;
        string? immagine = input.Immagine;

        if (nuovaImmagine is not null && contentType is not null)
        {
            // Nome sempre nuovo: così il browser non mostra mai la versione vecchia dalla cache
            var estensione = contentType switch { "image/png" => "png", "image/webp" => "webp", _ => "jpg" };
            immagine = $"{Guid.NewGuid():N}.{estensione}";
            await db.CaricaFileAsync(immagine, nuovaImmagine, contentType);
        }
        else if (rimuoviImmagine)
        {
            immagine = null;
        }

        var valori = new { nome = input.Nome.Trim(), colore = input.Colore, immagine };
        if (input.Id == 0)
            await db.InsertAsync("attivita", new Attivita { Nome = valori.nome, Colore = valori.colore, Immagine = immagine });
        else
            await db.UpdateAsync($"attivita?id=eq.{input.Id}", valori);

        if (vecchiaImmagine is not null && vecchiaImmagine != immagine)
            await db.EliminaFileAsync(vecchiaImmagine);
    }

    public async Task EliminaAttivitaAsync(Attivita attivita)
    {
        // Le pianificazioni collegate vengono cancellate dal database (on delete cascade)
        await db.DeleteAsync($"attivita?id=eq.{attivita.Id}");
        if (attivita.Immagine is not null) await db.EliminaFileAsync(attivita.Immagine);
    }

    // ---------- Pianificazione ----------

    /// <summary>Restituisce per la settimana indicata la mappa (data, periodo) -> id attività.</summary>
    public async Task<Dictionary<(DateOnly, Periodo), long>> GetSettimanaAsync(DateOnly inizio)
    {
        var righe = await db.SelectAsync<Pianificazione>(
            $"pianificazioni?select=data,periodo,attivita_id&data=gte.{D(inizio)}&data=lt.{D(inizio.AddDays(7))}");
        return righe.ToDictionary(p => (p.Data, p.Periodo), p => p.AttivitaId);
    }

    /// <summary>Imposta (o rimuove, se attivitaId è null) l'attività di un periodo.</summary>
    public Task ImpostaAsync(DateOnly data, Periodo periodo, long? attivitaId) =>
        attivitaId is null
            ? db.DeleteAsync($"pianificazioni?data=eq.{D(data)}&periodo=eq.{(int)periodo}")
            : db.UpsertAsync("pianificazioni", "data,periodo",
                new Pianificazione { Data = data, Periodo = periodo, AttivitaId = attivitaId.Value });

    /// <summary>Copia la pianificazione della settimana precedente su quella indicata (sovrascrivendo).</summary>
    public async Task CopiaSettimanaPrecedenteAsync(DateOnly inizio)
    {
        var prec = inizio.AddDays(-7);
        var sorgente = await db.SelectAsync<Pianificazione>(
            $"pianificazioni?select=data,periodo,attivita_id&data=gte.{D(prec)}&data=lt.{D(inizio)}");
        await db.DeleteAsync($"pianificazioni?data=gte.{D(inizio)}&data=lt.{D(inizio.AddDays(7))}");
        await db.InsertManyAsync("pianificazioni", sorgente
            .Select(p => new Pianificazione { Data = p.Data.AddDays(7), Periodo = p.Periodo, AttivitaId = p.AttivitaId })
            .ToList());
    }
}
