# ClipBridge B-roll cutter and gardening library (preview)

ClipBridge can export short reusable B-roll clips from your long recordings without modifying the originals. This feature runs **on your Windows PC** and does not upload videos or tags to the Cloudflare dashboard.

## Save a new shot

1. In ClipBridge Windows select **B-roll cutter & library** and choose an existing source video.
2. Play and scrub the video using the preview. At the beginning of the useful shot select **Mark In**, then at the end choose **Mark Out**.
3. If Windows cannot preview the source format, enter the start and end times manually as `mm:ss` or `hh:mm:ss`.
4. Give the clip a clear name, such as `Tomato transplant close-up`. Select a category folder such as `Garden`.
5. Tick one or more gardening topics, for example **Planting** and **Seedlings**.
6. Select **Save clip to library**. The local FFmpeg engine exports an H.264/AAC MP4 and validates that the duration roughly matches the selected range.

The default library folder is `Videos/ClipBridge/B-Roll` under your Windows user account. Files are organised into category folders. The source recording is never deleted or modified, even if you cancel an export.

## Checkbox gardening topics

- Planting
- Harvesting
- Watering
- Compost & Soil
- Pruning
- Seedlings
- Tools
- Wildlife
- Weeding
- Garden Overview

**Saving tags and filtering are separate.** The upper checkboxes label the shot being exported. The bottom checkboxes filter previously saved shots. Choose two or more filters to show clips matching **any** selected topic (OR logic). Clear all filters to show the full library, including untagged videos. Use **Refresh library** after externally copying files into the folder. Double-click an item to open it in the default Windows video player.

**Tag older footage too:** Select an existing saved clip, tick the relevant topics in the upper checkbox group, and click **Apply checked topic tags to selected saved clip**. The MP4 does not need to be re-exported.

Each MP4 has a small adjacent `.broll.json` file storing its title, original *file name* (not full path), category, selected tags, start/end seconds, and save date. Keep the sidecar with its MP4 if you move or back up the library; no account, server or internet connection is required.

## Current limitations

- Gardening classification is **manual**: the app cannot automatically identify planting, harvesting or gardening objects from the visual content. You choose the correct tags when saving.
- You can edit tags on a selected existing MP4. Bulk-tagging multiple selected clips is not implemented yet.
- The Windows MediaElement preview depends on codecs available on the computer. FFmpeg can support more source formats than Windows preview, so manual timestamps remain available.
- The current clip export uses a quality-oriented H.264/AAC re-encode for accurate boundaries. Export time and output size depend on source footage and PC performance.
- No multi-clip timeline, auto-detected highlights, cloud synchronisation or AI scene recognition yet.
- Successful Windows CI builds do not substitute for a real video-export smoke test on a Windows computer.
