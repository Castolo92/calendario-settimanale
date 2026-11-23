# 🌈 La mia settimana

Calendario settimanale colorato pensato per i bambini: ogni giorno è diviso in
Mattina, Pomeriggio, Sera e Notte e ogni attività è mostrata con la sua immagine.

- **Calendario** (`/`): da tenere su TV o tablet. Evidenzia oggi e il momento attuale,
  segna con ✔ quello che è già passato e si aggiorna da solo ogni minuto.
- **Pianifica** (`/pianifica`): pensata per il telefono, tocchi l'attività per ogni momento.
- **Attività** (`/attivita`): crei le attività con nome, colore e immagine.
  Con **Importa immagini** ne crei tante in un colpo solo: il nome del file diventa il nome dell'attività
  (`01 - Parco_giochi.jpg` → "Parco giochi").

È un'app **Blazor WebAssembly**: gira nel browser ed è pubblicata gratis su **GitHub Pages**.
Dati e immagini stanno su **Supabase** (piano gratuito). Per vedere o modificare serve l'accesso con email e password,
che su ogni dispositivo si fa una volta sola.

## Configurazione (una volta sola)

### 1. Supabase
1. Crea un account su [supabase.com](https://supabase.com) e un nuovo progetto (regione consigliata: Europa).
2. **SQL Editor → New query**: incolla il contenuto di [`supabase/schema.sql`](supabase/schema.sql) e premi **Run**.
   Crea le tabelle, le regole di sicurezza e il contenitore delle immagini.
3. **Authentication → Users → Add user → Create new user**: inserisci email e password di chi userà l'app
   (spunta *Auto Confirm User*). Ripeti per ogni persona.
4. **Authentication → Sign In / Providers**: disattiva **Allow new users to sign up**,
   così nessun altro può crearsi un account.
5. **Project Settings → API Keys**: copia l'indirizzo del progetto (`https://xxxx.supabase.co`)
   e la chiave **publishable** (o *anon*) e mettili in [`wwwroot/appsettings.json`](wwwroot/appsettings.json):

   ```json
   { "Supabase": { "Url": "https://xxxx.supabase.co", "Chiave": "sb_publishable_..." } }
   ```

   Questa chiave è fatta per stare nel browser: da sola non dà accesso ai dati,
   perché le regole di sicurezza richiedono l'accesso con email e password.
   **Non usare mai la chiave `secret` / `service_role`.**

### 2. GitHub Pages
1. Su GitHub: **Settings → Pages → Build and deployment → Source: GitHub Actions**.
2. A ogni push su `main` il workflow [`.github/workflows/pages.yml`](.github/workflows/pages.yml)
   compila e pubblica il sito su `https://<utente>.github.io/<nome-repository>/`.

> Con un account GitHub gratuito, Pages funziona solo con repository **pubblici**.
> Il codice è visibile a tutti, ma dati e immagini restano su Supabase, protetti dall'accesso.

## Sviluppo in locale

Serve il [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
dotnet run --launch-profile http
```

e apri http://localhost:5183 (usa lo stesso progetto Supabase configurato in `appsettings.json`).

> Su Windows conviene tenere il progetto in un percorso corto (es. `C:\Progetti`):
> con percorsi molto lunghi la compilazione può fallire per il limite di 260 caratteri.

## Note

- Momenti della giornata: Mattina 6–12, Pomeriggio 12–18, Sera 18–22, Notte 22–6.
- Le immagini vengono ridotte nel browser (max 800 px) prima di essere caricate.
- Il piano gratuito di Supabase mette in pausa i progetti dopo circa una settimana senza alcun utilizzo;
  basta riattivarlo dalla dashboard. Con il calendario usato ogni giorno non succede.
