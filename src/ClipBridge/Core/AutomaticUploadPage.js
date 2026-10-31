const verificationVisible = element => {
  const rect = element.getBoundingClientRect();
  const style = getComputedStyle(element);
  return rect.width > 0 && rect.height > 0 && style.visibility !== 'hidden' && style.display !== 'none';
};
if (/^just a moment[.\u2026!\s]*$/i.test(document.title.trim()) ||
    [...document.querySelectorAll('#challenge-form, iframe[src^="https://challenges.cloudflare.com/"]')].some(verificationVisible)) {
  return { error: 'Browser verification needs your attention. Playwright is disconnecting. Complete verification yourself in Brave, then click Start again. If it keeps repeating, leave automation stopped; ClipBridge cannot approve the challenge.' };
}
const c = globalThis.ClipBridgeComposer;
const ui = c.find();
const tag = (element, kind) => {
  if (!element) return null;
  const value = args.token + '-' + kind;
  element.setAttribute('data-clipbridge-automation', value);
  return '[data-clipbridge-automation="' + value + '"]';
};
const turns = role => {
  const selector = '[data-message-author-role="' + role + '"], article[data-turn="' + role + '"]';
  return [...document.querySelectorAll(selector)].filter(element => !element.querySelector(selector));
};
const users = turns('user');
const aliasOf = (actual, expected) => {
  const dot = expected.lastIndexOf('.');
  if (dot < 1 || !actual.endsWith(expected.slice(dot))) return false;
  const stem = actual.slice(0, -expected.slice(dot).length);
  return stem.startsWith(expected.slice(0, dot)) && /^ ?\([1-9]\d*\)$/.test(stem.slice(dot));
};
function batchNames(root, editor, items) {
  const counts = new Map();
  const relevant = name => items.some(item => name === item.name || aliasOf(name, item.name));
  for (const attribute of [null, 'title', 'aria-label']) {
    const nodes = [...root.querySelectorAll(attribute ? '[' + attribute + ']' : '*')]
      .filter(element => !editor.contains(element) && verificationVisible(element))
      .map(element => ({ element, name: (attribute ? element.getAttribute(attribute) : element.innerText || '').trim() }))
      .filter(item => relevant(item.name));
    const channel = new Map();
    for (const item of nodes.filter(item => !nodes.some(other => other !== item &&
        item.name === other.name && item.element.contains(other.element)))) {
      channel.set(item.name, (channel.get(item.name) || 0) + 1);
    }
    for (const [name, count] of channel) counts.set(name, Math.max(counts.get(name) || 0, count));
  }
  const missing = [];
  for (const item of items) {
    if (counts.get(item.name) > 0) counts.set(item.name, counts.get(item.name) - 1);
    else missing.push(item);
  }
  const needed = new Map();
  for (const item of missing) needed.set(item.name, (needed.get(item.name) || 0) + 1);
  for (const [name, count] of needed) {
    const aliases = [...counts].filter(([actual, remaining]) => remaining > 0 && aliasOf(actual, name));
    if (aliases.reduce((sum, entry) => sum + entry[1], 0) !== count) return false;
    for (const [actual] of aliases) counts.set(actual, 0);
  }
  return [...counts.values()].every(count => count === 0);
}
const state = {
  focused: document.hasFocus(), visiblePage: document.visibilityState === 'visible',
  found: !!ui, error: c.error(), empty: false, text: '', ready: false,
  thinking: '', thinkingControl: null,
  waiting: '',
  editor: null, send: null, input: null, menu: null, multiple: false,
  users: users.length, assistants: turns('assistant').length,
  responding: c.responding(), confirmed: users.slice(args.usersBefore || 0)
    .some(turn => args.message && turn.textContent.includes(args.message))
};
if (ui) {
  let attachmentRoot = ui.root;
  for (let parent = ui.root.parentElement, depth = 0; parent && depth < 4; parent = parent.parentElement, depth++) {
    if (parent.matches('body, html, main, [role="main"], article, [role="dialog"]') ||
        parent.querySelector('[data-message-author-role], article[data-turn], [data-testid^="conversation-turn"], [role="dialog"]') ||
        [...parent.querySelectorAll('[contenteditable="true"], [contenteditable="plaintext-only"], textarea')]
          .some(editor => editor !== ui.editor && verificationVisible(editor))) break;
    attachmentRoot = parent;
  }
  const attachmentUi = { ...ui, root: attachmentRoot };
  state.empty = c.empty(attachmentUi);
  state.text = c.text(ui.editor);
  const namesReady = batchNames(attachmentRoot, ui.editor, args.items || []);
  state.names = namesReady;
  state.pending = c.pending(attachmentRoot);
  state.sendEnabled = !!ui.button && !ui.button.disabled && ui.button.getAttribute('aria-disabled') !== 'true';
  state.textMatches = state.text === (args.message || '');
  state.ready = c.ready(attachmentUi, [], args.message || '') === true && namesReady;
  state.waiting = !namesReady ? 'Waiting to identify every attachment filename.' : state.pending ?
    'Uploads are still processing.' : !ui.button || ui.button.disabled || ui.button.getAttribute('aria-disabled') === 'true' ?
    'Waiting for ChatGPT to enable Send.' : c.responding() ? 'Waiting for the current response to finish.' : '';
  const level = button => {
    const text = (button.innerText || '').trim();
    if (/^(High|Medium|Low)$/.test(text)) return text;
    const labels = [...button.querySelectorAll('span')].filter(element => verificationVisible(element) &&
      !element.closest('[aria-hidden="true"]') && /^(High|Medium|Low)$/.test(element.innerText.trim()));
    const levels = [...new Set(labels.map(element => element.innerText.trim()))];
    return levels.length === 1 ? levels[0] : '';
  };
  let thinking = [];
  for (let scope = ui.root, depth = 0; scope && depth < 4; scope = scope.parentElement, depth++) {
    if (scope.matches('body, html, main, [role="main"], article, [role="dialog"]') ||
        scope.querySelector('[data-message-author-role], [data-testid^="conversation-turn"]')) break;
    thinking = [...scope.querySelectorAll('button, [role="button"]')].filter(button => verificationVisible(button) &&
      !button.closest('[aria-hidden="true"], [inert]') && (level(button) ||
        /^(Select )?Thinking effort$/i.test(button.getAttribute('aria-label') || '')));
    if (thinking.length) break;
  }
  if (thinking.length === 1) {
    state.thinking = level(thinking[0]);
    state.thinkingControl = tag(thinking[0], 'thinking');
  }
  state.editor = tag(ui.editor, 'editor');
  state.send = tag(ui.button, 'send');
  const candidates = [...document.querySelectorAll('input[type="file"]')].filter(input => {
    if (input.disabled || input.closest('[role="dialog"], [data-message-author-role], article')) return false;
    const accepts = input.accept.toLowerCase().split(',').map(value => value.trim());
    if (!input.accept || accepts.includes('*/*') || accepts.includes('video/*')) return true;
    return (args.items || []).length > 0 && args.items.every(item => {
      const extension = '.' + item.name.split('.').pop().toLowerCase();
      const mime = extension === '.mov' ? 'video/quicktime' : extension === '.webm' ? 'video/webm' : 'video/mp4';
      return accepts.includes(extension) || accepts.includes(mime);
    });
  });
  const inside = candidates.filter(input => ui.root.contains(input));
  const inputs = inside.length ? inside : candidates;
  if (inputs.length === 1 && !inputs[0].files?.length) {
    state.input = tag(inputs[0], 'file');
    state.multiple = inputs[0].multiple;
  }
  const menus = [...ui.root.querySelectorAll('button#composer-plus-btn, button[data-testid="composer-plus-btn"], button[aria-label="Add photos & files"], button[aria-label="Attach files"], button[aria-label="Add files and more"]')]
    .filter(button => button.getClientRects().length && !button.disabled);
  if (menus.length === 1) state.menu = tag(menus[0], 'menu');
  if (!state.confirmed && args.message && state.empty) {
    const normalized = value => value.replace(/\s+/g, ' ').trim();
    const expected = normalized(args.message);
    const rendered = [...document.querySelectorAll('main *, [role="main"] *')].filter(element =>
      verificationVisible(element) && !attachmentRoot.contains(element) &&
      !element.closest('aside, nav, [role="dialog"], [contenteditable], textarea, script, style, [hidden], [inert]') &&
      typeof element.innerText === 'string' && normalized(element.innerText) === expected);
    state.confirmed = rendered.filter(element => !rendered.some(other =>
      other !== element && element.contains(other))).length === 1;
  }
}
return state;
