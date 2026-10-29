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
    setText("jobHint", demo
      ? "Example video for layout preview. This is not a real processing job."
      : online
        ? "Latest status from ClipBridge Windows."
        : "This is the last reported job. The desktop is not currently reporting.");
    setText("progressText", Math.round(progress) + "%");
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
    byId("demoMarker").hidden = true;
    byId("jobState").dataset.state = "waiting";
    setText("jobState", "Waiting");
    setText("jobName", "No video reported");
    setText("jobHint", "Your latest video will appear here when the Windows app sends an update.");
    setText("progressText", "—");
    byId("progressFill").style.width = "0%";
    byId("progressTrack").setAttribute("aria-valuenow", "0");
    byId("progressTrack").setAttribute("aria-valuetext", "No job reported");
    setText("progressFoot", "No progress to display");
    setText("lastUpdate", "No update yet");
    setText("jobStatusFact", "Waiting for data");
    setText("jobClipsFact", "—");
    setText("jobSizeFact", "—");
    setText("activityTitle", "No updates received");
    setText("activityDescription", "Connect to view the last job status reported by ClipBridge.");
    setText("activityTime", "—");
  }

  function renderDisconnected(description = "Connect with your viewer token to read processing status.") {
    topState("offline", "Not connected", description);
    setText("railStatus", "Not reporting");
    setText("desktopConnection", "Not reporting");
    setText("accessStatus", "Not authenticated");
    setText("lastSync", "No update received");
    clearJob();
    byId("retryBtn").hidden = true;
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
  }

  function renderPayload(data) {
    state.latest = data;
    state.connected = true;
    if (!data || !data.job) {
      renderNoJob();
      return;
    }
    const online = data.online === true;
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
  }

  function renderError(description) {
    topState("error", "Connection error", description);
    setText("railStatus", "Connection error");
    setText("desktopConnection", "Connection error");
    setText("lastSync", "Status unavailable");
    byId("retryBtn").hidden = false;
    if (!state.latest) clearJob();
    else if (state.latest.job) setJob(state.latest.job, { online: false });
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

  async function refresh() {
    if (!state.token || state.demo || state.polling) return;
    state.polling = true;
    const currentToken = state.token;
    const result = await fetchStatus(currentToken);
    state.polling = false;
    if (state.demo || currentToken !== state.token) return;
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
  byId("retryBtn").addEventListener("click", refresh);
  byId("previewBtn").addEventListener("click", () => {
    if (!state.demo) {
      showDemo();
      return;
    }
    state.demo = false;
    byId("previewBtn").setAttribute("aria-pressed", "false");
    setText("previewBtn", "View sample");
    if (state.token) {
      if (state.latest) renderPayload(state.latest);
      else renderNoJob();
      refresh();
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
