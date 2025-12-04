using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

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
    private bool _isMenuVisible = true;
    private bool _isSerialConnected;
    // Используем логические координаты холста (0-600) вместо экранных
    private double _cursorX = AppConfig.CanvasWidth / 2.0;
    private double _cursorY = AppConfig.CanvasHeight / 2.0;
    private double _cursorLeft;
    private double _cursorTop;
    private double _cursorInnerLeft;
    private double _cursorInnerTop;
    private bool _isRecording;
    private bool _isPlaying;
    private PaintController? _controller;
    private double _playbackProgress;
    private JoystickMode _joystickMode = JoystickMode.Absolute;
    private TimeSpan _timedGameDuration = TimeSpan.FromSeconds(60);
    private readonly DispatcherTimer _timedGameTimer = new();
    private TimeSpan _timedGameRemaining;
    private bool _isTimedGameRunning;
    private bool _isTimedGameFinished;
    private string _timedGameStatusText = string.Empty;
    private string _timedGameTimerText = string.Empty;
    private int _timedGameTotalRegions;
    private TimeSpan _timedGameLastDuration;
    private TimeSpan _timedGameLastElapsed;
    private bool _timedGameLastSuccess;
    private int _timedGameLastFilledCount;
    private int _timedGameFillActions;
    private DateTime _timedGameStartTime;
    private DateTime _timedGameFirstFillTime;
    private DateTime _timedGameLastFillTime;

    public PaintViewModel()
    {
        _readonlyFilledFigures = new ReadOnlyObservableCollection<string>(_filledFigures);
        Palette = new ReadOnlyCollection<ColorOption>(ColorPalette.Default.ToList());
        CursorLeft = _cursorX - CursorRadius;
        CursorTop = _cursorY - CursorRadius;
        CursorInnerLeft = _cursorX - 2;
        CursorInnerTop = _cursorY - 2;
        SelectedColorIndex = 0;

        _timedGameTimer.Interval = TimeSpan.FromSeconds(1);
        _timedGameTimer.Tick += (_, _) => OnTimedGameTick();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<TimedGameResultEventArgs>? TimedGameCompleted;

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

    public bool IsMenuVisible
    {
        get => _isMenuVisible;
        set
        {
            if (SetField(ref _isMenuVisible, value))
            {
                OnPropertyChanged(nameof(IsCanvasVisible));
            }
        }
    }

    public bool IsCanvasVisible => !_isMenuVisible;

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
        // Ограничиваем координаты границами холста
        x = Math.Max(0, Math.Min(AppConfig.CanvasWidth, x));
        y = Math.Max(0, Math.Min(AppConfig.CanvasHeight, y));
        
        // Обновляем координаты напрямую, чтобы минимизировать количество PropertyChanged событий
        // Увеличиваем порог до 0.5 пикселя для еще большей плавности
        // Это предотвращает микро-обновления, которые создают лаги
        if (Math.Abs(_cursorX - x) > 0.5 || Math.Abs(_cursorY - y) > 0.5)
        {
            _cursorX = x;
            _cursorY = y;
            _cursorLeft = x - CursorRadius;
            _cursorTop = y - CursorRadius;
            _cursorInnerLeft = x - 2;
            _cursorInnerTop = y - 2;
            
            // Уведомляем об изменениях одним пакетом
            OnPropertyChanged(nameof(CursorX));
            OnPropertyChanged(nameof(CursorY));
            OnPropertyChanged(nameof(CursorLeft));
            OnPropertyChanged(nameof(CursorTop));
            OnPropertyChanged(nameof(CursorInnerLeft));
            OnPropertyChanged(nameof(CursorInnerTop));
            OnPropertyChanged(nameof(CursorPosition));
        }
    }

    public string CursorPosition => $"{(int)CursorX}, {(int)CursorY}";

    public string SelectedColorHex => $"#{SelectedColor.R:X2}{SelectedColor.G:X2}{SelectedColor.B:X2}";

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public string ConnectionStatus => IsSerialConnected ? "Устройство подключено" : "Ожидание устройства";

    public bool IsTimedGameVisible => _isTimedGameRunning || _isTimedGameFinished;

    public bool IsTimedGameRunning
    {
        get => _isTimedGameRunning;
        private set
        {
            if (SetField(ref _isTimedGameRunning, value))
            {
                OnPropertyChanged(nameof(IsTimedGameVisible));
            }
        }
    }

    private bool IsTimedGameFinished
    {
        get => _isTimedGameFinished;
        set
        {
            if (SetField(ref _isTimedGameFinished, value))
            {
                OnPropertyChanged(nameof(IsTimedGameVisible));
            }
        }
    }

    public string TimedGameTimerText
    {
        get => _timedGameTimerText;
        private set => SetField(ref _timedGameTimerText, value);
    }

    public string TimedGameStatusText
    {
        get => _timedGameStatusText;
        private set => SetField(ref _timedGameStatusText, value);
    }

    public int TimedGameTotalRegions
    {
        get => _timedGameTotalRegions;
        private set => SetField(ref _timedGameTotalRegions, value);
    }

    public TimeSpan TimedGameLastDuration => _timedGameLastDuration;

    public TimeSpan TimedGameLastElapsed => _timedGameLastElapsed;

    public bool TimedGameLastSuccess => _timedGameLastSuccess;

    public int TimedGameLastFilledCount => _timedGameLastFilledCount;

    public TimeSpan TimedGameDuration
    {
        get => _timedGameDuration;
        set
        {
            // Не даём установить нулевую или отрицательную длительность
            if (value <= TimeSpan.Zero)
            {
                value = TimeSpan.FromSeconds(10);
            }

            _timedGameDuration = value;
        }
    }

    public PaintController? Controller
    {
        get => _controller;
        set
        {
            if (SetField(ref _controller, value))
            {
                if (value != null)
                {
                    UpdateRecordingState();
                    UpdatePlayingState();
                }
            }
        }
    }

    public bool IsRecording
    {
        get => _isRecording;
        set
        {
            if (SetField(ref _isRecording, value))
            {
                if (_controller != null)
                {
                    _controller.IsRecording = value;
                }
                OnPropertyChanged(nameof(RecordingStatus));
            }
        }
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            if (SetField(ref _isPlaying, value))
            {
                if (!value && _controller != null)
                {
                    _controller.StopPlayback();
                }
                OnPropertyChanged(nameof(PlayingStatus));
                OnPropertyChanged(nameof(IsPaused));
            }
        }
    }

    public bool IsPaused
    {
        get => _controller?.IsPaused ?? false;
    }

    public double PlaybackProgress
    {
        get => _playbackProgress;
        set => SetField(ref _playbackProgress, value);
    }

    private int _currentActionIndex;
    private int _totalActions;

    public string PlaybackProgressText
    {
        get => _isPlaying ? $"{_playbackProgress:F0}% ({_currentActionIndex}/{_totalActions})" : "";
    }

    public int CurrentActionIndex
    {
        get => _currentActionIndex;
        set
        {
            if (SetField(ref _currentActionIndex, value))
            {
                OnPropertyChanged(nameof(PlaybackProgressText));
            }
        }
    }

    public int TotalActions
    {
        get => _totalActions;
        set
        {
            if (SetField(ref _totalActions, value))
            {
                OnPropertyChanged(nameof(PlaybackProgressText));
            }
        }
    }

    public void NotifyPropertyChanged(string propertyName)
    {
        OnPropertyChanged(propertyName);
    }

    public string RecordingStatus => _isRecording ? "● Запись" : "○ Запись остановлена";

    public string PlayingStatus => _isPlaying ? (_controller?.IsPaused == true ? "⏸ Пауза" : "▶ Воспроизведение") : "▶ Воспроизведение остановлено";

    public JoystickMode JoystickMode
    {
        get => _joystickMode;
        set
        {
            if (SetField(ref _joystickMode, value))
            {
                _controller?.SetJoystickMode(value);
                OnPropertyChanged(nameof(JoystickModeText));
            }
        }
    }

    public string JoystickModeText => _joystickMode switch
    {
        JoystickMode.Absolute => "Режим: Абсолютный",
        JoystickMode.Centered => "Режим: Центрированный",
        _ => "Режим: Неизвестен"
    };

    private void UpdateRecordingState()
    {
        if (_controller != null)
        {
            _isRecording = _controller.IsRecording;
            OnPropertyChanged(nameof(IsRecording));
            OnPropertyChanged(nameof(RecordingStatus));
        }
    }

    private void UpdatePlayingState()
    {
        if (_controller != null)
        {
            _isPlaying = _controller.IsPlaying;
            OnPropertyChanged(nameof(IsPlaying));
            OnPropertyChanged(nameof(PlayingStatus));
        }
    }

    public void StartTimedGame()
    {
        if (_controller == null)
        {
            return;
        }

        _controller.PrepareForTimedGame();
        _timedGameStartTime = DateTime.UtcNow;
        _timedGameFillActions = 0;
        _timedGameFirstFillTime = _timedGameStartTime;
        _timedGameLastFillTime = _timedGameStartTime;
        _timedGameRemaining = _timedGameDuration;
        TimedGameTimerText = FormatTime(_timedGameRemaining);
        TimedGameStatusText = "Раскрасьте как на образце, пока идет таймер";
        StatusMessage = "Игра на время запущена";
        IsTimedGameRunning = true;
        IsTimedGameFinished = false;
        _timedGameTimer.Start();
    }

    public void CancelTimedGame()
    {
        _timedGameTimer.Stop();
        IsTimedGameRunning = false;
        IsTimedGameFinished = false;
        TimedGameStatusText = string.Empty;
        TimedGameTimerText = string.Empty;
    }

    private void CompleteTimedGame(bool success, string message)
    {
        _timedGameTimer.Stop();
        IsTimedGameRunning = false;
        IsTimedGameFinished = true;

        // Расчёт статистики
        _timedGameLastDuration = _timedGameDuration;
        var elapsed = _timedGameDuration - _timedGameRemaining;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        _timedGameLastElapsed = elapsed;
        _timedGameLastSuccess = success;
        _timedGameLastFilledCount = _filledFigures.Count;

        // Активное время (от первой до последней заливки), остальное считаем бездействием
        TimeSpan activeTime = TimeSpan.Zero;
        if (_timedGameFillActions > 0)
        {
            activeTime = _timedGameLastFillTime - _timedGameFirstFillTime;
            if (activeTime < TimeSpan.Zero)
            {
                activeTime = TimeSpan.Zero;
            }
            if (activeTime > elapsed)
            {
                activeTime = elapsed;
            }
        }

        var idleTime = elapsed - activeTime;
        if (idleTime < TimeSpan.Zero)
        {
            idleTime = TimeSpan.Zero;
        }

        var prefix = success ? "🎉 " : "⌛ ";
        TimedGameStatusText = prefix + message;
        StatusMessage = prefix + message;

        TimedGameCompleted?.Invoke(this, new TimedGameResultEventArgs(
            success,
            _timedGameDuration,
            elapsed,
            _timedGameLastFilledCount,
            _timedGameTotalRegions,
            _timedGameFillActions,
            activeTime,
            idleTime));
    }

    private void OnTimedGameTick()
    {
        if (!IsTimedGameRunning)
        {
            return;
        }

        _timedGameRemaining -= TimeSpan.FromSeconds(1);
        if (_timedGameRemaining < TimeSpan.Zero)
        {
            _timedGameRemaining = TimeSpan.Zero;
        }

        TimedGameTimerText = FormatTime(_timedGameRemaining);

        if (_timedGameRemaining == TimeSpan.Zero)
        {
            var success = _controller?.IsCanvasMatchingReference() ?? false;
            var resultText = success
                ? "Отлично! Все области совпали с образцом"
                : "Время вышло. Попробуйте ещё раз";
            CompleteTimedGame(success, resultText);
        }
    }

    private void EvaluateTimedGameProgress()
    {
        if (!IsTimedGameRunning)
        {
            return;
        }

        if (_controller?.IsCanvasMatchingReference() == true)
        {
            CompleteTimedGame(true, "Готово! Раскраска совпала с образцом до окончания таймера");
        }
    }

    private static string FormatTime(TimeSpan value) => $"{value.Minutes:D2}:{value.Seconds:D2}";

    public void UpdatePicture(PaintEngine engine)
    {
        PictureKey = engine.Drawing.Key;
        PictureDisplayName = engine.Drawing.DisplayName;
        TimedGameTotalRegions = engine.Drawing.ReferenceColors.Count;
    }

    public void UpdateImages(PaintEngine engine)
    {
        // Создаем изображения асинхронно, чтобы не блокировать UI
        _ = Task.Run(() =>
        {
            var canvasImage = engine.CreateCanvasImage();
            var referenceImage = engine.CreateReferenceImage();
            var filledFigures = engine.FilledFigures;
            
            // Обновляем UI в UI потоке
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
            {
                CanvasImage = canvasImage;
                ReferenceImage = referenceImage;
                UpdateFilledFigures(filledFigures);
            }));
        });
    }

    public void UpdateFilledFigures(IReadOnlyDictionary<string, Color> filledFigures)
    {
        // Регистрируем действие в режиме игры на время
        RegisterTimedGameFill();

        _filledFigures.Clear();
        foreach (var (name, color) in filledFigures)
        {
            _filledFigures.Add($"{name} — #{color.R:X2}{color.G:X2}{color.B:X2}");
        }

        OnPropertyChanged(nameof(FilledCount));

        EvaluateTimedGameProgress();
    }

    public void RegisterTimedGameFill()
    {
        if (!IsTimedGameRunning)
        {
            return;
        }

        _timedGameFillActions++;
        var now = DateTime.UtcNow;
        if (_timedGameFillActions == 1)
        {
            _timedGameFirstFillTime = now;
        }

        _timedGameLastFillTime = now;
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
