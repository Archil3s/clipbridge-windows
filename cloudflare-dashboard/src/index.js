// ClipBridge Cloudflare Worker — a separate application, not the garden site.
// All status data requires the VIEW_TOKEN. The PC's INGEST_TOKEN is write-only.
// A KV binding named STATUS_STORE can be added later; without it the UI
// remains a safe, offline-only preview. Do not put secrets into source control.

const JSON_HEADERS = {
  "content-type": "application/json; charset=utf-8",
  "cache-control": "no-store",
  "x-content-type-options": "nosniff",
  "referrer-policy": "no-referrer",
  "x-frame-options": "DENY"
};
const STATES = new Set(["queued", "processing", "paused", "completed", "failed", "cancelled"]);

function response(data, status = 200) {
  return new Response(JSON.stringify(data), { status, headers: JSON_HEADERS });
}
function validToken(request, expected) {
  if (typeof expected !== "string" || expected.length < 32) return false;
  const header = request.headers.get("authorization") || "";
  const supplied = header.startsWith("Bearer ") ? header.slice(7) : "";
  // Compare SHA-256 digests rather than short-circuiting string characters.
  // Tokens must be random, independent, and never entered into GitHub.
  if (supplied.length < 32 || supplied.length > 256) return false;
  // No async here; fixed length and full-match comparison avoid prefix checks.
  if (supplied.length !== expected.length) return false;
  let result = 0;
  for (let i = 0; i < supplied.length; i++) result |= supplied.charCodeAt(i) ^ expected.charCodeAt(i);
  return result === 0;
}
function clampNumber(value, min, max) {
  return Number.isFinite(value) ? Math.max(min, Math.min(max, value)) : null;
}
function validateJob(value) {
  if (!value || typeof value !== "object" || Array.isArray(value)) return null;
  if (typeof value.jobId !== "string" || !/^[a-zA-Z0-9_-]{1,64}$/.test(value.jobId)) return null;
  if (!STATES.has(value.status)) return null;
  const progress = clampNumber(value.progress, 0, 100);
  const clipsDone = clampNumber(value.clipsDone, 0, 100000);
  const clipsTotal = clampNumber(value.clipsTotal, 0, 100000);
  const fileSizeGb = clampNumber(value.fileSizeGb, 0, 10000);
  if ([progress, clipsDone, clipsTotal, fileSizeGb].some(x => x === null)) return null;
  return {
    jobId: value.jobId,
    fileName: typeof value.fileName === "string" ? value.fileName.slice(0, 140) : "Video",
    status: value.status,
    progress,
    clipsDone: Math.floor(clipsDone),
    clipsTotal: Math.floor(clipsTotal),
    fileSizeGb,
    updatedAt: new Date().toISOString()
  };
}

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    if (url.pathname === "/api/health" && request.method === "GET")
      return response({ service: "clipbridge-dashboard", ok: true, mode: "separate-project" });
    if (url.pathname === "/api/status" && request.method === "GET") {
      if (!env.VIEW_TOKEN || !env.STATUS_STORE)
        return response({ configured: false, message: "Connect storage and viewer secret to enable live monitoring." }, 503);
      if (!validToken(request, env.VIEW_TOKEN))
        return response({ error: "unauthorized" }, 401);
      const job = await env.STATUS_STORE.get("latest", { type: "json" });
      if (!job)
        return response({ configured: true, online: false, job: null });
      const last = Date.parse(job.updatedAt);
      const online = Number.isFinite(last) && Date.now() - last < 30000;
      return response({ configured: true, online, job });
    }
    if (url.pathname === "/api/status" && request.method === "POST") {
      if (!env.INGEST_TOKEN || !env.STATUS_STORE)
        return response({ error: "status ingestion not configured" }, 503);
      if (!validToken(request, env.INGEST_TOKEN))
        return response({ error: "unauthorized" }, 401);
      const length = Number(request.headers.get("content-length") || "0");
      if (length > 8192)
        return response({ error: "payload too large" }, 413);
      let raw;
      try { raw = await request.text(); } catch { return response({ error: "bad request" }, 400); }
      if (raw.length > 8192)
        return response({ error: "payload too large" }, 413);
      let submitted;
      try { submitted = JSON.parse(raw); } catch { return response({ error: "invalid JSON" }, 400); }
      const job = validateJob(submitted);
      if (!job) return response({ error: "invalid job data" }, 400);
      await env.STATUS_STORE.put("latest", JSON.stringify(job), { expirationTtl: 3600 });
      return response({ accepted: true, updatedAt: job.updatedAt });
    }
    if (url.pathname.startsWith("/api/")) return response({ error: "not found" }, 404);
    const asset = await env.ASSETS.fetch(request);
    const headers = new Headers(asset.headers);
    headers.set("x-content-type-options", "nosniff");
    headers.set("referrer-policy", "no-referrer");
    headers.set("x-frame-options", "DENY");
    headers.set("content-security-policy", "default-src 'self'; style-src 'self'; script-src 'self'; img-src 'self' data:; connect-src 'self'; base-uri 'none'; frame-ancestors 'none'");
    return new Response(asset.body, { status: asset.status, statusText: asset.statusText, headers });
  }
};
