"use strict";
const byId = id => document.getElementById(id);
const setStatus = text => { byId('status').textContent = text; };
async function load() {
  byId('version').textContent = chrome.runtime.getManifest().version;
  await chrome.runtime.sendMessage({ type: 'settings-ready' });
  const values = await chrome.storage.local.get(['pairingKey', 'enabled', 'lastStatus', 'batchSize', 'autoSend', 'batchPrompt']);
  byId('key').value = values.pairingKey || '';
  byId('enabled').checked = values.enabled === true;
  byId('batchSize').value = values.batchSize || 3;
  byId('autoSend').checked = values.autoSend === true;
  if (typeof values.batchPrompt === 'string') byId('batchPrompt').value = values.batchPrompt;
  setStatus(values.lastStatus || 'Not paired');
}
async function save() {
  const key = byId('key').value.trim();
  const batchSize = Number(byId('batchSize').value);
  if (!/^[a-fA-F0-9]{64}$/.test(key)) throw new Error('Use the 64-character pairing key from ClipBridge Windows.');
  if (!Number.isInteger(batchSize) || batchSize < 1 || batchSize > 10) throw new Error('Choose 1 to 10 clips per message.');
  await chrome.runtime.sendMessage({ type: 'disable' });
  await chrome.storage.local.set({ pairingKey: key, enabled: byId('enabled').checked,
    batchSize, autoSend: byId('autoSend').checked, batchPrompt: byId('batchPrompt').value.trim() });
}
byId('save').addEventListener('click', async () => {
  try { await save(); setStatus('Saved. Clear the intended ChatGPT composer, then arm this tab.'); }
  catch (error) { setStatus(error.message); }
});
byId('disable').addEventListener('click', async () => {
  await chrome.runtime.sendMessage({ type: 'disable' });
  await chrome.storage.local.set({ enabled: false, lastStatus: 'Automatic attachment and send disabled.' });
  byId('enabled').checked = false;
  setStatus('Stopped. Review any batch already offered or sent before restarting.');
});
byId('arm').addEventListener('click', async () => {
  try {
    if (!byId('enabled').checked) throw new Error('Check Enable before arming.');
    await save();
    const result = await chrome.runtime.sendMessage({ type: 'arm' });
    if (result?.error) throw new Error(result.error);
    setStatus(byId('autoSend').checked ?
      'Armed for batch uploads and automatic Send in this tab. Keep it visible; Disable stops the queue.' :
      'Armed for one multi-clip batch. Auto-send is off; review and send manually.');
  } catch (error) { setStatus(error.message); }
});
chrome.storage.onChanged.addListener(changes => {
  if (changes.lastStatus) setStatus(changes.lastStatus.newValue);
});
load().catch(error => setStatus('Extension settings error: ' + error.message));
