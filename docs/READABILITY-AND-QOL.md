# Readability improvements and suggested QOL work

Windows 0.6.11 keeps the existing dark navy theme and makes text and controls easier to distinguish. Disabled lists and buttons no longer fall back to pale backgrounds. Secondary text is brighter, small labels are larger, selected rows have clear outlines, and keyboard-focus indicators remain visible. Upload states retain their labels and use blue for Ready, green for Submitted and amber for review. Paused, failed and completed statuses also have distinct colours.

B-roll categories remain editable. B-roll headers wrap without overlapping controls, and the screen scrolls when content exceeds the window height.

## Suggested next features

These are proposals, not implemented features.

| Priority | Feature | Benefit |
| --- | --- | --- |
| 1 | Recover attached/sent batches after restart | Reconcile the saved batch ID and chat message before offering a safe Resume action; reduce repeated uploads and manual review. |
| 2 | Pause after the current batch | Finish the current confirmed send, then pause between batches so stopping creates fewer uncertain attempts. |
| 3 | Text-size setting | Offer 100%, 125% and 150% interface scaling while preserving scrolling and readable layouts. |
| 4 | Completion and attention notifications | Notify when the queue finishes or needs input, useful while Brave is minimized. |
| 5 | Saved processing presets and folders | Reuse preferred clip sizes, compression modes, source/output locations and batch sizes. |
| 6 | Queue overview and filters | Show remaining files/bytes, current phase and elapsed wait; filter Ready, Submitted and Needs review. Add ETA only when enough measured throughput is available. |
| 7 | Per-source error summary for bulk splitting | Continue independent healthy sources when appropriate, then list failed inputs with recovery actions. Stop on shared problems such as an unavailable/full output drive. |

## Verification scope

The isolated Windows WPF harness loads the actual theme and screen XAML with business handlers removed. Synthetic queues are clearly labelled; user jobs, login state and media are not loaded. It renders minimum-width enabled/disabled screens and dropdowns, and checks palette contrast, selected-state colours, dark queue surfaces, popup toggling, editable category text and semantic statuses.

Measured palette checks target at least 4.5:1 for normal text and 3:1 for control outlines. These tests are not a full accessibility certification. Real keyboard/screen-reader use, system high-contrast mode and live processing interaction still need manual acceptance testing.
