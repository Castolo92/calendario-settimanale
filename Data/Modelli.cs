using System.ComponentModel.DataAnnotations;

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

public class Attivita
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Il nome è obbligatorio")]
    [MaxLength(80)]
    public string Nome { get; set; } = "";

    [MaxLength(9)]
    public string Colore { get; set; } = "#4f7cff";

    public byte[]? Immagine { get; set; }

    [MaxLength(50)]
    public string? ImmagineContentType { get; set; }

    /// <summary>Incrementata a ogni cambio immagine, serve per invalidare la cache del browser.</summary>
    public int VersioneImmagine { get; set; }

    public List<Pianificazione> Pianificazioni { get; set; } = [];

    /// <summary>Valorizzato nelle query di elenco, dove i byte dell'immagine non vengono caricati.</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool HaImmagine { get; set; }

    public string? UrlImmagine => HaImmagine ? $"immagini/{Id}?v={VersioneImmagine}" : null;
}

public class Pianificazione
{
    public int Id { get; set; }
    public DateOnly Data { get; set; }
    public Periodo Periodo { get; set; }
    public int AttivitaId { get; set; }
    public Attivita Attivita { get; set; } = null!;
}
