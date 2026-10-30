# Validation — ClipBridge Brave attachment reliability

## Automated evidence

- Windows 0.6.3: synthetic tests exercise numbered/nested attachment names, High menu and slider selection, one Enter per batch, missing or changed High controls, mismatched filenames and Enter without confirmation. A seven-clip scenario uses article-based conversation roles and submits 3+3+1 to one new chat. Queue tests verify that a failed confirmation write changes neither in-memory state nor conversation binding, and that a successful update restores both after restart. These checks do not establish live ChatGPT upload acceptance.

- Windows 0.6.2: synthetic verification title/form cases stop before claiming files or clicking verification controls; verification appearing during attachment retains only the attempted batch for review. Hidden challenge forms do not block a valid composer. The real loopback CDP test uses the production session and checks retained page state, no duplicate/reloaded tab, preserved dark preference, missing-target rejection, exact-byte batch uploads and browser survival after disconnect. Live Cloudflare acceptance remains unverified.

- Windows 0.6.1 / extension 0.3.2: inspected live composer controls in Brave without submitting files or messages. Observed a role textbox labelled Ask ChatGPT, an Add files and more attachment button, a plain Send label, and a button-based attachment popover with the subtitle Upload from computer. Synthetic regression tests cover this structure, ambiguous hidden inputs, native file chooser handling and waiting for a labelled Stop control to disappear. This validates the compatibility change; full live video acceptance remains unverified.

- Windows 0.6.0 Playwright suite (`--automation`): saved queue deduplication, single-instance locking, durable reservations/reviewed retries, changed-file rejection, corrupt journal preservation, destination restrictions and independent batch byte caps. Synthetic browser scenarios exercise automatic batches of 3+1, exact file-byte checks, dynamic file pickers, a real loopback CDP connection with sandbox enabled, confirmed new-chat navigation, progress, disabled Send, missing filenames, errors, drafts/edits, Stop, navigation and uncertain sends. These do not establish live ChatGPT or Google acceptance.
- Windows .NET 10 application: Release compilation with warnings treated as errors.
- .NET executable regression runner: authorization, webpage-origin rejection, origin-less Chromium extension requests, authenticated ranged transfers, claim rotation, stale acknowledgement rejection, idempotent reservations, explicit retries, legacy three-attempt ceiling, configurable atomic batches, and durable reservation history across bridge restart.
- Optional Cloudflare configuration: disabled/invalid URL, missing/short/malformed token and valid configuration paths. Invalid monitoring returns a disabled reporter and a local-continuation status.
- B-roll metadata: custom destination reload, multi-tag persistence, tag edits, any-selected-tag filtering and no-filter behavior.
- Real FFmpeg synthetic-video tests: stream-copy splitting, compression with invalid monitoring disabled, B-roll export into a category directory, expected In/Out duration and unchanged source bytes. Run with `dotnet run --project tests/ClipBridge.Core.Tests -c Release -- --video` on Windows with FFmpeg/FFprobe in ClipBridge's installed tools directory.
- Eleven Node tests cover manifest permissions, credential isolation, unarmed/foreign-tab/frame/document rejection, chunk protocol, navigation/reload/disable, rejected malformed ranges and explicit auto-send authorization and one-send reservation guards.
- Nineteen sandboxed Chromium layout checks cover textarea and rich editors, form and formless containers, hidden placeholders, sibling attachments, alternate editor attributes, labelled controls without IDs, disabled/read-only fields, dialogs, conversation editors and ambiguous layouts. A further rich-editor check exercises text insertion and readiness with pending uploads, disabled Send and user edits.
- Eleven real sandboxed Chromium scenarios load the actual MV3/composer code, use the real .NET bridge on an isolated test port, and exercise synthetic ChatGPT-shaped pages. They verify three-file dispatch with auto-send off, automatic batches of 3+1 with exact 700,000-byte payloads, formless rich editors with a hidden first editor, delayed composer mounting without early claims, pending uploads, disabled Send, missing filenames, visible limits, user edits, navigation and an existing draft. The test-only extension copy changes only its loopback port so the user's running app is untouched. No normal Brave profile or real account is used. Windows CI runs these before packaging.
- The existing dashboard CI runs desktop/phone and axe accessibility tests for every PR. No dashboard application source is changed by this task.

## Reproduce

```powershell
dotnet build src/ClipBridge/ClipBridge.csproj -c Release -warnaserror
dotnet run --project tests/ClipBridge.Core.Tests -c Release
pwsh tests/ClipBridge.Core.Tests/bin/Release/net10.0-windows/playwright.ps1 install chromium
dotnet run --project tests/ClipBridge.Core.Tests -c Release -- --automation
node --test tests/brave-extension/extension.test.cjs
npm --prefix cloudflare-dashboard install --no-audit --no-fund --no-package-lock
# Install the test Chromium through the existing dashboard Playwright dependency:
Push-Location cloudflare-dashboard
npx playwright install chromium
Pop-Location
node tests/brave-extension/composer-browser.cjs
node tests/brave-extension/browser-transport.cjs
```

The regression runner is an executable assertion harness without a test-framework package; Microsoft.Playwright supplies the browser automation. It is invoked with `dotnet run`, not reported as a discovered xUnit/MSTest suite. Exceptions/nonzero exits fail CI. Temporary fixtures are under ignored `work/`.

## Manual checks still required

- A completed Windows UI smoke test of the newly built executable: keyboard focus, narrow desktop/high-DPI layout, source preview/scrubbing and media-codec playback. The earlier desktop inspection was interrupted; compilation is not evidence of UI validation.
- Installation/uninstallation of the generated installer on a clean Windows account.
- Google sign-in in normal Brave. No login automation or security bypass is implemented or claimed as tested.
- Real ChatGPT video acceptance, upload completion, account-specific size/rate limits and changes to the live website. The synthetic browser test does not establish any of these.
- Long recordings (including 15 GB), disk-full recovery, interrupted encodes and live Cloudflare status delivery. The synthetic files do not establish those behaviors.

The legacy extension defaults to manual review and Send; its auto-send remains opt-in. The Playwright workflow starts through an explicit **Start automatic uploads + Send** action. Both use visible readiness checks, record a single send attempt, require a new user turn, and wait for an assistant response before the next batch. Limits, unknown states, edits or navigation pause rather than guessing. A reserved/attempted status never means upload accepted. Review uncertain or rejected attempts before retrying.

## Quality review

Pass (source/automated checks): existing dark theme preserved; scrolling prevents the main settings area from exceeding the viewport without a navigation path; pairing/attempt/queue statuses use actual state; optional monitoring cannot gate local work; no new package or framework is introduced into the app; no user videos, browser profiles, credentials or personal Worker URL are committed.

Unverified: rendered WPF layout, keyboard/focus behavior and measured contrast across all control states. No full desktop accessibility certification or live-site upload verification is claimed.

The supplied generic Flutter commands do not apply to this C#/WPF repository: it has no `pubspec.yaml` or Dart sources. The .NET build and executable regression checks are the project validation gates.
