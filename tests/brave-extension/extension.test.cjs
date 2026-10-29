"use strict";
const { test } = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const vm = require("node:vm");
const path = require("node:path");
const root = path.resolve(__dirname, "../..");
function worker(settings = {}) {
  const local = { enabled: true, pairingKey: "A".repeat(64), ...settings };
  const session = {};
  const requests = [];
  const callbacks = {};
  const area = data => ({
    get: async keys => Object.fromEntries((typeof keys === "string" ? [keys] : keys).map(k => [k, data[k]])),
    set: async values => Object.assign(data, values),
    remove: async keys => { for (const k of Array.isArray(keys) ? keys : [keys]) delete data[k]; },
    clear: async () => { for (const k of Object.keys(data)) delete data[k]; },
    setAccessLevel: async value => { callbacks.accessLevel = value.accessLevel; }
  });
  const tab = { id: 7, url: "https://chatgpt.com/c/intended", active: true };
  const sender = { id: "extension", tab, frameId: 0, documentId: "doc", url: tab.url };
  const popup = { id: "extension", url: "chrome-extension://extension/popup.html" };
  const chrome = {
    storage: { local: area(local), session: area(session) },
    runtime: { id: "extension", getURL: name => "chrome-extension://extension/" + name,
      onMessage: { addListener: fn => { callbacks.message = fn; } } },
    tabs: { get: async () => tab, query: async () => [tab], sendMessage: async () => ({ documentId: "doc" }),
      onRemoved: { addListener: fn => { callbacks.removed = fn; } },
      onUpdated: { addListener: fn => { callbacks.updated = fn; } } }
  };
  let fetchImpl = async url => url.endsWith("/api/health") ? Response.json({ ok: true, version: 2, maxBatchCount: 10 }) :
    url.includes("/api/batch?") ? Response.json({ ready: true, remaining: 0, items: Array.from({ length: Number(new URL(url).searchParams.get("count")) }, (_, i) => ({ id: "a".repeat(31) + (i+1), claim: "b".repeat(31) + (i+1), name: "clip" + i + ".mp4", size: 4 })) }) :
    url.endsWith("/api/next") ?
    Response.json({ ready: true, id: "a".repeat(32), claim: "b".repeat(32), name: "clip.mp4", size: 4 }) :
    url.includes("/api/file/") ? new Response(new Uint8Array([1, 2, 3, 4]),
      { status: 206, headers: { "Content-Range": "bytes 0-3/4" } }) : Response.json({ ok: true });
  const context = vm.createContext({ chrome, URL, AbortSignal, Uint8Array, btoa,
    fetch: async (url, options) => { requests.push({ url, options }); return fetchImpl(url, options); } });
  vm.runInContext(fs.readFileSync(path.join(root, "brave-extension/background.js"), "utf8"), context);
  const call = (message, from = sender) => new Promise(resolve => callbacks.message(message, from, resolve));
  return { call, local, session, requests, callbacks, tab, sender, popup,
    fetch: fn => { fetchImpl = fn; } };
}
test("manifest has isolated MV3 worker and narrow permissions", () => {
  const m = JSON.parse(fs.readFileSync(path.join(root, "brave-extension/manifest.json")));
  assert.equal(m.manifest_version, 3);
  assert.equal(m.background.service_worker, "background.js");
  assert.deepEqual(m.host_permissions, ["http://127.0.0.1:38419/*", "https://chatgpt.com/*"]);
  assert.deepEqual(m.content_scripts[0].matches, ["https://chatgpt.com/*"]);
  assert.equal(m.externally_connectable, undefined);
});
test("worker rejects unarmed, foreign tab, subframe and wrong document", async () => {
  const w = worker();
  assert.match((await w.call({ type: "next" })).error, /not armed/);
  assert.equal((await w.call({ type: "arm" }, w.popup)).ok, true);
  for (const from of [{ ...w.sender, tab: { id: 8 } }, { ...w.sender, frameId: 1 },
    { ...w.sender, documentId: "other" }, { ...w.sender, url: "https://chatgpt.com/c/other" }])
    assert.match((await w.call({ type: "next" }, from)).error, /not armed/);
  assert.equal(w.requests.length, 1);
  assert.equal(w.callbacks.accessLevel, "TRUSTED_CONTEXTS");
});
test("pairing key stays in worker; transfer is chunked; attempt stops arming", async () => {
  const w = worker();
  await w.call({ type: "arm" }, w.popup);
  const item = await w.call({ type: "next" });
  assert.equal(item.pairingKey, undefined);
  assert.match((await w.call({ type: "dispatch", id: item.id })).error, /incomplete/);
  const chunk = await w.call({ type: "chunk", id: item.id });
  assert.equal(chunk.data, "AQIDBA==");
  assert.equal(chunk.done, true);
  const fetch = w.requests.find(r => r.url.includes("/api/file/"));
  assert.equal(fetch.options.headers.Range, "bytes=0-3");
  assert.equal(fetch.options.headers["X-ClipBridge-Claim"], "b".repeat(32));
  assert.equal((await w.call({ type: "dispatch", id: item.id })).ok, true);
  assert.equal(w.session.armed, undefined);
  assert.match((await w.call({ type: "next" })).error, /not armed/);
});
test("disable, navigation and reload revoke access", async () => {
  for (const action of [async w => { w.local.enabled = false; },
    async w => { w.tab.url += "-other"; },
    async w => w.callbacks.updated(7, { status: "loading" }),
    async w => w.call({ type: "disable" }, w.popup)]) {
    const w = worker();
    await w.call({ type: "arm" }, w.popup);
    await action(w);
    assert.ok((await w.call({ type: "next" })).error);
  }
});
test("no arbitrary fetch proxy; invalid range responses stop transfers", async () => {
  const w = worker();
  await w.call({ type: "arm" }, w.popup);
  assert.ok((await w.call({ type: "fetch", url: "https://evil.example" })).error);
  const item = await w.call({ type: "next" });
  w.fetch(async () => new Response("whole file"));
  assert.match((await w.call({ type: "chunk", id: item.id })).error, /Invalid local file range/);
});
test("disabled extension cannot be armed implicitly", async () => {
  const w = worker(); w.local.enabled = false;
  assert.match((await w.call({ type: "arm" }, w.popup)).error, /enable/i);
  assert.equal(w.requests.length, 0);
});
test("content script keeps credentials isolated and reserves optional sends before clicking", () => {
  const source = fs.readFileSync(path.join(root, "brave-extension/content.js"), "utf8");
  assert.doesNotMatch(source, /fetch\(|pairingKey|storage\.local|KeyboardEvent/);
  assert.ok(source.indexOf("if (!options.autoSend)") < source.indexOf("ui.button.click()"));
  assert.ok(source.indexOf("type: 'reserve-send'") < source.indexOf("ui.button.click()"));
  assert.match(source, /composer.ready\(ui, batch.items, message\)/);
});

test("multi-file batch dispatch is atomic and auto-send stays off by default", async () => {
  const w = worker(); await w.call({ type: "arm" }, w.popup);
  const batch = await w.call({ type: "batch", multiple: true });
  assert.equal(batch.items.length, 3);
  assert.match((await w.call({ type: "dispatch-batch", batchId: batch.batchId })).error, /incomplete/);
  await Promise.all(batch.items.map(item => w.call({ type: "chunk", id: item.id })));
  assert.equal((await w.call({ type: "dispatch-batch", batchId: batch.batchId })).ok, true);
  assert.match((await w.call({ type: "reserve-send", batchId: batch.batchId })).error, /not authorized/);
  const reservation = w.requests.find(request => request.url.endsWith("/api/offered-batch"));
  assert.equal(JSON.parse(reservation.options.body).length, 3);
  assert.match((await w.call({ type: "batch", multiple: true })).error, /needs review/);
});

test("explicit auto-send authorizes one send only; unknown sends do not advance the queue", async () => {
  const w = worker({ autoSend: true, batchSize: 2 }); await w.call({ type: "arm" }, w.popup);
  const batch = await w.call({ type: "batch", multiple: true });
  assert.equal(batch.items.length, 2);
  await Promise.all(batch.items.map(item => w.call({ type: "chunk", id: item.id })));
  await w.call({ type: "dispatch-batch", batchId: batch.batchId });
  assert.equal((await w.call({ type: "reserve-send", batchId: batch.batchId })).ok, true);
  assert.ok((await w.call({ type: "reserve-send", batchId: batch.batchId })).error);
  assert.ok((await w.call({ type: "batch", multiple: true })).error);
  assert.equal((await w.call({ type: "sent", batchId: batch.batchId })).ok, true);
  assert.equal(w.session.batch.phase, "sent");
});

test("single-file website input and invalid batch settings are respected", async () => {
  const w = worker({ batchSize: 10 }); await w.call({ type: "arm" }, w.popup);
  assert.equal((await w.call({ type: "batch", multiple: false })).items.length, 1);
  const invalid = worker({ batchSize: 11 });
  assert.ok((await invalid.call({ type: "arm" }, invalid.popup)).error);
  assert.equal(invalid.requests.length, 0);
});

test("stopping the Windows bridge prevents a new auto-send reservation", async () => {
  const w = worker({ autoSend: true }); await w.call({ type: "arm" }, w.popup);
  const batch = await w.call({ type: "batch", multiple: true });
  await Promise.all(batch.items.map(item => w.call({ type: "chunk", id: item.id })));
  await w.call({ type: "dispatch-batch", batchId: batch.batchId });
  w.fetch(async () => new Response("offline", { status: 503 }));
  assert.ok((await w.call({ type: "reserve-send", batchId: batch.batchId })).error);
  assert.equal(w.session.batch.sendReserved, false);
});
