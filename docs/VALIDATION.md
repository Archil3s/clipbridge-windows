# Validation — ClipBridge Brave attachment reliability

## Automated evidence

- Windows .NET 10 application: Release compilation with warnings treated as errors.
- Dependency-free .NET regression runner: authorization, webpage-origin rejection, origin-less Chromium extension requests, authenticated ranged transfers, claim rotation, stale acknowledgement rejection, idempotent reservations, explicit retries, three-attempt batch ceiling, and durable reservation history across bridge restart.
- Optional Cloudflare configuration: disabled/invalid URL, missing/short/malformed token and valid configuration paths. Invalid monitoring returns a disabled reporter and a local-continuation status.
- B-roll metadata: custom destination reload, multi-tag persistence, tag edits, any-selected-tag filtering and no-filter behavior.
- Real FFmpeg synthetic-video tests: stream-copy splitting, compression with invalid monitoring disabled, B-roll export into a category directory, expected In/Out duration and unchanged source bytes. Run with `dotnet run --project tests/ClipBridge.Core.Tests -c Release -- --video` on Windows with FFmpeg/FFprobe in ClipBridge's installed tools directory.
- Seven Node tests cover manifest permissions, credential isolation, unarmed/foreign-tab/frame/document rejection, chunk protocol, navigation/reload/disable, rejected malformed ranges and absence of automatic send code.
- A real, sandboxed Chromium test loads the actual MV3 extension, uses the real .NET loopback bridge and transfers a synthetic 700,000-byte payload into a synthetic ChatGPT-shaped file input. It verifies exact bytes, one-attempt disarming, and zero Send events. This uses a separate test profile, never normal Brave or user credentials. It caught Chromium's missing GET Origin header and explicit MIME-input detection defects. Windows CI runs it before packaging.
- The existing dashboard CI runs desktop/phone and axe accessibility tests for every PR. No dashboard application source is changed by this task.

## Reproduce

```powershell
dotnet build src/ClipBridge/ClipBridge.csproj -c Release -warnaserror
dotnet run --project tests/ClipBridge.Core.Tests -c Release
node --test tests/brave-extension/extension.test.cjs
npm --prefix cloudflare-dashboard install --no-audit --no-fund --no-package-lock
# Install the test Chromium through the existing dashboard Playwright dependency:
Push-Location cloudflare-dashboard
npx playwright install chromium
Pop-Location
node tests/brave-extension/browser-transport.cjs
```

The regression runner is an executable assertion harness without additional NuGet dependencies; it is invoked with `dotnet run`, not reported as a discovered xUnit/MSTest suite. Exceptions/nonzero exits fail CI. Temporary fixtures are under ignored `work/`.

## Manual checks still required

- A completed Windows UI smoke test of the newly built executable: keyboard focus, narrow desktop/high-DPI layout, source preview/scrubbing and media-codec playback. The earlier desktop inspection was interrupted; compilation is not evidence of UI validation.
- Installation/uninstallation of the generated installer on a clean Windows account.
- Google sign-in in normal Brave. No login automation or security bypass is implemented or claimed as tested.
- Real ChatGPT video acceptance, upload completion, account-specific size/rate limits and changes to the live website. The synthetic browser test does not establish any of these.
- Long recordings (including 15 GB), disk-full recovery, interrupted encodes and live Cloudflare status delivery. The synthetic files do not establish those behaviors.

The supported conservative workflow is one automatic attempt per explicit arming, with up to three attempts per PC batch, then manual verification and Send. A reserved/attempted status never means upload accepted. For uncertain or rejected attempts use manual attachment; unattempted transfers can be explicitly retried.

## Quality review

Pass (source/automated checks): existing dark theme preserved; scrolling prevents the main settings area from exceeding the viewport without a navigation path; pairing/attempt/queue statuses use actual state; optional monitoring cannot gate local work; no new package or framework is introduced into the app; no user videos, browser profiles, credentials or personal Worker URL are committed.

Unverified: rendered WPF layout, keyboard/focus behavior and measured contrast across all control states. No full desktop accessibility certification or live-site upload verification is claimed.

The supplied generic Flutter commands do not apply to this C#/WPF repository: it has no `pubspec.yaml` or Dart sources. The .NET build and executable regression checks are the project validation gates.
