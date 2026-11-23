namespace CalendarioSettimanale.Data;

public static class Formati
{
    /// <summary>Es. "23 – 29 novembre 2026" oppure "30 novembre – 6 dicembre 2026".</summary>
    public static string Settimana(DateOnly inizio)
    {
        var fine = inizio.AddDays(6);
        if (inizio.Month == fine.Month)
            return $"{inizio.Day} – {fine:d MMMM yyyy}";
        if (inizio.Year == fine.Year)
            return $"{inizio:d MMMM} – {fine:d MMMM yyyy}";
        return $"{inizio:d MMMM yyyy} – {fine:d MMMM yyyy}";
    }
}
