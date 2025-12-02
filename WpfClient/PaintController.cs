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
    
    // Дросселирование записи движений курсора (записываем не чаще чем раз в 100мс)
    private DateTime _lastCursorMoveRecordTime = DateTime.MinValue;
    private const int CursorMoveRecordIntervalMs = 100;
    
    // Батчинг обновлений курсора - накапливаем изменения и обновляем пакетом
    private double _pendingCursorX;
    private double _pendingCursorY;
    private bool _hasPendingCursorUpdate;
    private readonly DispatcherTimer _cursorUpdateTimer;

    public PaintController(PaintViewModel viewModel, PaintEngine engine, bool enableDatabase = true)
    {
        _viewModel = viewModel;
        _engine = engine;
        _dispatcher = Application.Current.Dispatcher;

        // Используем логические координаты холста (0-600)
        var centerX = AppConfig.CanvasWidth / 2.0;
        var centerY = AppConfig.CanvasHeight / 2.0;
        _joystickModeHandler = new JoystickModeHandler(centerX, centerY);
        
        // Таймер для батчинга обновлений курсора (обновляем UI не чаще 60 раз в секунду)
        // Используем DispatcherTimer для работы в UI потоке - это более эффективно
        _cursorUpdateTimer = new DispatcherTimer(DispatcherPriority.Render, _dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(16.67) // ~60 FPS для плавного движения
        };
        _cursorUpdateTimer.Tick += (sender, e) =>
        {
            if (_hasPendingCursorUpdate)
            {
                _hasPendingCursorUpdate = false;
                _viewModel.UpdateCursor(_pendingCursorX, _pendingCursorY);
            }
        };
        _cursorUpdateTimer.Start();

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
                // Передаем текущее состояние холста (закрашенные фигуры) при старте записи
                var initialFilledFigures = _engine.FilledFigures;
                _ = _recorder.StartSessionAsync(_engine.Drawing.Key, initialFilledFigures);
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
                // Используем BeginInvoke для асинхронного обновления UI
                // Это предотвращает блокировку фонового потока и лаги курсора
                _dispatcher.BeginInvoke(new Action(() => ProcessSerialMessage(message)));
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

                // Батчинг обновлений курсора - накапливаем изменения и обновляем пакетом через таймер
                // Это предотвращает перегрузку UI потока и делает движение плавным
                _pendingCursorX = normalizedX;
                _pendingCursorY = normalizedY;
                _hasPendingCursorUpdate = true;

                // Записываем движение курсора с дросселированием (не чаще чем раз в 100мс)
                // Это предотвращает перегрузку БД и лаги при записи
                if (_isRecording && _recorder != null)
                {
                    var now = DateTime.UtcNow;
                    var timeSinceLastRecord = (now - _lastCursorMoveRecordTime).TotalMilliseconds;
                    
                    if (timeSinceLastRecord >= CursorMoveRecordIntervalMs)
                    {
                        _lastCursorMoveRecordTime = now;
                        _ = _recorder.RecordCursorMoveAsync(normalizedX, normalizedY, rawX, rawY);
                    }
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
        // Циклический перебор цветов (так как палитра теперь в ListBox и курсор работает только на холсте)
        var currentIndex = _viewModel.SelectedColorIndex;
        var nextIndex = (currentIndex + 1) % _viewModel.Palette.Count;
        _viewModel.SelectedColorIndex = nextIndex;
        _viewModel.StatusMessage = $"Выбран цвет #{nextIndex}: {_viewModel.SelectedColorName}";

        // Записываем выбор цвета
        if (_isRecording && _recorder != null)
        {
            _ = _recorder.RecordColorSelectAsync(nextIndex, _viewModel.SelectedColorHex, _viewModel.CursorX, _viewModel.CursorY);
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

        // Выполняем заливку асинхронно, чтобы не блокировать UI
        var color = _viewModel.SelectedColor;
        var colorIndex = _viewModel.SelectedColorIndex;
        var colorHex = _viewModel.SelectedColorHex;
        var cursorX = _viewModel.CursorX;
        var cursorY = _viewModel.CursorY;
        
        _viewModel.StatusMessage = "⏳ Заливка...";
        
        _ = Task.Run(() =>
        {
            // Выполняем заливку в фоновом потоке
            _engine.FillRegionAtPoint(canvasPoint.X, canvasPoint.Y, color);
            
            // Обновляем UI в UI потоке асинхронно
            _dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                _viewModel.UpdateImages(_engine);
                _viewModel.StatusMessage = $"✓ Залита область: {figure}";
            }));

            // Записываем заливку
            if (_isRecording && _recorder != null)
            {
                _ = _recorder.RecordFillAsync(canvasPoint.X, canvasPoint.Y, figure, colorIndex, colorHex, cursorX, cursorY);
            }
        });
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

        // Сохраняем значения для использования в замыкании
        var canvasX = canvasPoint.X;
        var canvasY = canvasPoint.Y;
        var cursorX = _viewModel.CursorX;
        var cursorY = _viewModel.CursorY;
        
        // Выполняем очистку асинхронно
        _ = Task.Run(() =>
        {
            var cleared = _engine.ClearFigure(figure);
            
            if (cleared)
            {
                // Обновляем UI в UI потоке асинхронно
                _dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                {
                    _viewModel.UpdateImages(_engine);
                    _viewModel.StatusMessage = $"✓ Очищена фигура: {figure}";
                }));

                // Записываем очистку фигуры
                if (_isRecording && _recorder != null)
                {
                    _ = _recorder.RecordClearFigureAsync(canvasX, canvasY, figure, cursorX, cursorY);
                }
            }
            else
            {
                _dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                {
                    _viewModel.StatusMessage = "Фигура уже пустая";
                }));
            }
        });
    }

    private void HandleNextPicture()
    {
        // Выполняем смену картинки асинхронно
        _ = Task.Run(() =>
        {
            _engine.NextPicture();
            
            // Обновляем UI в UI потоке асинхронно
            _dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                _viewModel.UpdatePicture(_engine);
                _viewModel.UpdateImages(_engine);
                _viewModel.SelectedColorIndex = 0;
                // Используем логические координаты холста (0-600)
                var centerX = AppConfig.CanvasWidth / 2.0;
                var centerY = AppConfig.CanvasHeight / 2.0;
                _viewModel.UpdateCursor(centerX, centerY);
                _viewModel.StatusMessage = "✓ Новая картинка готова";
            }));

            // Записываем смену картинки
            if (_isRecording && _recorder != null)
            {
                _ = _recorder.RecordNextPictureAsync("E");
                // Начинаем новую сессию для новой картинки
                _ = _recorder.StopSessionAsync();
                _ = _recorder.StartSessionAsync(_engine.Drawing.Key);
            }
        });
    }

    private void HandleClearAll()
    {
        // Выполняем очистку асинхронно
        _ = Task.Run(() =>
        {
            _engine.ClearAll();
            
            // Обновляем UI в UI потоке асинхронно
            _dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                _viewModel.UpdateImages(_engine);
                _viewModel.StatusMessage = "✓ Canvas очищен";
            }));

            // Записываем очистку всего
            if (_isRecording && _recorder != null)
            {
                _ = _recorder.RecordClearAllAsync("F");
            }
        });
    }

    private void ResetCursor()
    {
        // Используем логические координаты холста (0-600)
        var centerX = AppConfig.CanvasWidth / 2.0;
        var centerY = AppConfig.CanvasHeight / 2.0;
        _viewModel.UpdateCursor(centerX, centerY);
    }

    private bool TryGetCanvasPoint(out Point canvasPoint)
    {
        // Используем логические координаты холста напрямую (0-600)
        // Координаты уже ограничены в UpdateCursor, но проверяем еще раз для надежности
        var x = _viewModel.CursorX;
        var y = _viewModel.CursorY;

        // Проверяем, что координаты находятся в пределах холста (включая границы)
        if (x < 0 || x >= AppConfig.CanvasWidth ||
            y < 0 || y >= AppConfig.CanvasHeight)
        {
            canvasPoint = default;
            return false;
        }

        canvasPoint = new Point(x, y);
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
                // Начальное состояние будет восстановлено первым действием при воспроизведении
                _engine.ClearAll();
                _viewModel.UpdateImages(_engine);
                _viewModel.SelectedColorIndex = 0;
                ResetCursor();
                
                // TotalActions будет установлен через ProgressChanged в Demo.PlaybackSessionAsync
                // с учетом того, что InitialState не учитывается в прогрессе
            });
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
            case ActionType.InitialState:
                // Восстанавливаем начальное состояние холста
                if (!string.IsNullOrEmpty(action.AdditionalData))
                {
                    _dispatcher.Invoke(() =>
                    {
                        // Парсим формат: "figureName1:#RRGGBB;figureName2:#RRGGBB;..."
                        var stateParts = action.AdditionalData.Split(';', StringSplitOptions.RemoveEmptyEntries);
                        _engine.ClearAll(); // Очищаем текущее состояние
                        
                        foreach (var part in stateParts)
                        {
                            var colonIndex = part.IndexOf(':');
                            if (colonIndex > 0 && colonIndex < part.Length - 1)
                            {
                                var figureName = part.Substring(0, colonIndex);
                                var colorHex = part.Substring(colonIndex + 1);
                                
                                // Парсим цвет из hex
                                if (colorHex.StartsWith("#") && colorHex.Length == 7)
                                {
                                    try
                                    {
                                        var r = Convert.ToByte(colorHex.Substring(1, 2), 16);
                                        var g = Convert.ToByte(colorHex.Substring(3, 2), 16);
                                        var b = Convert.ToByte(colorHex.Substring(5, 2), 16);
                                        var color = System.Windows.Media.Color.FromRgb(r, g, b);
                                        _engine.FillFigure(figureName, color);
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Ошибка при парсинге цвета {colorHex}: {ex.Message}");
                                    }
                                }
                            }
                        }
                        
                        _viewModel.UpdateImages(_engine);
                        System.Diagnostics.Debug.WriteLine($"Восстановлено начальное состояние: {stateParts.Length} фигур");
                    });
                }
                break;

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
                if (action.ColorHex != null && action.CanvasX.HasValue && action.CanvasY.HasValue)
                {
                    // Используем flood fill с координатами из записи для правильного воспроизведения
                    var color = ParseColorHex(action.ColorHex);
                    _dispatcher.Invoke(() =>
                    {
                        _engine.FillRegionAtPoint(action.CanvasX.Value, action.CanvasY.Value, color);
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
        _cursorUpdateTimer?.Stop();
        
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
