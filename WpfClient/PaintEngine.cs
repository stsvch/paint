using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfClient.Drawings;

namespace WpfClient;

public sealed class PaintEngine
{
    private readonly IReadOnlyDictionary<string, DrawingDefinition> _drawings;
    private readonly DrawingDefinition[] _definitions;

    public PaintEngine()
    {
        var definitions = new DrawingDefinition[]
        {
            new HumanDrawing(),
        };

        _definitions = definitions;
        _drawings = definitions.ToDictionary(d => d.Key, d => d);
        Drawing = definitions.First();
    }

    public IReadOnlyList<DrawingDefinition> AvailablePictures => _definitions;

    public DrawingDefinition Drawing { get; private set; }

    // Храним заливки как выбранный цвет для каждой фигуры
    private readonly Dictionary<string, Color> _filledFigures = new();

    public IReadOnlyDictionary<string, Color> FilledFigures
    {
        get
        {
            return new Dictionary<string, Color>(_filledFigures);
        }
    }

    public bool IsFilledCorrectly()
    {
        var reference = Drawing.ReferenceColors;

        if (reference.Count != _filledFigures.Count)
        {
            return false;
        }

        foreach (var (name, referenceColor) in reference)
        {
            if (!_filledFigures.TryGetValue(name, out var filledColor))
            {
                return false;
            }

            if (!ColorsAreEqual(referenceColor, filledColor))
            {
                return false;
            }
        }

        return true;
    }

    public ImageSource CreateReferenceImage()
    {
        // Используем RenderTargetBitmap для более быстрого рендеринга
        var width = (int)AppConfig.ReferenceSize;
        var height = (int)AppConfig.ReferenceSize;
        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            Drawing.DrawReference(context);
        }
        
        rtb.Render(visual);
        rtb.Freeze();
        return rtb;
    }

    public ImageSource CreateCanvasImage()
    {
        // Используем RenderTargetBitmap для более быстрого рендеринга
        var width = (int)AppConfig.CanvasWidth;
        var height = (int)AppConfig.CanvasHeight;
        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            // Рисуем белый фон
            context.DrawRectangle(Brushes.White, null, new Rect(0, 0, AppConfig.CanvasWidth, AppConfig.CanvasHeight));

            var scaleX = AppConfig.CanvasWidth / Drawing.BaseSize.Width;
            var scaleY = AppConfig.CanvasHeight / Drawing.BaseSize.Height;

            // Рисуем залитые области
            DrawFilledRegions(context, scaleX, scaleY);

            // Рисуем контуры
            Drawing.DrawOutlines(context, scaleX, scaleY);
        }
        
        rtb.Render(visual);
        rtb.Freeze();
        return rtb;
    }

    private void DrawFilledRegions(DrawingContext context, double scaleX, double scaleY)
    {
        Drawing.DrawFilledFigures(context, _filledFigures, scaleX, scaleY);
    }

    public string? HitTest(double canvasX, double canvasY)
    {
        return Drawing.HitTest(new System.Windows.Point(canvasX, canvasY));
    }

    public void FillFigure(string figureName, Color color)
    {
        _filledFigures[figureName] = color;
    }

    public void FillRegionAtPoint(double canvasX, double canvasY, Color color)
    {
        var figureName = Drawing.HitTest(new Point(canvasX, canvasY));
        if (figureName == null)
        {
            return;
        }

        FillFigure(figureName, color);
    }

    public bool ClearFigure(string figureName)
    {
        return _filledFigures.Remove(figureName);
    }

    public void ClearAll()
    {
        _filledFigures.Clear();
    }

    public void NextPicture()
    {
        var index = Array.IndexOf(_definitions, Drawing);
        index = (index + 1) % _definitions.Length;
        SetPicture(_definitions[index]);
    }

    public void SetPicture(string key)
    {
        if (_drawings.TryGetValue(key, out var drawing))
        {
            SetPicture(drawing);
        }
    }

    private void SetPicture(DrawingDefinition drawing)
    {
        Drawing = drawing;
        ClearAll();
    }

    private static bool ColorsAreEqual(Color left, Color right)
    {
        return left.R == right.R && left.G == right.G && left.B == right.B;
    }

}
