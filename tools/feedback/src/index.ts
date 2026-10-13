// Anonymous, write-only endpoint. Feedback is read using the account's D1 console/CLI.
const headers = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Methods": "POST, OPTIONS",
  "Access-Control-Allow-Headers": "Content-Type",
  "Cache-Control": "no-store",
  "X-Content-Type-Options": "nosniff",
};

function reply(status: number, data: object): Response {
  return Response.json(data, { status, headers });
}

interface Submission {
  id: string;
  text: string;
  version: string;
  location: string;
  inEncounter: boolean;
  worldDay: number;
}

function valid(value: unknown): value is Submission {
  if (!value || typeof value !== "object") return false;
  const s = value as Record<string, unknown>;
  return typeof s.id === "string" && /^[0-9a-f]{32}$/.test(s.id)
    && typeof s.text === "string" && s.text.trim().length > 0 && s.text.length <= 2000
    && typeof s.version === "string" && s.version.length > 0 && s.version.length <= 100
    && typeof s.location === "string" && s.location.length > 0 && s.location.length <= 500
    && typeof s.inEncounter === "boolean"
    && typeof s.worldDay === "number" && Number.isSafeInteger(s.worldDay) && s.worldDay >= 0;
}

// Bound the actual stream, including chunked requests with no Content-Length.
async function readBody(request: Request): Promise<string | null> {
  if (!request.body) return "";
  const reader = request.body.getReader();
  const chunks: Uint8Array[] = [];
  let size = 0;
  for (;;) {
    const { done, value } = await reader.read();
    if (done) break;
    size += value.byteLength;
    if (size > 16384) {
      await reader.cancel();
      return null;
    }
    chunks.push(value);
  }
  const bytes = new Uint8Array(size);
  let offset = 0;
  for (const chunk of chunks) {
    bytes.set(chunk, offset);
    offset += chunk.byteLength;
  }
  return new TextDecoder("utf-8", { fatal: true }).decode(bytes);
}

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    if (new URL(request.url).pathname !== "/feedback") return reply(404, { error: "not_found" });
    if (request.method === "OPTIONS") return new Response(null, { status: 204, headers });
    if (request.method !== "POST") return reply(405, { error: "method_not_allowed" });
    // Generous IP limit accommodates testers sharing a network. IPs are never stored.
    const key = request.headers.get("CF-Connecting-IP") ?? "local";
    if (!(await env.SUBMISSIONS.limit({ key })).success) return reply(429, { error: "rate_limited" });
    if (request.headers.get("content-type")?.split(";")[0].trim() !== "application/json") {
      return reply(415, { error: "json_required" });
    }
    let submission: unknown;
    try {
      const body = await readBody(request);
      if (body === null) return reply(413, { error: "too_large" });
      submission = JSON.parse(body);
    } catch {
      return reply(400, { error: "invalid_json" });
    }
    if (!valid(submission)) return reply(400, { error: "invalid_feedback" });
    const s = submission;
    try {
      // Same ID + payload is safe to retry after an interrupted response.
      const result = await env.DB.prepare(`
        INSERT INTO feedback (id, text, version, location, in_encounter, world_day)
        VALUES (?, ?, ?, ?, ?, ?)
        ON CONFLICT(id) DO UPDATE SET id = excluded.id
        WHERE feedback.text = excluded.text AND feedback.version = excluded.version
          AND feedback.location = excluded.location AND feedback.in_encounter = excluded.in_encounter
          AND feedback.world_day = excluded.world_day
        RETURNING id
      `).bind(s.id, s.text, s.version, s.location, s.inEncounter ? 1 : 0, s.worldDay).first<{ id: string }>();
      if (!result) return reply(409, { error: "id_conflict" });
      return reply(200, { ok: true, id: result.id });
    } catch (error) {
      console.error("feedback_storage_failed", error instanceof Error ? error.message : "unknown");
      return reply(503, { error: "storage_unavailable" });
    }
  },
} satisfies ExportedHandler<Env>;
