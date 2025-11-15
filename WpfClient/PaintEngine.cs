using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
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
            new FlowerDrawing(),
        };

        _definitions = definitions;
        _drawings = definitions.ToDictionary(d => d.Key, d => d);
        Drawing = definitions.First();
    }

    public IReadOnlyList<DrawingDefinition> AvailablePictures => _definitions;

    public DrawingDefinition Drawing { get; private set; }

    public IDictionary<string, Color> FilledFigures { get; } = new Dictionary<string, Color>();

    public ImageSource CreateReferenceImage()
    {
        var group = new DrawingGroup();
        using (var context = group.Open())
        {
            Drawing.DrawReference(context);
        }

        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }

    public ImageSource CreateCanvasImage()
    {
        var group = new DrawingGroup();
        using (var context = group.Open())
        {
            Drawing.DrawCanvas(context, FilledFigures);
        }

        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }

    public string? HitTest(double canvasX, double canvasY)
    {
        return Drawing.HitTest(new System.Windows.Point(canvasX, canvasY));
    }

    public void FillFigure(string figureName, Color color)
    {
        FilledFigures[figureName] = color;
    }

    public bool ClearFigure(string figureName)
    {
        return FilledFigures.Remove(figureName);
    }

    public void ClearAll()
    {
        FilledFigures.Clear();
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
}
