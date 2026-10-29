# ClipBridge Dashboard — separate Cloudflare Worker

This is an **independent ClipBridge dashboard webpage** deployed to the existing fresh Cloudflare Worker project `clipbridge-windows` under the user's Cloudflare account. It does not modify or reuse the existing garden website, Worker, or routes.

## Dashboard v0.5 — processing console

The ClipBridge website now follows a denser professional video-processing interface: narrow navigation rail on desktop, phone bottom navigation, dark panels, vivid green live status, real progress/clip metric cards and an always-visible configuration inspector. It preserves `GET /api/health`, `GET /api/status`, auth, poll interval, manual refresh, retry and sample mode.

**Data limits:** The Worker currently reports the **latest** video job only; it does not report a complete video queue, source thumbnails, CPU load, GPU VRAM or configuration controls. The interface explicitly indicates those limits and does not invent readings. Trend charts use real updates received by this browser session; opening a fresh tab begins with no trend history. Sample charts are clearly marked and never appear as real status.

The separate gardening Worker and source video processing remain unchanged. Cloudflare KV and secret setup is still required to receive real status from the PC.

## Dashboard v0.4 premium visual redesign

The website uses a compact header, a prominent processing ring, and improved mobile hierarchy. It retains existing `/api/status` and `/api/health` routes, authenticated viewer-token connection, manual refresh, sample preview, and onboarding workflow.

**Do not interpret the sample preview as real progress.** When no job is reported, the ring shows a dash, not a fabricated percentage. No source videos are uploaded by this redesign. The production Worker must still be configured with a Workers KV binding and private secrets; GitHub deployments do not create them automatically.

The visual changes are confined to `public/index.html`, `public/styles.css`, and `public/app.js`. A PR runs responsive, authenticated-state, and automated accessibility tests, but passing them does not guarantee a deployment to Cloudflare or live PC telemetry.

## Dashboard usability v0.3

The dashboard now provides a first-time setup checklist instead of making visitors guess how to connect Cloudflare to the Windows app. Use **Check Worker API** to verify that the dashboard Worker is responding before entering any secret. This check calls `GET /api/health` without credentials. It **does not** verify `STATUS_STORE`, either secret, or the PC connection.

Once `VIEW_TOKEN` is authenticated, use **Refresh** to request the latest status immediately; the automatic 5-second polling continues. Loading, failed, and stale states do not claim that the desktop is online. The checklist remains visible until a fresh Worker response confirms the Windows client is reporting.

The dashboard **does not configure your Cloudflare account**. Create KV binding `STATUS_STORE` and separate `INGEST_TOKEN`/`VIEW_TOKEN` secrets in your Cloudflare Worker settings. Keep each token private.

## Dashboard design v0.2

The Cloudflare webpage now prioritises the current video, accurate connection state and the latest status message. It includes a clearly labelled sample preview, readable phone layout, accessible dialog and explicit error states. See [design review](DESIGN_REVIEW.md).

The **Worker status API has not changed**. Changes to `main` should redeploy the frontend through the existing Cloudflare GitHub integration, if it is configured and working. A successful GitHub test does not prove the live Cloudflare deployment has updated.

To check the website locally:

```bash
cd cloudflare-dashboard
npm install
npx playwright install chromium
npm run test:ui
```

## What works now
- Responsive Cloudflare webpage with desktop + phone layout.
- An explicit, clearly labelled sample-data preview.
- Private `GET /api/status` polling every 5 seconds, once configured.
- A validated, write-only `POST /api/status` ingestion endpoint for a future Windows agent.
- Safe offline state until the desktop integration is implemented.
- Existing video files are **never** uploaded to the dashboard.

## Deploy the independent webpage from GitHub
1. Log in to Cloudflare → **Workers & Pages** → **Create application** → **Import a repository**.
2. Select GitHub repository `Archil3s/clipbridge-windows` on the `main` branch.
3. Use the already-created Cloudflare Worker named **`clipbridge-windows`**, which matches `wrangler.jsonc`.
4. Set the project's **root directory** to **`cloudflare-dashboard`** (within this repository).
5. Leave build command blank, set deploy command to `npx wrangler deploy`.
6. Deploy. Open the actual Worker URL shown in Cloudflare. Do not rely on an assumed URL. If it still says **Hello world**, check **Settings → Build → Root directory** is `cloudflare-dashboard`, save, and retry a production deploy.

The default site safely shows **Not connected**. The Preview demo button is sample data only.

Cloudflare Git integration can deploy changes made to `main` automatically after it is connected.

## Turn on real-time data (additional setup, not required to see the site)

1. Create a **Workers KV namespace** in Cloudflare and bind it as **`STATUS_STORE`** to the ClipBridge Worker. Keep the binding in the dashboard or record the namespace ID in a Wrangler configuration for repeatable deployments.
2. Add two **different, randomly generated runtime secrets**, each at least 32 characters:
   - `VIEW_TOKEN`: used only by the browser dashboard to read status.
   - `INGEST_TOKEN`: used only by ClipBridge Windows to submit status.
3. Ideally protect the entire Worker with **Cloudflare Access** (Workers & Pages → select `clipbridge-windows` → Access → Protect this Worker, All traffic), allowing only your login.
4. Configure the Windows client to send an authenticated POST every few seconds. **That Windows status-sending integration has not been implemented yet.** It must never send the video itself or transmit ChatGPT cookies.

The status endpoint is intentionally disabled until both KV and the relevant token exist. `POST` is never available without an ingest secret and KV binding. `GET` is never available without a viewer secret and KV binding.

### Example payload (to be sent by the future Windows client)

```json
{
  "jobId": "garden_001",
  "fileName": "garden_video.mp4",
  "status": "processing",
  "progress": 68,
  "clipsDone": 26,
  "clipsTotal": 38,
  "fileSizeGb": 15
}
```

The server stamps the authoritative update time, validates the fields, stores only the latest job and expires it from KV after an hour. The dashboard marks the PC offline if it hasn't reported in 30 seconds. KV is eventually consistent, so this is **near-real-time monitoring** rather than guaranteed sub-second streaming.

## Security notes
- Do **not** put tokens, video recordings, browser profile files, ChatGPT session cookies or Cloudflare API tokens in GitHub.
- Do **not** use the same token for reading and writing.
- The viewer token is held **only in the currently open tab's memory**. After reloading, enter it again.
- This site doesn't implement remote start, pause, cancel, or ChatGPT login.
- Configure Cloudflare Access before sending sensitive job filenames.
- The site does not currently sync actual desktop data; the Windows agent is a future integration milestone.

## Local development

```bash
cd cloudflare-dashboard
npm install
npm run dev
```

No external front-end services, frameworks, trackers or cloud storage are required.
