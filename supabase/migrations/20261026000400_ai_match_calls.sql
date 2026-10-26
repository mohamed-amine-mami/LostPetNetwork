-- LostPetNetwork: a small log used to limit how often one user can ask the AI to compare photos
-- (each request costs money on the Gemini key). Applied to project oipjwpraaapwduhpyrcs on 2026-10-26.

create table public.ai_match_calls (
  id bigint generated always as identity primary key,
  user_id uuid not null default auth.uid() references auth.users (id) on delete cascade,
  called_at timestamptz not null default now()
);

create index ai_match_calls_user_time_idx on public.ai_match_calls (user_id, called_at desc);

alter table public.ai_match_calls enable row level security;
revoke all on public.ai_match_calls from anon, authenticated;
grant select, insert on public.ai_match_calls to authenticated;

create policy "ai_match_calls_select_own" on public.ai_match_calls
  for select to authenticated using ((select auth.uid()) = user_id);
create policy "ai_match_calls_insert_own" on public.ai_match_calls
  for insert to authenticated with check ((select auth.uid()) = user_id);
