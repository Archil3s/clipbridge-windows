"use strict";
const API = "http://127.0.0.1:38419";
const ready = chrome.storage.local.setAccessLevel({ accessLevel: "TRUSTED_CONTEXTS" });
const popupUrl = chrome.runtime.getURL("popup.html");
const chunkSize = 2 * 1024 * 1024;
const chunksInFlight = new Set();
let busy = false;

async function request(path, method = "GET", range, claim, body) {
  const { pairingKey, enabled } = await chrome.storage.local.get(["pairingKey", "enabled"]);
  if (!enabled || !/^[a-fA-F0-9]{64}$/.test(pairingKey || ""))
    throw new Error("Enable and pair the extension first.");
  const response = await fetch(API + path, {
    method, cache: "no-store", credentials: "omit", redirect: "error",
    headers: { "X-ClipBridge-Key": pairingKey, "X-ClipBridge-Extension": chrome.runtime.id, ...(range ? { Range: range } : {}), ...(claim ? { "X-ClipBridge-Claim": claim } : {}), ...(body ? { "Content-Type": "application/json" } : {}) },
    ...(body ? { body: JSON.stringify(body) } : {}),
    signal: AbortSignal.timeout(20000)
  });
  if (!response.ok) throw new Error(response.status === 401 ?
    "Pairing key rejected. Copy the current key from ClipBridge." :
    "Local bridge returned HTTP " + response.status);
  return response;
}

async function authorise(sender) {
  const { armed } = await chrome.storage.session.get("armed");
  const { enabled } = await chrome.storage.local.get("enabled");
  if (!enabled || !armed || sender.id !== chrome.runtime.id ||
      sender.frameId !== 0 || sender.tab?.id !== armed.tabId ||
      sender.url !== armed.url || sender.documentId !== armed.documentId)
    throw new Error("Tab is not armed. Review the conversation and arm it again.");
  const tab = await chrome.tabs.get(armed.tabId);
  if (tab.url !== armed.url || !tab.active) throw new Error("Armed tab is no longer active.");
  return armed;
}

