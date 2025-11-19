namespace WpfClient;

public static class AppConfig
{
    public const int ScreenWidth = 1000;
    public const int ScreenHeight = 700;
    public const int CanvasWidth = 600;
    public const int CanvasHeight = 600;
    public const int ReferenceSize = 200;
    public const int ColorPanelColumns = 2;
    public const int ColorCellSize = 56;
    public const int ColorCellSpacing = 12;
    public const int ColorPanelX = ScreenWidth - 264;
    public const int ColorPanelY = 302;

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

    public static readonly int CanvasLeft = (ScreenWidth - CanvasWidth) / 2;
    public static readonly int CanvasTop = (ScreenHeight - CanvasHeight) / 2;
}
