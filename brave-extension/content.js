"use strict";
(() => {
  let armed = false;
  let conversation = "";
  let busy = false;
  let documentId;
  const send = async message => {
    const result = await chrome.runtime.sendMessage(message);
    if (result?.error) throw new Error(result.error);
    return result;
  };
  const hello = send({ type: "hello" }).then(result => { documentId = result.documentId; });
  chrome.runtime.onMessage.addListener((message, sender, respond) => {
    if (sender.id !== chrome.runtime.id) return;
    if (message?.type === "clipbridge-disarm") { armed = false; respond({ ok: true }); return; }
    if (message?.type === "clipbridge-start") {
      armed = true; conversation = location.href; respond({ ok: true }); return;
    }
    if (message?.type !== "clipbridge-arm") return;
    hello.then(() => {
      armed = false;
      respond({ documentId });
    });
    return true;
  });
  function inputForVideo(name) {
    const extension = name ? "." + name.split(".").pop().toLowerCase() : null;
    return [...document.querySelectorAll('input[type="file"]')].find(input => {
      if (input.disabled) return false;
      const accept = input.accept.toLowerCase().split(",").map(x => x.trim());
      return !input.accept || accept.includes("*/*") || accept.includes("video/*") ||
        (!name && accept.some(type => /^(video\/(mp4|quicktime|webm)|\.(mp4|mov|webm|m4v))$/.test(type))) ||
        (extension && (accept.includes(extension) || accept.includes(
          extension === ".mov" ? "video/quicktime" : extension === ".webm" ? "video/webm" : "video/mp4")));
    });
  }
  function checkContext() {
    if (!armed || location.href !== conversation || document.visibilityState !== "visible")
      throw new Error("Conversation or visibility changed. Review and arm again.");
  }
  async function report(text) {
    try { await send({ type: "status", text }); } catch { /* Disarmed or browser closed. */ }
  }
  async function poll() {
    if (!armed || busy || document.visibilityState !== "visible") return;
    busy = true;
    try {
      checkContext();
      if (!inputForVideo()) {
        await report("No compatible file input. Open ChatGPT's attachment menu; manual attachment is available.");
        return;
      }
      const item = await send({ type: "next" });
      if (!item.ready) {
        await report(item.reason === "batch-complete" ?
          "Batch of up to 3 attempts complete. Review ChatGPT, then allow the next batch on the PC." :
          "Bridge reachable. No clip ready; check the Windows queue.");
        return;
      }
      if (item.size > 525_000_000 || !/\.(mp4|mov|webm|m4v)$/i.test(item.name))
        throw new Error("Unsupported clip or over 525 MB. Use manual attachment or compress to MP4.");
      await report("Transferring " + item.name + " locally. No ChatGPT upload confirmed.");
      const parts = [];
      let done = false;
      while (!done) {
        checkContext();
        const chunk = await send({ type: "chunk", id: item.id });
        parts.push(Uint8Array.from(atob(chunk.data), char => char.charCodeAt(0)));
        done = chunk.done;
      }
      checkContext();
      const input = inputForVideo(item.name);
      if (!input || input.files?.length) throw new Error("Input unavailable or contains files. Review ChatGPT first.");
      const file = new File(parts, item.name, { type: /\.mov$/i.test(item.name) ?
        "video/quicktime" : /\.webm$/i.test(item.name) ? "video/webm" : "video/mp4" });
      if (file.size !== item.size) throw new Error("Incomplete local file transfer.");
      const data = new DataTransfer();
      data.items.add(file);
      await report("Attempt reserved for " + item.name + ". Verify in ChatGPT; re-arm to continue. Upload acceptance is unknown.");
      await send({ type: "dispatch", id: item.id });
      checkContext();
      armed = false;
      input.files = data.files;
      input.dispatchEvent(new Event("change", { bubbles: true }));
    } catch (error) {
      await report("Attachment stopped: " + error.message + " Use manual attachment or retry unoffered clips on the PC.");
      armed = false;
    } finally { busy = false; }
  }
  setInterval(poll, 3500);
})();
