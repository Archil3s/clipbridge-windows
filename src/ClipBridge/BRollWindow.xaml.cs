using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ClipBridge.Core;
using Microsoft.Win32;

namespace ClipBridge;

public partial class BRollWindow : Window
{
    private readonly ObservableCollection<BRollLibraryItem> _savedItems = [];
    private readonly DispatcherTimer _playbackTimer;
    private string? _sourcePath;
    private TimeSpan _sourceDuration;
    private bool _playing;
    private bool _updatingSlider;
    private CancellationTokenSource? _exportCancellation;
    private int _refreshVersion;

    private CheckBox[] TagBoxes =>
    [
        TagPlanting, TagHarvesting, TagWatering, TagSoil, TagPruning,
        TagSeedlings, TagTools, TagWildlife, TagWeeding, TagOverview
    ];

    private CheckBox[] FilterBoxes =>
    [
        FilterPlanting, FilterHarvesting, FilterWatering, FilterSoil, FilterPruning,
        FilterSeedlings, FilterTools, FilterWildlife, FilterWeeding, FilterOverview
    ];

    public BRollWindow()
    {
        InitializeComponent();
        LibraryFolderBox.Text = BRollCutter.DefaultLibraryPath;
        SavedClipsList.ItemsSource = _savedItems;
        SeekSlider.IsEnabled = false;
        PlayPauseButton.IsEnabled = false;
        _playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _playbackTimer.Tick += (_, _) => UpdatePlaybackClock();
        Loaded += async (_, _) => await ReloadLibraryAsync();
    }

    private async void SelectSourceButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog
        {
            Title = "Choose a video to cut for B-roll",
            Filter = "Video files|*.mp4;*.mov;*.mkv;*.avi;*.m4v;*.webm|All files|*.*"
        };
        if (picker.ShowDialog(this) != true) return;
        string source = picker.FileName;
        SelectSourceButton.IsEnabled = false;
        ExportStatusLabel.Text = "Inspecting source video...";
        try
        {
            await DependencyInstaller.EnsureInstalledAsync(
                new Progress<string>(s => ExportStatusLabel.Text = s), CancellationToken.None);
            var cutter = new BRollCutter();
            TimeSpan duration = await cutter.GetDurationAsync(source, CancellationToken.None);

            Player.Stop();
            _sourcePath = source;
            _sourceDuration = duration;
            _playing = false;
            SourceNameLabel.Text = Path.GetFileName(source);
            ClipNameBox.Text = "B-Roll";
            StartTimeBox.Text = "00:00";
            EndTimeBox.Text = FormatTime(TimeSpan.FromSeconds(Math.Min(10, duration.TotalSeconds)));
            _updatingSlider = true;
            SeekSlider.Minimum = 0;
            SeekSlider.Maximum = duration.TotalSeconds;
            SeekSlider.Value = 0;
            _updatingSlider = false;
            SeekSlider.IsEnabled = true;
            PlayPauseButton.IsEnabled = true;
            Player.Source = new Uri(source, UriKind.Absolute);
            Player.Play();
            Player.Pause();
            _playbackTimer.Start();
            UpdatePlaybackClock();
            ExportStatusLabel.Text = $"Video ready: {FormatTime(duration)}. Mark the useful section and tag it.";
        }
        catch (Exception ex)
        {
            ExportStatusLabel.Text = "Cannot inspect the selected video.";
            MessageBox.Show(this, ex.Message, "B-roll source", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            SelectSourceButton.IsEnabled = true;
        }
    }

    private void Player_MediaOpened(object sender, RoutedEventArgs e)
    {
        if (Player.NaturalDuration.HasTimeSpan && _sourceDuration == TimeSpan.Zero)
        {
            _sourceDuration = Player.NaturalDuration.TimeSpan;
            SeekSlider.Maximum = _sourceDuration.TotalSeconds;
        }
        PreviewNotice.Text = "Preview loaded. Use the slider or Play, then Mark In / Mark Out.";
    }

