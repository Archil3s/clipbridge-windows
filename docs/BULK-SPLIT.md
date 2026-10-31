# Bulk splitting videos from a folder

1. In the main ClipBridge window, choose whether to enable **Include subfolders**. It is off initially.
2. Click **Add video folder** and choose the source folder, or drop one or more folders into the main window. You can add more folders to the same queue.
3. Check the queue, maximum clip size, processing mode and output folder.
4. Click **Split videos**. The batch processor handles each queued video sequentially, with per-video and overall progress. All finished clips go directly into the selected **Output folder**, numbered `part_0001.mp4`, `part_0002.mp4` and onward. Existing part numbers are skipped and files are never overwritten. Fast mode may retain the original video extension for a single unsplit clip. Originals are preserved.
5. Once processing finishes, **Automatic uploads (Playwright)** can use the prepared clip queue.

Folder discovery supports MP4, MOV, MKV, AVI, M4V and WEBM, including uppercase extensions. It normalizes paths and ignores duplicate queue entries while retaining separate source files that have the same basename in different folders. Discovery runs in the background. **Cancel** during discovery leaves the queue unchanged; an unreadable folder is reported before any results from that scan are added.

Recursive scans skip directory links to avoid cycles. A recursive scan includes supported videos in output folders too: select an output location outside the source tree if you want future scans to contain only originals.

Completed outputs can be recovered by the local journal on a later run with matching sources and settings. Valid cached clips from older per-source folders are copied into the selected output folder; those old folders are preserved. Clips already in the selected folder are reused without duplicating them. Hidden `.clipbridge-state` records associate each source with its own clips so recovery cannot mix videos. Keep that folder for recovery.

Splitting uses a hidden temporary work folder inside the output folder. It is removed after a successful run when empty. An interrupted or failed job may leave temporary files; the app does not delete partial footage automatically. Folder selection does not automatically start processing or upload footage.

Automated tests cover folder discovery, shared-folder naming, existing-file preservation, per-source recovery, cancellation, manifest boundaries and a real FFmpeg run using two same-named source videos. Build success does not establish real folder-picker usability, accessibility or large-folder/media acceptance.
