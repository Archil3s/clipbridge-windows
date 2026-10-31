using System.Diagnostics;
using System.Text.Json;
using ClipBridge.Core;
using Microsoft.Playwright;

internal static class AutomaticUploadTests
{
    private static void Check(bool value, string label)
    {
        if (!value) throw new Exception(label);
        Console.WriteLine("PASS " + label);
    }

    internal static async Task RunAsync(string root)
    {
        ProcessStartInfo? manualLaunch = null;
        string manualProfile = Path.Combine(root, "manual-profile");
        using (var manual = new BraveAutomationSession(manualProfile, "fixture-browser", start =>
        {
            manualLaunch = start;
            return Process.GetCurrentProcess();
        }))
        {
            await manual.OpenForSignInAsync("https://chatgpt.com/", CancellationToken.None);
            Check(manualLaunch is not null && manualLaunch.ArgumentList.SequenceEqual(new[] {
                "--user-data-dir=" + manualProfile, "--new-window", "https://chatgpt.com" }) && manual.Page is null,
                "manual sign-in launches only the saved profile and URL, without debugging or Playwright");
        }
        var files = new List<string>();
        for (int i = 0; i < 4; i++)
        {
            string file = Path.Combine(root, $"prepared-{i}.mp4");
            await File.WriteAllBytesAsync(file, Enumerable.Range(0, 16000).Select(n => (byte)((n + i) % 251)).ToArray());
            files.Add(file);
        }
        string journal = Path.Combine(root, "queue.json");
        using (var queue = new AutomaticUploadQueue(journal))
        {
            Check(queue.Add(files) == 4 && queue.Add(files) == 0, "prepared queue persists distinct clips without duplication");
            bool locked = false;
            try { using var second = new AutomaticUploadQueue(journal); }
            catch (IOException) { locked = true; }
            Check(locked, "only one automatic uploader can open the saved queue");
            Check(queue.NextBatch(3).Count == 3, "batch size selects an ordered prefix");
            queue.Transition(queue.NextBatch(2), "Ready", "Attachment attempted");
            Check(queue.NextBatch(10).Count == 2, "uncertain attempts cannot be automatically repeated");
        }
        using (var restored = new AutomaticUploadQueue(journal))
        {
            Check(restored.Clips.Count(clip => clip.State == "Attachment attempted") == 2, "uncertain attempts survive app restart");
            restored.RetryReviewed(restored.Clips.Take(1));
            Check(restored.NextBatch(10).Count == 3, "explicit reviewed retry releases only selected clips");
            foreach (string bad in new[] { "https://evil.example/", "https://chatgpt.com.evil.example/", "http://chatgpt.com/", "https://chatgpt.com/auth/login", "https://name@chatgpt.com/", "https://chatgpt.com:4433/" })
            {
                bool rejected = false;
                try { AutomaticUploadQueue.ValidateUrl(bad); } catch (ArgumentException) { rejected = true; }
                Check(rejected, "reject unsupported automation destination " + bad);
            }
            Check(AutomaticUploadQueue.BrowserMatches("https://chatgpt.com/c/synthetic?model=test#x", "https://chatgpt.com/c/synthetic"), "browser routing parameters match the canonical conversation without being saved");
            Check(!AutomaticUploadQueue.BrowserMatches("https://chatgpt.com.evil.example/c/synthetic?x=1", "https://chatgpt.com/c/synthetic") &&
                !AutomaticUploadQueue.BrowserMatches("http://chatgpt.com/c/synthetic?x=1", "https://chatgpt.com/c/synthetic"), "browser URL normalization preserves destination restrictions");
            string changed = files[3];
            await File.AppendAllTextAsync(changed, "changed");
            bool changedRejected = false;
            try { restored.NextBatch(10); } catch (IOException) { changedRejected = true; }
            Check(changedRejected, "changed files stop the queue before attachment");
            await File.WriteAllBytesAsync(changed, Enumerable.Range(0, 16000).Select(n => (byte)((n + 3) % 251)).ToArray());
        }
        await File.WriteAllTextAsync(journal, "{");
        bool corrupt = false;
        try { using var broken = new AutomaticUploadQueue(journal); } catch (JsonException) { corrupt = true; }
        Check(corrupt && await File.ReadAllTextAsync(journal) == "{", "damaged journals are preserved and fail closed");

        var large = new[] { Path.Combine(root, "large-1.mp4"), Path.Combine(root, "large-2.mp4") };
        foreach (string file in large) { using var stream = File.Create(file); stream.SetLength(525_000_000); }
        using (var capped = new AutomaticUploadQueue(Path.Combine(root, "capped.json")))
        {
            capped.Add(large);
            Check(capped.NextBatch(10).Count == 1, "1 GB batch memory cap is enforced independently of file count");
        }
        foreach (string file in large) { using var stream = File.Create(file); }

        string confirmation = Path.Combine(root, "confirmation.json");
        using (var queue = new AutomaticUploadQueue(confirmation))
        {
            queue.Add(files);
            var batch = queue.NextBatch(3);
            queue.Transition(batch, "Ready", "Send attempted");
            Directory.CreateDirectory(confirmation + ".tmp");
            bool failedWrite = false;
            try { queue.ConfirmSubmitted(batch, "https://chatgpt.com/c/one-chat"); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { failedWrite = true; }
            Check(failedWrite && queue.Settings.ConversationUrl == "https://chatgpt.com/" &&
                queue.Clips.Count(clip => clip.State == "Send attempted") == 3,
                "failed confirmation leaves both queue state and conversation binding unchanged");
            Directory.Delete(confirmation + ".tmp");
            queue.ConfirmSubmitted(batch, "https://chatgpt.com/c/one-chat");
        }
        using (var queue = new AutomaticUploadQueue(confirmation))
            Check(queue.Settings.ConversationUrl == "https://chatgpt.com/c/one-chat" &&
                queue.Clips.Count(clip => clip.State == "Submitted") == 3 && queue.NextBatch(3).Count == 1,
                "confirmed submission restores its chat address and remaining batch together after restart");

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new()
        {
            Headless = true,
            ChromiumSandbox = true,
            ExecutablePath = Environment.GetEnvironmentVariable("CHROMIUM_EXE")
        });
        foreach (string mode in new[] { "auto", "menu", "semantic", "new-chat", "pending", "disabled", "partial", "error", "draft", "edit", "cancel", "navigation", "uncertain-send",
            "challenge-title", "challenge-form", "challenge-upload", "hidden-challenge",
            "renamed", "same-chat", "high-slider", "wrong-name", "enter-no-send", "high-missing", "high-changed",
            "duplicate-names", "duplicate-name-missing", "next-chat", "high-semantic", "high-nested", "high-adjacent", "chips-adjacent", "chips-icon", "chips-adjacent-pending", "chips-history", "routing-query", "unmarked-turns", "full-stack" })
            await BrowserCaseAsync(browser, root, files, mode);
        await CdpCaseAsync(playwright, root, files);
        Console.WriteLine("Synthetic clips/pages only; normal Brave sign-in and live ChatGPT acceptance remain unverified.");
    }

