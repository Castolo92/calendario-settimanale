using System.Globalization;
using CalendarioSettimanale;
using CalendarioSettimanale.Data;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var cultura = new CultureInfo("it-IT");
CultureInfo.DefaultThreadCurrentCulture = cultura;
CultureInfo.DefaultThreadCurrentUICulture = cultura;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Indirizzo e chiave pubblica ("publishable" / "anon") del progetto Supabase: vedi wwwroot/appsettings.json
var config = new SupabaseConfig(
    (builder.Configuration["Supabase:Url"] ?? "").Trim().TrimEnd('/'),
    (builder.Configuration["Supabase:Chiave"] ?? "").Trim());

builder.Services.AddSingleton(config);
builder.Services.AddSingleton(new HttpClient());
builder.Services.AddSingleton<Sessione>();
builder.Services.AddSingleton<SupabaseClient>();
builder.Services.AddSingleton<CalendarioService>();

await builder.Build().RunAsync();
