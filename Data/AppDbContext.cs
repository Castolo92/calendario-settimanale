using Microsoft.EntityFrameworkCore;

namespace CalendarioSettimanale.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Attivita> Attivita => Set<Attivita>();
    public DbSet<Pianificazione> Pianificazioni => Set<Pianificazione>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Pianificazione>(e =>
        {
            // Una sola attività per giorno e periodo
            e.HasIndex(p => new { p.Data, p.Periodo }).IsUnique();
            e.HasOne(p => p.Attivita)
                .WithMany(a => a.Pianificazioni)
                .HasForeignKey(p => p.AttivitaId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
