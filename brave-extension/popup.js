"use strict";
const byId = id => document.getElementById(id);
const setStatus = text => { byId("status").textContent = text; };

async function load() {
  const { pairingKey = "", enabled = false, lastStatus = "Not connected" } =
    await chrome.storage.local.get(["pairingKey", "enabled", "lastStatus"]);
  byId("key").value = pairingKey;
  byId("enabled").checked = enabled;
  setStatus(lastStatus);
}
byId("save").addEventListener("click", async () => {
  const key = byId("key").value.trim();
  if (!/^[a-fA-F0-9]{64}$/.test(key)) {
    setStatus("Use the 64-character pairing key from ClipBridge Windows.");
    return;
  }
  await chrome.storage.local.set({ pairingKey: key, enabled: byId("enabled").checked });
  setStatus("Saved locally. Open the intended ChatGPT conversation and arm its tab.");
});
byId("disable").addEventListener("click", async () => {
  await chrome.storage.local.set({ enabled: false, lastStatus: "Automatic attachment disabled." });
  byId("enabled").checked = false;
  setStatus("Automatic attachment disabled. Clips stay on your PC.");
});
byId("arm").addEventListener("click", async () => {
  const key = byId("key").value.trim();
  if (!/^[a-fA-F0-9]{64}$/.test(key)) {
    setStatus("Paste the pairing key first.");
    return;
  }
  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
  if (!tab || !tab.url?.startsWith("https://chatgpt.com/")) {
    setStatus("Open a ChatGPT conversation in Brave and try again.");
    return;
  }
  await chrome.storage.local.set({ pairingKey: key, enabled: true });
  byId("enabled").checked = true;
  try {
    await chrome.tabs.sendMessage(tab.id, { type: "clipbridge-arm" });
    setStatus("This tab is armed. ClipBridge will attempt the next queued clips automatically.");
  } catch {
    setStatus("Refresh this ChatGPT tab, then click Arm again.");
  }
});
load().catch(err => setStatus("Extension settings error: " + err.message));
