using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace WpfClient;

public sealed class PaintViewModel : INotifyPropertyChanged
{
    private readonly ObservableCollection<string> _filledFigures = new();
    private readonly ReadOnlyObservableCollection<string> _readonlyFilledFigures;

    private ImageSource? _canvasImage;
    private ImageSource? _referenceImage;
    private int _selectedColorIndex;
    private string _selectedColorName = string.Empty;
    private string _pictureKey = string.Empty;
    private string _pictureDisplayName = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isSerialConnected;
    private double _cursorX = AppConfig.CanvasLeft + AppConfig.CanvasWidth / 2.0;
    private double _cursorY = AppConfig.CanvasTop + AppConfig.CanvasHeight / 2.0;
    private double _cursorLeft;
    private double _cursorTop;
    private double _cursorInnerLeft;
    private double _cursorInnerTop;

    public PaintViewModel()
    {
        _readonlyFilledFigures = new ReadOnlyObservableCollection<string>(_filledFigures);
        Palette = new ReadOnlyCollection<ColorOption>(ColorPalette.Default.ToList());
        CursorLeft = _cursorX - CursorRadius;
        CursorTop = _cursorY - CursorRadius;
        CursorInnerLeft = _cursorX - 2;
        CursorInnerTop = _cursorY - 2;
        SelectedColorIndex = 0;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<ColorOption> Palette { get; }

    public ReadOnlyObservableCollection<string> FilledFigures => _readonlyFilledFigures;

    public int FilledCount => _filledFigures.Count;

    public ImageSource? CanvasImage
    {
        get => _canvasImage;
        set => SetField(ref _canvasImage, value);
    }

    public ImageSource? ReferenceImage
    {
        get => _referenceImage;
        set => SetField(ref _referenceImage, value);
    }

    public int SelectedColorIndex
    {
        get => _selectedColorIndex;
        set
        {
            if (SetField(ref _selectedColorIndex, value))
            {
                var option = Palette.ElementAtOrDefault(_selectedColorIndex) ?? Palette.First();
                SelectedColorName = option.Name;
                SelectedColor = option.Color;
                SelectedColorBrush = option.Brush;
            }
        }
    }

    private SolidColorBrush _selectedColorBrush = Brushes.Black;

    public SolidColorBrush SelectedColorBrush
    {
        get => _selectedColorBrush;
        private set => SetField(ref _selectedColorBrush, value);
    }

    private Color _selectedColor = Colors.Black;

    public Color SelectedColor
    {
        get => _selectedColor;
        private set
        {
            if (SetField(ref _selectedColor, value))
            {
                OnPropertyChanged(nameof(SelectedColorHex));
            }
        }
    }

    public string SelectedColorName
    {
        get => _selectedColorName;
        private set => SetField(ref _selectedColorName, value);
    }

    public string PictureKey
    {
        get => _pictureKey;
        private set => SetField(ref _pictureKey, value);
    }

    public string PictureDisplayName
    {
        get => _pictureDisplayName;
        private set => SetField(ref _pictureDisplayName, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (SetField(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatusMessage));
            }
        }
    }

    public bool IsSerialConnected
    {
        get => _isSerialConnected;
        set
        {
            if (SetField(ref _isSerialConnected, value))
            {
                OnPropertyChanged(nameof(ConnectionStatus));
            }
        }
    }

    public double CursorX
    {
        get => _cursorX;
        private set
        {
            if (SetField(ref _cursorX, value))
            {
                CursorLeft = value - CursorRadius;
                CursorInnerLeft = value - 2;
            }
        }
    }

    public double CursorY
    {
        get => _cursorY;
        private set
        {
            if (SetField(ref _cursorY, value))
            {
                CursorTop = value - CursorRadius;
                CursorInnerTop = value - 2;
            }
        }
    }

    public double CursorLeft
    {
        get => _cursorLeft;
        private set => SetField(ref _cursorLeft, value);
    }

    public double CursorTop
    {
        get => _cursorTop;
        private set => SetField(ref _cursorTop, value);
    }

    public double CursorRadius { get; } = 6.0;

    public double CursorDiameter => CursorRadius * 2;

    public double CursorInnerLeft
    {
        get => _cursorInnerLeft;
        private set => SetField(ref _cursorInnerLeft, value);
    }

    public double CursorInnerTop
    {
        get => _cursorInnerTop;
        private set => SetField(ref _cursorInnerTop, value);
    }

    public void UpdateCursor(double x, double y)
    {
        CursorX = x;
        CursorY = y;
        OnPropertyChanged(nameof(CursorPosition));
    }

    public string CursorPosition => $"{(int)CursorX}, {(int)CursorY}";

    public string SelectedColorHex => $"#{SelectedColor.R:X2}{SelectedColor.G:X2}{SelectedColor.B:X2}";

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public string ConnectionStatus => IsSerialConnected ? "Устройство подключено" : "Ожидание устройства";

    public void UpdatePicture(PaintEngine engine)
    {
        PictureKey = engine.Drawing.Key;
        PictureDisplayName = engine.Drawing.DisplayName;
    }

    public void UpdateImages(PaintEngine engine)
    {
        CanvasImage = engine.CreateCanvasImage();
        ReferenceImage = engine.CreateReferenceImage();
        UpdateFilledFigures(engine.FilledFigures);
    }

    public void UpdateFilledFigures(IReadOnlyDictionary<string, Color> filledFigures)
    {
        _filledFigures.Clear();
        foreach (var (name, color) in filledFigures)
        {
            _filledFigures.Add($"{name} — #{color.R:X2}{color.G:X2}{color.B:X2}");
        }

        OnPropertyChanged(nameof(FilledCount));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
