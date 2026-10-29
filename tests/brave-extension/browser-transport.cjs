"use strict";
const assert = require("node:assert/strict");
const { spawn } = require("node:child_process");
const fs = require("node:fs/promises");
const path = require("node:path");
const { chromium } = require("../../cloudflare-dashboard/node_modules/playwright");
const root = path.resolve(__dirname, "../..");
(async () => {
  const dotnet = process.env.DOTNET_EXE || "dotnet";
  const dll = path.join(root, "tests/ClipBridge.Core.Tests/bin/Release/net10.0-windows/ClipBridge.Core.Tests.dll");
  const server = spawn(dotnet, [dll, "--serve"], { cwd: root, windowsHide: true });
  let output = "";
  const ready = new Promise((resolve, reject) => {
    const timeout = setTimeout(() => reject(new Error("Bridge fixture did not start: " + output)), 20000);
    server.stdout.on("data", data => {
      output += data;
      if (output.includes("BRIDGE_TEST_READY")) { clearTimeout(timeout); resolve(); }
    });
    server.stderr.on("data", data => { output += data; });
    server.once("error", error => { clearTimeout(timeout); reject(error); });
    server.once("exit", code => { clearTimeout(timeout); reject(new Error("Fixture exited: " + code + " " + output)); });
  });
  let context;
  try {
    await ready;
    const profile = await fs.mkdtemp(path.join(root, "work", "extension-browser-"));
    const extension = path.join(root, "brave-extension");
    context = await chromium.launchPersistentContext(profile, {
      channel: "chromium", headless: true, chromiumSandbox: true,
      ...(process.env.CHROMIUM_EXE ? { executablePath: process.env.CHROMIUM_EXE } : {}),
      args: [`--disable-extensions-except=${extension}`, `--load-extension=${extension}`]
    });
    const worker = context.serviceWorkers()[0] || await context.waitForEvent("serviceworker");
    const page = await context.newPage();
    await page.route("https://chatgpt.com/**", route => route.fulfill({
      contentType: "text/html", body: '<!doctype html><html><body><input type="file" accept="video/mp4"><button id="send">Send</button><script>window.attachments=[];window.sent=0;document.querySelector("input").onchange=e=>window.attachments.push({name:e.target.files[0].name,size:e.target.files[0].size});document.querySelector("button").onclick=()=>window.sent++;</script></body></html>'
    }));
    await page.goto("https://chatgpt.com/c/clipbridge-fixture");
    await page.bringToFront();
    await worker.evaluate(async () => {
      await chrome.storage.local.set({ pairingKey: "A".repeat(64), enabled: true });
      const result = await handle({ type: "arm" }, {
        id: chrome.runtime.id, url: chrome.runtime.getURL("popup.html")
      });
      if (!result.ok) throw new Error("Arming failed");
    });
    console.log("Armed synthetic page");
    await page.waitForFunction(() => window.attachments.length === 1, null, { timeout: 20000 });
    const observed = await page.evaluate(async () => ({ attachments: window.attachments, sent: window.sent,
      bytes: Array.from(new Uint8Array(await document.querySelector('input').files[0].arrayBuffer())) }));
    assert.deepEqual(observed.attachments, [{ name: "transport.mp4", size: 700000 }]);
    assert.equal(observed.sent, 0);
    assert.equal(observed.bytes.length, 700000);
    assert.ok(observed.bytes.every((byte, i) => byte === i % 251));
    const state = await worker.evaluate(() => chrome.storage.session.get(["armed", "transfer"]));
    assert.equal(state.armed, undefined);
    assert.equal(state.transfer, undefined);
    console.log("PASS sandboxed Chromium MV3 -> authenticated .NET bridge -> chunked content-script transfer -> synthetic file input");
    console.log("PASS exact 700000-byte payload, disarming after one attempt, zero Send events");
    console.log("Synthetic local UI only: no Brave, Google login or real ChatGPT upload acceptance tested.");
  } finally {
    await context?.close();
    server.kill();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
