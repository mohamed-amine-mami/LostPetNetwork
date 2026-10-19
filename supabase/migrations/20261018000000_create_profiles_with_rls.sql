-- LostPetNetwork: user profiles linked to Supabase Auth, protected by Row Level Security.
-- Not yet applied to the hosted project. Review, then apply via the Supabase dashboard
-- SQL editor, the Supabase CLI, or the MCP apply_migration tool once approved.

create table public.profiles (
  id uuid primary key references auth.users (id) on delete cascade,
  display_name text not null check (char_length(display_name) between 2 and 50),
  avatar_url text,
  phone text check (phone is null or char_length(phone) <= 30),
  city text check (city is null or char_length(city) <= 80),
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

alter table public.profiles enable row level security;

-- Least privilege: clients get no default access; grant only what the app needs.
revoke all on public.profiles from anon, authenticated;
grant select on public.profiles to authenticated;
grant update (display_name, avatar_url, phone, city) on public.profiles to authenticated;

-- A signed-in user can read and edit only their own profile.
create policy "profiles_select_own" on public.profiles
  for select to authenticated
  using ((select auth.uid()) = id);

create policy "profiles_update_own" on public.profiles
  for update to authenticated
  using ((select auth.uid()) = id)
  with check ((select auth.uid()) = id);

-- No insert/delete policies: profiles are created by the trigger below and
-- removed automatically when the auth user is deleted (on delete cascade).

create function public.set_updated_at()
returns trigger
language plpgsql
set search_path = ''
as $$
begin
  new.updated_at = now();
  return new;
end;
$$;

create trigger profiles_set_updated_at
  before update on public.profiles
  for each row execute function public.set_updated_at();

-- Create the profile row automatically when a user registers.
create function public.handle_new_user()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
declare
  name_value text;
begin
  name_value := left(
    trim(coalesce(nullif(trim(new.raw_user_meta_data ->> 'display_name'), ''), split_part(new.email, '@', 1))),
    50
  );
  if char_length(name_value) < 2 then
    name_value := 'Neighbour';
  end if;
  insert into public.profiles (id, display_name) values (new.id, name_value);
  return new;
end;
$$;

revoke execute on function public.handle_new_user() from public, anon, authenticated;
revoke execute on function public.set_updated_at() from public, anon, authenticated;

create trigger on_auth_user_created
  after insert on auth.users
  for each row execute function public.handle_new_user();
