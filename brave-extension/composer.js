"use strict";
(() => {
  const visible = element => !!element && element.getClientRects().length > 0 &&
    getComputedStyle(element).visibility !== "hidden" && !element.closest('[hidden], [inert]');
  const editorSelector = '#prompt-textarea, [data-testid="prompt-textarea"], textarea[name="prompt-textarea"], ' +
    '[contenteditable="true"].ProseMirror, [contenteditable="true"][role="textbox"], [contenteditable="plaintext-only"][role="textbox"]';
  const sendSelector = 'button[data-testid="send-button"], button#composer-submit-button, ' +
    'button[aria-label="Send prompt"], button[aria-label="Send message"], button[aria-label="Send"]';
  const stopSelector = 'button[data-testid="stop-button"], button[aria-label="Stop"], ' +
    'button[aria-label="Stop generating"], button[aria-label="Stop streaming"]';
  const rootSelector = 'form, [data-testid="composer"], [data-type="unified-composer"], #composer-background';
  const boundarySelector = 'body, html, main, [role="main"], article, [data-message-author-role], ' +
    '[data-testid^="conversation-turn"], [role="dialog"]';
  const controlSelector = sendSelector + ', ' + stopSelector + ', button#composer-plus-btn, ' +
    'button[data-testid="composer-plus-btn"], button[data-testid="voice-mode-button"], ' +
    'button[aria-label="Add files and more"], button[aria-label="Add photos & files"], button[aria-label="Attach files"]';
  const text = editor => ("value" in editor ? editor.value : editor.innerText || editor.textContent || "").trim();
  function editors() {
    return [...document.querySelectorAll(editorSelector)].filter(editor => visible(editor) &&
      !editor.closest('[role="dialog"], article, [data-message-author-role], [data-testid^="conversation-turn"]') &&
      ((editor instanceof HTMLTextAreaElement && !editor.disabled && !editor.readOnly) || editor.isContentEditable));
  }
  function rootFor(editor, candidates) {
    let fallback = null;
    let controls = null;
    for (let root = editor.parentElement, depth = 0; root && depth < 12; root = root.parentElement, depth++) {
      if (root.matches(boundarySelector) || root.querySelector('[data-message-author-role], [data-testid^="conversation-turn"]')) break;
      if (candidates.filter(candidate => root.contains(candidate)).length !== 1) break;
      if (!controls && [...root.querySelectorAll(controlSelector)].some(visible)) controls = root;
      if (root.matches(rootSelector)) fallback = root;
    }
    return fallback && (!controls || fallback.contains(controls)) ? fallback : controls;
  }
  function find() {
    const candidates = editors();
    const composers = candidates.map(editor => {
      const root = rootFor(editor, candidates);
      if (!root) return null;
      const buttons = [...root.querySelectorAll(sendSelector)].filter(visible);
      return { editor, root, button: buttons.length === 1 ? buttons[0] : null };
    }).filter(Boolean);
    return composers.length === 1 ? composers[0] : null;
  }
  function problem() {
    return editors().length === 0 ? 'No visible ChatGPT message box found. Open the chat and refresh the page, then arm again.' :
      'The message box layout is ambiguous or unsupported. No clips were claimed. Refresh the chat before arming again.';
  }
  function error() {
    const alert = [...document.querySelectorAll('[role="alert"], [data-sonner-toast][data-type="error"]')]
      .find(element => visible(element) && element.textContent.trim());
    if (alert) return "ChatGPT reported: " + alert.textContent.trim().slice(0, 240);
    if ([...document.querySelectorAll('[role="dialog"][aria-modal="true"]')].some(visible))
      return "A ChatGPT dialog is open. Review it before continuing.";
    return null;
  }
  function pending(root) {
    return [...root.querySelectorAll('[role="progressbar"], [aria-busy="true"], .animate-spin, [data-state="uploading"], [data-state="processing"], [data-status="uploading"], [aria-label*="Cancel upload" i]')].some(visible);
  }
  function hasNames(root, items) {
    const counts = new Map();
    for (const item of items) counts.set(item.name, (counts.get(item.name) || 0) + 1);
    for (const [name, count] of counts) {
      const textMatches = [...root.querySelectorAll('*')].filter(element => visible(element) &&
        element.children.length === 0 && element.textContent.trim() === name).length;
      const labelMatches = [...root.querySelectorAll('[title], [aria-label]')].filter(element => visible(element) &&
        (element.getAttribute('title') === name || element.getAttribute('aria-label') === name)).length;
      if (Math.max(textMatches, labelMatches) < count) return false;
    }
    return true;
  }
  function empty(composer) {
    return !text(composer.editor) && !pending(composer.root) &&
      !composer.root.querySelector('img, video, audio, [data-testid="file-upload"], [data-testid="attachment"]') &&
      !/\.(mp4|mov|m4v|webm|pdf|png|jpe?g)\b/i.test(composer.root.innerText) &&
      ![...composer.root.querySelectorAll('input[type="file"]')].some(input => input.files?.length);
  }
  function ready(composer, items, expectedText) {
    return composer && text(composer.editor) === expectedText && hasNames(composer.root, items) &&
      !pending(composer.root) && composer.button && !composer.button.disabled &&
      composer.button.getAttribute('aria-disabled') !== 'true' &&
      !responding();
  }
  function write(editor, value) {
    if (text(editor)) throw new Error("The composer contains your text. Send manually or clear it before arming.");
    editor.focus();
    if (editor instanceof HTMLTextAreaElement) {
      Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype, 'value').set.call(editor, value);
      editor.dispatchEvent(new Event('input', { bubbles: true }));
    } else {
      if (!document.execCommand('insertText', false, value)) throw new Error("Could not insert the batch message. Send manually.");
    }
    if (text(editor) !== value) throw new Error("Could not verify batch message text. Send manually.");
  }
  function turns(role) { return [...document.querySelectorAll('[data-message-author-role="' + role + '"]')]; }
  function responding() { return [...document.querySelectorAll(stopSelector)].some(visible); }
  globalThis.ClipBridgeComposer = { find, problem, text, error, pending, hasNames, empty, ready, write, turns, responding };
})();
