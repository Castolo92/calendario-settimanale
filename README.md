# Calendario settimanale (Blazor, .NET 10)

Calendario settimanale colorato pensato per i bambini: ogni giorno è diviso in
Mattina, Pomeriggio, Sera e Notte e ogni attività è mostrata con la sua immagine.

## Requisiti

- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0)
- Git (per scaricare il progetto)

## Avvio

```powershell
git clone <indirizzo-del-repository>
cd <cartella-del-repository>
dotnet run --launch-profile http
```

> Su Windows conviene clonare in un percorso corto (es. `C:\Progetti`): con percorsi
> molto lunghi la compilazione può fallire per il limite di 260 caratteri.

Poi apri http://localhost:5183. Per usarlo dal cellulare sulla stessa rete:

```powershell
dotnet run --launch-profile http --urls http://0.0.0.0:5183
```

e apri `http://<IP-del-PC>:5183/pianifica` dal telefono (potrebbe servire aprire la porta nel firewall di Windows).

## Pagine

| Pagina | URL | Uso |
|---|---|---|
| Calendario | `/` | Settimana corrente, giorni × Mattina/Pomeriggio/Sera/Notte. Pensata per schermo grande: occupa tutta la finestra, evidenzia oggi e il periodo attuale, si aggiorna da sola quando qualcuno modifica la pianificazione. Su tablet e telefono si riorganizza. |
| Pianifica | `/pianifica` | Ottimizzata per il cellulare: scegli il giorno, poi tocca l'attività per ciascun periodo (salva subito). C'è anche "Copia settimana precedente". |
| Attività | `/attivita` | Crea, modifica ed elimina attività con nome, colore e immagine. L'immagine viene ridimensionata nel browser (max 800 px) prima dell'invio, quindi vanno bene anche le foto del telefono. |

## Dati

Database SQLite in `App_Data/calendario.db`, creato automaticamente al primo avvio (immagini comprese).
Per usare un altro percorso imposta `ConnectionStrings:Calendario` in `appsettings.json`.

Periodi: Mattina 6–12, Pomeriggio 12–18, Sera 18–22, Notte 22–6 (usati per evidenziare il periodo attuale).
