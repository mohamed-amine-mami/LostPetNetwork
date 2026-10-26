// match-pets: ranks found/lost reports that may be the same animal as a given report, using Gemini.
//
// The Gemini key lives only here, as the secret GEMINI_API_KEY (Supabase dashboard > Edge Functions > Secrets).
// The app calls this function with the signed-in user's token, so Row Level Security decides which reports
// and photos the user may see. Photos are sent to Google's Gemini API to be compared.
//
// Request:  POST { "report_id": "<uuid>" }
// Response: { "matches": [{ "id", "score", "reason" }], "model": "..." }
// Errors:   { "error": "<code>", "message": "<text for the user>" } with 400/401/404/429/502/503.

import "jsr:@supabase/functions-js/edge-runtime.d.ts";

const SUPABASE_URL = Deno.env.get("SUPABASE_URL")!;
const GEMINI_API_KEY = Deno.env.get("GEMINI_API_KEY") ?? "";
const GEMINI_MODEL = Deno.env.get("GEMINI_MODEL") ?? "gemini-3.1-flash-lite";
// Tried in order when the main model is busy (503) or unavailable (404).
const FALLBACK_MODELS = ["gemini-3.8-flash", "gemini-3-flash-preview"];

const MAX_CANDIDATES = 8;
const MAX_DISTANCE_KM = 40;
const MAX_CALLS_PER_HOUR = 15;
const MAX_IMAGE_BYTES = 1_500_000;

const CORS = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, apikey, content-type, x-client-info",
  "Access-Control-Allow-Methods": "POST, OPTIONS",
};

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status, headers: { ...CORS, "Content-Type": "application/json" } });
}

function fail(status: number, error: string, message: string): Response {
  return json({ error, message }, status);
}

interface Report {
  id: string;
  owner_id: string;
  type: string;
  species: string;
  pet_name: string | null;
  breed: string | null;
  description: string | null;
  location_text: string;
  latitude: number | null;
  longitude: number | null;
  photo_path: string | null;
  status: string;
}

function km(a: Report, b: Report): number | null {
  if (a.latitude == null || a.longitude == null || b.latitude == null || b.longitude == null) return null;
  const R = 6371;
  const dLat = ((b.latitude - a.latitude) * Math.PI) / 180;
  const dLon = ((b.longitude - a.longitude) * Math.PI) / 180;
  const x = Math.sin(dLat / 2) ** 2 +
    Math.cos((a.latitude * Math.PI) / 180) * Math.cos((b.latitude * Math.PI) / 180) * Math.sin(dLon / 2) ** 2;
  return 2 * R * Math.asin(Math.sqrt(x));
}

function base64(bytes: Uint8Array): string {
  let binary = "";
  const chunk = 0x8000;
  for (let i = 0; i < bytes.length; i += chunk) binary += String.fromCharCode(...bytes.subarray(i, i + chunk));
  return btoa(binary);
}

