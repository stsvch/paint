using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace WpfClient;

public sealed class ColorOption
{
    public ColorOption(int index, string name, Color color)
    {
        Index = index;
        Name = name;
        Color = color;
        Brush = new SolidColorBrush(color);
        Brush.Freeze();
    }

    public int Index { get; }

    public string Name { get; }

    public Color Color { get; }

    public SolidColorBrush Brush { get; }
}

public static class ColorPalette
{
    public static IReadOnlyList<ColorOption> Default { get; } = CreateDefaultPalette();

    private static IReadOnlyList<ColorOption> CreateDefaultPalette()
    {
        var colors = new (string Name, Color Color)[]
        {
            ("Чёрный", Color.FromRgb(0, 0, 0)),
            ("Красный", Color.FromRgb(255, 0, 0)),
            ("Зелёный", Color.FromRgb(0, 255, 0)),
            ("Синий", Color.FromRgb(0, 0, 255)),
            ("Жёлтый", Color.FromRgb(255, 255, 0)),
            ("Бирюзовый", Color.FromRgb(0, 255, 255)),
            ("Пурпурный", Color.FromRgb(255, 0, 255)),
            ("Оранжевый", Color.FromRgb(255, 165, 0)),
            ("Фиолетовый", Color.FromRgb(128, 0, 128)),
            ("Коричневый", Color.FromRgb(165, 42, 42)),
            ("Розовый", Color.FromRgb(255, 192, 203)),
            ("Белый", Color.FromRgb(255, 255, 255)),
        };

        return colors
            .Select((entry, index) => new ColorOption(index, entry.Name, entry.Color))
            .ToArray();
    }
}
