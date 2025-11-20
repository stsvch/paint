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
            new FlowerDrawing(),
        };

        _definitions = definitions;
        _drawings = definitions.ToDictionary(d => d.Key, d => d);
        Drawing = definitions.First();
    }

    public IReadOnlyList<DrawingDefinition> AvailablePictures => _definitions;

    public DrawingDefinition Drawing { get; private set; }

    // Храним заливки как маски областей (список точек для каждой заливки)
    private readonly Dictionary<string, (Color color, HashSet<Point> filledPoints)> _filledRegions = new();

    public IReadOnlyDictionary<string, Color> FilledFigures
    {
        get
        {
            // Преобразуем внутреннее представление в старое для совместимости
            var result = new Dictionary<string, Color>();
            foreach (var (name, (color, _)) in _filledRegions)
            {
                result[name] = color;
            }
            return result;
        }
    }

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
            // Рисуем белый фон
            context.DrawRectangle(Brushes.White, null, new Rect(0, 0, AppConfig.CanvasWidth, AppConfig.CanvasHeight));

            var scaleX = AppConfig.CanvasWidth / Drawing.BaseSize.Width;
            var scaleY = AppConfig.CanvasHeight / Drawing.BaseSize.Height;

            // Рисуем залитые области
            DrawFilledRegions(context, scaleX, scaleY);

           
            Drawing.DrawOutlines(context, scaleX, scaleY);
        }

        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }

    private void DrawFilledRegions(DrawingContext context, double scaleX, double scaleY)
    {
        foreach (var (figureName, (color, points)) in _filledRegions)
        {
            if (points.Count == 0)
            {
                // Если нет точек (старая заливка), используем старый метод
                var oldFilled = new Dictionary<string, Color> { [figureName] = color };
                Drawing.DrawFilledFigures(context, oldFilled, scaleX, scaleY);
            }
            else
            {
                // Рисуем залитые области как прямоугольники для лучшей производительности
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                
                // Группируем точки в прямоугольники для оптимизации
                var sortedPoints = points.OrderBy(p => p.Y).ThenBy(p => p.X).ToList();
                var rectangles = new List<Rect>();
                
                foreach (var point in sortedPoints)
                {
                    rectangles.Add(new Rect(point.X, point.Y, 1, 1));
                }
                
                // Рисуем все прямоугольники за один раз
                var geometry = new GeometryGroup();
                foreach (var rect in rectangles)
                {
                    geometry.Children.Add(new RectangleGeometry(rect));
                }
                geometry.Freeze();
                
                context.DrawGeometry(brush, null, geometry);
            }
        }
    }

    public string? HitTest(double canvasX, double canvasY)
    {
        return Drawing.HitTest(new System.Windows.Point(canvasX, canvasY));
    }

    public void FillFigure(string figureName, Color color)
    {
        // Старый метод для совместимости - заливает всю фигуру
        // Используется при воспроизведении записей
        if (!_filledRegions.ContainsKey(figureName))
        {
            _filledRegions[figureName] = (color, new HashSet<Point>());
        }
        else
        {
            var (_, points) = _filledRegions[figureName];
            _filledRegions[figureName] = (color, points);
        }
    }

    public void FillRegionAtPoint(double canvasX, double canvasY, Color color)
    {
        // Создаем растровое изображение только контуров (без уже залитых областей) для flood fill
        // Это позволяет перезакрашивать уже залитые области
        var outlineBitmap = CreateOutlineBitmapOnly();
        if (outlineBitmap == null)
        {
            return;
        }

        // Выполняем flood fill
        var filledPoints = FloodFill(outlineBitmap, (int)canvasX, (int)canvasY, color);
        
        if (filledPoints.Count > 0)
        {
            // Определяем имя фигуры по точке
            var figureName = Drawing.HitTest(new Point(canvasX, canvasY));
            if (figureName != null)
            {
                // Заменяем существующую заливку новой (для перезакраски)
                // Сначала удаляем старые точки этой фигуры, которые попадают в новую область
                if (_filledRegions.TryGetValue(figureName, out var existing))
                {
                    // Удаляем точки, которые будут перезакрашены
                    existing.filledPoints.ExceptWith(filledPoints);
                    // Добавляем новые точки
                    existing.filledPoints.UnionWith(filledPoints);
                    _filledRegions[figureName] = (color, existing.filledPoints);
                }
                else
                {
                    _filledRegions[figureName] = (color, filledPoints);
                }
            }
        }
    }

    public bool ClearFigure(string figureName)
    {
        return _filledRegions.Remove(figureName);
    }

    public void ClearAll()
    {
        _filledRegions.Clear();
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

    private WriteableBitmap? CreateOutlineBitmapOnly()
    {
        var width = (int)AppConfig.CanvasWidth;
        var height = (int)AppConfig.CanvasHeight;
        var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);

        // Рисуем только контуры на растровом изображении (без уже залитых областей)
        // Это позволяет перезакрашивать уже залитые области
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            // Белый фон
            context.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
            
            var scaleX = AppConfig.CanvasWidth / Drawing.BaseSize.Width;
            var scaleY = AppConfig.CanvasHeight / Drawing.BaseSize.Height;
            
            // Рисуем только контуры (без уже залитых областей)
            Drawing.DrawOutlines(context, scaleX, scaleY);
        }

        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        
        // Копируем в WriteableBitmap
        var pixels = new byte[width * height * 4];
        rtb.CopyPixels(pixels, width * 4, 0);
        bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);

        return bitmap;
    }

    private HashSet<Point> FloodFill(WriteableBitmap bitmap, int startX, int startY, Color fillColor)
    {
        var filledPoints = new HashSet<Point>();
        var width = bitmap.PixelWidth;
        var height = bitmap.PixelHeight;

        if (startX < 0 || startX >= width || startY < 0 || startY >= height)
        {
            return filledPoints;
        }

        // Получаем цвет начальной точки
        var pixels = new byte[width * height * 4];
        bitmap.CopyPixels(pixels, width * 4, 0);
        
        var startIndex = (startY * width + startX) * 4;
        var startR = pixels[startIndex + 2];
        var startG = pixels[startIndex + 1];
        var startB = pixels[startIndex];
        var startA = pixels[startIndex + 3];

        // Определяем, является ли пиксель контуром
        bool IsOutline(int r, int g, int b, int a)
        {
            // Черный контур - останавливаемся на нем
            // Используем более строгий порог для определения контура
            return r < 20 && g < 20 && b < 20 && a > 100;
        }

        // Если начальная точка является контуром, выходим
        if (IsOutline(startR, startG, startB, startA))
        {
            return filledPoints;
        }

        // Запоминаем целевой цвет для сравнения
        var targetR = startR;
        var targetG = startG;
        var targetB = startB;
        var targetA = startA;

        // Определяем, можно ли залить пиксель (не контур)
        bool IsFillable(int r, int g, int b, int a)
        {
            return !IsOutline(r, g, b, a);
        }

        // Определяем, является ли пиксель белым/прозрачным (незалитым)
        bool IsUnfilled(int r, int g, int b, int a)
        {
            return (a == 0) || (r >= 240 && g >= 240 && b >= 240);
        }

        // Определяем, является ли пиксель того же цвета, что и начальная точка
        bool IsSameColorAsStart(int r, int g, int b, int a)
        {
            // Если начальная точка белая/прозрачная, заливаем только белые/прозрачные
            if (IsUnfilled(targetR, targetG, targetB, targetA))
            {
                return IsUnfilled(r, g, b, a);
            }
            
            // Если начальная точка уже залита, заливаем только пиксели того же цвета
            // Используем порог для сравнения цветов (учитываем возможные различия из-за антиалиасинга)
            var colorDiff = Math.Abs(r - targetR) + Math.Abs(g - targetG) + Math.Abs(b - targetB);
            return colorDiff < 30 && Math.Abs(a - targetA) < 50;
        }

        // Используем очередь для flood fill
        var queue = new Queue<Point>();
        queue.Enqueue(new Point(startX, startY));
        filledPoints.Add(new Point(startX, startY));

        // Простой flood fill - заливаем только область того же цвета, что и начальная точка, до контуров
        // Это предотвращает заливку всего круга, если часть уже закрашена
        while (queue.Count > 0)
        {
            var point = queue.Dequeue();
            var x = (int)point.X;
            var y = (int)point.Y;

            // Проверяем соседние пиксели (4-связность)
            var neighbors = new[]
            {
                new Point(x - 1, y),
                new Point(x + 1, y),
                new Point(x, y - 1),
                new Point(x, y + 1)
            };

            foreach (var neighbor in neighbors)
            {
                var nx = (int)neighbor.X;
                var ny = (int)neighbor.Y;

                if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                {
                    continue;
                }

                if (filledPoints.Contains(neighbor))
                {
                    continue;
                }

                var index = (ny * width + nx) * 4;
                var r = pixels[index + 2];
                var g = pixels[index + 1];
                var b = pixels[index];
                var a = pixels[index + 3];

                // Если пиксель можно залить (не контур и того же цвета, что и начальная точка)
                if (IsFillable(r, g, b, a) && IsSameColorAsStart(r, g, b, a))
                {
                    filledPoints.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }

        return filledPoints;
    }

}
