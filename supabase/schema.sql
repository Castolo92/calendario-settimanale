-- =====================================================================
--  Calendario settimanale - struttura del database Supabase
--  Da eseguire una volta in: Supabase > SQL Editor > New query > Run
-- =====================================================================

-- Attività (con immagine nel bucket "immagini")
create table if not exists public.attivita (
    id         bigint generated always as identity primary key,
    nome       text not null check (char_length(nome) between 1 and 80),
    colore     text not null default '#4f7cff' check (colore ~ '^#[0-9a-fA-F]{6}$'),
    immagine   text,
    creato_il  timestamptz not null default now()
);

-- Un'attività per ogni giorno e momento della giornata
-- periodo: 0 = mattina, 1 = pomeriggio, 2 = sera, 3 = notte
create table if not exists public.pianificazioni (
    id           bigint generated always as identity primary key,
    data         date not null,
    periodo      smallint not null check (periodo between 0 and 3),
    attivita_id  bigint not null references public.attivita (id) on delete cascade,
    unique (data, periodo)
);

create index if not exists pianificazioni_attivita_idx on public.pianificazioni (attivita_id);

-- ---------------------------------------------------------------------
--  Sicurezza: solo gli utenti che hanno fatto l'accesso possono leggere
--  e modificare. Chi non è collegato non vede nulla.
-- ---------------------------------------------------------------------
alter table public.attivita enable row level security;
alter table public.pianificazioni enable row level security;

drop policy if exists "utenti collegati" on public.attivita;
create policy "utenti collegati" on public.attivita
    for all to authenticated using (true) with check (true);

drop policy if exists "utenti collegati" on public.pianificazioni;
create policy "utenti collegati" on public.pianificazioni
    for all to authenticated using (true) with check (true);

-- Gli utenti anonimi non hanno alcun permesso sulle tabelle
revoke all on public.attivita, public.pianificazioni from anon;
grant select, insert, update, delete on public.attivita, public.pianificazioni to authenticated;

-- ---------------------------------------------------------------------
--  Immagini: bucket pubblico in lettura (i nomi dei file sono casuali,
--  quindi non si possono indovinare), scrittura solo per utenti collegati.
--  Limite 5 MB per file e solo immagini.
-- ---------------------------------------------------------------------
insert into storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
values ('immagini', 'immagini', true, 5242880, array['image/jpeg', 'image/png', 'image/webp'])
on conflict (id) do update
    set public = excluded.public,
        file_size_limit = excluded.file_size_limit,
        allowed_mime_types = excluded.allowed_mime_types;

drop policy if exists "immagini: caricamento" on storage.objects;
create policy "immagini: caricamento" on storage.objects
    for insert to authenticated with check (bucket_id = 'immagini');

drop policy if exists "immagini: modifica" on storage.objects;
create policy "immagini: modifica" on storage.objects
    for update to authenticated using (bucket_id = 'immagini');

drop policy if exists "immagini: eliminazione" on storage.objects;
create policy "immagini: eliminazione" on storage.objects
    for delete to authenticated using (bucket_id = 'immagini');
