using System.ComponentModel;
using System.IO;
using System.Windows;
using ClipBridge.Core;
using Microsoft.Win32;

namespace ClipBridge;

public partial class AutomaticUploadWindow : Window
{
    private readonly AutomaticUploadQueue _queue;
    private readonly BraveAutomationSession _session = new();
    private CancellationTokenSource? _cancellation;
    private bool _busy;
    private bool _closing;
    private readonly UploadDebugLog _debug = new();

    public AutomaticUploadWindow(IEnumerable<string> prepared) : this(prepared,
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "ClipBridge")) { }

    internal AutomaticUploadWindow(IEnumerable<string> prepared, string folder)
    {
        InitializeComponent();
        _session.DebugLog = _debug;
        _session.ConnectionStatus = message => UploadStatus.Text = message;
        FolderPathBox.Text = folder;
        _queue = new AutomaticUploadQueue();
        try
        {
            _queue.Add(prepared);
            UrlBox.Text = _queue.Settings.ConversationUrl;
            BatchBox.Text = _queue.Settings.BatchSize.ToString();
            PromptBox.Text = _queue.Settings.Prompt;
            RefreshQueue();
        }
        catch { _queue.Dispose(); throw; }
    }

    private void RefreshQueue()
    {
        ClipsList.ItemsSource = _queue.Clips.ToArray();
        QueueLabel.Text = $"{_queue.Clips.Count(clip => clip.State == "Ready")} ready; " +
            $"{_queue.Clips.Count(clip => clip.State == "Submitted")} submitted; " +
            $"{_queue.Clips.Count(clip => clip.State is "Attachment attempted" or "Send attempted")} need review.";
    }

    private void AddPaths(IEnumerable<string> paths)
    {
        try
        {
            string[] files = paths.ToArray();
            int count = _queue.Add(files);
            RefreshQueue();
            ImportStatus.Text = files.Length == 0 ? "No MP4, MOV, M4V or WebM clips found directly in this folder. Choose the folder containing the video files." :
                $"Found {files.Length} clip(s): {count} added, {files.Length - count} already queued. No processing required.";
            UploadStatus.Text = ImportStatus.Text;
        }
        catch (Exception ex) { ImportStatus.Text = UploadStatus.Text = ex.Message; }
    }

    private void LoadFolder_Click(object sender, RoutedEventArgs e)
    {
        try { AddPaths(PreparedClipFolder.Read(FolderPathBox.Text.Trim())); }
        catch (Exception ex) { ImportStatus.Text = UploadStatus.Text = ex.Message; }
    }

    private void Folder_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFolderDialog { Title = "Choose the clip folder, then click Select Folder" };
        if (Directory.Exists(FolderPathBox.Text)) picker.InitialDirectory = FolderPathBox.Text;
        if (picker.ShowDialog(this) != true) return;
        FolderPathBox.Text = picker.FolderName;
        LoadFolder_Click(sender, e);
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog
        {
            Title = "Choose prepared video clips",
            Multiselect = true,
            Filter = "Prepared video clips|*.mp4;*.mov;*.m4v;*.webm"
        };
        if (Directory.Exists(FolderPathBox.Text)) picker.InitialDirectory = FolderPathBox.Text;
        if (picker.ShowDialog(this) == true) AddPaths(picker.FileNames);
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        try { _queue.Remove(ClipsList.SelectedItems.Cast<AutomaticClip>().ToArray()); RefreshQueue(); }
        catch (Exception ex) { UploadStatus.Text = ex.Message; }
    }

    private void Retry_Click(object sender, RoutedEventArgs e)
    {
        var clips = ClipsList.SelectedItems.Cast<AutomaticClip>().ToArray();
        if (clips.Length == 0) { UploadStatus.Text = "Select the clips you have reviewed in ChatGPT first."; return; }
        if (MessageBox.Show(this, "These clips may already be attached or sent. Retry the selected clips after reviewing the chat? This can create duplicates.",
            "Retry reviewed clips", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try { _queue.RetryReviewed(clips); RefreshQueue(); UploadStatus.Text = "Selected clips are ready for another attempt."; }
        catch (Exception ex) { UploadStatus.Text = ex.Message; }
    }

    private void SaveSettings()
    {
        if (!int.TryParse(BatchBox.Text, out int count)) throw new ArgumentException("Enter a batch size from 1 to 10.");
        _queue.Configure(UrlBox.Text, count, PromptBox.Text);
    }

    private async void Open_Click(object sender, RoutedEventArgs e) => await RunOperationAsync(upload: false);
    private async void Start_Click(object sender, RoutedEventArgs e) => await RunOperationAsync(upload: true);

    private async Task RunOperationAsync(bool upload)
    {
        if (_busy) return;
        try
        {
            _debug.Stage(upload ? UploadDebugStage.Starting : UploadDebugStage.SignIn);
            SaveSettings();
            if (upload && !_queue.Clips.Any(clip => clip.State == "Ready"))
                throw new InvalidOperationException("Choose a clip folder or add prepared clips first. Review uncertain attempts before retrying them.");
            _busy = true;
            SettingsPanel.IsEnabled = false;
            OpenButton.IsEnabled = StartUploadButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            _cancellation = new CancellationTokenSource();
            if (upload)
            {
                UploadStatus.Text = "Opening your saved Brave profile for uploads. The manual sign-in window must be closed first.";
                await _session.OpenAsync(_queue.Settings.ConversationUrl, _cancellation.Token);
                var runner = new PlaywrightChatUploader(_session.Page!, _queue, message =>
                {
                    UploadStatus.Text = message;
                    UrlBox.Text = _queue.Settings.ConversationUrl;
                    RefreshQueue();
                });
                runner.DebugLog = _debug;
                await runner.RunAsync(_cancellation.Token);
            }
            else
            {
                UploadStatus.Text = "Opening Brave for manual sign-in. Playwright is disconnected.";
                await _session.OpenForSignInAsync(_queue.Settings.ConversationUrl, _cancellation.Token);
                UploadStatus.Text = "Sign in and finish verification yourself in the separate Brave window. Once the chat loads, close that window, then click Start automatic uploads + Send. Your login stays saved.";
            }
        }
        catch (OperationCanceledException) { _debug.Stage(UploadDebugStage.Stopped); UploadStatus.Text = "Stopped. Review any batch already attached or sent before retrying."; }
        catch (Exception ex) { _debug.Failure(ex); UploadStatus.Text = "Paused: " + ex.Message; }
        finally
        {
            _session.Dispose();
            _cancellation?.Dispose();
            _cancellation = null;
            _busy = false;
            SettingsPanel.IsEnabled = true;
            OpenButton.IsEnabled = StartUploadButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            RefreshQueue();
            UrlBox.Text = _queue.Settings.ConversationUrl;
            if (_closing) Close();
        }
    }

    private void CopyDebug_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(_debug.Export(_queue.Clips, _queue.Settings.ConversationUrl, _busy));
            DebugCopyStatus.Text = "Debug report copied. Paste it into Codex.";
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            DebugCopyStatus.Text = "Clipboard is busy. Click Copy debug report again.";
        }
    }
    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        _cancellation?.Cancel();
        UploadStatus.Text = "Stopping. A file attachment already in progress may finish; no further Send will be started.";
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_busy) return;
        e.Cancel = true;
        _closing = true;
        _cancellation?.Cancel();
    }

    protected override void OnClosed(EventArgs e)
    {
        _cancellation?.Cancel();
        _session.Dispose();
        _queue.Dispose();
        base.OnClosed(e);
    }
}
