using System.Globalization;
using CalendarioSettimanale.Components;
using CalendarioSettimanale.Data;
using Microsoft.EntityFrameworkCore;

var cultura = new CultureInfo("it-IT");
CultureInfo.DefaultThreadCurrentCulture = cultura;
CultureInfo.DefaultThreadCurrentUICulture = cultura;

var builder = WebApplication.CreateBuilder(args);

var cartellaDati = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(cartellaDati);
var connectionString = builder.Configuration.GetConnectionString("Calendario")
    ?? $"Data Source={Path.Combine(cartellaDati, "calendario.db")}";

builder.Services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite(connectionString));
builder.Services.AddSingleton<CalendarioService>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();
    db.Database.EnsureCreated();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();

// Immagini delle attività (salvate nel database)
app.MapGet("/immagini/{id:int}", async (int id, CalendarioService svc, HttpContext ctx) =>
{
    var img = await svc.GetImmagineAsync(id);
    if (img is null) return Results.NotFound();
    // L'URL contiene la versione (?v=), quindi si può mettere in cache a lungo
    ctx.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
    return Results.File(img.Value.Dati, img.Value.ContentType);
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
