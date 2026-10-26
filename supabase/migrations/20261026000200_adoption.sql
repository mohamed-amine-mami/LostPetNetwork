-- LostPetNetwork: adoption listings, adopter home profiles and adoption requests.
-- Applied to the hosted project on 2026-10-26.

create table public.adoption_listings (
  id uuid primary key default gen_random_uuid(),
  owner_id uuid not null default auth.uid() references auth.users (id) on delete cascade,
  owner_name text not null check (char_length(owner_name) between 2 and 50),
  species text not null check (species in ('dog', 'cat')),
  name text not null check (char_length(name) between 1 and 40),
  age_months integer not null check (age_months between 0 and 360),
  size text not null check (size in ('small', 'medium', 'large')),
  energy text not null check (energy in ('low', 'medium', 'high')),
  good_with_kids boolean not null default true,
  good_with_pets boolean not null default true,
  needs_garden boolean not null default false,
  description text check (description is null or char_length(description) <= 500),
  location_text text not null check (char_length(location_text) between 3 and 120),
  latitude double precision check (latitude between -90 and 90),
  longitude double precision check (longitude between -180 and 180),
  pet_avatar jsonb check (pet_avatar is null or pg_column_size(pet_avatar) <= 1024),
  photo_path text check (photo_path is null or char_length(photo_path) <= 200),
  status text not null default 'available' check (status in ('available', 'adopted')),
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create index adoption_listings_created_idx on public.adoption_listings (created_at desc);
create index adoption_listings_owner_idx on public.adoption_listings (owner_id);

alter table public.adoption_listings enable row level security;
revoke all on public.adoption_listings from anon, authenticated;
grant select, delete on public.adoption_listings to authenticated;
grant insert (owner_name, species, name, age_months, size, energy, good_with_kids, good_with_pets,
              needs_garden, description, location_text, latitude, longitude, pet_avatar, photo_path)
  on public.adoption_listings to authenticated;
grant update (status, name, age_months, size, energy, good_with_kids, good_with_pets, needs_garden,
              description, location_text, pet_avatar, photo_path)
  on public.adoption_listings to authenticated;

create policy "adoption_listings_select_signed_in" on public.adoption_listings
  for select to authenticated using (true);
create policy "adoption_listings_insert_own" on public.adoption_listings
  for insert to authenticated with check ((select auth.uid()) = owner_id);
create policy "adoption_listings_update_own" on public.adoption_listings
  for update to authenticated
  using ((select auth.uid()) = owner_id) with check ((select auth.uid()) = owner_id);
create policy "adoption_listings_delete_own" on public.adoption_listings
  for delete to authenticated using ((select auth.uid()) = owner_id);

create trigger adoption_listings_set_updated_at
  before update on public.adoption_listings
  for each row execute function public.set_updated_at();

-- The adopter's home, used to compute a compatibility score. Private to the user.
create table public.adopter_profiles (
  user_id uuid primary key default auth.uid() references auth.users (id) on delete cascade,
  home_type text not null check (home_type in ('apartment', 'house')),
  home_size_m2 integer not null check (home_size_m2 between 10 and 1000),
  has_garden boolean not null default false,
  has_kids boolean not null default false,
  has_other_pets boolean not null default false,
  activity text not null check (activity in ('low', 'medium', 'high')),
  updated_at timestamptz not null default now()
);

alter table public.adopter_profiles enable row level security;
revoke all on public.adopter_profiles from anon, authenticated;
grant select, delete on public.adopter_profiles to authenticated;
grant insert (home_type, home_size_m2, has_garden, has_kids, has_other_pets, activity)
  on public.adopter_profiles to authenticated;
grant update (home_type, home_size_m2, has_garden, has_kids, has_other_pets, activity)
  on public.adopter_profiles to authenticated;

create policy "adopter_profiles_select_own" on public.adopter_profiles
  for select to authenticated using ((select auth.uid()) = user_id);
create policy "adopter_profiles_insert_own" on public.adopter_profiles
  for insert to authenticated with check ((select auth.uid()) = user_id);
create policy "adopter_profiles_update_own" on public.adopter_profiles
  for update to authenticated
  using ((select auth.uid()) = user_id) with check ((select auth.uid()) = user_id);
create policy "adopter_profiles_delete_own" on public.adopter_profiles
  for delete to authenticated using ((select auth.uid()) = user_id);

create trigger adopter_profiles_set_updated_at
  before update on public.adopter_profiles
  for each row execute function public.set_updated_at();

create table public.adoption_requests (
  id uuid primary key default gen_random_uuid(),
  listing_id uuid not null references public.adoption_listings (id) on delete cascade,
  applicant_id uuid not null default auth.uid() references auth.users (id) on delete cascade,
  message text check (message is null or char_length(message) <= 300),
  created_at timestamptz not null default now(),
  unique (listing_id, applicant_id)
);

alter table public.adoption_requests enable row level security;
revoke all on public.adoption_requests from anon, authenticated;
grant select on public.adoption_requests to authenticated;
grant insert (listing_id, message) on public.adoption_requests to authenticated;

create policy "adoption_requests_select_involved" on public.adoption_requests
  for select to authenticated
  using (
    (select auth.uid()) = applicant_id
    or exists (select 1 from public.adoption_listings l where l.id = listing_id and l.owner_id = (select auth.uid()))
  );
create policy "adoption_requests_insert_other_listing" on public.adoption_requests
  for insert to authenticated
  with check (
    (select auth.uid()) = applicant_id
    and exists (
      select 1 from public.adoption_listings l
      where l.id = listing_id and l.owner_id <> (select auth.uid()) and l.status = 'available'
    )
  );
