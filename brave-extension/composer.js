"use strict";
(() => {
  const visible = element => !!element && element.getClientRects().length > 0 &&
    getComputedStyle(element).visibility !== "hidden";
  const text = editor => ("value" in editor ? editor.value : editor.innerText || editor.textContent || "").trim();
  function find() {
    const editor = document.querySelector('#prompt-textarea, textarea[data-testid="prompt-textarea"]');
    if (!editor || !visible(editor)) return null;
    const root = editor.closest('form') || editor.closest('[data-testid="composer"], [data-type="unified-composer"]');
    if (!root) return null;
    const button = [...root.querySelectorAll('button[data-testid="send-button"], button[aria-label="Send prompt"], button[aria-label="Send message"]')].find(visible);
    return { editor, root, button };
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
      ![...document.querySelectorAll('button[data-testid="stop-button"]')].some(visible);
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
  function responding() { return [...document.querySelectorAll('button[data-testid="stop-button"]')].some(visible); }
  globalThis.ClipBridgeComposer = { find, text, error, pending, hasNames, empty, ready, write, turns, responding };
})();
