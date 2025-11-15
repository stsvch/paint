using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace WpfClient.Drawings;

public sealed class FlowerDrawing : DrawingDefinition
{
    public override string Key => "flower";

    public override string DisplayName => "Цветок";

    public override void DrawReference(DrawingContext context)
    {
        context.DrawRectangle(Brushes.White, null, new Rect(new Size(BaseSize.Width, BaseSize.Height)));

        var petalColors = new[]
        {
            CreateBrush(Color.FromRgb(255, 192, 203)),
            CreateBrush(Color.FromRgb(255, 255, 0)),
            CreateBrush(Color.FromRgb(255, 0, 255)),
            CreateBrush(Color.FromRgb(0, 255, 255)),
        };

        var petalCenters = new[]
        {
            new Point(100, 70),
            new Point(120, 90),
            new Point(100, 110),
            new Point(80, 90),
        };

        for (var i = 0; i < petalCenters.Length; i++)
        {
            context.DrawEllipse(petalColors[i], null, petalCenters[i], 20, 20);
        }

        var stemBrush = CreateBrush(Color.FromRgb(0, 255, 0));
        context.DrawRectangle(stemBrush, null, new Rect(95, 130, 10, 50));
        context.DrawEllipse(stemBrush, null, new Point(110, 140), 12, 12);
        context.DrawEllipse(stemBrush, null, new Point(85, 150), 12, 12);

        DrawReferenceOutlines(context);
    }

    protected override void DrawFilledFigures(
        DrawingContext context,
        IReadOnlyDictionary<string, Color> filledFigures,
        double scaleX,
        double scaleY)
    {
        foreach (var (name, color) in filledFigures)
        {
            var brush = CreateBrush(color);
            switch (name)
            {
                case "petal_top":
                    context.DrawEllipse(brush, null, new Point(100 * scaleX, 70 * scaleY), 20 * scaleX, 20 * scaleY);
                    break;
                case "petal_right":
                    context.DrawEllipse(brush, null, new Point(120 * scaleX, 90 * scaleY), 20 * scaleX, 20 * scaleY);
                    break;
                case "petal_bottom":
                    context.DrawEllipse(brush, null, new Point(100 * scaleX, 110 * scaleY), 20 * scaleX, 20 * scaleY);
                    break;
                case "petal_left":
                    context.DrawEllipse(brush, null, new Point(80 * scaleX, 90 * scaleY), 20 * scaleX, 20 * scaleY);
                    break;
                case "stem":
                    context.DrawRectangle(brush, null, new Rect(95 * scaleX, 130 * scaleY, 10 * scaleX, 50 * scaleY));
                    break;
                case "leaf1":
                    context.DrawEllipse(brush, null, new Point(110 * scaleX, 140 * scaleY), 12 * scaleX, 12 * scaleY);
                    break;
                case "leaf2":
                    context.DrawEllipse(brush, null, new Point(85 * scaleX, 150 * scaleY), 12 * scaleX, 12 * scaleY);
                    break;
            }
        }
    }

    protected override void DrawOutlines(DrawingContext context, double scaleX, double scaleY)
    {
        context.DrawEllipse(null, OutlinePen, new Point(100 * scaleX, 70 * scaleY), 20 * scaleX, 20 * scaleY);
        context.DrawEllipse(null, OutlinePen, new Point(120 * scaleX, 90 * scaleY), 20 * scaleX, 20 * scaleY);
        context.DrawEllipse(null, OutlinePen, new Point(100 * scaleX, 110 * scaleY), 20 * scaleX, 20 * scaleY);
        context.DrawEllipse(null, OutlinePen, new Point(80 * scaleX, 90 * scaleY), 20 * scaleX, 20 * scaleY);

        context.DrawRectangle(null, OutlinePen, new Rect(95 * scaleX, 130 * scaleY, 10 * scaleX, 50 * scaleY));
        context.DrawEllipse(null, OutlinePen, new Point(110 * scaleX, 140 * scaleY), 12 * scaleX, 12 * scaleY);
        context.DrawEllipse(null, OutlinePen, new Point(85 * scaleX, 150 * scaleY), 12 * scaleX, 12 * scaleY);
    }

    public override void DrawReferenceOutlines(DrawingContext context)
    {
        context.DrawEllipse(null, OutlinePen, new Point(100, 70), 20, 20);
        context.DrawEllipse(null, OutlinePen, new Point(120, 90), 20, 20);
        context.DrawEllipse(null, OutlinePen, new Point(100, 110), 20, 20);
        context.DrawEllipse(null, OutlinePen, new Point(80, 90), 20, 20);

        context.DrawRectangle(null, OutlinePen, new Rect(95, 130, 10, 50));
        context.DrawEllipse(null, OutlinePen, new Point(110, 140), 12, 12);
        context.DrawEllipse(null, OutlinePen, new Point(85, 150), 12, 12);
    }

    protected override string? HitTestCore(Point normalizedPoint)
    {
        var x = normalizedPoint.X;
        var y = normalizedPoint.Y;

        if (IsWithinCircle(x, y, 100, 70, 20))
        {
            return "petal_top";
        }

        if (IsWithinCircle(x, y, 120, 90, 20))
        {
            return "petal_right";
        }

        if (IsWithinCircle(x, y, 100, 110, 20))
        {
            return "petal_bottom";
        }

        if (IsWithinCircle(x, y, 80, 90, 20))
        {
            return "petal_left";
        }

        if (new Rect(95, 130, 10, 50).Contains(normalizedPoint))
        {
            return "stem";
        }

        if (IsWithinCircle(x, y, 110, 140, 12))
        {
            return "leaf1";
        }

        if (IsWithinCircle(x, y, 85, 150, 12))
        {
            return "leaf2";
        }

        return null;
    }

    private static bool IsWithinCircle(double x, double y, double centerX, double centerY, double radius)
    {
        var dx = x - centerX;
        var dy = y - centerY;
        return dx * dx + dy * dy <= radius * radius;
    }

    private static SolidColorBrush CreateBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
