# Releasing ClipBridge

## Versioning

Use `MAJOR.MINOR.PATCH` version numbers:
- Patch: fixes with no new user-facing features.
- Minor: backward-compatible features.
- Major: intentional breaking changes.

Keep `src/ClipBridge/ClipBridge.csproj` and `installer/ClipBridge.iss` version values identical. GitHub CI checks this automatically.

## Development workflow

1. Create a feature branch; do not commit directly to `main` for complex work.
2. Add tests and docs for every functional change.
3. Update the `[Unreleased]` section of `CHANGELOG.md` and `ROADMAP.md`.
4. Open a pull request. Run Windows build and Cloudflare dashboard checks.
5. Review and merge.

## Shipping a tested version

1. Perform a Windows smoke test: first-run dependency install, 1 GB then 15 GB source, audio playback, size checks, cancellation, restart, disk-full behavior and Cloudflare opt-in/off.
2. Update the release section of CHANGELOG.md and reconcile known limitations.
3. Ensure versions match, and run GitHub Actions green.
4. Create an annotated version tag and push it:
   ```bash
   git tag -a v0.5.0 -m "ClipBridge v0.5.0"
   git push origin v0.5.0
   ```
5. The tagged GitHub Action publishes a Windows portable ZIP, Inno Setup installer and a GitHub Release with automatically generated change notes.
6. Verify artifacts are present and downloadable, then test installation from the Release.

**Do not tag an untested preview as stable.** Builds from Actions are preview artifacts. The workflow does not create a release until someone deliberately pushes a matching version tag.

## Security

Use GitHub Secrets only for authorised deployment credentials. Cloudflare Worker `INGEST_TOKEN` and `VIEW_TOKEN` must be created in Cloudflare, not committed. Uploaded video files, browser profiles and local job journals remain outside Git.

## Preview artifact checks

The Windows workflow runs `dotnet run --project tests/ClipBridge.Core.Tests -c Release` and `node --test tests/brave-extension/*.test.cjs`. These are executable regression checks without additional test packages. A non-zero result fails CI. Inspect the installer and `ClipBridge-Brave-Extension` artifacts from the same run; the extension must include `background.js` and `composer.js`. Main-branch builds publish preview Actions artifacts without creating a stable tag.

Before claiming real upload support, perform the small-video checklist in `brave-extension/README.md` using normal Brave. Record separately whether Google sign-in, DOM dispatch, and ChatGPT upload acceptance were observed.

For Windows 0.5.0 / extension 0.3.0, test manual multi-file dispatch before enabling optional auto-send. Verify that upload progress, disabled Send, missing filenames, alerts, user edits and navigation prevent sending. Never describe synthetic browser results as live ChatGPT acceptance. Existing per-file bridge endpoints remain for backward compatibility; the new extension requires the batch-capable desktop build.