async function handle(message, sender) {
  await ready;
  if (sender.id !== chrome.runtime.id) throw new Error("Untrusted sender.");
  if (!sender.tab && sender.url === popupUrl) {
    if (message.type === "settings-ready") return { ok: true };
    if (message.type === "arm") {
      const { enabled, batchSize = 3, autoSend = false, batchPrompt = "Gardening B-roll clips for reels. Please wait for my instructions." } =
        await chrome.storage.local.get(["enabled", "batchSize", "autoSend", "batchPrompt"]);
      if (!Number.isInteger(batchSize) || batchSize < 1 || batchSize > 10 ||
          typeof batchPrompt !== "string" || batchPrompt.length > 1000) throw new Error("Choose 1–10 clips and a message up to 1000 characters.");
      if (!enabled) throw new Error("Check Enable and save settings before arming.");
      const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
      if (!tab?.url || new URL(tab.url).origin !== "https://chatgpt.com")
        throw new Error("Open the intended ChatGPT conversation first.");
      if (busy || chunksInFlight.size) throw new Error("Wait for the current transfer operation, then arm again.");
      const health = await (await request("/api/health")).json();
      if (health.version < 2 || !health.maxBatchCount) throw new Error("Install ClipBridge Windows 0.5.0 or later for batch uploads.");
      const response = await chrome.tabs.sendMessage(tab.id, { type: "clipbridge-arm" }, { frameId: 0 });
      if (!response?.documentId) throw new Error("Refresh ChatGPT and try again.");
      await chrome.storage.session.clear();
      await chrome.storage.session.set({ armed: {
        tabId: tab.id, url: tab.url, documentId: response.documentId,
        batchSize, autoSend: autoSend === true, batchPrompt, batchNumber: 0
      } });
      await chrome.tabs.sendMessage(tab.id, { type: "clipbridge-start", options: { batchSize, autoSend: autoSend === true, batchPrompt } }, { documentId: response.documentId });
      return { ok: true };
    }
    if (message.type === "disable") {
      const { armed } = await chrome.storage.session.get("armed");
      await chrome.storage.session.clear();
      if (armed) {
        try { await chrome.tabs.sendMessage(armed.tabId, { type: "clipbridge-disarm" }, { documentId: armed.documentId }); }
        catch { /* The tab may have closed. */ }
      }
      return { ok: true };
    }
    throw new Error("Unknown popup command.");
  }
  if (message.type === "hello") return { documentId: sender.documentId };
  await authorise(sender);
  if (message.type === "status") {
    await chrome.storage.local.set({ lastStatus: String(message.text).slice(0, 500) });
    return { ok: true };
  }
  if (message.type === "pause") {
    await chrome.storage.local.set({ lastStatus: String(message.text || "Paused. Review the composer before arming again.").slice(0, 500) });
    await chrome.storage.session.remove("armed");
    return { ok: true };
  }
  const { batch } = await chrome.storage.session.get("batch");
  if (["batch", "dispatch-batch", "reserve-send", "sent"].includes(message.type) ||
      (message.type === "chunk" && batch)) return handleBatch(message, sender);
  if (busy) throw new Error("Another transfer operation is in progress.");
  busy = true;
  try {
    const { transfer } = await chrome.storage.session.get("transfer");
    if (message.type === "next") {
      if (transfer) throw new Error("Previous clip needs review. Check ChatGPT before arming again.");
      const item = await (await request("/api/next")).json();
      if (item.ready) {
        if (!/^[a-f0-9]{32}$/.test(item.id) || !Number.isSafeInteger(item.size) || item.size <= 0)
          throw new Error("Invalid local clip metadata.");
        await chrome.storage.session.set({ transfer: { ...item, offset: 0 } });
      }
      return item;
    }
    if (!transfer || transfer.id !== message.id) throw new Error("No matching clip transfer.");
    if (message.type === "chunk") {
      const end = Math.min(transfer.size, transfer.offset + chunkSize) - 1;
      const response = await request("/api/file/" + transfer.id, "GET", `bytes=${transfer.offset}-${end}`, transfer.claim);
      if (response.status !== 206 || response.headers.get("Content-Range") !==
          `bytes ${transfer.offset}-${end}/${transfer.size}`) throw new Error("Invalid local file range.");
      const bytes = new Uint8Array(await response.arrayBuffer());
      if (bytes.length !== end - transfer.offset + 1) throw new Error("Incomplete local transfer.");
      await authorise(sender);
      let binary = "";
      for (let i = 0; i < bytes.length; i += 8192)
        binary += String.fromCharCode(...bytes.subarray(i, i + 8192));
      transfer.offset = end + 1;
      await chrome.storage.session.set({ transfer });
      return { data: btoa(binary), done: transfer.offset === transfer.size };
    }
    if (message.type === "dispatch") {
      if (transfer.offset !== transfer.size) throw new Error("Transfer is incomplete.");
      // Reserve the attempt before touching the DOM: a lost response must not replay it.
      await request("/api/offered/" + transfer.id, "POST", null, transfer.claim);
      await chrome.storage.session.remove("transfer");
      await chrome.storage.session.remove("armed");
      return { ok: true };
    }
    throw new Error("Unknown transfer command.");
  } finally { busy = false; }
}

chrome.runtime.onMessage.addListener((message, sender, respond) => {
  handle(message, sender).then(respond, error => respond({ error: error.message }));
  return true;
});
chrome.tabs.onRemoved.addListener(async tabId => {
  const { armed } = await chrome.storage.session.get("armed");
  if (armed?.tabId === tabId) await chrome.storage.session.remove("armed");
});
chrome.tabs.onUpdated.addListener(async (tabId, change) => {
  const { armed } = await chrome.storage.session.get("armed");
  if (armed?.tabId === tabId && (change.url || change.status === "loading")) {
    await chrome.storage.session.remove("armed");
    await chrome.storage.local.set({ lastStatus: "Conversation changed or reloaded. Review any dispatched batch and arm the intended conversation again." });
  }
});

