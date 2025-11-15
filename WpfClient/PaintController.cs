using System;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace WpfClient;

public sealed class PaintController : IDisposable
{
    private static readonly Regex JoystickRegex = new("^X:(\\d+),Y:(\\d+),B:(\\d+)", RegexOptions.Compiled);

    private readonly PaintViewModel _viewModel;
    private readonly PaintEngine _engine;
    private readonly Dispatcher _dispatcher;
    private readonly CancellationTokenSource _cancellation = new();

    private SerialPort? _serialPort;
    private Task? _serialTask;

    public PaintController(PaintViewModel viewModel, PaintEngine engine)
    {
        _viewModel = viewModel;
        _engine = engine;
        _dispatcher = Application.Current.Dispatcher;
    }

    public void Run()
    {
        _engine.SetPicture(_engine.Drawing.Key);
        _viewModel.UpdatePicture(_engine);
        _viewModel.UpdateImages(_engine);
        ResetCursor();
        _viewModel.StatusMessage = "Подключите контроллер или используйте режим эмуляции.";

        _serialTask = Task.Run(OpenSerialLoopAsync, _cancellation.Token);
    }

    private async Task OpenSerialLoopAsync()
    {
        while (!_cancellation.IsCancellationRequested)
        {
            try
            {
                _serialPort = TryOpenSerialPort();
                if (_serialPort is null)
                {
                    _dispatcher.Invoke(() =>
                    {
                        _viewModel.IsSerialConnected = false;
                        _viewModel.StatusMessage = "Поиск доступного порта...";
                    });
                    await Task.Delay(TimeSpan.FromSeconds(2), _cancellation.Token);
                    continue;
                }

                _dispatcher.Invoke(() =>
                {
                    _viewModel.IsSerialConnected = true;
                    _viewModel.StatusMessage = $"✓ Подключено к {_serialPort.PortName} ({_serialPort.BaudRate} бод)";
                });

                await ReadSerialAsync(_serialPort, _cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _dispatcher.Invoke(() =>
                {
                    _viewModel.StatusMessage = $"Ошибка соединения: {ex.Message}";
                    _viewModel.IsSerialConnected = false;
                });
            }
            finally
            {
                if (_serialPort is not null)
                {
                    try
                    {
                        if (_serialPort.IsOpen)
                        {
                            _serialPort.Close();
                        }
                    }
                    catch
                    {
                        // ignored
                    }
                    finally
                    {
                        _serialPort.Dispose();
                        _serialPort = null;
                    }

                    _dispatcher.Invoke(() =>
                    {
                        if (!_cancellation.IsCancellationRequested)
                        {
                            _viewModel.IsSerialConnected = false;
                            _viewModel.StatusMessage = "Ожидание устройства...";
                        }
                    });
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(2), _cancellation.Token);
        }
    }

    private static SerialPort? TryOpenSerialPort()
    {
        var candidateNames = SerialPort.GetPortNames().ToList();
        var preferred = Enumerable.Range(3, 8).Select(n => $"COM{n}");
        foreach (var name in preferred)
        {
            if (!candidateNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                candidateNames.Add(name);
            }
        }

        foreach (var portName in candidateNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var port = new SerialPort(portName, AppConfig.BaudRate)
                {
                    ReadTimeout = 500,
                    WriteTimeout = 500,
                    NewLine = "\n"
                };
                port.Open();
                if (port.IsOpen)
                {
                    return port;
                }
                port.Dispose();
            }
            catch
            {
                // ignore and try next
            }
        }

        return null;
    }

    private Task ReadSerialAsync(SerialPort port, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            while (!cancellationToken.IsCancellationRequested && port.IsOpen)
            {
                string? line = null;
                try
                {
                    line = port.ReadLine();
                }
                catch (TimeoutException)
                {
                    continue;
                }
                catch (InvalidOperationException)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var message = line.Trim();
                _dispatcher.Invoke(() => ProcessSerialMessage(message));
            }
        }, cancellationToken);
    }

    private void ProcessSerialMessage(string message)
    {
        if (message.StartsWith("BTN:", StringComparison.OrdinalIgnoreCase))
        {
            HandleButton(message[4..].Trim().ToUpperInvariant());
            return;
        }

        if (message.StartsWith("X:", StringComparison.OrdinalIgnoreCase) && message.Contains('Y'))
        {
            var match = JoystickRegex.Match(message);
            if (match.Success)
            {
                var rawX = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                var rawY = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);

                var normalizedX = NormalizeJoystickX(rawX);
                var normalizedY = NormalizeJoystickY(rawY);

                _viewModel.UpdateCursor(normalizedX, normalizedY);
            }
        }
    }

    private void HandleButton(string button)
    {
        switch (button)
        {
            case "A":
            case "D":
                HandleColorSelection();
                break;
            case "B":
                HandleFillRequest();
                break;
            case "C":
                HandleClearFigure();
                break;
            case "E":
                HandleNextPicture();
                break;
            case "F":
                HandleClearAll();
                break;
        }
    }

    private void HandleColorSelection()
    {
        var index = GetColorIndexAt(_viewModel.CursorX, _viewModel.CursorY);
        if (index is null)
        {
            _viewModel.StatusMessage = "Наведите курсор на палитру цветов";
            return;
        }

        _viewModel.SelectedColorIndex = index.Value;
        _viewModel.StatusMessage = $"Выбран цвет #{index.Value}: {_viewModel.SelectedColorName}";
    }

    private void HandleFillRequest()
    {
        if (!TryGetCanvasPoint(out var canvasPoint))
        {
            _viewModel.StatusMessage = "Курсор вне области рисунка";
            return;
        }

        var figure = _engine.HitTest(canvasPoint.X, canvasPoint.Y);
        if (figure is null)
        {
            _viewModel.StatusMessage = "Фигура не найдена. Переместите курсор";
            return;
        }

        _engine.FillFigure(figure, _viewModel.SelectedColor);
        _viewModel.UpdateImages(_engine);
        _viewModel.StatusMessage = $"✓ Залита фигура: {figure}";
    }

    private void HandleClearFigure()
    {
        if (!TryGetCanvasPoint(out var canvasPoint))
        {
            _viewModel.StatusMessage = "Курсор вне области рисунка";
            return;
        }

        var figure = _engine.HitTest(canvasPoint.X, canvasPoint.Y);
        if (figure is null)
        {
            _viewModel.StatusMessage = "Фигура не найдена";
            return;
        }

        if (_engine.ClearFigure(figure))
        {
            _viewModel.UpdateImages(_engine);
            _viewModel.StatusMessage = $"✓ Очищена фигура: {figure}";
        }
        else
        {
            _viewModel.StatusMessage = "Фигура уже пустая";
        }
    }

    private void HandleNextPicture()
    {
        _engine.NextPicture();
        _viewModel.UpdatePicture(_engine);
        _viewModel.UpdateImages(_engine);
        _viewModel.SelectedColorIndex = 0;
        ResetCursor();
        _viewModel.StatusMessage = "✓ Новая картинка готова";
    }

    private void HandleClearAll()
    {
        _engine.ClearAll();
        _viewModel.UpdateImages(_engine);
        _viewModel.StatusMessage = "✓ Canvas очищен";
    }

    private void ResetCursor()
    {
        var centerX = AppConfig.CanvasLeft + AppConfig.CanvasWidth / 2.0;
        var centerY = AppConfig.CanvasTop + AppConfig.CanvasHeight / 2.0;
        _viewModel.UpdateCursor(centerX, centerY);
    }

    private bool TryGetCanvasPoint(out Point canvasPoint)
    {
        var canvasLeft = AppConfig.CanvasLeft;
        var canvasTop = AppConfig.CanvasTop;
        var x = _viewModel.CursorX;
        var y = _viewModel.CursorY;

        if (x < canvasLeft || x > canvasLeft + AppConfig.CanvasWidth ||
            y < canvasTop || y > canvasTop + AppConfig.CanvasHeight)
        {
            canvasPoint = default;
            return false;
        }

        canvasPoint = new Point(x - canvasLeft, y - canvasTop);
        return true;
    }

    private double NormalizeJoystickX(int rawValue)
    {
        var current = _viewModel.CursorX;
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
        return Math.Min(AppConfig.ScreenWidth - 1, current + speedRight);
    }

    private double NormalizeJoystickY(int rawValue)
    {
        var current = _viewModel.CursorY;
        if (Math.Abs(rawValue - AppConfig.JoyYCenter) < AppConfig.JoyDeadZone)
        {
            return current;
        }

        if (rawValue < AppConfig.JoyYCenter)
        {
            var delta = AppConfig.JoyYCenter - rawValue;
            var speed = Math.Min(delta / AppConfig.JoySpeedDivider, AppConfig.JoyMaxSpeed);
            return Math.Min(AppConfig.ScreenHeight - 1, current + speed);
        }

        var deltaDown = rawValue - AppConfig.JoyYCenter;
        var speedDown = Math.Min(deltaDown / AppConfig.JoySpeedDivider, AppConfig.JoyMaxSpeed);
        return Math.Max(0, current - speedDown);
    }

    private static int? GetColorIndexAt(double x, double y)
    {
        const int panelStartY = 250;
        const int colorSize = 40;
        const int colorSpacing = 10;

        if (x < AppConfig.ColorPanelX || x > AppConfig.ScreenWidth - 20)
        {
            return null;
        }

        for (var i = 0; i < ColorPalette.Default.Count; i++)
        {
            var colorY = panelStartY + i * (colorSize + colorSpacing);
            if (y >= colorY && y <= colorY + colorSize)
            {
                return i;
            }
        }

        return null;
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        try
        {
            _serialTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
            // ignored
        }

        if (_serialPort is not null)
        {
            try
            {
                if (_serialPort.IsOpen)
                {
                    _serialPort.Close();
                }
            }
            catch
            {
                // ignored
            }
            _serialPort.Dispose();
            _serialPort = null;
        }

        _cancellation.Dispose();
    }
}
