"use strict";
const API = "http://127.0.0.1:38419";
const ready = chrome.storage.local.setAccessLevel({ accessLevel: "TRUSTED_CONTEXTS" });
const popupUrl = chrome.runtime.getURL("popup.html");
const chunkSize = 192 * 1024;
let busy = false;

async function request(path, method = "GET", range, claim) {
  const { pairingKey, enabled } = await chrome.storage.local.get(["pairingKey", "enabled"]);
  if (!enabled || !/^[a-fA-F0-9]{64}$/.test(pairingKey || ""))
    throw new Error("Enable and pair the extension first.");
  const response = await fetch(API + path, {
    method, cache: "no-store", credentials: "omit", redirect: "error",
    headers: { "X-ClipBridge-Key": pairingKey, "X-ClipBridge-Extension": chrome.runtime.id, ...(range ? { Range: range } : {}), ...(claim ? { "X-ClipBridge-Claim": claim } : {}) },
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
      const { enabled } = await chrome.storage.local.get("enabled");
      if (!enabled) throw new Error("Check Enable and save settings before arming.");
      const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
      if (!tab?.url || new URL(tab.url).origin !== "https://chatgpt.com")
        throw new Error("Open the intended ChatGPT conversation first.");
      if (busy) throw new Error("Wait for the current transfer operation, then arm again.");
      await request("/api/health");
      const response = await chrome.tabs.sendMessage(tab.id, { type: "clipbridge-arm" }, { frameId: 0 });
      if (!response?.documentId) throw new Error("Refresh ChatGPT and try again.");
      await chrome.storage.session.remove("transfer");
      await chrome.storage.session.set({ armed: {
        tabId: tab.id, url: tab.url, documentId: response.documentId
      } });
      await chrome.tabs.sendMessage(tab.id, { type: "clipbridge-start" }, { documentId: response.documentId });
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
  if (armed?.tabId === tabId && (change.url || change.status === "loading"))
    await chrome.storage.session.remove("armed");
});
