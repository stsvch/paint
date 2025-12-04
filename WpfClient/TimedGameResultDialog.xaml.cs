using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WpfClient;

public enum TimedGameResultAction
{
    None,
    Retry,
    BackToMenu
}

public partial class TimedGameResultDialog : Window
{
    public TimedGameResultAction Action { get; private set; } = TimedGameResultAction.None;

    public TimedGameResultDialog(TimedGameResultEventArgs result)
    {
        InitializeComponent();
        Populate(result);
    }

    private void Populate(TimedGameResultEventArgs result)
    {
        if (result.Success)
        {
            ResultIcon.Text = "🏆";
            ResultTitle.Text = "Отличный результат!";
            ResultSubtitle.Text = "Вы успели раскрасить картинку до окончания таймера.";
        }
        else
        {
            ResultIcon.Text = "⌛";
            ResultTitle.Text = "Время вышло";
            ResultSubtitle.Text = "Немного не хватило — попробуйте ещё раз или выберите другой рисунок.";
        }

        RoundDurationText.Text = Format(result.RoundDuration);
        ElapsedText.Text = Format(result.Elapsed);

        RegionsText.Text = result.TotalRegions > 0
            ? $"{result.FilledRegions} из {result.TotalRegions}"
            : $"{result.FilledRegions}";

        var accuracyText = result.TotalRegions > 0
            ? $"{result.CompletionPercent:F0}%"
            : "-";
        AccuracyText.Text = accuracyText;
        AccuracyPercentText.Text = accuracyText;

        // Время активности / бездействия
        ActiveTimeText.Text = Format(result.ActiveTime);
        IdleTimeText.Text = Format(result.IdleTime);

        // Кол-во действий и скорость
        FillActionsText.Text = result.FillActions.ToString();
        ActionsPerMinuteText.Text = result.ActionsPerMinute > 0
            ? $"{result.ActionsPerMinute:F1} / мин"
            : "—";

        UpdateAccuracyArc(result.CompletionPercent);
    }

    private void UpdateAccuracyArc(double percent)
    {
        if (percent <= 0)
        {
            AccuracyArc.Data = null;
            return;
        }

        if (percent > 100)
        {
            percent = 100;
        }

        var angle = 360.0 * percent / 100.0;
        var radians = Math.PI * angle / 180.0;

        const double centerX = 60;
        const double centerY = 60;
        const double radius = 50;

        // Начинаем сверху круга
        var startPoint = new Point(centerX, centerY - radius);
        var endX = centerX + radius * Math.Sin(radians);
        var endY = centerY - radius * Math.Cos(radians);
        var endPoint = new Point(endX, endY);

        var largeArc = angle > 180.0;

        var figure = new PathFigure
        {
            StartPoint = startPoint,
            IsClosed = false
        };

        var arcSegment = new ArcSegment
        {
            Point = endPoint,
            Size = new Size(radius, radius),
            IsLargeArc = largeArc,
            SweepDirection = SweepDirection.Clockwise
        };

        figure.Segments.Add(arcSegment);

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);

        AccuracyArc.Data = geometry;
    }

    private static string Format(TimeSpan value)
    {
        var totalSeconds = (int)value.TotalSeconds;
        var ts = TimeSpan.FromSeconds(totalSeconds);
        return $"{ts.Minutes:D2}:{ts.Seconds:D2}";
    }

    private void OnRetryClick(object sender, RoutedEventArgs e)
    {
        Action = TimedGameResultAction.Retry;
        DialogResult = true;
        Close();
    }

    private void OnBackToMenuClick(object sender, RoutedEventArgs e)
    {
        Action = TimedGameResultAction.BackToMenu;
        DialogResult = true;
        Close();
    }
}

