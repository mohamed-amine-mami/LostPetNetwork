-- LostPetNetwork: store each user's 3D avatar settings on their profile.
-- Applied to the hosted project on 2026-10-26.
--
-- The avatar is a few small indexes (skin, hair, outfit...) as JSON, never an image.
-- Only the owner can read or change it, using the profile policies that already exist.

alter table public.profiles
  add column avatar jsonb
  check (avatar is null or pg_column_size(avatar) <= 1024);

-- The earlier migration grants column-level UPDATE; the new column needs its own grant.
grant update (avatar) on public.profiles to authenticated;
