-- LostPetNetwork: emergency SOS alerts for injured or trapped animals.
-- Applied to the hosted project on 2026-10-26.
--
-- Any signed-in user can raise an alert and read open alerts (so nearby neighbours can help).
-- The reporter can resolve their alert; a responder can claim an unclaimed alert and resolve it.
-- Each user can have at most one open alert at a time, which limits spam.
-- The app polls for new alerts (no realtime channel is enabled).

create table public.sos_alerts (
  id uuid primary key default gen_random_uuid(),
  reporter_id uuid not null default auth.uid() references auth.users (id) on delete cascade,
  reporter_name text not null check (char_length(reporter_name) between 2 and 50),
  latitude double precision not null check (latitude between -90 and 90),
  longitude double precision not null check (longitude between -180 and 180),
  note text check (note is null or char_length(note) <= 300),
  audio_path text check (audio_path is null or char_length(audio_path) <= 200),
  status text not null default 'open' check (status in ('open', 'responding', 'resolved')),
  responder_id uuid references auth.users (id) on delete set null,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create index sos_alerts_status_idx on public.sos_alerts (status, created_at desc);
create unique index sos_alerts_one_open_per_user on public.sos_alerts (reporter_id) where status in ('open', 'responding');

alter table public.sos_alerts enable row level security;
revoke all on public.sos_alerts from anon, authenticated;
grant select on public.sos_alerts to authenticated;
grant insert (reporter_name, latitude, longitude, note, audio_path) on public.sos_alerts to authenticated;
grant update (status, responder_id) on public.sos_alerts to authenticated;

create policy "sos_alerts_select_signed_in" on public.sos_alerts
  for select to authenticated using (true);

create policy "sos_alerts_insert_own" on public.sos_alerts
  for insert to authenticated with check ((select auth.uid()) = reporter_id);

create policy "sos_alerts_update_involved" on public.sos_alerts
  for update to authenticated
  using (
    (select auth.uid()) = reporter_id
    or (select auth.uid()) = responder_id
    or (responder_id is null and status = 'open')
  )
  with check (
    (select auth.uid()) = reporter_id
    or (select auth.uid()) = responder_id
  );

create trigger sos_alerts_set_updated_at
  before update on public.sos_alerts
  for each row execute function public.set_updated_at();

-- Optional voice note recorded with the alert. Path format: <user id>/<alert id>.wav
insert into storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
values ('sos-audio', 'sos-audio', false, 3145728, array['audio/wav', 'audio/x-wav']);

create policy "sos_audio_read_signed_in" on storage.objects
  for select to authenticated using (bucket_id = 'sos-audio');
create policy "sos_audio_insert_own_folder" on storage.objects
  for insert to authenticated
  with check (bucket_id = 'sos-audio' and (storage.foldername(name))[1] = (select auth.uid())::text);
create policy "sos_audio_delete_own_folder" on storage.objects
  for delete to authenticated
  using (bucket_id = 'sos-audio' and (storage.foldername(name))[1] = (select auth.uid())::text);
