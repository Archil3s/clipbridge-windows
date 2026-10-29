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

    public AutomaticUploadWindow(IEnumerable<string> prepared)
    {
        InitializeComponent();
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
            int count = _queue.Add(paths);
            RefreshQueue();
            UploadStatus.Text = $"Added {count} clips to the saved automatic upload queue. No processing required.";
        }
        catch (Exception ex) { UploadStatus.Text = ex.Message; }
    }

    private void Folder_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFolderDialog { Title = "Choose a folder containing prepared clips" };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            string[] extensions = [".mp4", ".mov", ".m4v", ".webm"];
            AddPaths(Directory.EnumerateFiles(picker.FolderName).Where(path =>
                extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
        }
        catch (Exception ex) { UploadStatus.Text = ex.Message; }
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog
        {
            Title = "Choose prepared video clips",
            Multiselect = true,
            Filter = "Prepared video clips|*.mp4;*.mov;*.m4v;*.webm"
        };
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
            SaveSettings();
            if (upload && !_queue.Clips.Any(clip => clip.State == "Ready"))
                throw new InvalidOperationException("Choose a clip folder or add prepared clips first. Review uncertain attempts before retrying them.");
            _busy = true;
            SettingsPanel.IsEnabled = false;
            OpenButton.IsEnabled = StartUploadButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            _cancellation = new CancellationTokenSource();
            UploadStatus.Text = "Opening the saved automation Brave profile...";
            await _session.OpenAsync(_queue.Settings.ConversationUrl, _cancellation.Token);
            if (upload)
            {
                var runner = new PlaywrightChatUploader(_session.Page!, _queue, message =>
                {
                    UploadStatus.Text = message;
                    UrlBox.Text = _queue.Settings.ConversationUrl;
                    RefreshQueue();
                });
                await runner.RunAsync(_cancellation.Token);
            }
            else UploadStatus.Text = "Sign in yourself in the automation browser, then click Start automatic uploads + Send here.";
        }
        catch (OperationCanceledException) { UploadStatus.Text = "Stopped. Review any batch already attached or sent before retrying."; }
        catch (Exception ex) { UploadStatus.Text = "Paused: " + ex.Message; }
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
