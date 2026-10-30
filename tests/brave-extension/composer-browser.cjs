"use strict";
const assert = require('node:assert/strict');
const path = require('node:path');
const { chromium } = require('../../cloudflare-dashboard/node_modules/playwright');
const source = path.resolve(__dirname, '../../brave-extension/composer.js');
const editor = '<div id="prompt-textarea" contenteditable="true" class="ProseMirror" role="textbox"><p><br></p></div>';
const chips = '<div id="chips"></div>';
const send = '<button id="composer-submit-button" type="button">Send</button>';
const wrap = html => '<!doctype html><html><body><main>' + html + '</main></body></html>';

(async () => {
  const browser = await chromium.launch({headless: true, chromiumSandbox: true,
    ...(process.env.CHROMIUM_EXE ? {executablePath: process.env.CHROMIUM_EXE} : {})});
  try {
    const page = await browser.newPage();
    const cases = [
      ['legacy textarea form', '<form><textarea id="prompt-textarea"></textarea>' + chips + send + '</form>', true],
      ['formless contenteditable', '<div><section>' + editor + '</section>' + chips + send + '</div>', true],
      ['hidden textarea before visible editor', '<textarea id="prompt-textarea" hidden></textarea><div id="composer-background">' + editor + chips + send + '</div>', true],
      ['textarea name without id', '<form><textarea name="prompt-textarea"></textarea>' + chips + send + '</form>', true],
      ['role textbox without prompt id', '<div data-testid="composer">' + editor.replace('id="prompt-textarea"', '') + chips + send + '</div>', true],
      ['nested composer wrapper and sibling attachments', '<form>' + chips + '<div data-type="unified-composer">' + editor + send + '</div></form>', true],
      ['textbox data-testid without id', '<div data-type="unified-composer">' + editor.replace('id="prompt-textarea"', 'data-testid="prompt-textarea"') + chips + send + '</div>', true],
      ['unknown empty form with editor id', '<form>' + editor + chips + '</form>', true],
      ['empty composer with only attachment button', '<div>' + editor + chips + '<button id="composer-plus-btn">Attach</button></div>', true],
      ['current semantic composer with no IDs', '<div><div><div><div><div>' + editor.replace('id="prompt-textarea"', 'aria-label="Ask ChatGPT"') + '</div></div></div></div>' + chips + '<button aria-label="Add files and more">+</button><button aria-label="Start Voice">Voice</button></div>', true],
      ['unrelated search textarea', '<form><textarea placeholder="Search"></textarea>' + send + '</form>', false],
      ['disabled editor', '<form><textarea id="prompt-textarea" disabled></textarea>' + send + '</form>', false],
      ['read-only editor', '<form><textarea id="prompt-textarea" readonly></textarea>' + send + '</form>', false],
      ['noneditable placeholder with prompt id', '<form><div id="prompt-textarea">Loading</div>' + send + '</form>', false],
      ['message editor within conversation', '<article><form>' + editor + send + '</form></article>', false],
      ['editor in modal dialog', '<div role="dialog"><form>' + editor + send + '</form></div>', false],
      ['two visible composers', '<form>' + editor + send + '</form><form>' + editor + send + '</form>', false],
      ['editor shares page root with unrelated send', editor + '<aside>' + send + '</aside>', false],
      ['container includes conversation history', '<div><div data-message-author-role="user">old.mp4</div>' + editor + send + '</div>', false]
    ];
    for (const [name, html, expected] of cases) {
      await page.setContent(wrap(html));
      await page.addScriptTag({path: source});
      const result = await page.evaluate(() => {
        const ui = ClipBridgeComposer.find();
        return {found: !!ui, includesChips: !!ui?.root.querySelector('#chips')};
      });
      assert.equal(result.found, expected, name);
      if (expected) assert.equal(result.includesChips, true, name + ' includes sibling attachments');
      console.log('PASS composer detection: ' + name);
    }
    await page.setContent(wrap('<div id="composer-background">' + editor + chips + send + '</div>'));
    await page.addScriptTag({path: source});
    const readiness = await page.evaluate(() => {
      const c = ClipBridgeComposer;
      const ui = c.find();
      const results = [c.empty(ui)];
      c.write(ui.editor, 'Batch for reels');
      const items = [{name: 'clip.mp4'}];
      results.push(c.ready(c.find(), items, 'Batch for reels'));
      document.querySelector('#chips').innerHTML = '<span>clip.mp4</span><span role="progressbar">Uploading</span>';
      results.push(c.ready(c.find(), items, 'Batch for reels'));
      document.querySelector('[role="progressbar"]').remove();
      results.push(c.ready(c.find(), items, 'Batch for reels'));
      ui.button.disabled = true;
      results.push(c.ready(c.find(), items, 'Batch for reels'));
      ui.button.disabled = false;
      ui.button.setAttribute('aria-disabled', 'true');
      results.push(c.ready(c.find(), items, 'Batch for reels'));
      ui.button.removeAttribute('aria-disabled');
      ui.editor.textContent = 'User changed the message';
      results.push(c.ready(c.find(), items, 'Batch for reels'));
      return results;
    });
    assert.deepEqual(readiness, [true, false, false, true, false, false, false]);
    console.log('PASS formless rich composer readiness, progress, disabled Send and draft edits');
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
