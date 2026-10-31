using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using ClipBridge;

internal static class ThemeTests
{
    [STAThread]
    private static void Main(string[] args)
    {
        string repo = Directory.GetCurrentDirectory();
        string output = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.Combine(repo, "work", "theme-previews");
        Directory.CreateDirectory(output);
        var app = new Application();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var theme = XDocument.Load(Path.Combine(repo, "src", "ClipBridge", "App.xaml"));
        var resources = new XElement(presentation + "ResourceDictionary",
            new XAttribute("xmlns", presentation.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "x", "http://schemas.microsoft.com/winfx/2006/xaml"),
            new XAttribute(XNamespace.Xmlns + "local", "clr-namespace:ClipBridge;assembly=ClipBridge"),
            theme.Root!.Element(presentation + "Application.Resources")!.Elements());
        foreach (var element in resources.Descendants().Where(e => e.Name.NamespaceName == "clr-namespace:ClipBridge"))
            element.Name = XName.Get(element.Name.LocalName, "clr-namespace:ClipBridge;assembly=ClipBridge");
        app.Resources = (ResourceDictionary)XamlReader.Parse(resources.ToString());
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        try
        {
            foreach (string background in new[] { "WindowBackground", "PanelBackground", "InputBackground", "DisabledBackground", "SelectionBackground" })
            {
                Check(Ratio(Brush(app, "TextPrimary"), Brush(app, background)) >= 4.5, "primary text contrast on " + background);
                Check(Ratio(Brush(app, "TextSecondary"), Brush(app, background)) >= 4.5, "secondary text contrast on " + background);
            }
            foreach (string state in new[] { "ReadyText", "SubmittedText", "ReviewText", "ErrorText" })
                Check(Ratio(Brush(app, state), Brush(app, "SelectionBackground")) >= 4.5, "state contrast on selected row: " + state);
            Check(Ratio(Brush(app, "TextPrimary"), Brush(app, "PrimaryAccent")) >= 4.5, "primary button text contrast");
            Check(Ratio(Brush(app, "ControlEdge"), Brush(app, "PanelBackground")) >= 3, "control outline contrast");
            foreach (var sample in new[] { ("Paused: demo", "ReviewText"), ("Complete: demo", "SubmittedText"), ("Processing failed: demo", "ErrorText") })
            {
                var status = new TextBlock { Style = (Style)app.FindResource("StatusText"), Text = sample.Item1 };
                Pump();
                Check(((SolidColorBrush)status.Foreground).Color == Brush(app, sample.Item2), "status text uses semantic colour: " + sample.Item2);
            }
            foreach (string screen in new[] { "MainWindow", "AutomaticUploadWindow", "BRollWindow" })
            foreach (bool disabled in new[] { false, true })
            {
                string source = File.ReadAllText(Path.Combine(repo, "src", "ClipBridge", screen + ".xaml"));
                source = Regex.Replace(source, "x:Class=\"[^\"]+\"", "");
                source = Regex.Replace(source, "\\s(?:Click|Drop|DragOver|Closing|Checked|Unchecked|MediaOpened|MediaEnded|MediaFailed|ValueChanged|MouseDoubleClick)=\"[^\"]+\"", "");
                var window = (Window)XamlReader.Parse(source);
                window.ShowActivated = false;
                window.ShowInTaskbar = false;
                window.WindowStyle = WindowStyle.None;
                window.Left = -10000; window.Top = -10000;
                window.Width = screen == "MainWindow" ? 850 : screen == "BRollWindow" ? 780 : 760;
                window.Height = screen == "BRollWindow" ? 1080 : 1000;
                if (window.FindName("VersionLabel") is TextBlock version) version.Text = "THEME PREVIEW";
                if (window.FindName("VideoList") is ListBox videos) videos.ItemsSource = new[] { "DEMO — garden-source-01.MOV", "DEMO — garden-source-02.MOV" };
                if (window.FindName("ClipsList") is ListBox clips) clips.ItemsSource = new[] {
                    new { State="Ready", Name="DEMO — clip-01.mp4", Path="Synthetic preview" },
                    new { State="Submitted", Name="DEMO — clip-02.mp4", Path="Synthetic preview" },
                    new { State="Attachment attempted", Name="DEMO — clip-03.mp4", Path="Synthetic preview" },
                    new { State="Send attempted", Name="DEMO — clip-04.mp4", Path="Synthetic preview" }};
                foreach (string name in new[] { "StatusLabel", "UploadStatus", "ExportStatusLabel" })
                    if (window.FindName(name) is TextBlock status) status.Text = "Paused: SYNTHETIC THEME PREVIEW — sample state, no real job.";
                if (window.FindName("QueueLabel") is TextBlock count) count.Text = "SYNTHETIC PREVIEW — sample queue states";
                if (window.FindName("CloudflareTokenBox") is PasswordBox password) password.Password = "demo";
                window.Show();
                Pump();
                if (disabled)
                {
                    if (window.FindName("SettingsPanel") is UIElement settings) settings.IsEnabled = false;
                    foreach (string name in new[] { "VideoList", "ModeBox", "StartButton", "OpenButton", "StartUploadButton", "AddVideosButton", "AddFolderButton", "CloudflareTokenBox", "ExportButton", "SavedClipsList" })
                        if (window.FindName(name) is UIElement control) control.IsEnabled = false;
                }
                Pump();
                foreach (ListBox list in Descendants<ListBox>((DependencyObject)window.Content))
                {
                    Check(((SolidColorBrush)((Border)VisualTreeHelper.GetChild(list, 0)).Background).Color != Colors.White, screen + " queue remains dark, disabled=" + disabled);
                    if (list.Items.Count > 0) list.SelectedIndex = 0;
                }
                Pump();
                Save((FrameworkElement)window.Content, Path.Combine(output, screen + (disabled ? "-disabled" : "-ready") + ".png"));
                if (!disabled && window.FindName("ModeBox") is ComboBox combo)
                {
                    var toggle = Descendants<ToggleButton>(combo).First();
                    toggle.SetCurrentValue(ToggleButton.IsCheckedProperty, true); Pump();
                    Check(combo.IsDropDownOpen, "dropdown toggle opens the popup through its binding");
                    var popup = (Popup)combo.Template.FindName("PART_Popup", combo);
                    Save((FrameworkElement)popup.Child, Path.Combine(output, "mode-dropdown.png"));
                    combo.SelectedIndex = 1; combo.IsDropDownOpen = false; Pump();
                    Check(combo.SelectedIndex == 1, "mode selection remains functional");
                }
                if (!disabled && window.FindName("CategoryBox") is ComboBox category)
                {
                    var editor = (TextBox)category.Template.FindName("PART_EditableTextBox", category);
                    Check(editor.Text == "Garden", "editable category displays its current text");
                    editor.Text = "DEMO custom category"; Pump();
                    Check(category.Text == "DEMO custom category", "editable category preserves custom input");
                }
                if (window.Content is ScrollViewer scroll)
                {
                    scroll.ScrollToEnd(); Pump();
                    Save(scroll, Path.Combine(output, screen + (disabled ? "-disabled-bottom" : "-ready-bottom") + ".png"));
                }
                window.Close();
            }
        }
        finally { app.Shutdown(); }
    }

