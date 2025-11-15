using System.Windows;
using System.Windows.Media;

namespace WpfClient.Drawings;

public abstract class DrawingDefinition
{
    protected static readonly Pen OutlinePen = new(new SolidColorBrush(Color.FromRgb(0, 0, 0)), 2.0)
    {
        LineJoin = PenLineJoin.Round
    };

    protected static readonly Brush OutlineBrush = new SolidColorBrush(Color.FromRgb(0, 0, 0));

    static DrawingDefinition()
    {
        OutlinePen.Brush.Freeze();
        OutlinePen.Freeze();
        OutlineBrush.Freeze();
    }

    public abstract string Key { get; }

    public abstract string DisplayName { get; }

    public Size BaseSize { get; } = new(AppConfig.ReferenceSize, AppConfig.ReferenceSize);

    public abstract void DrawReference(DrawingContext context);

    public void DrawCanvas(DrawingContext context, IReadOnlyDictionary<string, Color> filledFigures)
    {
        context.DrawRectangle(Brushes.White, null, new Rect(0, 0, AppConfig.CanvasWidth, AppConfig.CanvasHeight));

        var scaleX = AppConfig.CanvasWidth / BaseSize.Width;
        var scaleY = AppConfig.CanvasHeight / BaseSize.Height;

        DrawFilledFigures(context, filledFigures, scaleX, scaleY);
        DrawOutlines(context, scaleX, scaleY);
    }

    protected abstract void DrawFilledFigures(
        DrawingContext context,
        IReadOnlyDictionary<string, Color> filledFigures,
        double scaleX,
        double scaleY);

    protected abstract void DrawOutlines(DrawingContext context, double scaleX, double scaleY);

    public abstract void DrawReferenceOutlines(DrawingContext context);

    public string? HitTest(Point point)
    {
        var scaleX = BaseSize.Width / AppConfig.CanvasWidth;
        var scaleY = BaseSize.Height / AppConfig.CanvasHeight;
        var normalized = new Point(point.X * scaleX, point.Y * scaleY);
        return HitTestCore(normalized);
    }

    protected abstract string? HitTestCore(Point normalizedPoint);
}