async function handleBatch(message, sender) {
  const armed = await authorise(sender);
  let { batch } = await chrome.storage.session.get("batch");
  if (message.type === "chunk") {
    const item = batch?.items.find(item => item.id === message.id);
    if (!item || batch.phase !== "transferring") throw new Error("No matching active batch transfer.");
    if (busy || chunksInFlight.has(item.id)) throw new Error("Clip transfer operation already in progress.");
    chunksInFlight.add(item.id);
    try {
      const offsetKey = "offset-" + item.id;
      const saved = await chrome.storage.session.get(offsetKey);
      const offset = saved[offsetKey] || 0;
      if (offset >= item.size) throw new Error("Clip transfer already complete.");
      const end = Math.min(item.size, offset + chunkSize) - 1;
      const response = await request("/api/file/" + item.id, "GET", "bytes=" + offset + "-" + end, item.claim);
      if (response.status !== 206 || response.headers.get("Content-Range") !==
          "bytes " + offset + "-" + end + "/" + item.size) throw new Error("Invalid local file range.");
      const bytes = new Uint8Array(await response.arrayBuffer());
      if (bytes.length !== end - offset + 1) throw new Error("Incomplete local transfer.");
      await authorise(sender);
      const current = await chrome.storage.session.get("batch");
      if (current.batch?.id !== batch.id) throw new Error("Batch changed during transfer.");
      let binary = "";
      for (let i = 0; i < bytes.length; i += 8192)
        binary += String.fromCharCode(...bytes.subarray(i, i + 8192));
      await chrome.storage.session.set({ [offsetKey]: end + 1 });
      return { data: btoa(binary), done: end + 1 === item.size };
    } finally { chunksInFlight.delete(item.id); }
  }
  if (busy || chunksInFlight.size) throw new Error("Wait for the active batch operation.");
  busy = true;
  try {
    if (message.type === "batch") {
      if (batch && batch.phase !== "sent") throw new Error("Previous batch needs review before another attempt.");
      const capacity = message.multiple === true ? armed.batchSize : 1;
      const result = await (await request("/api/batch?count=" + capacity)).json();
      await authorise(sender);
      if (!result.ready) return result;
      if (!Array.isArray(result.items) || !result.items.length || result.items.length > capacity ||
          result.items.some(item => !/^[a-f0-9]{32}$/.test(item.id) || !/^[a-f0-9]{32}$/.test(item.claim) ||
            !Number.isSafeInteger(item.size) || item.size <= 0 || item.size > 525000000 ||
            typeof item.name !== "string" || !/\.(mp4|mov|webm|m4v)$/i.test(item.name)) ||
          result.items.reduce((sum, item) => sum + item.size, 0) > 1000000000)
        throw new Error("Unsupported local batch. Use smaller MP4 clips or manual attachment.");
      if (batch) await chrome.storage.session.remove(batch.items.map(item => "offset-" + item.id));
      armed.batchNumber++;
      batch = { id: result.items[0].claim, items: result.items, remaining: result.remaining,
        number: armed.batchNumber, phase: "transferring", sendReserved: false };
      await chrome.storage.session.set({ armed, batch });
      return { ...result, batchId: batch.id, number: batch.number };
    }
    if (!batch || message.batchId !== batch.id) throw new Error("No matching batch.");
    if (message.type === "dispatch-batch") {
      if (batch.phase !== "transferring") throw new Error("Batch attempt already reserved; review it in ChatGPT.");
      const offsets = await chrome.storage.session.get(batch.items.map(item => "offset-" + item.id));
      if (batch.items.some(item => offsets["offset-" + item.id] !== item.size)) throw new Error("Batch transfer is incomplete.");
      await request("/api/offered-batch", "POST", null, null,
        batch.items.map(item => ({ id: item.id, claim: item.claim })));
      batch.phase = "reserved";
      await chrome.storage.session.set({ batch });
      await authorise(sender);
      return { ok: true };
    }
    if (message.type === "reserve-send") {
      if (!armed.autoSend || batch.phase !== "reserved" || batch.sendReserved)
        throw new Error("Auto-send is not authorized or this batch already has an uncertain send attempt.");
      await request("/api/health");
      await authorise(sender);
      batch.sendReserved = true;
      batch.phase = "sending";
      await chrome.storage.session.set({ batch });
      return { ok: true };
    }
    if (message.type === "sent") {
      if (!armed.autoSend || !batch.sendReserved || batch.phase !== "sending") throw new Error("No reserved send attempt.");
      batch.phase = "sent";
      await chrome.storage.session.set({ batch });
      return { ok: true };
    }
    throw new Error("Unknown batch operation.");
  } finally { busy = false; }
}
