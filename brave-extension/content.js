"use strict";

// Runs only on chatgpt.com. Operates inside the user's normally signed-in
// Brave browser; no CDP, Playwright, --no-sandbox or account login automation.
(() => {
  const API = "http://127.0.0.1:38419";
  let armed = false;
  let armedConversation = "";
  let busy = false;
  let lastStatus = "";
  const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));

  chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
    if (message?.type === "clipbridge-arm") {
      armed = true;
      armedConversation = location.pathname + location.search;
      report("Armed in this ChatGPT tab. Waiting for local ClipBridge clips.");
      sendResponse({ ok: true });
    }
  });

  async function report(text) {
    if (text === lastStatus) return;
    lastStatus = text;
    try { await chrome.storage.local.set({ lastStatus: text }); }
    catch { /* Browser can be closing or the extension reloaded. */ }
  }

  function inputForVideo() {
    // ChatGPT changes its UI; never assume that an input is guaranteed.
    const inputs = [...document.querySelectorAll('input[type="file"]')];
    return inputs.find(input => !input.disabled && (input.accept === "" ||
      /video|mp4|mov|\*/i.test(input.accept))) ||
      inputs.find(input => !input.disabled) || null;
  }

  async function request(path, key, options = {}) {
    return fetch(API + path, {
      method: options.method || "GET",
      mode: "cors",
      cache: "no-store",
      headers: { "X-ClipBridge-Key": key },
      signal: AbortSignal.timeout(options.timeout || 15000)
    });
  }

  async function poll() {
    if (busy || !armed || document.visibilityState !== "visible") return;
    if (armedConversation !== location.pathname + location.search) {
      armed = false;
      await report("ChatGPT conversation changed. Auto attach stopped; arm the intended tab again.");
      return;
    }
    busy = true;
    let item = null;
    let key = null;
    try {
      const settings = await chrome.storage.local.get(["pairingKey", "enabled"]);
      key = settings.pairingKey;
      if (!settings.enabled || !key) return;

      // Do not claim a file until the ChatGPT attachment input exists.
      const input = inputForVideo();
      if (!input) {
        await report("ChatGPT file input unavailable. Open the attachment menu or refresh this tab.");
        return;
      }

      const result = await request("/api/next", key);
      if (result.status === 401) {
        await report("Pairing key rejected. Copy the current key from ClipBridge Windows.");
        return;
      }
      if (!result.ok) throw new Error("Local ClipBridge service unavailable.");
      item = await result.json();
      if (!item.ready) {
        if (item.reason === "batch-complete")
          await report("Up to 3 clips offered. Verify uploads and send in ChatGPT; click Next batch on the PC.");
        else
          await report("Connected. No prepared clips waiting for upload.");
        return;
      }
      if (!item.id || !item.name || !Number.isFinite(item.size)) throw new Error("Invalid clip data.");
      if (item.size > 525_000_000) throw new Error("Clip exceeds extension's 525 MB safety limit.");

      await report("Preparing " + item.name + " for ChatGPT...");
      const download = await request("/api/file/" + encodeURIComponent(item.id), key, { timeout: 240000 });
      if (!download.ok) throw new Error("Clip could not be retrieved from the local PC.");
      const blob = await download.blob();
      if (blob.size !== item.size) throw new Error("Incomplete local clip transfer.");
      const type = /\.mov$/i.test(item.name) ? "video/quicktime" :
        /\.webm$/i.test(item.name) ? "video/webm" : "video/mp4";
      const file = new File([blob], item.name, { type });
      const data = new DataTransfer();
      data.items.add(file);
      input.files = data.files;
      input.dispatchEvent(new Event("input", { bubbles: true }));
      input.dispatchEvent(new Event("change", { bubbles: true }));
      // This only verifies dispatch to a file input, not the service's upload.
      await sleep(1100);
      const ack = await request("/api/offered/" + encodeURIComponent(item.id),
        key, { method: "POST" });
      if (!ack.ok) throw new Error("Could not record the attachment attempt.");
      await report("Offered " + item.name + " to ChatGPT. Confirm it uploaded before sending.");
    } catch (error) {
      await report("Auto attach problem: " + (error?.message || String(error)) +
        " — use manual attachment if needed.");
      if (item?.id && key) {
        try {
          await request("/api/retry/" + encodeURIComponent(item.id),
            key, { method: "POST" });
        } catch { /* Keep error visible; claim also expires on the PC. */ }
      }
    } finally { busy = false; }
  }

  setInterval(poll, 3500);
})();
