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
    private readonly JobJournal _journal = new();
    private CloudflareStatusReporter? _cloudflareReporter;

    public MainWindow()
    {
        InitializeComponent();
        VideoList.ItemsSource = _videos;
        Loaded += Window_Loaded;
        OutputFolderBox.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "ClipBridge");
        var unfinished = _journal.RecoverableInputs();
        AddVideos(unfinished);
        if (unfinished.Count > 0)
            StatusLabel.Text = $"Restored {unfinished.Count} unfinished source video(s). " +
                "Processing will restart for those sources; any previously completed jobs are reused.";
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
        Progress.Value = 0;
        ProgressPercentLabel.Text = "0%";
        ProgressInfoLabel.Text = "Starting batch...";
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

            if (_cloudflareReporter is not null)
            {
                await _cloudflareReporter.DisposeAsync();
                _cloudflareReporter = null;
            }
            if (CloudflareEnabledBox.IsChecked == true)
            {
                _cloudflareReporter = new CloudflareStatusReporter(
                    CloudflareUrlBox.Text, CloudflareTokenBox.Password,
                    status => Dispatcher.BeginInvoke(new Action(() =>
                        CloudflareStatusLabel.Text = status)));
                _cloudflareReporter.Start();
                CloudflareStatusLabel.Text = "Cloudflare: connecting (status only, no videos)";
            }
            else CloudflareStatusLabel.Text = "Cloudflare: disabled; no video data sent";

            foreach (string file in files)
            {
                _cancellation.Token.ThrowIfCancellationRequested();
                string current = Path.GetFileName(file);
                var sourceInfo = new FileInfo(file);
                string jobId = Guid.NewGuid().ToString("N");
                double sizeGb = Math.Round(sourceInfo.Length / 1_000_000_000.0, 3);
                StatusLabel.Text = $"Splitting {completed + 1}/{files.Length}: {current}";
                VideoList.SelectedItem = file;
                SplitResult? result = null;
                string journalOutput = "";
                try
                {
                    result = _journal.FindCompleted(file, targetMb, compress, crf);
                    if (result is not null)
                    {
                        StatusLabel.Text = $"Reusing validated clips from {current}.";
                        ProgressInfoLabel.Text = "Completed clips recovered from previous run.";
                    }
                    else
                    {
                        VideoSplitter.CheckDiskSpace(OutputFolderBox.Text, sourceInfo.Length, compress);
                        _journal.SetState(file, targetMb, compress, crf, "", "processing");
                        _cloudflareReporter?.Publish(new CloudflareStatus(jobId, current,
                            "processing", 0, 0, 0, sizeGb));
                        int processedBefore = completed;
                        var update = new Progress<VideoProgress>(p =>
                        {
                            double batchPct = 100 * (processedBefore + p.Percent / 100.0) / files.Length;
                            Progress.Value = batchPct;
                            ProgressPercentLabel.Text = $"{batchPct:0}%";
                            ProgressInfoLabel.Text = $"{p.Phase} · {p.Percent:0}% of current video";
                            StatusLabel.Text = $"{current}: {p.Phase} · {p.ClipsDone} clips completed";
                            _cloudflareReporter?.Publish(new CloudflareStatus(jobId, current,
                                "processing", p.Percent, p.ClipsDone, p.ClipsTotal, sizeGb));
                        });
                        result = await splitter.SplitAsync(file, OutputFolderBox.Text,
                            targetMb, compress, crf, update, _cancellation.Token);
                        journalOutput = result.OutputDirectory;
                        if (!result.AllWithinLimit)
                            throw new InvalidOperationException(
                                $"Some segments of {current} exceed {targetMb} MB. " +
                                $"Try a smaller target or a video with more frequent keyframes. Output: {result.OutputDirectory}");
                        _journal.SetState(file, targetMb, compress, crf, result.OutputDirectory, "completed");
                    }

                    _uploadQueue.AddRange(Directory.EnumerateFiles(result.OutputDirectory, "part_*.*")
                        .Where(path => SupportedExtensions.Contains(Path.GetExtension(path)))
                        .OrderBy(path => path, StringComparer.Ordinal));
                    AttachButton.IsEnabled = _uploadQueue.Count > 0;
                    completed++;
                    Progress.Value = completed * 100.0 / files.Length;
                    ProgressPercentLabel.Text = $"{Progress.Value:0}%";
                    ProgressInfoLabel.Text = $"{result.Parts} clips verified for {current}";
                    _cloudflareReporter?.Publish(new CloudflareStatus(jobId, current,
                        "completed", 100, result.Parts, result.Parts, sizeGb));
                    StatusLabel.Text = $"Finished {completed}/{files.Length}: {result.Parts} clips saved to {result.OutputDirectory}";
                }
                catch (OperationCanceledException)
                {
                    _journal.SetState(file, targetMb, compress, crf, journalOutput, "cancelled");
                    _cloudflareReporter?.Publish(new CloudflareStatus(jobId, current,
                        "cancelled", 0, 0, 0, sizeGb));
                    throw;
                }
                catch (Exception ex)
                {
                    _journal.SetState(file, targetMb, compress, crf, journalOutput, "failed", ex.Message);
                    _cloudflareReporter?.Publish(new CloudflareStatus(jobId, current,
                        "failed", 0, 0, 0, sizeGb));
                    throw;
                }
            }
            StatusLabel.Text = $"Complete: {completed} video(s), {_uploadQueue.Count} clip(s) ready. All clips passed the size check. Open normal Brave, then choose clips to attach manually.";
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
        BRollButton.IsEnabled = !busy;
        AddVideosButton.IsEnabled = !busy;
        ClearButton.IsEnabled = !busy;
        TargetSizeBox.IsEnabled = !busy;
        ModeBox.IsEnabled = !busy;
        CancelButton.IsEnabled = busy;
        Progress.IsIndeterminate = busy && _setupCancellation is not null;
        CloudflareEnabledBox.IsEnabled = !busy;
        CloudflareUrlBox.IsEnabled = !busy;
        CloudflareTokenBox.IsEnabled = !busy;
        if (!busy) Progress.IsIndeterminate = false;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _cancellation?.Cancel();
        _setupCancellation?.Cancel();
    }

    private void BRollButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new BRollWindow { Owner = this };
        window.Show();
    }

    private void OpenOutputButton_Click(object sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(OutputFolderBox.Text)) Directory.CreateDirectory(OutputFolderBox.Text);
        Process.Start(new ProcessStartInfo("explorer.exe", OutputFolderBox.Text) { UseShellExecute = true });
    }

    private void ChatGptLoginButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _chatGptBrowser.Open();
            StatusLabel.Text = "Opened ChatGPT in normal Brave without browser automation. Sign in there, then attach clips manually.";
        }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception)
        {
            MessageBox.Show(this,
                "Could not open normal Brave: " + ex.Message +
                "\\nInstall Brave or open ChatGPT manually in your browser.",
                "ClipBridge browser", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void AttachButton_Click(object sender, RoutedEventArgs e)
    {
        if (_uploadPosition >= _uploadQueue.Count)
        {
            _uploadPosition = 0;
            StatusLabel.Text = "Reached the end of the prepared clips. Starting again from the first set.";
        }

        string[] next = _uploadQueue.Skip(_uploadPosition).Take(3).Where(File.Exists).ToArray();
        if (next.Length == 0)
        {
            StatusLabel.Text = "No prepared clips are available. Split a video first.";
            AttachButton.IsEnabled = false;
            return;
        }

        try
        {
            _chatGptBrowser.Open();
            string[] folders = next.Select(Path.GetDirectoryName)
                .Where(folder => !string.IsNullOrWhiteSpace(folder))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(folder => folder!)
                .ToArray();
            foreach (string folder in folders)
            {
                var explorer = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    UseShellExecute = true
                };
                explorer.ArgumentList.Add(folder);
                Process.Start(explorer);
            }
            _uploadPosition += next.Length;

            string newLine = Environment.NewLine;
            string fileNames = string.Join(newLine, next.Select(Path.GetFileName));
            string summary = "Files to select and attach manually:" + newLine + newLine +
                fileNames + newLine + newLine +
                "The output folder(s) and normal Brave have been opened. " +
                "Select these clips in File Explorer, drag them into ChatGPT, check that they finish uploading, and send the message yourself. " +
                "ClipBridge has not uploaded any files.";
            StatusLabel.Text = $"Showing {next.Length} clip(s) for manual upload. " +
                $"{_uploadQueue.Count - _uploadPosition} remain after this group. No upload was attempted.";
            MessageBox.Show(this, summary, "Next clips for manual attachment",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception)
        {
            MessageBox.Show(this,
                "Could not open Brave or the clip folder: " + ex.Message,
                "ClipBridge browser", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    protected override async void OnClosed(EventArgs e)
    {
        _cancellation?.Cancel();
        _setupCancellation?.Cancel();
        if (_cloudflareReporter is not null)
            await _cloudflareReporter.DisposeAsync();
        base.OnClosed(e);
    }

}
