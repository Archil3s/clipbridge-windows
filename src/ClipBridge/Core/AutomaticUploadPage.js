const c = globalThis.ClipBridgeComposer;
const ui = c.find();
const tag = (element, kind) => {
  if (!element) return null;
  const value = args.token + '-' + kind;
  element.setAttribute('data-clipbridge-automation', value);
  return '[data-clipbridge-automation="' + value + '"]';
};
const users = c.turns('user');
const state = {
  found: !!ui, error: c.error(), empty: false, text: '', ready: false,
  editor: null, send: null, input: null, menu: null, multiple: false,
  users: users.length, assistants: c.turns('assistant').length,
  responding: c.responding(), confirmed: users.slice(args.usersBefore || 0)
    .some(turn => args.message && turn.textContent.includes(args.message))
};
if (ui) {
  state.empty = c.empty(ui);
  state.text = c.text(ui.editor);
  state.ready = c.ready(ui, args.items || [], args.message || '') === true;
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
}
return state;
