using Microsoft.EntityFrameworkCore;

namespace CalendarioSettimanale.Data;

public class CalendarioService(IDbContextFactory<AppDbContext> dbFactory)
{
    /// <summary>Notifica i componenti aperti (es. il calendario a muro) quando cambia qualcosa.</summary>
    public event Action? DatiCambiati;

    public static DateOnly InizioSettimana(DateOnly data)
    {
        int diff = ((int)data.DayOfWeek + 6) % 7; // lunedì = 0
        return data.AddDays(-diff);
    }

    public static DateOnly Oggi => DateOnly.FromDateTime(DateTime.Now);

    // ---------- Attività ----------

    public async Task<List<Attivita>> GetAttivitaAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        // Non carichiamo i byte dell'immagine: servono solo per l'URL
        return await db.Attivita
            .OrderBy(a => a.Nome)
            .Select(a => new Attivita
            {
                Id = a.Id,
                Nome = a.Nome,
                Colore = a.Colore,
                VersioneImmagine = a.VersioneImmagine,
                HaImmagine = a.Immagine != null
            })
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task SalvaAttivitaAsync(Attivita input, byte[]? nuovaImmagine, string? contentType, bool rimuoviImmagine)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        Attivita entita;
        if (input.Id == 0)
        {
            entita = new Attivita();
            db.Attivita.Add(entita);
        }
        else
        {
            entita = await db.Attivita.FirstAsync(a => a.Id == input.Id);
        }

        entita.Nome = input.Nome.Trim();
        entita.Colore = input.Colore;

        if (nuovaImmagine is not null)
        {
            entita.Immagine = nuovaImmagine;
            entita.ImmagineContentType = contentType;
            entita.VersioneImmagine++;
        }
        else if (rimuoviImmagine)
        {
            entita.Immagine = null;
            entita.ImmagineContentType = null;
            entita.VersioneImmagine++;
        }

        await db.SaveChangesAsync();
        DatiCambiati?.Invoke();
    }

    public async Task EliminaAttivitaAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.Attivita.Where(a => a.Id == id).ExecuteDeleteAsync();
        DatiCambiati?.Invoke();
    }

    public async Task<(byte[] Dati, string ContentType)?> GetImmagineAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var r = await db.Attivita
            .Where(a => a.Id == id && a.Immagine != null)
            .Select(a => new { a.Immagine, a.ImmagineContentType })
            .FirstOrDefaultAsync();
        return r is null ? null : (r.Immagine!, r.ImmagineContentType ?? "image/jpeg");
    }

    // ---------- Pianificazione ----------

    /// <summary>Restituisce per la settimana indicata la mappa (data, periodo) -> id attività.</summary>
    public async Task<Dictionary<(DateOnly, Periodo), int>> GetSettimanaAsync(DateOnly inizio)
    {
        var fine = inizio.AddDays(7);
        await using var db = await dbFactory.CreateDbContextAsync();
        var righe = await db.Pianificazioni
            .Where(p => p.Data >= inizio && p.Data < fine)
            .AsNoTracking()
            .ToListAsync();
        return righe.ToDictionary(p => (p.Data, p.Periodo), p => p.AttivitaId);
    }

    /// <summary>Imposta (o rimuove, se attivitaId è null) l'attività di un periodo.</summary>
    public async Task ImpostaAsync(DateOnly data, Periodo periodo, int? attivitaId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var esistente = await db.Pianificazioni.FirstOrDefaultAsync(p => p.Data == data && p.Periodo == periodo);

        if (attivitaId is null)
        {
            if (esistente is not null) db.Pianificazioni.Remove(esistente);
        }
        else if (esistente is null)
        {
            db.Pianificazioni.Add(new Pianificazione { Data = data, Periodo = periodo, AttivitaId = attivitaId.Value });
        }
        else
        {
            esistente.AttivitaId = attivitaId.Value;
        }

        await db.SaveChangesAsync();
        DatiCambiati?.Invoke();
    }

    /// <summary>Copia la pianificazione della settimana precedente su quella indicata (sovrascrivendo).</summary>
    public async Task CopiaSettimanaPrecedenteAsync(DateOnly inizio)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var prec = inizio.AddDays(-7);
        var sorgente = await db.Pianificazioni.Where(p => p.Data >= prec && p.Data < inizio).AsNoTracking().ToListAsync();
        await db.Pianificazioni.Where(p => p.Data >= inizio && p.Data < inizio.AddDays(7)).ExecuteDeleteAsync();
        db.Pianificazioni.AddRange(sorgente.Select(p => new Pianificazione
        {
            Data = p.Data.AddDays(7),
            Periodo = p.Periodo,
            AttivitaId = p.AttivitaId
        }));
        await db.SaveChangesAsync();
        DatiCambiati?.Invoke();
    }
}