Deno.serve(async (req: Request) => {
  if (req.method === "OPTIONS") return new Response("ok", { headers: CORS });
  if (req.method !== "POST") return fail(405, "method_not_allowed", "Use POST.");

  const auth = req.headers.get("Authorization");
  const apikey = req.headers.get("apikey") ?? "";
  if (!auth) return fail(401, "unauthorized", "Please sign in again.");
  if (!GEMINI_API_KEY) return fail(503, "not_configured", "AI matching isn't set up yet.");

  let reportId = "";
  try {
    reportId = String((await req.json()).report_id ?? "");
  } catch {
    return fail(400, "bad_request", "Send a JSON body with report_id.");
  }
  if (!/^[0-9a-f-]{36}$/i.test(reportId)) return fail(400, "bad_request", "report_id must be a UUID.");

  const headers = { Authorization: auth, apikey };
  const rest = (path: string, init: RequestInit = {}) =>
    fetch(`${SUPABASE_URL}/rest/v1/${path}`, { ...init, headers: { ...headers, ...(init.headers ?? {}) } });

  // Per-user hourly limit, kept in the database so it survives restarts.
  const since = new Date(Date.now() - 3600_000).toISOString();
  const usedResp = await rest(`ai_match_calls?select=id&called_at=gte.${encodeURIComponent(since)}`, { headers: { Prefer: "count=exact", Range: "0-0" } });
  if (!usedResp.ok) return fail(401, "unauthorized", "Please sign in again.");
  const used = Number((usedResp.headers.get("content-range") ?? "*/0").split("/")[1]);
  if (used >= MAX_CALLS_PER_HOUR) return fail(429, "rate_limited", "You've used the AI helper a lot. Try again in a little while.");

  const cols = "id,owner_id,type,species,pet_name,breed,description,location_text,latitude,longitude,photo_path,status";
  const baseResp = await rest(`reports?id=eq.${reportId}&select=${cols}`, { headers: { Accept: "application/vnd.pgrst.object+json" } });
  if (baseResp.status === 406 || baseResp.status === 404) return fail(404, "not_found", "That report was not found.");
  if (!baseResp.ok) return fail(401, "unauthorized", "Please sign in again.");
  const base: Report = await baseResp.json();
  if (!base.photo_path) return fail(400, "no_photo", "Add a photo to the report first, so the AI has something to compare.");

  const oppositeType = base.type === "lost" ? "found" : "lost";
  const candResp = await rest(
    `reports?type=eq.${oppositeType}&species=eq.${base.species}&status=eq.active&photo_path=not.is.null&id=neq.${base.id}` +
      `&select=${cols}&order=created_at.desc&limit=40`,
  );
  if (!candResp.ok) return fail(502, "upstream", "Couldn't load reports to compare.");
  let candidates: Report[] = await candResp.json();
  candidates = candidates
    .filter((c) => { const d = km(base, c); return d == null || d <= MAX_DISTANCE_KM; })
    .slice(0, MAX_CANDIDATES);

  // A call is counted as soon as the work starts: it costs money even when nothing matches.
  await rest("ai_match_calls", { method: "POST", body: "{}", headers: { "Content-Type": "application/json", Prefer: "return=minimal" } });

  if (candidates.length === 0) return json({ matches: [], model: GEMINI_MODEL });

  async function image(path: string): Promise<{ mime_type: string; data: string } | null> {
    const r = await fetch(`${SUPABASE_URL}/storage/v1/object/authenticated/report-photos/${path}`, { headers });
    if (!r.ok) return null;
    const bytes = new Uint8Array(await r.arrayBuffer());
    if (bytes.length === 0 || bytes.length > MAX_IMAGE_BYTES) return null;
    return { mime_type: "image/jpeg", data: base64(bytes) };
  }

  const baseImage = await image(base.photo_path);
  if (!baseImage) return fail(502, "photo_unavailable", "Couldn't read the report's photo.");

  const parts: unknown[] = [{
    text:
      `You help reunite lost pets with their owners. The first photo is a ${base.type} ${base.species}` +
      `${base.breed ? ` (${base.breed})` : ""}${base.description ? `. Notes: ${base.description}` : ""}. ` +
      `Below are candidate photos from the opposite kind of report, each labelled with an id. ` +
      `For each candidate, rate from 0 to 100 how likely it is the SAME individual animal, judging markings, coat colour and pattern, ` +
      `size, build and any collar. Be conservative: similar breeds are not enough, 80+ means distinctive features clearly match. ` +
      `Give a one-sentence reason in plain English that mentions what you compared. Return JSON only.`,
  }, { inline_data: baseImage }];

  const used_ids: string[] = [];
  for (const c of candidates) {
    const img = await image(c.photo_path!);
    if (!img) continue;
    used_ids.push(c.id);
    parts.push({ text: `Candidate id=${c.id}${c.pet_name ? ` name=${c.pet_name}` : ""}${c.description ? ` notes=${c.description}` : ""}` });
    parts.push({ inline_data: img });
  }
  if (used_ids.length === 0) return json({ matches: [], model: GEMINI_MODEL });

  const body = {
    contents: [{ role: "user", parts }],
    generationConfig: {
      temperature: 0.1,
      responseMimeType: "application/json",
      responseSchema: {
        type: "ARRAY",
        items: {
          type: "OBJECT",
          properties: { id: { type: "STRING" }, score: { type: "INTEGER" }, reason: { type: "STRING" } },
          required: ["id", "score", "reason"],
        },
      },
    },
  };

  let g: Response | null = null;
  let modelUsed = GEMINI_MODEL;
  for (const model of [GEMINI_MODEL, ...FALLBACK_MODELS.filter((m) => m !== GEMINI_MODEL)]) {
    for (let attempt = 0; attempt < 1; attempt++) {
      try {
        g = await fetch(`https://generativelanguage.googleapis.com/v1beta/models/${model}:generateContent`, {
          method: "POST",
          headers: { "Content-Type": "application/json", "x-goog-api-key": GEMINI_API_KEY },
          body: JSON.stringify(body),
          signal: AbortSignal.timeout(35_000),
        });
      } catch {
        g = new Response("timeout", { status: 503 });
      }
      modelUsed = model;
      if (g.status !== 503) break;
      await new Promise((r) => setTimeout(r, 1500));
    }
    if (g!.ok || (g!.status !== 503 && g!.status !== 404)) break;
  }
  if (!g) return fail(502, "upstream", "The AI helper didn't answer. Please try again.");
  if (!g.ok) {
    const status = g.status;
    console.error("Gemini error", status, (await g.text()).slice(0, 300));
    if (status === 429) return fail(503, "ai_busy", "The AI helper is busy right now. Please try again shortly.");
    if (status === 404) return fail(503, "not_configured", "AI matching isn't set up correctly (model not found).");
    if (status === 400 || status === 401 || status === 403) return fail(503, "not_configured", "AI matching isn't set up correctly (check the API key).");
    return fail(502, "upstream", "The AI helper didn't answer. Please try again.");
  }

  let parsed: { id: string; score: number; reason: string }[] = [];
  try {
    const data = await g.json();
    const textParts = (data?.candidates?.[0]?.content?.parts ?? []).filter((p: { text?: string; thought?: boolean }) => p.text && !p.thought);
    const text = textParts.length ? textParts[textParts.length - 1].text : "[]";
    parsed = JSON.parse(text);
  } catch {
    return fail(502, "upstream", "The AI helper's answer couldn't be read.");
  }

  const allowed = new Set(used_ids);
  const matches = parsed
    .filter((m) => allowed.has(m.id) && Number.isFinite(m.score))
    .map((m) => ({ id: m.id, score: Math.max(0, Math.min(100, Math.round(m.score))), reason: String(m.reason ?? "").slice(0, 240) }))
    .sort((a, b) => b.score - a.score);

  return json({ matches, model: modelUsed });
});