    private void Player_MediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        PreviewNotice.Text = "Windows cannot preview this codec. You can still enter precise start and end times manually.";
        ExportStatusLabel.Text = "Preview unavailable; FFmpeg may still export your chosen time range.";
    }

    private void Player_MediaEnded(object sender, RoutedEventArgs e)
    {
        _playing = false;
        Player.Pause();
        PlayPauseButton.Content = "Play";
    }

    private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_sourcePath is null) return;
        if (_playing)
        {
            Player.Pause();
            _playing = false;
            PlayPauseButton.Content = "Play";
        }
        else
        {
            Player.Play();
            _playing = true;
            PlayPauseButton.Content = "Pause";
        }
    }

    private void UpdatePlaybackClock()
    {
        if (_sourcePath is null) return;
        TimeSpan position;
        try { position = Player.Position; }
        catch (InvalidOperationException) { return; }
        if (position < TimeSpan.Zero) position = TimeSpan.Zero;
        _updatingSlider = true;
        SeekSlider.Value = Math.Clamp(position.TotalSeconds, 0, SeekSlider.Maximum);
        _updatingSlider = false;
        PlaybackTimeLabel.Text = $"{FormatTime(position)} / {FormatTime(_sourceDuration)}";
    }

    private void SeekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded || _sourcePath is null || _updatingSlider) return;
        try { Player.Position = TimeSpan.FromSeconds(Math.Max(0, e.NewValue)); }
        catch (InvalidOperationException) { }
        PlaybackTimeLabel.Text = $"{FormatTime(TimeSpan.FromSeconds(e.NewValue))} / {FormatTime(_sourceDuration)}";
    }

    private void BackFiveButton_Click(object sender, RoutedEventArgs e) =>
        SeekRelative(-5);

    private void ForwardFiveButton_Click(object sender, RoutedEventArgs e) =>
        SeekRelative(5);

    private void SeekRelative(double seconds)
    {
        if (_sourcePath is null) return;
        SeekSlider.Value = Math.Clamp(SeekSlider.Value + seconds, 0, SeekSlider.Maximum);
    }

    private void MarkInButton_Click(object sender, RoutedEventArgs e)
    {
        if (_sourcePath is not null) StartTimeBox.Text = FormatTime(TimeSpan.FromSeconds(SeekSlider.Value));
    }

    private void MarkOutButton_Click(object sender, RoutedEventArgs e)
    {
        if (_sourcePath is not null) EndTimeBox.Text = FormatTime(TimeSpan.FromSeconds(SeekSlider.Value));
    }

    private static string FormatTime(TimeSpan time) =>
        time.TotalHours >= 1 ? time.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)
            : time.ToString(@"mm\:ss", CultureInfo.InvariantCulture);

    // Two groups mean mm:ss (not hh:mm). Three groups mean hh:mm:ss.
    private static bool TryParseClipTime(string input, out TimeSpan time)
    {
        time = default;
        string[] parts = input.Trim().Split(':');
        if (parts.Length is < 1 or > 3) return false;
        if (!double.TryParse(parts[^1], NumberStyles.Float, CultureInfo.InvariantCulture,
            out double seconds) || !double.IsFinite(seconds) || seconds < 0)
            return false;
        if (parts.Length > 1 && seconds >= 60) return false;
        long hours = 0;
        long minutes = 0;
        if (parts.Length >= 2 &&
            (!long.TryParse(parts[^2], NumberStyles.None, CultureInfo.InvariantCulture,
                out minutes) || minutes < 0 || (parts.Length == 3 && minutes >= 60)))
            return false;
        if (parts.Length == 3 &&
            (!long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture,
                out hours) || hours < 0 || hours > 10000))
            return false;
        try
        {
            time = TimeSpan.FromSeconds(hours * 3600 + minutes * 60 + seconds);
            return time >= TimeSpan.Zero;
        }
        catch (OverflowException) { return false; }
    }

    private string[] CheckedTags(IEnumerable<CheckBox> checkboxes) =>
        checkboxes.Where(box => box.IsChecked == true)
            .Select(box => box.Tag?.ToString())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag!)
            .ToArray();

    private void ChooseLibraryButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFolderDialog { Title = "Choose the reusable B-roll library folder" };
        if (picker.ShowDialog(this) == true)
        {
            LibraryFolderBox.Text = picker.FolderName;
            _ = ReloadLibraryAsync();
        }
    }

    private async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_sourcePath is null || !File.Exists(_sourcePath))
        {
            MessageBox.Show(this, "Choose a source video first.", "B-roll library");
            return;
        }
        if (!TryParseClipTime(StartTimeBox.Text, out TimeSpan start) ||
            !TryParseClipTime(EndTimeBox.Text, out TimeSpan end) ||
            end <= start || end > _sourceDuration + TimeSpan.FromMilliseconds(250))
        {
            MessageBox.Show(this, "Enter a valid start and end within the source video. Use mm:ss or hh:mm:ss.",
                "Check clip times", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string[] tags = CheckedTags(TagBoxes);
        if (tags.Length == 0)
        {
            MessageBox.Show(this, "Tick at least one gardening topic for this clip. That is how it can be found using the library filters.",
                "Add gardening tags", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string path = _sourcePath;
        string root = LibraryFolderBox.Text;
        string category = CategoryBox.Text;
        string title = ClipNameBox.Text;
        _exportCancellation = new CancellationTokenSource();
        SetExportBusy(true);
        ExportProgress.Value = 0;
        ExportStatusLabel.Text = "Preparing B-roll clip...";
        try
        {
            await DependencyInstaller.EnsureInstalledAsync(
                new Progress<string>(s => ExportStatusLabel.Text = s), _exportCancellation.Token);
            var cutter = new BRollCutter();
            var progress = new Progress<double>(pct =>
            {
                ExportProgress.Value = pct;
                ExportStatusLabel.Text = $"Exporting B-roll: {pct:0}%";
            });
            BRollClip output = await cutter.CutAsync(path, root, category, title,
                start, end, progress, _exportCancellation.Token);
            try
            {
                await BRollLibrary.SaveMetadataAsync(output, path, title, tags, _exportCancellation.Token);
                ExportStatusLabel.Text = $"Saved: {output.FilePath}. Tags: {string.Join(", ", tags)}";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ExportStatusLabel.Text = $"Video saved to {output.FilePath}, but its gardening tags could not be written: {ex.Message}";
            }
            await ReloadLibraryAsync();
        }
        catch (OperationCanceledException)
        {
            ExportStatusLabel.Text = "B-roll export cancelled. The original source is unchanged.";
        }
        catch (Exception ex)
        {
            ExportStatusLabel.Text = "B-roll export failed: " + ex.Message;
            MessageBox.Show(this, ex.Message, "B-roll export", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _exportCancellation.Dispose();
            _exportCancellation = null;
            SetExportBusy(false);
        }
    }

    private void SetExportBusy(bool busy)
    {
        ExportButton.IsEnabled = !busy;
        SelectSourceButton.IsEnabled = !busy;
        CancelExportButton.IsEnabled = busy;
        StartTimeBox.IsEnabled = !busy;
        EndTimeBox.IsEnabled = !busy;
        ClipNameBox.IsEnabled = !busy;
        CategoryBox.IsEnabled = !busy;
        foreach (var box in TagBoxes) box.IsEnabled = !busy;
    }

    private void CancelExportButton_Click(object sender, RoutedEventArgs e) =>
        _exportCancellation?.Cancel();

    private void OpenLibraryButton_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(LibraryFolderBox.Text);
        Process.Start(new ProcessStartInfo("explorer.exe", LibraryFolderBox.Text)
        {
            UseShellExecute = true
        });
    }

    private async Task ReloadLibraryAsync()
    {
        if (!IsLoaded) return;
        int version = ++_refreshVersion;
        string root = LibraryFolderBox.Text;
        string[] filters = CheckedTags(FilterBoxes);
        try
        {
            IReadOnlyList<BRollLibraryItem> items = await Task.Run(
                () => BRollLibrary.FindClips(root, filters));
            if (version != _refreshVersion || !IsLoaded) return;
            _savedItems.Clear();
            foreach (BRollLibraryItem item in items) _savedItems.Add(item);
            string scope = filters.Length > 0 ? " matching " + string.Join(" or ", filters) : "";
            LibraryCountLabel.Text = $"{items.Count} saved clip(s){scope}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LibraryCountLabel.Text = "Could not read the B-roll folder: " + ex.Message;
        }
    }

    private async void Filter_Changed(object sender, RoutedEventArgs e) =>
        await ReloadLibraryAsync();

    private async void ClearFiltersButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var checkBox in FilterBoxes) checkBox.IsChecked = false;
        await ReloadLibraryAsync();
    }

    private async void RefreshLibraryButton_Click(object sender, RoutedEventArgs e) =>
        await ReloadLibraryAsync();

    private async void ApplyTagsToSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        if (SavedClipsList.SelectedItem is not BRollLibraryItem item)
        {
            MessageBox.Show(this, "First select a clip from the saved library list.", "B-roll tags");
            return;
        }
        string[] tags = CheckedTags(TagBoxes);
        if (tags.Length == 0)
        {
            MessageBox.Show(this, "Tick at least one gardening topic in the upper tag section, then apply it to the selected clip.", "B-roll tags");
            return;
        }
        try
        {
            await BRollLibrary.UpdateTagsAsync(item, tags, CancellationToken.None);
            ExportStatusLabel.Text = $"Updated gardening topics on {item.Title}: {string.Join(", ", tags)}";
            await ReloadLibraryAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            MessageBox.Show(this, ex.Message, "Could not update B-roll tags",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SavedClipsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SavedClipsList.SelectedItem is BRollLibraryItem item && File.Exists(item.FilePath))
            Process.Start(new ProcessStartInfo(item.FilePath) { UseShellExecute = true });
    }

    protected override void OnClosed(EventArgs e)
    {
        _exportCancellation?.Cancel();
        _playbackTimer.Stop();
        Player.Stop();
        Player.Close();
        base.OnClosed(e);
    }
}
