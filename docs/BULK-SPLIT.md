# Bulk splitting videos from a folder

1. In the main ClipBridge window, choose whether to enable **Include subfolders**. It is off initially.
2. Click **Add video folder** and choose the source folder, or drop one or more folders into the main window. You can add more folders to the same queue.
3. Check the queue, maximum clip size, processing mode and output folder.
4. Click **Split videos**. The existing batch processor handles each queued video sequentially, with per-video and overall progress. Each source gets a separate output directory; the originals are preserved.
5. Once processing finishes, **Automatic uploads (Playwright)** can use the prepared clip queue.

Folder discovery supports MP4, MOV, MKV, AVI, M4V and WEBM, including uppercase extensions. It normalizes paths and ignores duplicate queue entries while retaining separate source files that have the same basename in different folders. Discovery runs in the background. **Cancel** during discovery leaves the queue unchanged; an unreadable folder is reported before any results from that scan are added.

Recursive scans skip directory links to avoid cycles. A recursive scan includes supported videos in output folders too: select an output location outside the source tree if you want future scans to contain only originals.

Processing uses the existing error/recovery behavior. Completed outputs can be recovered by the local journal on a later run with matching settings. Folder selection does not automatically start processing or upload footage.

Automated discovery tests cover recursion, overlapping/case-varied paths, unsupported inputs, cancellation and source preservation. Build success does not establish real folder-picker usability, accessibility or large-folder/media acceptance.
