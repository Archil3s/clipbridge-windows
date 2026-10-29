# Anti Slop upstream attribution

ClipBridge vendors the [Anti Slop project](https://github.com/miqdadbadjuber/anti-slop) by Miqdad Badjuber for local coding-agent guidance.

- Upstream version: **3.2.24**
- Pinned source commit: `3005235d030b4793146ad9afe8d01045efd90e1d`
- Source URL: https://github.com/miqdadbadjuber/anti-slop/tree/3005235d030b4793146ad9afe8d01045efd90e1d
- Licence: **MIT**, copied as [LICENSE](LICENSE).
- Additional notices are preserved alongside the UI and accessibility skills in `skills/antislop-ui/LICENSE-web-quality-skills` and `skills/antislop-human/LICENSE-web-quality-skills`.
- The upstream core is copied unmodified to `/antislop.md`. Selected skill files and their required reference files are copied unmodified under `/skills/`.

The vendored rules are **development instructions**, not shipped or executed within ClipBridge itself. No runtime npm package, network calls, npm lifecycle scripts or new application dependencies are installed by this integration.

## Included scopes

- Core Anti Slop rules
- UI / visual guidance
- Copywriting
- Human accessibility
- Responsive/mobile layout
- Code comment hygiene

## Updating safely

The source is intentionally pinned to avoid changing agent instructions silently. To update:

1. Review the upstream release notes, changes, licence terms and any new skill dependencies.
2. Open a dedicated PR with a new upstream commit hash/version.
3. Replace vendored files together from that one revision; retain all licence notices.
4. Update `AGENTS.md` references, this record, and `CHANGELOG.md`.
5. Verify the new files and run the quality checks. Never use unattended auto-update to change agent instructions.

To remove the integration, remove `/antislop.md`, `/skills/antislop-*`, the attribution folder and the Anti Slop section in `AGENTS.md`. Keep independent ClipBridge design requirements as desired.
