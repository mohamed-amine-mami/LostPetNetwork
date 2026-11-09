-- LostPetNetwork: directory of veterinary clinics shown in the Rescue section ("Vets near you").
--
-- Any signed-in user can read the directory. The app cannot add, change or delete clinics: rows are
-- loaded by the project team (SQL editor / migration), so nobody can inject fake clinics or phone numbers.
-- Source: test data collected by hand from public map listings; replace with a proper source later.

create table public.vet_clinics (
  id uuid primary key default gen_random_uuid(),
  name text not null check (char_length(name) between 2 and 120),
  address text check (address is null or char_length(address) <= 240),
  city text check (city is null or char_length(city) <= 80),
  governorate text check (governorate is null or char_length(governorate) <= 80),
  phone text check (phone is null or phone ~ '^\+?[0-9]{6,15}$'),
  latitude double precision not null check (latitude between -90 and 90),
  longitude double precision not null check (longitude between -180 and 180),
  opening_hours text check (opening_hours is null or char_length(opening_hours) <= 160),
  is_24_7 boolean not null default false,
  website text check (website is null or char_length(website) <= 300),
  maps_url text check (maps_url is null or char_length(maps_url) <= 600),
  source text not null default 'import' check (char_length(source) <= 40),
  created_at timestamptz not null default now()
);

-- The same clinic is never listed twice at the same spot.
create unique index vet_clinics_name_place_key
  on public.vet_clinics (lower(name), round(latitude::numeric, 4), round(longitude::numeric, 4));
create index vet_clinics_governorate_idx on public.vet_clinics (governorate, city);

alter table public.vet_clinics enable row level security;
revoke all on public.vet_clinics from anon, authenticated;
grant select on public.vet_clinics to authenticated;

create policy "vet_clinics_select_signed_in" on public.vet_clinics
  for select to authenticated using (true);
