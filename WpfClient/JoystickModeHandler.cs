namespace WpfClient;

public enum JoystickMode
{
    Absolute,
    Centered
}

public sealed class JoystickModeHandler
{
    private readonly double _centerX;
    private readonly double _centerY;

    public JoystickModeHandler(double centerX, double centerY)
    {
        _centerX = centerX;
        _centerY = centerY;
        Mode = JoystickMode.Absolute;
    }

    public JoystickMode Mode { get; set; }

    public double NormalizeX(int rawValue, double currentX)
    {
        return Mode switch
        {
            JoystickMode.Absolute => NormalizeAbsoluteX(rawValue, currentX),
            JoystickMode.Centered => NormalizeCenteredX(rawValue),
            _ => currentX
        };
    }

    public double NormalizeY(int rawValue, double currentY)
    {
        return Mode switch
        {
            JoystickMode.Absolute => NormalizeAbsoluteY(rawValue, currentY),
            JoystickMode.Centered => NormalizeCenteredY(rawValue),
            _ => currentY
        };
    }

    public bool IsInDeadZone(int rawX, int rawY)
    {
        return Math.Abs(rawX - AppConfig.JoyXCenter) < AppConfig.JoyDeadZone &&
               Math.Abs(rawY - AppConfig.JoyYCenter) < AppConfig.JoyDeadZone;
    }

    public (double X, double Y) GetCenter() => (_centerX, _centerY);

    private double NormalizeAbsoluteX(int rawValue, double current)
    {
        if (Math.Abs(rawValue - AppConfig.JoyXCenter) < AppConfig.JoyDeadZone)
        {
            return current;
        }

        if (rawValue < AppConfig.JoyXCenter)
        {
            var delta = AppConfig.JoyXCenter - rawValue;
            var speed = Math.Min(delta / AppConfig.JoySpeedDivider, AppConfig.JoyMaxSpeed);
            return Math.Max(0, current - speed);
        }

        var deltaRight = rawValue - AppConfig.JoyXCenter;
        var speedRight = Math.Min(deltaRight / AppConfig.JoySpeedDivider, AppConfig.JoyMaxSpeed);
        // Используем логические координаты холста (0-600)
        return Math.Min(AppConfig.CanvasWidth - 1, current + speedRight);
    }

    private double NormalizeAbsoluteY(int rawValue, double current)
    {
        if (Math.Abs(rawValue - AppConfig.JoyYCenter) < AppConfig.JoyDeadZone)
        {
            return current;
        }

        if (rawValue < AppConfig.JoyYCenter)
        {
            var delta = AppConfig.JoyYCenter - rawValue;
            var speed = Math.Min(delta / AppConfig.JoySpeedDivider, AppConfig.JoyMaxSpeed);
            // Используем логические координаты холста (0-600)
            return Math.Min(AppConfig.CanvasHeight - 1, current + speed);
        }

        var deltaDown = rawValue - AppConfig.JoyYCenter;
        var speedDown = Math.Min(deltaDown / AppConfig.JoySpeedDivider, AppConfig.JoyMaxSpeed);
        return Math.Max(0, current - speedDown);
    }

    private double NormalizeCenteredX(int rawValue)
    {
        if (Math.Abs(rawValue - AppConfig.JoyXCenter) < AppConfig.JoyDeadZone)
        {
            return _centerX;
        }

        var offset = rawValue - AppConfig.JoyXCenter;
        var maxOffset = AppConfig.JoyXMax - AppConfig.JoyXCenter;
        var normalized = (double)offset / maxOffset;
        normalized = Math.Clamp(normalized, -1.0, 1.0);

        const double speedFactor = 1;
        // Используем логические координаты холста (0-600)
        var maxScreenOffset = (AppConfig.CanvasWidth / 2.0) * speedFactor;
        var cursorOffset = normalized * maxScreenOffset;
        var newX = _centerX + cursorOffset;

        return Math.Clamp(newX, 0, AppConfig.CanvasWidth - 1);
    }

    private double NormalizeCenteredY(int rawValue)
    {
        if (Math.Abs(rawValue - AppConfig.JoyYCenter) < AppConfig.JoyDeadZone)
        {
            return _centerY;
        }

        var offset = rawValue - AppConfig.JoyYCenter;
        var maxOffset = AppConfig.JoyYMax - AppConfig.JoyYCenter;
        var normalized = (double)offset / maxOffset;
        normalized = Math.Clamp(normalized, -1.0, 1.0);

        const double speedFactor = 1;
        // Используем логические координаты холста (0-600)
        var maxScreenOffset = (AppConfig.CanvasHeight / 2.0) * speedFactor;
        var cursorOffset = -normalized * maxScreenOffset;
        var newY = _centerY + cursorOffset;

        return Math.Clamp(newY, 0, AppConfig.CanvasHeight - 1);
    }
}
