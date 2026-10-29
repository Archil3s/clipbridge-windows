"use strict";
(() => {
  const composer = globalThis.ClipBridgeComposer;
  let armed = false;
  let conversation = "";
  let busy = false;
  let documentId;
  let options = { autoSend: false, batchSize: 3, batchPrompt: "" };
  const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
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
      options = message.options;
      conversation = location.href;
      armed = true;
      respond({ ok: true });
      return;
    }
    if (message?.type !== "clipbridge-arm") return;
    hello.then(() => { armed = false; respond({ documentId }); });
    return true;
  });
  function inputForVideos(names = []) {
    return [...document.querySelectorAll('input[type="file"]')].filter(input => {
      if (input.disabled) return false;
      const accepts = input.accept.toLowerCase().split(',').map(type => type.trim());
      const general = !input.accept || accepts.includes('*/*') || accepts.includes('video/*');
      if (!names.length) return general || accepts.some(type => /^(video\/(mp4|quicktime|webm)|\.(mp4|mov|webm|m4v))$/.test(type));
      return names.every(name => {
        const extension = '.' + name.split('.').pop().toLowerCase();
        const type = extension === '.mov' ? 'video/quicktime' : extension === '.webm' ? 'video/webm' : 'video/mp4';
        return general || accepts.includes(extension) || accepts.includes(type);
      });
    }).sort((a, b) => Number(b.multiple) - Number(a.multiple))[0] || null;
  }
  function checkContext() {
    if (!armed || location.href !== conversation || document.visibilityState !== 'visible')
      throw new Error("Conversation or visibility changed. Review the batch and arm again.");
    const error = composer.error();
    if (error) throw new Error(error);
  }
  async function report(text) { await send({ type: 'status', text }); }
  async function pause(text) {
    try { await send({ type: 'pause', text }); } catch { /* Navigation may already have revoked arming. */ }
    armed = false;
  }
  async function waitFor(predicate, timeout, description, stableMs = 0) {
    const deadline = Date.now() + timeout;
    let since = 0;
    while (Date.now() < deadline) {
      checkContext();
      if (predicate()) {
        if (!since) since = Date.now();
        if (Date.now() - since >= stableMs) return;
      } else since = 0;
      await sleep(250);
    }
    throw new Error(description + " timed out. No automatic retry or send will be made.");
  }
  async function download(item) {
    const parts = [];
    let done = false;
    while (!done) {
      checkContext();
      const chunk = await send({ type: 'chunk', id: item.id });
      const binary = atob(chunk.data);
      const bytes = new Uint8Array(binary.length);
      for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
      parts.push(bytes);
      done = chunk.done;
    }
    const file = new File(parts, item.name, { type: /\.mov$/i.test(item.name) ?
      'video/quicktime' : /\.webm$/i.test(item.name) ? 'video/webm' : 'video/mp4' });
    if (file.size !== item.size) throw new Error("Incomplete local transfer for " + item.name);
    return file;
  }
  async function downloadBatch(items) {
    const files = new Array(items.length);
    let next = 0;
    await Promise.all(Array.from({ length: Math.min(2, items.length) }, async () => {
      while (next < items.length) {
        const index = next++;
        files[index] = await download(items[index]);
      }
    }));
    return files;
  }
  async function poll() {
    if (!armed || busy || document.visibilityState !== 'visible') return;
    busy = true;
    try {
      checkContext();
      let ui = composer.find();
      if (!ui) {
        await report('Waiting for the ChatGPT message box to load. No clips claimed.');
        await waitFor(() => { ui = composer.find(); return !!ui; }, 30000, composer.problem());
      }
      if (!composer.empty(ui)) throw new Error("The composer already contains text or attachments. Send or remove them before arming.");
      const firstInput = inputForVideos();
      if (!firstInput) {
        await report("No compatible file input. Open ChatGPT's attachment menu; waiting without claiming clips.");
        return;
      }
      const batch = await send({ type: 'batch', multiple: firstInput.multiple });
      if (!batch.ready) {
        const reason = batch.reason === 'empty' ? 'No more unattempted clips. Batch queue complete.' :
          'Queue paused: ' + batch.reason + '. Review the Windows queue or retry unattempted transfers.';
        await pause(reason);
        return;
      }
      await report("Transferring batch " + batch.number + ': ' + batch.items.length +
        ' clips locally (two transfers at once). ChatGPT upload acceptance is not confirmed.');
      const files = await downloadBatch(batch.items);
      checkContext();
      ui = composer.find();
      const input = inputForVideos(batch.items.map(item => item.name));
      if (!ui || !composer.empty(ui) || !input || input.files?.length || (files.length > 1 && !input.multiple))
        throw new Error("Composer or file input changed during transfer. Review before retrying.");
      const data = new DataTransfer();
      for (const file of files) data.items.add(file);
      await send({ type: 'dispatch-batch', batchId: batch.batchId });
      checkContext();
      input.files = data.files;
      input.dispatchEvent(new Event('change', { bubbles: true }));
      if (!options.autoSend) {
        await pause('Offered ' + files.length + ' clips together. Auto-send is off: verify uploads and send manually, then arm again.');
        return;
      }
      const message = (options.batchPrompt.trim() || 'Gardening B-roll clips for reels. Please wait for my instructions.') +
        ' (Batch ' + batch.number + ')';
      ui = composer.find();
      if (!ui) throw new Error("Composer unavailable after attachment. Send manually.");
      composer.write(ui.editor, message);
      await report('Batch ' + batch.number + ': waiting for all filenames, no upload indicators, and an enabled Send button.');
      await waitFor(() => {
        const current = composer.find();
        if (!current || composer.text(current.editor) !== message) throw new Error("Composer text changed. Auto-send paused to preserve your edits.");
        return composer.ready(current, batch.items, message);
      }, 600000, 'ChatGPT upload readiness', 1500);
      const usersBefore = composer.turns('user').length;
      const assistantsBefore = composer.turns('assistant').length;
      await send({ type: 'reserve-send', batchId: batch.batchId });
      checkContext();
      ui = composer.find();
      if (!composer.ready(ui, batch.items, message)) throw new Error("Upload readiness changed before Send. Send manually after checking.");
      // Click once after recording the send attempt; an uncertain result is never replayed.
      ui.button.click();
      await waitFor(() => {
        const users = composer.turns('user');
        const current = composer.find();
        return users.length > usersBefore && users.slice(usersBefore).some(turn => turn.textContent.includes(message)) &&
          current && composer.text(current.editor) === '' && !composer.hasNames(current.root, batch.items);
      }, 30000, 'New user message confirmation');
      await send({ type: 'sent', batchId: batch.batchId });
      await report('Batch ' + batch.number + ': a new user message is visible in chat. ' + batch.remaining + ' clips remain.');
      if (batch.remaining === 0) { await pause('All queued batches submitted; new user messages were observed. Review the conversation for website errors.'); return; }
      await report('Batch ' + batch.number + ' submitted. Waiting for ChatGPT to finish responding before the next batch.');
      await waitFor(() => composer.turns('assistant').length > assistantsBefore && !composer.responding(),
        600000, 'ChatGPT response completion', 1500);
    } catch (error) {
      await pause('Batch stopped: ' + error.message + ' Reserved attempts are not repeated; review ChatGPT or use manual attachment.');
    } finally { busy = false; }
  }
  setInterval(poll, 1500);
})();
