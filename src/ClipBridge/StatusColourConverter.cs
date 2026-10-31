using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ClipBridge;

public sealed class StatusColourConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string text = value as string ?? "";
        string key = text.StartsWith("Paused", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("Stopped", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("Cancelled", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("Folder scan cancelled", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("B-roll export cancelled", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("Video saved to", StringComparison.OrdinalIgnoreCase) ? "ReviewText" :
            text.StartsWith("Processing failed", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("Could not", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("Cannot inspect", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("B-roll export failed", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("Video engine setup failed", StringComparison.OrdinalIgnoreCase) ? "ErrorText" :
            text.StartsWith("Complete", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("All queued clips submitted", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("Saved:", StringComparison.OrdinalIgnoreCase) ? "SubmittedText" : "ReadyText";
        return Application.Current.FindResource(key);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
