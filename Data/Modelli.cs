using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CalendarioSettimanale.Data;

public enum Periodo
{
    Mattina = 0,
    Pomeriggio = 1,
    Sera = 2,
    Notte = 3
}

public static class PeriodoInfo
{
    public static readonly Periodo[] Tutti = Enum.GetValues<Periodo>();

    public static string Icona(Periodo p) => p switch
    {
        Periodo.Mattina => "☀️",
        Periodo.Pomeriggio => "🌤️",
        Periodo.Sera => "🌆",
        Periodo.Notte => "🌙",
        _ => ""
    };

    /// <summary>Periodo corrispondente all'ora indicata (06-12, 12-18, 18-22, 22-06).</summary>
    public static Periodo DaOra(int ora) => ora switch
    {
        >= 6 and < 12 => Periodo.Mattina,
        >= 12 and < 18 => Periodo.Pomeriggio,
        >= 18 and < 22 => Periodo.Sera,
        _ => Periodo.Notte
    };
}

/// <summary>Riga della tabella "attivita" su Supabase.</summary>
public class Attivita
{
    /// <summary>Indirizzo pubblico del bucket delle immagini, impostato all'avvio.</summary>
    public static string BaseUrlImmagini { get; set; } = "";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long Id { get; set; }

    [Required(ErrorMessage = "Il nome è obbligatorio")]
    [MaxLength(80)]
    public string Nome { get; set; } = "";

    [MaxLength(9)]
    public string Colore { get; set; } = "#4f7cff";

    /// <summary>Nome del file nel bucket "immagini" (es. "3f2a….jpg"), null se senza immagine.</summary>
    public string? Immagine { get; set; }

    [JsonIgnore]
    public string? UrlImmagine => Immagine is null ? null : $"{BaseUrlImmagini}/{Uri.EscapeDataString(Immagine)}";

    public Attivita Copia() => (Attivita)MemberwiseClone();
}

/// <summary>Riga della tabella "pianificazioni" su Supabase.</summary>
public class Pianificazione
{
    public DateOnly Data { get; set; }
    public Periodo Periodo { get; set; }
    public long AttivitaId { get; set; }
}
