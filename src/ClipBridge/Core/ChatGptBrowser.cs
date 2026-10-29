using System.IO;
using System.Linq;
using Microsoft.Playwright;

namespace ClipBridge.Core;

/// <summary>
/// Experimental UI automation only; not an official ChatGPT API.
/// Uses an isolated, user-controlled Brave profile stored locally on this PC.
/// It never reads or exports credentials and never submits the chat message.
/// </summary>
public sealed class ChatGptBrowser : IAsyncDisposable
{
    private IPlaywright? _playwright;
    private IBrowserContext? _context;
    private IPage? _page;

    public async Task OpenAsync()
    {
        if (_page is not null)
        {
            try
            {
                await _page.BringToFrontAsync();
                return;
            }
            catch (PlaywrightException)
            {
                await ResetAsync();
            }
        }

        _playwright ??= await Playwright.CreateAsync();
        string profilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClipBridge", "ChatGptBraveProfile");
        Directory.CreateDirectory(profilePath);
        try
        {
            _context = await _playwright.Chromium.LaunchPersistentContextAsync(
                profilePath, new BrowserTypeLaunchPersistentContextOptions
                {
                    ExecutablePath = BraveBrowserLocator.FindRequired(),
                    Headless = false,
                    AcceptDownloads = false,
                    ViewportSize = null
                });
            _page = _context.Pages.FirstOrDefault() ?? await _context.NewPageAsync();
            await _page.GotoAsync("https://chatgpt.com/", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 45000
            });
            await _page.BringToFrontAsync();
        }
        catch
        {
            await ResetAsync();
            throw;
        }
    }

    /// <summary>
    /// Attempts to put files into the ChatGPT file picker.
    /// This does NOT prove ChatGPT accepted or completed the upload.
    /// Returns how many files were handed to the file input.
    /// </summary>
    public async Task<int> TryAttachAsync(IReadOnlyList<string> files)
    {
        if (files.Count == 0) return 0;
        if (files.Count > 3)
            throw new ArgumentException("Attach no more than 3 files per attempt.", nameof(files));
        foreach (string file in files)
            if (!File.Exists(file)) throw new FileNotFoundException("Clip no longer exists.", file);

        await OpenAsync();
        IPage page = _page!;
        if (!Uri.TryCreate(page.Url, UriKind.Absolute, out Uri? current) ||
            (current.Host != "chatgpt.com" && !current.Host.EndsWith(".chatgpt.com", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Open a ChatGPT conversation in the Brave window, then retry.");

        // A hidden file input often exists even before the attachment menu is opened.
        var inputs = page.Locator("input[type='file']");
        if (await inputs.CountAsync() == 0)
        {
            var attach = page.Locator(
                "button[aria-label*='Attach' i], button[aria-label*='Add photos' i], button[data-testid*='attach' i]");
            if (await attach.CountAsync() > 0)
                await attach.First.ClickAsync(new LocatorClickOptions { Timeout = 3000 });
        }

        inputs = page.Locator("input[type='file']");
        if (await inputs.CountAsync() == 0)
            throw new InvalidOperationException(
                "Could not locate ChatGPT's file picker. Use its attachment menu manually. " +
                "The website interface may have changed.");

        var input = inputs.Last;
        string? acceptsMultiple = await input.GetAttributeAsync("multiple");
        int count = acceptsMultiple is null ? 1 : files.Count;
        await input.SetInputFilesAsync(files.Take(count).ToArray(),
            new LocatorSetInputFilesOptions { Timeout = 20000 });

        await page.BringToFrontAsync();
        return count;
    }

    private async Task ResetAsync()
    {
        if (_context is not null)
        {
            try { await _context.CloseAsync(); } catch { /* Browser may already be closed. */ }
        }
        _context = null;
        _page = null;
    }

    public async ValueTask DisposeAsync()
    {
        await ResetAsync();
        _playwright?.Dispose();
        _playwright = null;
    }
}
