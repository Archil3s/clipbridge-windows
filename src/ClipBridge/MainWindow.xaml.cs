using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClipBridge.Core;
using Microsoft.Win32;

namespace ClipBridge;

public partial class MainWindow : Window
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".mkv", ".avi", ".m4v", ".webm"
    };

    private readonly ObservableCollection<string> _videos = [];
    private CancellationTokenSource? _cancellation;
    private CancellationTokenSource? _setupCancellation;
    private readonly ChatGptBrowser _chatGptBrowser = new();
    private readonly List<string> _uploadQueue = [];
    private int _uploadPosition;

    public MainWindow()
    {
        InitializeComponent();
        VideoList.ItemsSource = _videos;
        Loaded += Window_Loaded;
        OutputFolderBox.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "ClipBridge");
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _setupCancellation = new CancellationTokenSource();
        SetBusy(true);
        try
        {
            await DependencyInstaller.EnsureInstalledAsync(
                new Progress<string>(message => StatusLabel.Text = message), _setupCancellation.Token);
            var splitter = new VideoSplitter();
            await splitter.VerifyToolsAsync(_setupCancellation.Token);
            StatusLabel.Text = "Ready — video engine installed and verified.";
        }
        catch (OperationCanceledException)
        {
            StatusLabel.Text = "Video engine setup cancelled. Click Split videos to retry.";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = "Video engine setup failed. Check your internet connection; click Split videos to retry.";
            MessageBox.Show(this, ex.Message, "ClipBridge first-run setup",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _setupCancellation.Dispose();
            _setupCancellation = null;
            SetBusy(false);
        }
    }

    private void AddVideos(IEnumerable<string> paths)
    {
        foreach (string path in paths)
        {
            if (File.Exists(path) && SupportedExtensions.Contains(Path.GetExtension(path)) && !_videos.Contains(path))
                _videos.Add(path);
        }
        StatusLabel.Text = $"{_videos.Count} video(s) in queue";
    }

    private void AddVideosButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose videos to split",
            Multiselect = true,
            Filter = "Videos|*.mp4;*.mov;*.mkv;*.avi;*.m4v;*.webm|All files|*.*"
        };
        if (dialog.ShowDialog() == true) AddVideos(dialog.FileNames);
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
            AddVideos(paths);
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _videos.Clear();
        _uploadQueue.Clear();
        _uploadPosition = 0;
        AttachButton.IsEnabled = false;
        StatusLabel.Text = "Queue cleared";
    }

    private void BrowseOutputButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Choose an output folder" };
        if (dialog.ShowDialog() == true) OutputFolderBox.Text = dialog.FolderName;
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (_videos.Count == 0)
        {
            MessageBox.Show(this, "Select at least one video first.", "ClipBridge");
            return;
        }
        if (!int.TryParse(TargetSizeBox.Text, out int targetMb) || targetMb < 10 || targetMb > 500)
        {
            MessageBox.Show(this, "Enter a target between 10 and 500 MB.", "ClipBridge");
            return;
        }
        if (string.IsNullOrWhiteSpace(OutputFolderBox.Text))
        {
            MessageBox.Show(this, "Choose an output folder.", "ClipBridge");
            return;
        }

        _cancellation = new CancellationTokenSource();
        SetBusy(true);
        _uploadQueue.Clear();
        _uploadPosition = 0;
        AttachButton.IsEnabled = false;
        bool compress = ModeBox.SelectedIndex > 0;
        int crf = ModeBox.SelectedIndex switch { 1 => 24, 2 => 28, 3 => 32, _ => 28 };
        string[] files = _videos.ToArray();
        int completed = 0;
        try
        {
            Directory.CreateDirectory(OutputFolderBox.Text);
            await DependencyInstaller.EnsureInstalledAsync(
                new Progress<string>(message => StatusLabel.Text = message), _cancellation.Token);
            var splitter = new VideoSplitter();
            await splitter.VerifyToolsAsync(_cancellation.Token);

            foreach (string file in files)
            {
                _cancellation.Token.ThrowIfCancellationRequested();
                string current = Path.GetFileName(file);
                StatusLabel.Text = $"Splitting {completed + 1}/{files.Length}: {current}";
                VideoList.SelectedItem = file;
                SplitResult result = await splitter.SplitAsync(file, OutputFolderBox.Text, targetMb, compress, crf,
                    _cancellation.Token);
                if (!result.AllWithinLimit)
                    throw new InvalidOperationException(
                        $"Some segments of {current} exceed {targetMb} MB. Try a smaller target or an input with more frequent keyframes. Output: {result.OutputDirectory}");
                _uploadQueue.AddRange(Directory.EnumerateFiles(result.OutputDirectory, "part_*.*")
                    .Where(path => SupportedExtensions.Contains(Path.GetExtension(path)))
                    .OrderBy(path => path, StringComparer.Ordinal));
                AttachButton.IsEnabled = _uploadQueue.Count > 0;
                completed++;
                Progress.Value = completed * 100.0 / files.Length;
                StatusLabel.Text = $"Finished {completed}/{files.Length}: {result.Parts} clips saved to {result.OutputDirectory}";
            }
            StatusLabel.Text = $"Complete: {completed} video(s), {_uploadQueue.Count} clip(s) ready. All clips passed the size check. Open ChatGPT in Edge to attach them.";
        }
        catch (OperationCanceledException)
        {
            StatusLabel.Text = $"Cancelled. {completed} video(s) completed; partial output may remain.";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = "Processing failed: " + ex.Message;
            MessageBox.Show(this, ex.Message, "ClipBridge error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _cancellation.Dispose();
            _cancellation = null;
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        StartButton.IsEnabled = !busy;
        AddVideosButton.IsEnabled = !busy;
        ClearButton.IsEnabled = !busy;
        TargetSizeBox.IsEnabled = !busy;
        ModeBox.IsEnabled = !busy;
        CancelButton.IsEnabled = busy;
        Progress.IsIndeterminate = busy;
        if (!busy) Progress.IsIndeterminate = false;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _cancellation?.Cancel();
        _setupCancellation?.Cancel();
    }

    private void OpenOutputButton_Click(object sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(OutputFolderBox.Text)) Directory.CreateDirectory(OutputFolderBox.Text);
        Process.Start(new ProcessStartInfo("explorer.exe", OutputFolderBox.Text) { UseShellExecute = true });
    }

    private async void ChatGptLoginButton_Click(object sender, RoutedEventArgs e)
    {
        ChatGptLoginButton.IsEnabled = false;
        try
        {
            await _chatGptBrowser.OpenAsync();
            StatusLabel.Text = "Edge opened with a dedicated ClipBridge profile. Sign into ChatGPT there once and choose a conversation.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "Could not start the managed Edge browser: " + ex.Message +
                "\\nYou can still click Manual ChatGPT and attach clips yourself.",
                "ClipBridge browser", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { ChatGptLoginButton.IsEnabled = true; }
    }

    private async void AttachButton_Click(object sender, RoutedEventArgs e)
    {
        if (_uploadPosition >= _uploadQueue.Count)
        {
            StatusLabel.Text = "All available clips have been offered to ChatGPT. Check Edge before sending.";
            return;
        }

        AttachButton.IsEnabled = false;
        try
        {
            string[] next = _uploadQueue.Skip(_uploadPosition).Take(3).ToArray();
            int attempted = await _chatGptBrowser.TryAttachAsync(next);
            _uploadPosition += attempted;
            StatusLabel.Text = $"Attempted to attach {attempted} file(s) to ChatGPT. " +
                $"{_uploadQueue.Count - _uploadPosition} remain. Verify upload acceptance in Edge and send manually.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                ex.Message + "\\nIf the ChatGPT interface or permissions prevent uploads, use Manual ChatGPT.",
                "ClipBridge attachment", MessageBoxButton.OK, MessageBoxImage.Warning);
            StatusLabel.Text = "Browser attachment did not complete. No files were marked attached; use the manual fallback.";
        }
        finally { AttachButton.IsEnabled = _uploadPosition < _uploadQueue.Count; }
    }

    protected override async void OnClosed(EventArgs e)
    {
        _cancellation?.Cancel();
        _setupCancellation?.Cancel();
        await _chatGptBrowser.DisposeAsync();
        base.OnClosed(e);
    }

    private void OpenChatGptButton_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://chatgpt.com/") { UseShellExecute = true });
    }
}
