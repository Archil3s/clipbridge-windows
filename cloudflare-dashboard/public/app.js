(() => {
  "use strict";
  const el = id => document.getElementById(id);
  let token = "";
  let demo = false;
  let lastStatus = null;
  let online = false;

  function text(id, value) { el(id).textContent = String(value); }
  function setMode(tag, desc, style, button) {
    text("modeTag", tag);
    el("modeTag").className = "mode-tag" + (style ? " " + style : "");
    text("modeDescription", desc);
    el("previewBtn").innerHTML = "";
    el("previewBtn").append(document.createTextNode(button));
    el("previewBtn").append(document.createTextNode(" ↗"));
  }
  function setConnection(isOnline) {
    online = isOnline;
    el("sideDot").classList.toggle("connected", isOnline);
    text("sideDeviceStatus", isOnline ? "Connected" : "Not connected");
    text("connectionValue", isOnline ? "Online" : "Offline");
    text("connectionSub", isOnline ? "Receiving desktop updates" : "No desktop updates received");
  }
  function setProgress(percent) {
    const p = Math.max(0, Math.min(100, Number(percent) || 0));
    el("progressFill").style.width = p + "%";
    el("progressTrack").setAttribute("aria-valuenow", String(p));
    text("progressText", Math.round(p) + "%");
  }
  function prettyDate(value) {
    if (!value || !Number.isFinite(Date.parse(value))) return "No updates";
    return new Date(value).toLocaleTimeString(undefined, {hour: "2-digit", minute: "2-digit"});
  }
  function renderOffline(message = "A separate Cloudflare dashboard is ready. Link the Windows app to display live progress.") {
    setMode("NOT CONNECTED", message, "", "Preview demo");
    setConnection(false);
    text("activeJobValue", "—"); text("activeJobSub", "No active processing");
    text("clipsValue", "—"); text("clipsSub", "Waiting for first update");
    text("sizeValue", "—"); text("sizeSub", "Current source file");
    text("jobState", "Waiting"); el("jobState").className = "state-pill idle";
    el("jobState").innerHTML = "<span></span> Waiting";
    text("jobName", "No video processing yet");
    text("jobHint", "When ClipBridge starts a job, its progress can appear here.");
    setProgress(0); text("progressText", "—");
    text("progressFoot", "Waiting for desktop connection"); text("lastUpdate", "No updates");
    text("lastSync", "Not synced yet");
    text("activityLabel", "No activity"); text("activityTitle", "No processing updates yet");
    text("activityDescription", "Once connected, this panel shows the most recent desktop job status.");
    text("activityTime", "—");
    text("accessStatus", token ? "Viewer token provided" : "Token required for live data");
  }
  function renderJob(job, isOnline, isDemo) {
    setConnection(isOnline);
    const status = String(job.status || "queued");
    const stateClass = status === "failed" ? "failed" : (status === "processing" ? "live" : "idle");
    el("jobState").className = "state-pill " + stateClass;
    el("jobState").innerHTML = "<span></span> ";
    el("jobState").append(document.createTextNode(status[0].toUpperCase() + status.slice(1)));
    const fileName = String(job.fileName || "Video");
    text("jobName", fileName);
    text("jobHint", isDemo ? "Sample job for visual preview. No real files are being processed." : "Processing data received from ClipBridge Windows.");
    text("activeJobValue", status === "processing" ? "1" : "0");
    text("activeJobSub", status[0].toUpperCase() + status.slice(1) + " job");
    text("clipsValue", job.clipsDone || 0); text("clipsSub", (job.clipsTotal || 0) + " expected clips");
    text("sizeValue", Number(job.fileSizeGb || 0).toFixed(1) + " GB");
    text("sizeSub", "Current source file");
    setProgress(job.progress || 0);
    text("progressFoot", (job.clipsDone || 0) + " of " + (job.clipsTotal || 0) + " clips complete");
    text("lastUpdate", isDemo ? "Example only" : "Updated " + prettyDate(job.updatedAt));
    text("lastSync", isDemo ? "Demo preview" : "Last update " + prettyDate(job.updatedAt));
    text("activityLabel", isDemo ? "Demo activity" : "Last known update");
    text("activityTitle", fileName);
    text("activityDescription", "Status: " + status + " · " + Math.round(job.progress || 0) + "% complete");
    text("activityTime", isDemo ? "Preview" : prettyDate(job.updatedAt));
    text("accessStatus", isDemo ? "Demo data only" : "Private viewer token");
    if (isDemo)
      setMode("DEMO MODE", "Illustrative progress only. Your PC has not been connected to Cloudflare.", "demo", "Exit demo");
    else if (isOnline)
      setMode("LIVE", "Status is arriving from the Windows app through Cloudflare.", "live", "Preview demo");
    else
      setMode("OFFLINE", "Showing the last job received, but the Windows app is not reporting now.", "", "Preview demo");
  }
  function showDemo() {
    demo = true;
    renderJob({
      status: "processing",
      fileName: "garden_video_01.mp4",
      fileSizeGb: 15,
      progress: 68,
      clipsDone: 26,
      clipsTotal: 38,
      updatedAt: new Date().toISOString()
    }, false, true);
    text("connectionValue", "Demo");
    text("connectionSub", "Example data, not a real connection");
  }
  async function refresh() {
    if (demo || !token) return;
    try {
      const result = await fetch("/api/status", {
        headers: {authorization: "Bearer " + token}, cache: "no-store"
      });
      if (result.status === 401) {
        token = "";
        renderOffline("Viewer token was rejected. Reconnect using your dashboard's viewer token.");
        return;
      }
      if (result.status === 503) {
        renderOffline("Website deployed. Cloudflare KV and dashboard secrets still need setup.");
        return;
      }
      if (!result.ok) throw Error("HTTP " + result.status);
      const data = await result.json();
      lastStatus = data;
      if (data.job) renderJob(data.job, Boolean(data.online), false);
      else renderOffline("Private connection ready. Waiting for the Windows app to send its first update.");
    } catch {
      renderOffline("Cannot reach the Cloudflare status endpoint. Check your network or try again.");
    }
  }
  function openDialog() {
    el("viewerToken").value = "";
    text("dialogError", "");
    el("connectDialog").showModal();
  }
  el("connectBtn").addEventListener("click", openDialog);
  el("setupBtn").addEventListener("click", openDialog);
  el("previewBtn").addEventListener("click", () => {
    if (demo) {
      demo = false;
      if (lastStatus && token && lastStatus.job) renderJob(lastStatus.job, Boolean(lastStatus.online), false);
      else renderOffline();
      refresh();
    } else showDemo();
  });
  el("disconnectBtn").addEventListener("click", () => {
    token = "";
    lastStatus = null;
    demo = false;
    renderOffline();
    el("connectDialog").close();
  });
  el("saveConnectionBtn").addEventListener("click", async () => {
    const candidate = el("viewerToken").value.trim();
    if (candidate.length < 32) {
      text("dialogError", "Please enter the private viewer token (32 characters or longer).");
      return;
    }
    token = candidate;
    demo = false;
    await refresh();
    if (!token) {
      text("dialogError", "Token was rejected. Check the value configured in Cloudflare.");
      return;
    }
    el("connectDialog").close();
  });
  document.querySelectorAll(".nav-item").forEach(item => {
    item.addEventListener("click", () => {
      document.querySelectorAll(".nav-item").forEach(node => node.classList.remove("active"));
      item.classList.add("active");
    });
  });
  renderOffline();
  setInterval(refresh, 5000);
})();
