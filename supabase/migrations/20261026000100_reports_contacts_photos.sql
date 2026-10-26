-- LostPetNetwork: lost/found reports, contact requests and report photos.
-- Applied to the hosted project on 2026-10-26.
--
-- Access model
--   reports           every signed-in user can read; only the owner can create/edit/delete their own.
--   contact_requests  the requester and the report's owner can read; only a different signed-in user can create one.
--   report-photos     private bucket; signed-in users can read; each user writes only inside their own folder.
-- Nothing is readable or writable by anonymous (signed-out) users.

create table public.reports (
  id uuid primary key default gen_random_uuid(),
  owner_id uuid not null default auth.uid() references auth.users (id) on delete cascade,
  owner_name text not null check (char_length(owner_name) between 2 and 50),
  type text not null check (type in ('lost', 'found')),
  status text not null default 'active' check (status in ('active', 'reunited')),
  species text not null check (species in ('dog', 'cat', 'bird', 'other')),
  pet_name text check (pet_name is null or char_length(pet_name) <= 40),
  breed text check (breed is null or char_length(breed) <= 60),
  description text check (description is null or char_length(description) <= 500),
  location_text text not null check (char_length(location_text) between 3 and 120),
  latitude double precision check (latitude between -90 and 90),
  longitude double precision check (longitude between -180 and 180),
  occurred_at timestamptz not null default now(),
  pet_avatar jsonb check (pet_avatar is null or pg_column_size(pet_avatar) <= 1024),
  photo_path text check (photo_path is null or char_length(photo_path) <= 200),
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create index reports_created_idx on public.reports (created_at desc);
create index reports_owner_idx on public.reports (owner_id);
create index reports_geo_idx on public.reports (latitude, longitude);

alter table public.reports enable row level security;
revoke all on public.reports from anon, authenticated;
grant select, delete on public.reports to authenticated;
grant insert (owner_name, type, species, pet_name, breed, description, location_text,
              latitude, longitude, occurred_at, pet_avatar, photo_path)
  on public.reports to authenticated;
grant update (status, pet_name, breed, description, location_text, latitude, longitude,
              pet_avatar, photo_path)
  on public.reports to authenticated;

create policy "reports_select_signed_in" on public.reports
  for select to authenticated using (true);

create policy "reports_insert_own" on public.reports
  for insert to authenticated
  with check ((select auth.uid()) = owner_id);

create policy "reports_update_own" on public.reports
  for update to authenticated
  using ((select auth.uid()) = owner_id)
  with check ((select auth.uid()) = owner_id);

create policy "reports_delete_own" on public.reports
  for delete to authenticated
  using ((select auth.uid()) = owner_id);

create trigger reports_set_updated_at
  before update on public.reports
  for each row execute function public.set_updated_at();

create table public.contact_requests (
  id uuid primary key default gen_random_uuid(),
  report_id uuid not null references public.reports (id) on delete cascade,
  requester_id uuid not null default auth.uid() references auth.users (id) on delete cascade,
  message text check (message is null or char_length(message) <= 300),
  created_at timestamptz not null default now(),
  unique (report_id, requester_id)
);

create index contact_requests_report_idx on public.contact_requests (report_id);

alter table public.contact_requests enable row level security;
revoke all on public.contact_requests from anon, authenticated;
grant select on public.contact_requests to authenticated;
grant insert (report_id, message) on public.contact_requests to authenticated;

create policy "contact_requests_select_involved" on public.contact_requests
  for select to authenticated
  using (
    (select auth.uid()) = requester_id
    or exists (select 1 from public.reports r where r.id = report_id and r.owner_id = (select auth.uid()))
  );

-- A user can ask about an active report that belongs to someone else, once.
create policy "contact_requests_insert_other_report" on public.contact_requests
  for insert to authenticated
  with check (
    (select auth.uid()) = requester_id
    and exists (
      select 1 from public.reports r
      where r.id = report_id and r.owner_id <> (select auth.uid()) and r.status = 'active'
    )
  );

-- Report photos: private bucket, JPEG only, 2 MB max. Path format: <user id>/<report id>.jpg
insert into storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
values ('report-photos', 'report-photos', false, 2097152, array['image/jpeg']);

create policy "report_photos_read_signed_in" on storage.objects
  for select to authenticated
  using (bucket_id = 'report-photos');

create policy "report_photos_insert_own_folder" on storage.objects
  for insert to authenticated
  with check (bucket_id = 'report-photos' and (storage.foldername(name))[1] = (select auth.uid())::text);

create policy "report_photos_update_own_folder" on storage.objects
  for update to authenticated
  using (bucket_id = 'report-photos' and (storage.foldername(name))[1] = (select auth.uid())::text)
  with check (bucket_id = 'report-photos' and (storage.foldername(name))[1] = (select auth.uid())::text);

create policy "report_photos_delete_own_folder" on storage.objects
  for delete to authenticated
  using (bucket_id = 'report-photos' and (storage.foldername(name))[1] = (select auth.uid())::text);
