# LostPetNetwork — Phase 2 Report (3D, map, real data, adoption, SOS, AR, AI)

Date: 2026-10-26. Covers everything added after the first commit (`af3670a`). Nothing here is committed yet.

## 1. What was added

| Area | What the user gets | Where |
|---|---|---|
| 3D user avatar | Create a low-poly character (skin, hair, eyes, outfit, accessory), turn it with a drag, save it to the account, see its portrait on Profile | `Scripts/UI/Avatar`, `AvatarCreatorScreen`, `Services/Avatar` |
| 3D pet look | Design a dog or cat (coat, markings, size, ears, collar); attached to reports and adoption listings; tilt-to-turn on a phone | `Scripts/UI/Pets`, `PetCreatorScreen` |
| Real map | Live Mapbox street map, photo pins at real coordinates, zoom, "my location", attribution | `MapView`, `MapScreen`, `Services/Map` |
| Real reports | Create/list/edit reports, photos in private storage, pet look, contact requests, "use my location" with street name | `SupabaseReportRepository` |
| Search | Newest / Nearest / Within 5 km, real distances | `ReportSearch`, `ExploreScreen` |
| Adoption | Pets for adoption with 3D picture, home questionnaire, explainable 0-100 compatibility score, request to adopt, list a pet | `Services/Adoption`, 4 screens, new "Adopt" tab |
| Emergency SOS | Send an alert with position, note and voice note; shake phone to open the form; rescuers see open alerts nearest first, claim, resolve, directions | `Services/Sos`, 3 screens, `ShakeToSos` |
| AR | Try a life-size pet at home with a space ring; avatar in AR; AR radar of nearby reports (GPS + compass); arrow guide to an SOS alert | `Scripts/AR`, `Scenes/AR` |
| AI matching | "Find possible matches" for a report (Gemini compares photos). Deployed and tested live (see section 5) | `AiMatchService`, `supabase/functions/match-pets` |

Sensors per team member: camera (AR placement, report photos), gyroscope/accelerometer (tilt to turn, shake to SOS), GPS + compass (map dot, AR radar, SOS guide), microphone (SOS voice note).

## 2. Backend (Supabase project `oipjwpraaapwduhpyrcs`)

Applied and verified: `profile_avatar`, `reports_contacts_photos`, `adoption`, `sos_alerts` (7 tables, RLS on all, buckets `report-photos` and `sos-audio`, both private).

- Security advisor after applying: one warning only, "Leaked password protection disabled" (a dashboard switch, see section 6).
- Cross-user rules were tested inside a rolled-back transaction: others can read but not edit or delete a report; one contact request per person; owners cannot contact themselves; no forged `owner_id`; adopter profiles are private; one open SOS alert per user; signed-out users get nothing.
- Also applied: `20261026000400_ai_match_calls.sql` (rate-limit log). Edge Function `match-pets` deployed with JWT required; Gemini key stored as the Supabase secret `GEMINI_API_KEY`. Model order: gemini-3.1-flash-lite, then gemini-3.8-flash, then gemini-3-flash-preview (gemini-2.5-flash is closed to new keys).

## 3. Test results

| Suite | Result |
|---|---|
| EditMode | 253 / 253 passed |
| PlayMode (Editor, demo data, fake sensors) | 57 passed, 5 skipped |
| Live tests against the real Supabase project | 5 / 5 passed when credentials are supplied in memory; skipped otherwise (they are the 5 skipped above) |
| Android build | Succeeded, 0 errors, 45.7 MB (`Builds/Android/LostPetNetwork.apk`, git-ignored) |

The live tests create a report with photo and 3D pet look, an adoption listing with home profile, an SOS alert with a voice note, and the avatar, read them back, then delete them. The database was checked afterwards: 0 leftover rows or files.

## 4. What is verified, and how

| Feature | Verified in Editor | Verified against the real database | Needs a real phone |
|---|---|---|---|
| Avatar and pet 3D | Yes (rendered and inspected) | Yes (avatar sync, pet look round trip) | Gyroscope tilt |
| Map | Yes (real Mapbox tiles downloaded, pins placed) | n/a | GPS dot accuracy |
| Reports, photos, contacts | Yes | Yes | Camera capture, gallery picker (not implemented) |
| Adoption | Yes | Yes | no |
| SOS | Yes (logic, shake algorithm, WAV codec) | Yes | Real shake, microphone, GPS |
| AR placement and radar | Yes, on AR Foundation's XR Simulation (open, place, measure, close, pins by bearing) | n/a | Everything about real tracking, depth, light, compass quality |
| AI matching | Yes | Yes (live function, real photos, 63/63 PlayMode) | n/a |

No Android phone was connected, so nothing was run on a device.

## 5. Needs your decision

1. **Commit and push** the phase 2 work (about 170 changed files, nothing committed since `af3670a`).
2. **Rotate the Gemini keys.** Two keys were pasted into the chat; replace the one stored as `GEMINI_API_KEY` with a fresh key.
3. The AI helper limits each user to 15 requests per hour. Photos are sent to Google's Gemini API to be compared, and the screen says so. A request can take 10 to 80 seconds when Google is busy (the app waits up to 2 minutes).

## 6. Manual actions and known limits

- Test on a real Android phone with ARCore: AR, GPS, compass, shake, microphone, tilt. Phone installs the APK even without ARCore (AR is optional and its buttons stay hidden).
- Supabase dashboard: turn on "Leaked password protection"; set the Reset password email template to include `{{ .Token }}`; add custom SMTP (built-in email is limited to about 2 per hour).
- Mapbox token: stored in a git-ignored file (`Assets/_Project/Resources/MapConfig.local.json`). A fresh clone has no map token and shows the stylised map until you add one (example file in `Assets/_Project/Config`). Restrict the token in the Mapbox dashboard.
- Accelerometer units differ between phones (g vs m/s²); the shake detector assumes g. Check on a phone.
- Gallery photo picker is still not implemented (camera only).
- SOS alerts and their voice notes cannot be deleted by the app (only resolved). The live SOS test leaves 3 small test `.wav` files in the `sos-audio` bucket; delete them in the dashboard (Storage).
- Compass accuracy is poor indoors; the AR radar is meant for outdoors.
- The Android system Back button and screen-state restore are handled by `ScreenManager`; AR scenes open on top of the main scene and give control back when closed.
- Tokens are still stored in unencrypted PlayerPrefs (from phase 1); a secure-storage plugin is still open.

## 7. How to run

1. Open the project in Unity 6000.6.6f1, open `Assets/_Project/Scenes/Main.unity`, press Play.
2. Sign in with a Supabase account (the Editor uses real data once signed in).
3. After changing UI code, run **LostPetNetwork ▸ Build App UI** (it regenerates `Main.unity` and the AR scenes; it also runs **Setup AR**).
