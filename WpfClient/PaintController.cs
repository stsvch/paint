using System;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using WpfClient.Models;
using WpfClient.Services;

namespace WpfClient;

public sealed class PaintController : IDisposable
{
    private static readonly Regex JoystickRegex = new("^X:(\\d+),Y:(\\d+),B:(\\d+)", RegexOptions.Compiled);

    private readonly PaintViewModel _viewModel;
    private readonly PaintEngine _engine;
    private readonly Dispatcher _dispatcher;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly ActionRecorder? _recorder;
    private readonly Demo? _playback;
    private readonly JoystickModeHandler _joystickModeHandler;
    private bool _isRecording;
    private bool _isPlaying;
    private string? _previousDrawingKey;

    private SerialPort? _serialPort;
    private Task? _serialTask;

    public PaintController(PaintViewModel viewModel, PaintEngine engine, bool enableDatabase = true)
    {
        _viewModel = viewModel;
        _engine = engine;
        _dispatcher = Application.Current.Dispatcher;

        var centerX = AppConfig.CanvasLeft + AppConfig.CanvasWidth / 2.0;
        var centerY = AppConfig.CanvasTop + AppConfig.CanvasHeight / 2.0;
        _joystickModeHandler = new JoystickModeHandler(centerX, centerY);

        if (enableDatabase)
        {
            try
            {
                _recorder = new ActionRecorder(DatabaseConfig.ConnectionString);
                _playback = new Demo(DatabaseConfig.ConnectionString, _dispatcher);
                _playback.ActionReplayed += OnActionReplayed;
                _playback.ProgressChanged += OnPlaybackProgressChanged;
                System.Diagnostics.Debug.WriteLine("Demo инициализирован, обработчики подключены");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Не удалось инициализировать запись: {ex.Message}");
            }
        }
    }

