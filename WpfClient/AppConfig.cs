namespace WpfClient;

public static class AppConfig
{
    // Используем double для параметров, к которым напрямую привязывается XAML.
    // Это избавляет от ошибок преобразования типов при установке Width/Height
    // во время инициализации окна.
    public const double ScreenWidth = 1000.0;
    public const double ScreenHeight = 700.0;
    public const double CanvasWidth = 600.0;
    public const double CanvasHeight = 600.0;
    public const double ReferenceSize = 200.0;
    public const int ColorPanelColumns = 2;
    public const double ColorCellSize = 56.0;
    public const double ColorCellSpacing = 12.0;
    public const double ColorPanelX = ScreenWidth - 264;
    public const double ColorPanelY = 302;

    public const int BaudRate = 115200;

    public const int JoyXMin = 0;
    public const int JoyXMax = 4095;
    public const int JoyYMin = 0;
    public const int JoyYMax = 4095;
    public const int JoyXCenter = 2048;
    public const int JoyYCenter = 2048;
    public const int JoyDeadZone = 100;
    public const double JoySpeedDivider = 100.0;
    public const double JoyMaxSpeed = 10.0;

    public static readonly double CanvasLeft = (ScreenWidth - CanvasWidth) / 2;
    public static readonly double CanvasTop = (ScreenHeight - CanvasHeight) / 2;
}
