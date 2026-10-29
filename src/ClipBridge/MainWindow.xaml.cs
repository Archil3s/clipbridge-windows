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

    public MainWindow()
    {
        InitializeComponent();
        VideoList.ItemsSource = _videos;
        OutputFolderBox.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "ClipBridge");
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
        var splitter = new VideoSplitter();
        string[] files = _videos.ToArray();
        int completed = 0;
        try
        {
            Directory.CreateDirectory(OutputFolderBox.Text);
            await splitter.VerifyToolsAsync(_cancellation.Token);

            foreach (string file in files)
            {
                _cancellation.Token.ThrowIfCancellationRequested();
                string current = Path.GetFileName(file);
                StatusLabel.Text = $"Splitting {completed + 1}/{files.Length}: {current}";
                VideoList.SelectedItem = file;
                SplitResult result = await splitter.SplitAsync(file, OutputFolderBox.Text, targetMb,
                    _cancellation.Token);
                if (!result.AllWithinLimit)
                    throw new InvalidOperationException(
                        $"Some segments of {current} exceed {targetMb} MB. Try a smaller target or an input with more frequent keyframes. Output: {result.OutputDirectory}");
                completed++;
                Progress.Value = completed * 100.0 / files.Length;
                StatusLabel.Text = $"Finished {completed}/{files.Length}: {result.Parts} clips saved to {result.OutputDirectory}";
            }
            StatusLabel.Text = $"Complete: {completed} video(s) processed. All output clips passed the file-size check.";
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

    private void CancelButton_Click(object sender, RoutedEventArgs e) => _cancellation?.Cancel();

    private void OpenOutputButton_Click(object sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(OutputFolderBox.Text)) Directory.CreateDirectory(OutputFolderBox.Text);
        Process.Start(new ProcessStartInfo("explorer.exe", OutputFolderBox.Text) { UseShellExecute = true });
    }

    private void OpenChatGptButton_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://chatgpt.com/") { UseShellExecute = true });
    }
}
