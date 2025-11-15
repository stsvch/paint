using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace WpfClient.Drawings;

public sealed class HumanDrawing : DrawingDefinition
{
    public override string Key => "human";

    public override string DisplayName => "Человечек";

    public override void DrawReference(DrawingContext context)
    {
        context.DrawRectangle(Brushes.White, null, new Rect(new Size(BaseSize.Width, BaseSize.Height)));

        var blue = CreateBrush(Color.FromRgb(0, 0, 255));
        var yellow = CreateBrush(Color.FromRgb(255, 255, 0));
        var red = CreateBrush(Color.FromRgb(255, 0, 0));
        var green = CreateBrush(Color.FromRgb(0, 255, 0));

        // body
        context.DrawRectangle(blue, null, new Rect(80, 100, 40, 60));
        // head
        context.DrawEllipse(yellow, null, new Point(100, 80), 20, 20);
        // arms
        context.DrawRectangle(red, null, new Rect(60, 110, 20, 40));
        context.DrawRectangle(red, null, new Rect(120, 110, 20, 40));
        // legs
        context.DrawRectangle(green, null, new Rect(85, 160, 15, 40));
        context.DrawRectangle(green, null, new Rect(100, 160, 15, 40));

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
                case "head":
                    context.DrawEllipse(brush, null, new Point(100 * scaleX, 80 * scaleY), 20 * scaleX, 20 * scaleY);
                    break;
                case "body":
                    context.DrawRectangle(brush, null, new Rect(80 * scaleX, 100 * scaleY, 40 * scaleX, 60 * scaleY));
                    break;
                case "left_arm":
                    context.DrawRectangle(brush, null, new Rect(60 * scaleX, 110 * scaleY, 20 * scaleX, 40 * scaleY));
                    break;
                case "right_arm":
                    context.DrawRectangle(brush, null, new Rect(120 * scaleX, 110 * scaleY, 20 * scaleX, 40 * scaleY));
                    break;
                case "left_leg":
                    context.DrawRectangle(brush, null, new Rect(85 * scaleX, 160 * scaleY, 15 * scaleX, 40 * scaleY));
                    break;
                case "right_leg":
                    context.DrawRectangle(brush, null, new Rect(100 * scaleX, 160 * scaleY, 15 * scaleX, 40 * scaleY));
                    break;
            }
        }
    }

    protected override void DrawOutlines(DrawingContext context, double scaleX, double scaleY)
    {
        context.DrawRectangle(null, OutlinePen, new Rect(80 * scaleX, 100 * scaleY, 40 * scaleX, 60 * scaleY));
        context.DrawEllipse(null, OutlinePen, new Point(100 * scaleX, 80 * scaleY), 20 * scaleX, 20 * scaleY);
        context.DrawRectangle(null, OutlinePen, new Rect(60 * scaleX, 110 * scaleY, 20 * scaleX, 40 * scaleY));
        context.DrawRectangle(null, OutlinePen, new Rect(120 * scaleX, 110 * scaleY, 20 * scaleX, 40 * scaleY));
        context.DrawRectangle(null, OutlinePen, new Rect(85 * scaleX, 160 * scaleY, 15 * scaleX, 40 * scaleY));
        context.DrawRectangle(null, OutlinePen, new Rect(100 * scaleX, 160 * scaleY, 15 * scaleX, 40 * scaleY));

        context.DrawEllipse(OutlineBrush, null, new Point(95 * scaleX, 75 * scaleY), 3 * scaleX, 3 * scaleY);
        context.DrawEllipse(OutlineBrush, null, new Point(105 * scaleX, 75 * scaleY), 3 * scaleX, 3 * scaleY);
    }

    public override void DrawReferenceOutlines(DrawingContext context)
    {
        context.DrawRectangle(null, OutlinePen, new Rect(80, 100, 40, 60));
        context.DrawEllipse(null, OutlinePen, new Point(100, 80), 20, 20);
        context.DrawRectangle(null, OutlinePen, new Rect(60, 110, 20, 40));
        context.DrawRectangle(null, OutlinePen, new Rect(120, 110, 20, 40));
        context.DrawRectangle(null, OutlinePen, new Rect(85, 160, 15, 40));
        context.DrawRectangle(null, OutlinePen, new Rect(100, 160, 15, 40));

        context.DrawEllipse(OutlineBrush, null, new Point(95, 75), 3, 3);
        context.DrawEllipse(OutlineBrush, null, new Point(105, 75), 3, 3);
    }

    protected override string? HitTestCore(Point normalizedPoint)
    {
        var x = normalizedPoint.X;
        var y = normalizedPoint.Y;

        var headDist = (x - 100) * (x - 100) + (y - 80) * (y - 80);
        if (headDist <= 20 * 20)
        {
            return "head";
        }

        if (new Rect(80, 100, 40, 60).Contains(normalizedPoint))
        {
            return "body";
        }

        if (new Rect(60, 110, 20, 40).Contains(normalizedPoint))
        {
            return "left_arm";
        }

        if (new Rect(120, 110, 20, 40).Contains(normalizedPoint))
        {
            return "right_arm";
        }

        if (new Rect(85, 160, 15, 40).Contains(normalizedPoint))
        {
            return "left_leg";
        }

        if (new Rect(100, 160, 15, 40).Contains(normalizedPoint))
        {
            return "right_leg";
        }

        return null;
    }

    private static SolidColorBrush CreateBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