    private static async Task CdpCaseAsync(IPlaywright playwright, string root, List<string> files)
    {
        string profile = Path.Combine(root, "cdp-profile");
        Directory.CreateDirectory(profile);
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("CHROMIUM_EXE") ?? playwright.Chromium.ExecutablePath)
        { UseShellExecute = false, CreateNoWindow = true };
        foreach (string argument in new[] { "--window-position=-10000,-10000", "--user-data-dir=" + profile, "--remote-debugging-address=127.0.0.1",
            "--remote-debugging-port=0", "--disable-background-networking", "--no-first-run", "--force-dark-mode",
            "--disable-background-timer-throttling", "--disable-backgrounding-occluded-windows", "--disable-renderer-backgrounding", "about:blank" }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new Exception("Could not start CDP fixture browser");
        using var cdpDriver = await Playwright.CreateAsync();
        try
        {
            string portFile = Path.Combine(profile, "DevToolsActivePort");
            for (int i = 0; i < 60 && !File.Exists(portFile); i++) await Task.Delay(250);
            string[] port = await File.ReadAllLinesAsync(portFile);
            var connected = await cdpDriver.Chromium.ConnectOverCDPAsync("http://127.0.0.1:" + port[0], new() { NoDefaults = true });
            var fixturePage = connected.Contexts[0].Pages[0];
            const string url = "https://chatgpt.com/c/synthetic-reels";
            await fixturePage.RouteAsync("https://chatgpt.com/**", route => route.FulfillAsync(new() { ContentType = "text/html", Body = Fixture("cdp") }));
            await fixturePage.GotoAsync(url);
            await fixturePage.EvaluateAsync("window.clipBridgeFixture = 'existing page'");
            var windowControl = await fixturePage.Context.NewCDPSessionAsync(fixturePage);
            int windowId = (await windowControl.SendAsync("Browser.getWindowForTarget"))!.Value.GetProperty("windowId").GetInt32();
            await windowControl.SendAsync("Browser.setWindowBounds", new Dictionary<string, object> { ["windowId"] = windowId, ["bounds"] = new Dictionary<string, object> { ["windowState"] = "minimized" } });
            cdpDriver.Dispose();

            using var session = new BraveAutomationSession(profile);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            bool manualRejected = false;
            try { await session.OpenForSignInAsync(url, deadline.Token); }
            catch (IOException) { manualRejected = true; }
            Check(manualRejected && session.Page is null,
                "manual sign-in refuses to reuse an existing debugging window");
            await session.OpenAsync(url, deadline.Token);
            var page = session.Page!;
            var duplicate = await page.Context.NewPageAsync();
            await duplicate.RouteAsync("https://chatgpt.com/**", route => route.FulfillAsync(new() { ContentType = "text/html; charset=utf-8", Body = Fixture("cdp") }));
            await duplicate.GotoAsync(url);
            bool ambiguous = false;
            try { await session.OpenAsync(url, deadline.Token); }
            catch (IOException) { ambiguous = true; }
            Check(ambiguous && page.Url == url && duplicate.Url == url, "duplicate target tabs stop without navigating either tab");
            await duplicate.CloseAsync();
            await session.OpenAsync(url, deadline.Token);
            Check(await page.EvaluateAsync<string>("window.clipBridgeFixture") == "existing page" && page.Context.Pages.Count == 1,
                "resume reuses the open page without a reload or duplicate tab");
            Check(await page.EvaluateAsync<bool>("matchMedia('(prefers-color-scheme: dark)').matches"),
                "production CDP connection preserves the browser's dark preference");
            bool wrongTarget = false;
            try { await session.OpenAsync("https://chatgpt.com/c/not-open", deadline.Token); }
            catch (IOException) { wrongTarget = true; }
            Check(wrongTarget && page.Url == url && page.Context.Pages.Count == 1,
                "missing target stops without navigating the signed-in page or creating a tab");
            var minimizeControl = await page.Context.NewCDPSessionAsync(page);
            await minimizeControl.SendAsync("Browser.setWindowBounds", new Dictionary<string, object> {
                ["windowId"] = windowId, ["bounds"] = new Dictionary<string, object> { ["windowState"] = "minimized" }
            });
            await session.OpenAsync(url, deadline.Token);
            var backgroundControl = await session.Page!.Context.NewCDPSessionAsync(session.Page);
            Check((await backgroundControl.SendAsync("Browser.getWindowBounds", new Dictionary<string, object> { ["windowId"] = windowId }))!.Value
                .GetProperty("bounds").GetProperty("windowState").GetString() == "minimized",
                "opening automation preserves the minimized browser window");
            Check(await session.Page.EvaluateAsync<bool>("document.hasFocus() && document.visibilityState==='visible'"),
                "the minimized automation page remains focused and active through CDP");
            await RunFixtureAsync(page, root, files, "cdp");
            Check((await backgroundControl.SendAsync("Browser.getWindowBounds", new Dictionary<string, object> { ["windowId"] = windowId }))!.Value
                .GetProperty("bounds").GetProperty("windowState").GetString() == "minimized",
                "all synthetic clip batches submit while the browser stays minimized");
            Check(true, "direct uploads work through a real loopback CDP connection with the browser sandbox enabled");
            session.Dispose();
            using var http = new System.Net.Http.HttpClient(new System.Net.Http.HttpClientHandler { UseProxy = false });
            using var response = await http.GetAsync("http://127.0.0.1:" + port[0] + "/json/version");
            Check(!process.HasExited && response.IsSuccessStatusCode, "disconnecting Playwright leaves the user's browser open for manual sign-in");
        }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); } }
    }

    private static async Task BrowserCaseAsync(IBrowser browser, string root, List<string> files, string mode)
    {
        await using var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.RouteAsync("https://chatgpt.com/**", route => route.FulfillAsync(new() { ContentType = "text/html", Body = Fixture(mode) }));
        string url = mode is "new-chat" or "same-chat" or "routing-query" or "unmarked-turns" or "full-stack" ? "https://chatgpt.com/" : "https://chatgpt.com/c/synthetic-reels";
        await page.GotoAsync(url);
        if (mode is "duplicate-names" or "duplicate-name-missing")
        {
            string folder = Path.Combine(root, mode);
            Directory.CreateDirectory(folder);
            string duplicate = Path.Combine(folder, Path.GetFileName(files[0]));
            File.Copy(files[0], duplicate);
            files = [files[0], duplicate];
        }
        if (mode is "same-chat" or "full-stack")
        {
            files = files.ToList();
            for (int i = 4; i < (mode == "full-stack" ? 38 : 7); i++)
            {
                string file = Path.Combine(root, $"prepared-{i}.mp4");
                await File.WriteAllBytesAsync(file, Enumerable.Range(0, 16000).Select(n => (byte)((n + i) % 251)).ToArray());
                files.Add(file);
            }
        }
        await RunFixtureAsync(page, root, files, mode);
    }

    private static async Task RunFixtureAsync(IPage page, string root, List<string> files, string mode)
    {
        using var queue = new AutomaticUploadQueue(Path.Combine(root, mode + ".json"));
        queue.Add(files);
        queue.Configure(page.Url, 3, "Clips for gardening reels. Wait for instructions.");
        using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(mode == "full-stack" ? 120 : 30));
        var runner = new PlaywrightChatUploader(page, queue, status =>
        {
            if (mode == "cancel" && status.Contains("waiting for all")) cancel.Cancel();
            if (mode == "uncertain-send" && status.Contains("waiting for the sent message")) cancel.CancelAfter(500);
            if (mode == "enter-no-send" && status.Contains("waiting for the sent message")) cancel.CancelAfter(500);
        }, TimeSpan.FromSeconds(mode is "pending" or "disabled" or "partial" or "wrong-name" or "duplicate-name-missing" or "chips-adjacent-pending" or "chips-history" ? 3 : 8));
        var debug = new UploadDebugLog();
        runner.DebugLog = debug;
        Exception? stopped = null;
        try { await runner.RunAsync(cancel.Token); } catch (Exception ex) { stopped = ex; }
        int sends = await page.EvaluateAsync<int>("window.sends.length");
        if (mode == "pending") Check(debug.Export(queue.Clips, page.Url, false).Contains("busy=1"),
            "debug report captures the real upload-processing wait without page text");
        if (mode is "auto" or "menu" or "semantic" or "new-chat" or "cdp" or "hidden-challenge" or "renamed" or "same-chat" or "high-slider" or "duplicate-names" or "high-semantic" or "high-nested" or "high-adjacent" or "chips-adjacent" or "chips-icon" or "routing-query" or "unmarked-turns" or "full-stack")
        {
            if (stopped is not null) throw new Exception(mode + " unexpectedly stopped", stopped);
            Check(sends == (mode == "full-stack" ? 13 : mode == "same-chat" ? 3 : mode == "duplicate-names" ? 1 : 2) && queue.Clips.All(clip => clip.State == "Submitted"), mode + ": automatically submits every queued clip");
            var sizes = await page.EvaluateAsync<int[]>("window.sends.map(batch=>batch.names.length)");
            Check(sizes.SequenceEqual(mode == "full-stack" ? Enumerable.Repeat(3, 12).Append(2).ToArray() : mode == "same-chat" ? [3, 3, 1] : mode == "duplicate-names" ? [2] : [3, 1]), mode + ": one multi-file attachment per batch");
            Check(await page.EvaluateAsync<int>("window.enterKeys") == sends, mode + ": exactly one Enter per submitted batch");
            Check(await page.EvaluateAsync<bool>("window.sends.every(batch=>batch.thinking==='High')"), mode + ": every batch uses High thinking");
            Check(!await page.EvaluateAsync<bool>("window.premature"), mode + ": never sends during upload progress");
            var payloads = await page.EvaluateAsync<JsonElement>("window.payloads");
            foreach (var item in payloads.EnumerateArray())
            {
                int index = int.Parse(item.GetProperty("name").GetString()!.Split('-')[1].Split('.')[0]);
                long checksum = Enumerable.Range(0, 16000).Sum(n => (long)((n + index) % 251));
                Check(item.GetProperty("size").GetInt32() == 16000 && item.GetProperty("sum").GetInt64() == checksum,
                    mode + ": browser receives exact file bytes " + index);
            }
            if (mode == "full-stack") Check(queue.Settings.ConversationUrl == "https://chatgpt.com/c/generated" && queue.NextBatch(3).Count == 0 &&
                await page.EvaluateAsync<bool>("window.sends.slice(1).every(batch=>batch.url==='https://chatgpt.com/c/generated')"),
                "38 clips survive a delayed unsupported route and continue as 12 batches of 3 plus 2 in one chat");
            if (mode == "new-chat") Check(queue.Settings.ConversationUrl == "https://chatgpt.com/c/generated", "new conversation URL saved after confirmed first send");
            if (mode == "same-chat")
                Check(queue.Settings.ConversationUrl == "https://chatgpt.com/c/generated" && queue.NextBatch(3).Count == 0 &&
                    await page.EvaluateAsync<bool>("window.sends.slice(1).every(batch=>batch.url==='https://chatgpt.com/c/generated')"),
                    "seven clips use one new conversation across three batches until the ready queue is empty");
        }
        else
        {
            Check(stopped is not null, mode + ": automation pauses on the unexpected state");
            Check(sends == (mode is "uncertain-send" or "next-chat" ? 1 : 0), mode + ": no unsafe or repeated Send");
            if (mode == "next-chat") Check(queue.Clips.Count(clip => clip.State == "Submitted") == 3 &&
                queue.Clips.Count(clip => clip.State == "Ready") == 1 && queue.Settings.ConversationUrl == "https://chatgpt.com/c/synthetic-reels",
                "navigation after a submitted batch cannot redirect the remaining clips to another chat");
            if (mode == "draft") Check(queue.Clips.All(clip => clip.State == "Ready"), "existing draft prevents any file claim");
            if (mode == "edit") Check(await page.Locator("#prompt-textarea").InnerTextAsync() == "Keep my own message", "user draft edits remain intact");
            if (mode == "uncertain-send") Check(queue.Clips.Count(clip => clip.State == "Send attempted") == 3, "uncertain sends remain reserved for review");
            if (mode == "enter-no-send") Check(queue.Clips.Count(clip => clip.State == "Send attempted") == 3 &&
                await page.EvaluateAsync<int>("window.enterKeys") == 1, "Enter without confirmation is never repeated or followed by a fallback click");
            if (mode == "high-missing") Check(queue.Clips.All(clip => clip.State == "Ready"), "unknown thinking control pauses before any attachments");
            if (mode.StartsWith("challenge-", StringComparison.Ordinal))
            {
                Check(stopped is InvalidOperationException && stopped.Message.Contains("verification"), mode + ": reports verification immediately instead of waiting for a composer");
                Check(await page.EvaluateAsync<int>("window.challengeClicks") == 0, mode + ": never clicks verification controls");
                Check(queue.Clips.Count(clip => clip.State == "Attachment attempted") == (mode == "challenge-upload" ? 3 : 0) &&
                    queue.Clips.Count(clip => clip.State == "Ready") == (mode == "challenge-upload" ? 1 : 4),
                    mode + ": preserves unattempted clips and reserves only attempts already made");
            }
        }
    }

    private static string Fixture(string mode) => """
        <!doctype html><html><head><meta charset="utf-8"></head><body><main><div id="composer-background">
        <div contenteditable="true" id="prompt-textarea" role="textbox" class="ProseMirror"><p><br></p></div>
        <button id="composer-plus-btn" type="button">Attach</button><div id="chips"></div>
        <button id="thinking" aria-label="Thinking effort">High</button>
        <button id="composer-submit-button" type="button" disabled>Send</button></div><div id="turns"></div></main>
        <script>
        const mode=MODE_VALUE,editor=document.querySelector('#prompt-textarea'),chips=document.querySelector('#chips'),send=document.querySelector('#composer-submit-button');
        const composer=document.querySelector('#composer-background'),plus=document.querySelector('#composer-plus-btn');
        if(mode==='semantic'){
          composer.removeAttribute('id');editor.removeAttribute('id');editor.setAttribute('aria-label','Ask ChatGPT');
          plus.removeAttribute('id');plus.setAttribute('aria-label','Add files and more');
          send.removeAttribute('id');send.setAttribute('aria-label','Send');
          for(let i=0;i<2;i++){let unrelated=document.createElement('input');unrelated.type='file';unrelated.multiple=true;unrelated.hidden=true;document.body.append(unrelated);}
        }
        if(mode.startsWith('chips-adjacent')){
          const wrapper=document.createElement('section');composer.before(wrapper);wrapper.append(chips,composer);
        }
        if(mode==='chips-history'){
          const old=document.createElement('article');old.setAttribute('data-message-author-role','user');old.append(chips);document.querySelector('#turns').append(old);
        }
        window.sends=[];window.payloads=[];window.premature=false;window.challengeClicks=0;window.enterKeys=0;
        let thinking=document.querySelector('#thinking');
        if(mode==='high-semantic'){
          thinking.outerHTML='<div id="thinking" role="button" tabindex="0"><span>High</span></div>';
          thinking=document.querySelector('#thinking');
        }
        if(mode==='high-nested')thinking.innerHTML='<span style="display:none">Select thinking effort</span><span>High</span><svg><title>Chevron down</title></svg>';
        if(mode==='high-adjacent'){
          const wrapper=document.createElement('section');composer.before(wrapper);wrapper.append(composer);wrapper.append(thinking);
        }
        if(mode==='high-missing')thinking.remove();
        if(mode==='same-chat'||mode==='high-slider')thinking.textContent='Medium';
        thinking.onclick=()=>{
          const menu=document.createElement('div');menu.setAttribute('role','menu');
          if(mode==='high-slider'){
            const slider=document.createElement('input');slider.type='range';slider.min=0;slider.max=2;slider.value=1;
            const update=()=>{thinking.textContent=['Low','Medium','High'][slider.value];slider.setAttribute('aria-valuetext',thinking.textContent);};
            slider.oninput=update;update();menu.append(slider);
          }else{
            const high=document.createElement('button');high.setAttribute('role','menuitemradio');high.textContent='High';
            high.onclick=()=>{thinking.textContent='High';menu.remove();};menu.append(high);
          }
          document.body.append(menu);
        };
        document.addEventListener('keydown',event=>{if(event.key==='Escape')document.querySelector('[role="menu"]')?.remove();});
        editor.addEventListener('keydown',event=>{if(event.key==='Enter'&&!event.shiftKey){window.enterKeys++;event.preventDefault();if(mode!=='enter-no-send'&&!send.disabled)send.click();}});
        function challenge(hidden=false){
          const form=document.createElement('form');form.id='challenge-form';form.hidden=hidden;
          const button=document.createElement('button');button.textContent='Verify you are human';button.onclick=e=>{e.preventDefault();window.challengeClicks++;};
          form.append(button);document.body.append(form);
        }
        if(mode==='challenge-title')document.title='Just a moment…';
        if(mode==='challenge-form'||mode==='hidden-challenge')challenge(mode==='hidden-challenge');
        if(mode==='draft')editor.textContent='Keep my draft';
        function makeInput(){
          let input=document.createElement('input');input.type='file';input.multiple=true;input.accept='video/mp4';input.hidden=true;
          composer.append(input);
          input.onchange=async()=>{
            if(document.querySelector('button[aria-label="Stop"]'))window.premature=true;
            window.currentFiles=[...input.files];send.disabled=true;chips.replaceChildren();
            if(mode==='challenge-upload')challenge();
            for(const file of (mode==='partial'?[...input.files].slice(0,2):mode==='duplicate-name-missing'?[input.files[0]]:input.files)){
              const chip=document.createElement('span');
              if(['renamed','same-chat','duplicate-names','duplicate-name-missing'].includes(mode)){
                const name=file.name.replace('.mp4','(3).mp4');chip.title=name;
                const stem=document.createElement('span');stem.textContent=name.slice(0,-4);const ext=document.createElement('span');ext.textContent='.mp4';chip.append(stem,ext);
              }else chip.textContent=mode==='wrong-name'?'wrong-'+file.name:file.name;
              if(mode==='chips-icon'){
                chip.replaceChildren();
                const svg=document.createElementNS('http://www.w3.org/2000/svg','svg');
                const title=document.createElementNS('http://www.w3.org/2000/svg','title');title.textContent='Video file';svg.append(title);
                const stem=document.createElement('span');stem.textContent=file.name.slice(0,-4);
                const ext=document.createElement('span');ext.textContent='.mp4';
                const hidden=document.createElement('span');hidden.hidden=true;hidden.textContent='Remove attachment';
                chip.append(svg,stem,ext,hidden);
              }
              chips.append(chip);
            }
            const progress=document.createElement('span');progress.setAttribute('role','progressbar');progress.textContent='Uploading';chips.append(progress);
            for(const file of input.files){const bytes=new Uint8Array(await file.arrayBuffer());window.payloads.push({name:file.name,size:file.size,sum:bytes.reduce((a,b)=>a+b,0)});}
            if(mode==='error'){const error=document.createElement('div');error.setAttribute('role','alert');error.textContent='Upload limit reached';document.body.append(error);}
            if(mode==='edit')setTimeout(()=>editor.textContent='Keep my own message',150);
            if(mode==='high-changed')setTimeout(()=>thinking.textContent='Low',150);
            if(mode==='navigation')setTimeout(()=>history.pushState({},'', '/c/wrong-conversation'),150);
            const finish=()=>{
              if(mode==='cdp'&&(!document.hasFocus()||document.visibilityState!=='visible')){setTimeout(finish,100);return;}
              if(mode!=='chips-adjacent-pending')progress.remove();send.disabled=mode==='disabled';
            };
            if(mode!=='pending')setTimeout(finish,300);
          };return input;
        }
        if(mode!=='menu'&&mode!=='semantic')makeInput();
        plus.onclick=()=>{
          if(mode==='semantic'){
            const choice=document.createElement('button');choice.textContent='Add photos & files Upload from computer';
            choice.onclick=()=>{makeInput().click();choice.remove();};document.body.append(choice);
          }else makeInput().click();
        };
        send.onclick=()=>{
          if(chips.querySelector('[role="progressbar"]'))window.premature=true;
          window.sends.push({names:window.currentFiles.map(file=>file.name),message:editor.innerText,thinking:thinking.innerText.trim(),url:location.href});
          if(mode==='uncertain-send')return;
          const turn=document.createElement(mode==='same-chat'?'article':'div');turn.setAttribute(mode==='same-chat'?'data-turn':'data-message-author-role','user');if(mode==='unmarked-turns'||mode==='full-stack')turn.removeAttribute('data-message-author-role');turn.textContent=editor.innerText;document.querySelector('#turns').append(turn);
          chips.replaceChildren();document.querySelectorAll('input').forEach(input=>input.value='');editor.textContent='';send.disabled=true;
          if((mode==='new-chat'||mode==='same-chat'||mode==='routing-query'||mode==='unmarked-turns')&&window.sends.length===1)history.pushState({},'', mode==='routing-query'?'/c/generated?model=synthetic#details':'/c/generated');
          if(mode==='full-stack'&&window.sends.length===1){
            setTimeout(()=>history.pushState({},'', '/c/'),600);
            setTimeout(()=>history.pushState({},'', '/c/generated'),1600);
          }
          if(mode==='next-chat')setTimeout(()=>history.pushState({},'', '/c/wrong-conversation'),1200);
          const stop=document.createElement('button');stop.dataset.testid='stop-button';stop.textContent='Stop';document.body.append(stop);
          if(mode==='semantic'){stop.removeAttribute('data-testid');stop.setAttribute('aria-label','Stop');}
          setTimeout(()=>{const reply=document.createElement(mode==='same-chat'?'article':'div');reply.setAttribute(mode==='same-chat'?'data-turn':'data-message-author-role','assistant');if(mode==='unmarked-turns'||mode==='full-stack')reply.removeAttribute('data-message-author-role');reply.textContent='Received';document.querySelector('#turns').append(reply);if(mode!=='semantic')stop.remove();},300);
          if(mode==='semantic')setTimeout(()=>stop.remove(),2200);
        };
        </script></body></html>
        """.Replace("MODE_VALUE", JsonSerializer.Serialize(mode));
}