    private static Color Brush(Application app, string key) => ((SolidColorBrush)app.FindResource(key)).Color;
    private static double Ratio(Color a, Color b)
    {
        static double L(Color c)
        {
            static double S(byte n) { double v = n / 255d; return v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4); }
            return .2126 * S(c.R) + .7152 * S(c.G) + .0722 * S(c.B);
        }
        double x=L(a), y=L(b);
        return (Math.Max(x,y)+.05)/(Math.Min(x,y)+.05);
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
        {
            var child=VisualTreeHelper.GetChild(root,i);
            if(child is T item) yield return item;
            foreach(var nested in Descendants<T>(child)) yield return nested;
        }
    }
    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static void Save(FrameworkElement element, string path)
    {
        element.UpdateLayout();
        var image=new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth),(int)Math.Ceiling(element.ActualHeight),96,96,PixelFormats.Pbgra32);
        var drawing = new DrawingVisual();
        using (var context = drawing.RenderOpen())
        {
            var rect = new Rect(0, 0, element.ActualWidth, element.ActualHeight);
            context.DrawRectangle((Brush)Application.Current.FindResource("WindowBackground"), null, rect);

        }
        image.Render(drawing);
        image.Render(element);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));
        using var file=File.Create(path);encoder.Save(file);
    }
    private static void Check(bool value,string message)
    {
        if(!value) throw new Exception(message);
        Console.WriteLine("PASS "+message);
    }
}
