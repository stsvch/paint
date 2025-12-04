using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WpfClient;

public partial class TimedGameDialog : Window
{
    public TimeSpan SelectedDuration { get; private set; } = TimeSpan.FromSeconds(60);

    public TimedGameDialog()
    {
        InitializeComponent();
        SelectedDuration = TimeSpan.FromSeconds(60);
        DurationSlider.Value = 60;
        UpdateDurationText();
        // По умолчанию подсвечиваем 1 минуту
        HighlightPreset(Preset60Button);
    }

    private void OnDurationChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        SelectedDuration = TimeSpan.FromSeconds(DurationSlider.Value);
        UpdateDurationText();
        // При ручной настройке снимаем подсветку пресетов
        ClearPresetHighlight();
    }

    private void OnPresetClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        if (button.Tag is string tag && int.TryParse(tag, out var seconds))
        {
            SelectedDuration = TimeSpan.FromSeconds(seconds);
            DurationSlider.Value = seconds;
            UpdateDurationText();
            HighlightPreset(button);
        }
    }

    private void HighlightPreset(Button activeButton)
    {
        if (activeButton.Parent is not Panel panel)
        {
            return;
        }

        foreach (var child in panel.Children)
        {
            if (child is not Button button)
            {
                continue;
            }

            var isActive = ReferenceEquals(button, activeButton);
            button.Background = isActive
                ? (Brush)FindResource("AccentBrush")
                : new SolidColorBrush(Color.FromRgb(0xEF, 0xF3, 0xFB));
            button.Foreground = isActive
                ? Brushes.White
                : (Brush)FindResource("MutedTextBrush");
        }
    }

    private void ClearPresetHighlight()
    {
        if (Preset60Button.Parent is not Panel panel)
        {
            return;
        }

        foreach (var child in panel.Children)
        {
            if (child is not Button button)
            {
                continue;
            }

            button.Background = new SolidColorBrush(Color.FromRgb(0xEF, 0xF3, 0xFB));
            button.Foreground = (Brush)FindResource("MutedTextBrush");
        }
    }

    private void UpdateDurationText()
    {
        var totalSeconds = (int)SelectedDuration.TotalSeconds;
        var ts = TimeSpan.FromSeconds(totalSeconds);
        DurationText.Text = $"{ts.Minutes:D2}:{ts.Seconds:D2}";
    }

    private void OnStartClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

