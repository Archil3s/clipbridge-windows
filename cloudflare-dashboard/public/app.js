(() => {
  "use strict";

  const byId = id => document.getElementById(id);
  const state = {
    token: "",
    demo: false,
    latest: null,
    polling: false,
    connected: false
  };

  // The Worker currently exposes only one job. Trends are derived from samples
  // received during this browser session, not fabricated server history.
  const liveSamples = [];
  let trackedJobId = null;
  let trackedUpdate = null;
  const sampleProgress = [4, 14, 25, 38, 51, 68];
  const sampleClips = [1, 4, 9, 15, 20, 26];

  function drawTrend(elementId, emptyId, values, top = 100) {
    const path = byId(elementId);
    const empty = byId(emptyId);
    if (values.length < 2) {
      path.setAttribute("d", "");
      empty.hidden = false;
      return;
    }
    const scale = Math.max(1, top);
    const result = values.map((value, index) => {
      const x = (index * 320 / (values.length - 1)).toFixed(1);
      const y = (62 - Math.min(1, Math.max(0, value / scale)) * 55).toFixed(1);
      return (index ? "L" : "M") + x + " " + y;
    }).join(" ");
    path.setAttribute("d", result);
    empty.hidden = true;
  }

  function appendLiveSample(job) {
    if (!job || typeof job.jobId !== "string") return;
    if (trackedJobId !== job.jobId) {
      liveSamples.length = 0;
      trackedJobId = job.jobId;
      trackedUpdate = null;
    }
    if (job.updatedAt && job.updatedAt === trackedUpdate) return;
    trackedUpdate = job.updatedAt || null;
    liveSamples.push({
      progress: Math.max(0, Math.min(100, Number(job.progress) || 0)),
      clips: Math.max(0, Number(job.clipsDone) || 0)
    });
    if (liveSamples.length > 24) liveSamples.shift();
  }

  function updateMetrics(job, demo) {
    const progress = Math.max(0, Math.min(100, Number(job.progress) || 0));
    const clips = Math.max(0, Math.floor(Number(job.clipsDone) || 0));
    const total = Math.max(0, Math.floor(Number(job.clipsTotal) || 0));
    setText("metricProgress", Math.round(progress) + "%");
    setText("metricProgressUnit", demo ? "Example only" : "Latest video");
    setText("metricProgressTag", demo ? "SAMPLE" : "REPORTED");
    setText("metricClips", String(clips));
    setText("metricClipsUnit", total > 0 ? "of " + total + " clips" : "total not known");
    setText("metricClipsTag", demo ? "SAMPLE" : "REPORTED");
    setText("queueCount", demo ? "1 sample" : "1 reported");
    const ps = demo ? sampleProgress : liveSamples.map(x => x.progress);
    const cs = demo ? sampleClips : liveSamples.map(x => x.clips);
    drawTrend("progressTrend", "progressTrendEmpty", ps, 100);
    drawTrend("clipsTrend", "clipsTrendEmpty", cs,
      total || Math.max(1, ...cs));
  }

  function clearMetrics() {
    setText("metricProgress", "—");
    setText("metricProgressUnit", "Waiting for job");
    setText("metricProgressTag", "NO DATA");
    setText("metricClips", "—");
    setText("metricClipsUnit", "No output reported");
    setText("metricClipsTag", "WAITING");
    setText("queueCount", "0 reported");
    drawTrend("progressTrend", "progressTrendEmpty", []);
    drawTrend("clipsTrend", "clipsTrendEmpty", []);
  }

  function setText(id, value) {
    const node = byId(id);
    const next = String(value);
    if (node.textContent !== next) node.textContent = next;
  }

  function readableTime(value) {
    if (!value || !Number.isFinite(Date.parse(value))) return "Unknown time";
    return new Date(value).toLocaleTimeString(undefined, {
      hour: "numeric", minute: "2-digit"
    });
  }

  function statusLabel(value) {
    const map = {
      queued: "Queued",
      processing: "Processing",
      paused: "Paused",
      completed: "Completed",
      failed: "Failed",
      cancelled: "Cancelled"
    };
    return map[value] || "Unknown";
  }

  function updateControls() {
    const canRefresh = Boolean(state.token) && !state.demo && !state.polling;
    byId("refreshBtn").disabled = !canRefresh;
    byId("refreshBtn").title = canRefresh
      ? "Request the latest processing status"
      : "Connect a viewer token to refresh";
    setText("connectBtn", state.connected ? "Connection settings" : "Connect dashboard");
    byId("setupGuide").hidden = Boolean(state.connected && state.latest?.online && !state.demo && byId("statePanel").dataset.state === "online");
  }

  function topState(kind, title, description) {
    byId("statePanel").dataset.state = kind;
    byId("stateLight").dataset.state = kind;
    byId("railLight").dataset.state = kind;
    byId("activitySymbol").dataset.state = kind;
    setText("stateTitle", title);
    setText("stateDescription", description);
  }

  function setJob(job, { demo = false, online = false } = {}) {
    const filename = typeof job.fileName === "string" ? job.fileName : "Unnamed video";
    const progress = Math.min(100, Math.max(0, Number(job.progress) || 0));
    const complete = Math.max(0, Math.floor(Number(job.clipsDone) || 0));
    const total = Math.max(0, Math.floor(Number(job.clipsTotal) || 0));
    const size = Number.isFinite(Number(job.fileSizeGb)) && Number(job.fileSizeGb) >= 0
      ? Number(job.fileSizeGb).toFixed(2) + " GB"
      : "Not reported";
    const status = statusLabel(job.status);
    const updated = readableTime(job.updatedAt);

    byId("demoMarker").hidden = !demo;
    setText("jobState", status);
    byId("jobState").dataset.state = job.status;
    setText("jobName", filename);
    updateMetrics(job, demo);
    setText("jobHint", demo
      ? "Example video for layout preview. This is not a real processing job."
      : online
        ? "Latest status from ClipBridge Windows."
        : "This is the last reported job. The desktop is not currently reporting.");
    setText("progressText", Math.round(progress) + "%");
    byId("jobRing").style.setProperty("--value", String(progress));
    setText("ringValue", Math.round(progress) + "%");
    setText("ringCaption", demo ? "Sample progress" : "Processed");
    setText("ringHelp", demo
      ? "Example only — not your PC"
      : online ? "Latest video processing status" : "Last known status — PC offline");
    byId("progressFill").style.width = progress + "%";
    byId("progressTrack").setAttribute("aria-valuenow", String(progress));
    byId("progressTrack").setAttribute("aria-valuetext",
      (demo ? "Sample progress: " : "") + Math.round(progress) + " percent");
    setText("progressFoot", total > 0
      ? complete + " of " + total + " clips completed"
      : complete + " clips completed (total not yet known)");
    setText("lastUpdate", demo ? "Example only" : "Reported " + updated);
    setText("jobStatusFact", status);
    setText("jobClipsFact", total > 0 ? complete + " / " + total : String(complete));
    setText("jobSizeFact", size);
    setText("activityTitle", filename);
    setText("activityDescription", (demo ? "Sample: " : "") + status + ", " +
      Math.round(progress) + "% complete");
    setText("activityTime", demo ? "Example only" : updated);
  }

  function clearJob() {
    clearMetrics();
    byId("demoMarker").hidden = true;
    byId("jobState").dataset.state = "waiting";
    setText("jobState", "Waiting");
    setText("jobName", "No video reported");
    setText("jobHint", "Your latest video will appear here when the Windows app sends an update.");
    setText("progressText", "Not reported");
    byId("jobRing").style.setProperty("--value", "0");
    setText("ringValue", "—");
    setText("ringCaption", "No job yet");
    setText("ringHelp", "Waiting for video data");
    byId("progressFill").style.width = "0%";
    byId("progressTrack").setAttribute("aria-valuenow", "0");
    byId("progressTrack").setAttribute("aria-valuetext", "No job reported");
    setText("progressFoot", "No progress to display");
    setText("lastUpdate", "No update yet");
    setText("jobStatusFact", "Waiting for data");
    setText("jobClipsFact", "Not reported");
    setText("jobSizeFact", "Not reported");
    setText("activityTitle", "No updates received");
    setText("activityDescription", "Connect to view the last job status reported by ClipBridge.");
    setText("activityTime", "Not reported");
  }

  function renderDisconnected(description = "Connect with your viewer token to read processing status.") {
    topState("offline", "Not connected", description);
    setText("railStatus", "Not reporting");
    setText("desktopConnection", "Not reporting");
    setText("accessStatus", "Not authenticated");
    setText("lastSync", "No update received");
    clearJob();
    byId("retryBtn").hidden = true;
    updateControls();
  }

  function renderNoJob() {
    topState("offline", "Waiting for desktop", "Viewer access is working. ClipBridge Windows has not reported a job yet.");
    setText("railStatus", "No status received");
    setText("desktopConnection", "No status received");
    setText("accessStatus", "Viewer authenticated");
    setText("lastSync", "Connected to dashboard");
    byId("retryBtn").hidden = true;
    clearJob();
    setText("activityDescription", "Start a video job on your PC with Cloudflare monitoring enabled.");
    updateControls();
  }

  function renderPayload(data) {
    state.latest = data;
    state.connected = true;
    if (!data || !data.job) {
      renderNoJob();
      return;
    }
    const online = data.online === true;
    appendLiveSample(data.job);
    const updated = readableTime(data.job.updatedAt);
    if (online) {
      topState("online", "Desktop reporting", "Status is arriving from the Windows app. Updates are checked every 5 seconds.");
    } else {
      topState("offline", "Desktop not reporting", "Showing the last job update. The Windows app has not reported recently.");
    }
    setText("railStatus", online ? "Reporting" : "Last signal expired");
    setText("desktopConnection", online ? "Reporting" : "Not reporting");
    setText("accessStatus", "Viewer authenticated");
    setText("lastSync", "Last report " + updated);
    byId("retryBtn").hidden = true;
    setJob(data.job, { online });
    updateControls();
  }

  function renderError(description) {
    topState("error", "Connection error", description);
    setText("railStatus", "Connection error");
    setText("desktopConnection", "Connection error");
    setText("lastSync", "Status unavailable");
    byId("retryBtn").hidden = false;
    setText("accessStatus", "Last access check unavailable");
    if (!state.latest) clearJob();
    else if (state.latest.job) setJob(state.latest.job, { online: false });
    updateControls();
  }

  function renderLoading() {
    topState("loading", "Checking desktop", "Requesting the latest status from Cloudflare.");
    setText("desktopConnection", "Checking");
    setText("railStatus", "Checking status");
    byId("retryBtn").hidden = true;
    if (state.latest?.job) setJob(state.latest.job, { online: false });
    else clearJob();
  }

  function showDemo() {
    state.demo = true;
    byId("previewBtn").setAttribute("aria-pressed", "true");
    setText("previewBtn", "Exit sample");
    topState("demo", "Sample preview", "These figures illustrate the layout. They are not from your computer.");
    setText("railStatus", "Sample only");
    setText("desktopConnection", "Sample, not connected");
    setText("accessStatus", state.connected ? "Viewer authenticated" : "Not authenticated");
    setText("lastSync", "Sample only");
    byId("retryBtn").hidden = true;
    setJob({
      fileName: "example_garden_video.mp4",
      status: "processing",
      progress: 68,
      clipsDone: 26,
      clipsTotal: 38,
      fileSizeGb: 15,
      updatedAt: new Date().toISOString()
    }, { demo: true, online: false });
    updateControls();
  }

  async function fetchStatus(token) {
    let response;
    try {
      response = await fetch("/api/status", {
        headers: { authorization: "Bearer " + token },
        cache: "no-store",
        signal: AbortSignal.timeout(12000)
      });
    } catch {
      return { ok: false, reason: "Could not reach Cloudflare. Check your connection and try again." };
    }
    if (response.status === 401)
      return { ok: false, unauthorized: true, reason: "The viewer token was rejected. Check VIEW_TOKEN in your Worker settings." };
    if (response.status === 503)
      return { ok: false, reason: "Cloudflare is missing STATUS_STORE or VIEW_TOKEN. Configure them in Worker settings." };
    if (!response.ok)
      return { ok: false, reason: "Cloudflare returned HTTP " + response.status + ". Try again or check the Worker logs." };
    try {
      const data = await response.json();
      if (!data || typeof data !== "object" || !("online" in data))
        return { ok: false, reason: "Cloudflare returned an unexpected status format." };
      return { ok: true, data };
    } catch {
      return { ok: false, reason: "Cloudflare returned invalid status data." };
    }
  }

  async function refresh({ showLoading = false } = {}) {
    if (!state.token || state.demo || state.polling) return;
    state.polling = true;
    if (showLoading) renderLoading();
    updateControls();
    const currentToken = state.token;
    const result = await fetchStatus(currentToken);
    state.polling = false;
    if (state.demo || currentToken !== state.token) {
      updateControls();
      return;
    }
    if (result.ok) {
      renderPayload(result.data);
    } else if (result.unauthorized) {
      state.token = "";
      state.connected = false;
      state.latest = null;
      renderDisconnected(result.reason);
    } else {
      renderError(result.reason);
    }
    updateControls();
  }

  async function checkWorker() {
    const btn = byId("checkWorkerBtn");
    btn.disabled = true;
    setText("checkWorkerBtn", "Checking…");
    setText("workerConnection", "Checking");
    const note = byId("workerCheckNote");
    note.dataset.state = "";
    setText("workerCheckNote", "Contacting the ClipBridge Worker API.");
    try {
      const response = await fetch("/api/health", {
        cache: "no-store",
        signal: AbortSignal.timeout(10000)
      });
      if (!response.ok) throw Error("HTTP " + response.status);
      const result = await response.json();
      if (result?.service !== "clipbridge-dashboard" || result?.ok !== true)
        throw Error("Unexpected API response");
      setText("workerConnection", "Available");
      byId("workerDot").dataset.state = "ok";
      note.dataset.state = "ok";
      setText("workerCheckNote", "Worker API is responding. This does not verify KV, viewer access or your PC.");
    } catch {
      setText("workerConnection", "Unavailable");
      byId("workerDot").dataset.state = "error";
      note.dataset.state = "error";
      setText("workerCheckNote", "Worker API unavailable. Check the Cloudflare deployment and use cloudflare-dashboard as its root directory.");
    } finally {
      btn.disabled = false;
      setText("checkWorkerBtn", "Check Worker API");
    }
  }

  function showError(message) {
    setText("dialogError", message);
    byId("dialogError").hidden = false;
  }

  function openDialog() {
    byId("viewerToken").value = "";
    byId("dialogError").hidden = true;
    byId("connectDialog").showModal();
    byId("viewerToken").focus();
  }

  function closeDialog() {
    byId("connectDialog").close();
  }

  async function connect(event) {
    event.preventDefault();
    const candidate = byId("viewerToken").value.trim();
    if (candidate.length < 32) {
      showError("Enter the private viewer token (32 characters or longer).");
      return;
    }
    byId("dialogError").hidden = true;
    byId("saveConnectionBtn").disabled = true;
    setText("saveConnectionBtn", "Checking…");
    const result = await fetchStatus(candidate);
    byId("saveConnectionBtn").disabled = false;
    setText("saveConnectionBtn", "Connect");
    if (!result.ok) {
      showError(result.reason);
      return;
    }
    state.token = candidate;
    state.demo = false;
    byId("previewBtn").setAttribute("aria-pressed", "false");
    setText("previewBtn", "View sample");
    renderPayload(result.data);
    closeDialog();
  }

  byId("connectBtn").addEventListener("click", openDialog);
  byId("setupBtn").addEventListener("click", openDialog);
  byId("dialogCloseBtn").addEventListener("click", closeDialog);
  byId("connectForm").addEventListener("submit", connect);
  byId("retryBtn").addEventListener("click", () => refresh({ showLoading: true }));
  byId("refreshBtn").addEventListener("click", () => refresh({ showLoading: true }));
  byId("checkWorkerBtn").addEventListener("click", checkWorker);
  byId("previewBtn").addEventListener("click", () => {
    if (!state.demo) {
      showDemo();
      return;
    }
    state.demo = false;
    byId("previewBtn").setAttribute("aria-pressed", "false");
    setText("previewBtn", "View sample");
    if (state.token) {
      refresh({ showLoading: true });
    } else renderDisconnected();
  });
  byId("disconnectBtn").addEventListener("click", () => {
    state.token = "";
    state.connected = false;
    state.latest = null;
    state.demo = false;
    byId("previewBtn").setAttribute("aria-pressed", "false");
    setText("previewBtn", "View sample");
    closeDialog();
    renderDisconnected();
  });

  document.querySelectorAll(".rail-nav a").forEach(link => {
    link.addEventListener("click", () => {
      document.querySelectorAll(".rail-nav a").forEach(other => other.removeAttribute("aria-current"));
      link.setAttribute("aria-current", "page");
    });
  });

  renderDisconnected();
  window.setInterval(refresh, 5000);
})();