    public bool IsRecording
    {
        get => _isRecording;
        set
        {
            // Запись не начинается автоматически - только по нажатию кнопки
            if (value == _isRecording)
            {
                return;
            }

            _isRecording = value;
            if (value && _recorder != null)
            {
                _ = _recorder.StartSessionAsync(_engine.Drawing.Key);
            }
            else if (!value && _recorder != null)
            {
                _ = _recorder.StopSessionAsync();
            }
        }
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            if (value && !_isPlaying)
            {
                _isPlaying = true;
            }
            else if (!value && _isPlaying)
            {
                _playback?.StopPlayback();
                _isPlaying = false;
            }
        }
    }

    public bool IsPaused => _playback?.IsPaused ?? false;

    public void PausePlayback()
    {
        _playback?.PausePlayback();
    }

    public void ResumePlayback()
    {
        _playback?.ResumePlayback();
    }

    public void StopPlayback()
    {
        _playback?.StopPlayback();
        _isPlaying = false;
        _viewModel.PlaybackProgress = 0;
        _viewModel.CurrentActionIndex = 0;
        _viewModel.TotalActions = 0;

        // При выходе из демо возвращаемся к исходному рисунку пользователя
        if (_previousDrawingKey is not null)
        {
            _dispatcher.Invoke(() =>
            {
                _engine.SetPicture(_previousDrawingKey);
                _viewModel.UpdatePicture(_engine);
                _engine.ClearAll();
                _viewModel.UpdateImages(_engine);
                _viewModel.SelectedColorIndex = 0;
                ResetCursor();
                _viewModel.StatusMessage = "Демо остановлено, возвращены ваши настройки.";
            });

            _previousDrawingKey = null;
        }
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
        // Игнорируем сообщения во время воспроизведения (кроме случая, когда пользователь явно хочет прервать)
        if (_isPlaying && !message.StartsWith("STOP", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (message.StartsWith("BTN:", StringComparison.OrdinalIgnoreCase))
        {
            var button = message[4..].Trim().ToUpperInvariant();
            HandleButton(button);
            return;
        }

        if (message.StartsWith("X:", StringComparison.OrdinalIgnoreCase) && message.Contains('Y'))
        {
            var match = JoystickRegex.Match(message);
            if (match.Success)
            {
                var rawX = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                var rawY = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);

                // Используем обработчик режимов для нормализации
                // В центрированном режиме нормализация автоматически вернет центр, если джойстик в dead zone
                var normalizedX = _joystickModeHandler.NormalizeX(rawX, _viewModel.CursorX);
                var normalizedY = _joystickModeHandler.NormalizeY(rawY, _viewModel.CursorY);

                _viewModel.UpdateCursor(normalizedX, normalizedY);

                // Записываем движение курсора
                if (_isRecording && _recorder != null)
                {
                    _ = _recorder.RecordCursorMoveAsync(normalizedX, normalizedY, rawX, rawY);
                }
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

        // Записываем выбор цвета
        if (_isRecording && _recorder != null)
        {
            _ = _recorder.RecordColorSelectAsync(index.Value, _viewModel.SelectedColorHex, _viewModel.CursorX, _viewModel.CursorY);
        }
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

        // Записываем заливку
        if (_isRecording && _recorder != null)
        {
            _ = _recorder.RecordFillAsync(canvasPoint.X, canvasPoint.Y, figure, _viewModel.SelectedColorIndex, _viewModel.SelectedColorHex, _viewModel.CursorX, _viewModel.CursorY);
        }
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

            // Записываем очистку фигуры
            if (_isRecording && _recorder != null)
            {
                _ = _recorder.RecordClearFigureAsync(canvasPoint.X, canvasPoint.Y, figure, _viewModel.CursorX, _viewModel.CursorY);
            }
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

        // Записываем смену картинки
        if (_isRecording && _recorder != null)
        {
            _ = _recorder.RecordNextPictureAsync("E");
            // Начинаем новую сессию для новой картинки
            _ = _recorder.StopSessionAsync();
            _ = _recorder.StartSessionAsync(_engine.Drawing.Key);
        }
    }

    private void HandleClearAll()
    {
        _engine.ClearAll();
        _viewModel.UpdateImages(_engine);
        _viewModel.StatusMessage = "✓ Canvas очищен";

        // Записываем очистку всего
        if (_isRecording && _recorder != null)
        {
            _ = _recorder.RecordClearAllAsync("F");
        }
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

    public void SetJoystickMode(JoystickMode mode)
    {
        _joystickModeHandler.Mode = mode;
        
        // При переключении в центрированный режим возвращаем курсор в центр
        if (mode == JoystickMode.Centered)
        {
            var (centerX, centerY) = _joystickModeHandler.GetCenter();
            ResetCursor();
        }
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

    public async Task StartPlaybackAsync(int sessionId)
    {
        System.Diagnostics.Debug.WriteLine($"StartPlaybackAsync вызван для сессии {sessionId}");
        
        if (_playback == null)
        {
            _viewModel.StatusMessage = "Воспроизведение недоступно";
            System.Diagnostics.Debug.WriteLine("Ошибка: _playback == null");
            return;
        }

        // Запоминаем текущий рисунок пользователя, чтобы вернуться к нему после демо
        _previousDrawingKey = _engine.Drawing.Key;

        // Получаем информацию о сессии для установки правильного рисунка
        var sessions = await _playback.GetAvailableSessionsAsync();
        var session = sessions.FirstOrDefault(s => s.Id == sessionId);
        
        System.Diagnostics.Debug.WriteLine($"Найдено сессий: {sessions.Count}, выбранная сессия: {(session != null ? session.Id.ToString() : "null")}");
        
        if (session != null)
        {
            // Устанавливаем правильный рисунок из сессии
            _dispatcher.Invoke(() =>
            {
                _engine.SetPicture(session.DrawingKey);
                _viewModel.UpdatePicture(_engine);
                _viewModel.UpdateImages(_engine);
                
                // Очищаем все перед воспроизведением
                _engine.ClearAll();
                _viewModel.UpdateImages(_engine);
                _viewModel.SelectedColorIndex = 0;
                ResetCursor();
                
                // Устанавливаем общее количество действий для прогресс-бара
                _viewModel.TotalActions = (int)session.ActionCount;
            });
            System.Diagnostics.Debug.WriteLine($"Установлено TotalActions: {_viewModel.TotalActions}");
        }

        try
        {
            _dispatcher.Invoke(() =>
            {
                _isPlaying = true;
                _viewModel.PlaybackProgress = 0;
                _viewModel.CurrentActionIndex = 0;
                _viewModel.StatusMessage = "Воспроизведение сессии...";
            });
            System.Diagnostics.Debug.WriteLine("Запуск PlaybackSessionAsync...");
            await _playback.PlaybackSessionAsync(sessionId, OnActionReplayedCallback);
            System.Diagnostics.Debug.WriteLine("PlaybackSessionAsync завершен");
            _dispatcher.Invoke(() =>
            {
                _viewModel.StatusMessage = "Воспроизведение завершено";
            });
        }
        catch (Exception ex)
        {
            _dispatcher.Invoke(() =>
            {
                _viewModel.StatusMessage = $"Ошибка воспроизведения: {ex.Message}";
            });
            System.Diagnostics.Debug.WriteLine($"Ошибка при воспроизведении в контроллере: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
        }
        finally
        {
            _dispatcher.Invoke(() =>
            {
                _isPlaying = false;
                _viewModel.PlaybackProgress = 0;
                _viewModel.CurrentActionIndex = 0;
                _viewModel.TotalActions = 0;

                // После завершения демо выходим из него и возвращаем пользователя к его рисунку
                if (_previousDrawingKey is not null)
                {
                    _engine.SetPicture(_previousDrawingKey);
                    _viewModel.UpdatePicture(_engine);
                    _engine.ClearAll();
                    _viewModel.UpdateImages(_engine);
                    _viewModel.SelectedColorIndex = 0;
                    ResetCursor();
                    _viewModel.StatusMessage = "Демо завершено, возвращены ваши настройки.";
                    _previousDrawingKey = null;
                }
            });
        }
    }

    private void OnActionReplayedCallback(ActionRecord action)
    {
        System.Diagnostics.Debug.WriteLine($"OnActionReplayedCallback: {action.ActionType}, Timestamp: {action.TimestampMs}");
        OnActionReplayed(null, action);
    }

    private void OnPlaybackProgressChanged(object? sender, Services.PlaybackProgressEventArgs e)
    {
        _viewModel.PlaybackProgress = e.Progress;
        _viewModel.CurrentActionIndex = e.CurrentAction;
        _viewModel.TotalActions = e.TotalActions;
    }

    private void OnActionReplayed(object? sender, ActionRecord action)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"OnActionReplayed: обработка {action.ActionType}");
            switch (action.ActionType)
        {
            case ActionType.CursorMove:
                if (action.CursorX.HasValue && action.CursorY.HasValue)
                {
                    _dispatcher.Invoke(() =>
                    {
                        _viewModel.UpdateCursor(action.CursorX.Value, action.CursorY.Value);
                    });
                    System.Diagnostics.Debug.WriteLine($"  Курсор перемещен: ({action.CursorX.Value}, {action.CursorY.Value})");
                }
                break;

            case ActionType.ColorSelect:
                if (action.ColorIndex.HasValue)
                {
                    _dispatcher.Invoke(() =>
                    {
                        _viewModel.SelectedColorIndex = action.ColorIndex.Value;
                    });
                }
                break;

            case ActionType.Fill:
                if (!string.IsNullOrEmpty(action.FigureName) && action.ColorHex != null)
                {
                    // Используем сохраненное имя фигуры напрямую, без HitTest
                    var color = ParseColorHex(action.ColorHex);
                    _dispatcher.Invoke(() =>
                    {
                        _engine.FillFigure(action.FigureName, color);
                        _viewModel.UpdateImages(_engine);
                        
                        // Также обновляем выбранный цвет, если указан
                        if (action.ColorIndex.HasValue)
                        {
                            _viewModel.SelectedColorIndex = action.ColorIndex.Value;
                        }
                    });
                }
                break;

            case ActionType.ClearFigure:
                if (!string.IsNullOrEmpty(action.FigureName))
                {
                    // Используем сохраненное имя фигуры напрямую, без HitTest
                    _dispatcher.Invoke(() =>
                    {
                        _engine.ClearFigure(action.FigureName);
                        _viewModel.UpdateImages(_engine);
                    });
                }
                break;

            case ActionType.NextPicture:
                _dispatcher.Invoke(() =>
                {
                    _engine.NextPicture();
                    _viewModel.UpdatePicture(_engine);
                    _viewModel.UpdateImages(_engine);
                    _viewModel.SelectedColorIndex = 0;
                    ResetCursor();
                });
                break;

            case ActionType.ClearAll:
                _dispatcher.Invoke(() =>
                {
                    _engine.ClearAll();
                    _viewModel.UpdateImages(_engine);
                });
                break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка при обработке действия {action.ActionType}: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
        }
    }

    private static System.Windows.Media.Color ParseColorHex(string hex)
    {
        if (hex.StartsWith("#"))
        {
            hex = hex.Substring(1);
        }

        var r = Convert.ToByte(hex.Substring(0, 2), 16);
        var g = Convert.ToByte(hex.Substring(2, 2), 16);
        var b = Convert.ToByte(hex.Substring(4, 2), 16);

        return System.Windows.Media.Color.FromRgb(r, g, b);
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        _playback?.StopPlayback();
        
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

        _recorder?.Dispose();
        _playback?.Dispose();
        _cancellation.Dispose();
    }
}
