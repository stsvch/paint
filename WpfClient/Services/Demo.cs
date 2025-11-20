using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using MySqlConnector;
using WpfClient.Models;

namespace WpfClient.Services;

public sealed class Demo : IDisposable
{
    private readonly string _connectionString;
    private readonly Dispatcher _dispatcher;
    private CancellationTokenSource? _cancellation;
    private bool _isPlaying;
    private bool _isPaused;
    private DateTime _pauseStartTime;

    public Demo(string connectionString, Dispatcher dispatcher)
    {
        _connectionString = connectionString;
        _dispatcher = dispatcher;
    }

    public bool IsPlaying => _isPlaying;
    
    public bool IsPaused => _isPaused;

    public event EventHandler<ActionRecord>? ActionReplayed;
    public event EventHandler<PlaybackProgressEventArgs>? ProgressChanged;

    public async Task<List<SessionInfo>> GetAvailableSessionsAsync()
    {
        var sessions = new List<SessionInfo>();

        try
        {
            System.Diagnostics.Debug.WriteLine($"Попытка подключения к БД: {_connectionString.Replace(DatabaseConfig.Password, "***")}");
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            System.Diagnostics.Debug.WriteLine("Подключение к БД успешно");

            var command = new MySqlCommand(
                @"SELECT s.id, s.started_at, s.ended_at, s.drawing_key,
                         COUNT(a.id) as action_count
                  FROM sessions s
                  LEFT JOIN actions a ON s.id = a.session_id
                  GROUP BY s.id, s.started_at, s.ended_at, s.drawing_key
                  ORDER BY s.started_at DESC",
                connection);

            await using var reader = await command.ExecuteReaderAsync();
            var idOrdinal = reader.GetOrdinal("id");
            var startedAtOrdinal = reader.GetOrdinal("started_at");
            var endedAtOrdinal = reader.GetOrdinal("ended_at");
            var drawingKeyOrdinal = reader.GetOrdinal("drawing_key");
            var actionCountOrdinal = reader.GetOrdinal("action_count");
            
            while (await reader.ReadAsync())
            {
                sessions.Add(new SessionInfo
                {
                    Id = reader.GetInt32(idOrdinal),
                    StartedAt = reader.GetDateTime(startedAtOrdinal),
                    EndedAt = reader.IsDBNull(endedAtOrdinal) ? null : reader.GetDateTime(endedAtOrdinal),
                    DrawingKey = reader.GetString(drawingKeyOrdinal),
                    ActionCount = reader.GetInt64(actionCountOrdinal)
                });
            }
            
            System.Diagnostics.Debug.WriteLine($"Загружено сессий: {sessions.Count}");
        }
        catch (MySqlException dbEx)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка MySQL при получении сессий: {dbEx.Message}");
            System.Diagnostics.Debug.WriteLine($"Error Code: {dbEx.ErrorCode}, Number: {dbEx.Number}");
            throw; // Пробрасываем исключение дальше, чтобы показать пользователю
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка при получении сессий: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
            throw; // Пробрасываем исключение дальше
        }

        return sessions;
    }

    public async Task PlaybackSessionAsync(int sessionId, Action<ActionRecord>? onAction = null)
    {
        if (_isPlaying)
        {
            StopPlayback();
        }

        _cancellation = new CancellationTokenSource();
        _isPlaying = true;
        var totalActions = 0;

        try
        {
            var actions = await LoadSessionActionsAsync(sessionId);
            System.Diagnostics.Debug.WriteLine($"Загружено действий для воспроизведения: {actions.Count}");
            if (actions.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("Нет действий для воспроизведения, выход");
                _isPlaying = false;
                return;
            }

            // Применяем начальное состояние, если оно есть (первое действие с типом InitialState)
            ActionRecord? initialStateAction = null;
            var actionsToPlay = new List<ActionRecord>();
            
            foreach (var action in actions)
            {
                if (action.ActionType == ActionType.InitialState && initialStateAction == null)
                {
                    initialStateAction = action;
                    System.Diagnostics.Debug.WriteLine("Найдено начальное состояние, будет применено первым");
                }
                else
                {
                    actionsToPlay.Add(action);
                }
            }

            // Применяем начальное состояние сразу, если оно есть
            if (initialStateAction != null)
            {
                _dispatcher.Invoke(() =>
                {
                    onAction?.Invoke(initialStateAction);
                    ActionReplayed?.Invoke(this, initialStateAction);
                });
            }

            if (actionsToPlay.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("Нет действий для воспроизведения после начального состояния");
                _isPlaying = false;
                return;
            }

            var firstTimestamp = actionsToPlay[0].TimestampMs;
            var startTime = DateTime.UtcNow;
            long totalPauseDuration = 0;
            DateTime pauseStartTime = DateTime.MinValue;
            totalActions = actionsToPlay.Count;
            int currentActionIndex = 0;

            // Уведомляем о начале воспроизведения (0%)
            _dispatcher.Invoke(() =>
            {
                ProgressChanged?.Invoke(this, new PlaybackProgressEventArgs(0, 0, totalActions));
            });

            System.Diagnostics.Debug.WriteLine($"Начало цикла обработки {actionsToPlay.Count} действий");
            foreach (var action in actionsToPlay)
            {
                if (_cancellation.Token.IsCancellationRequested)
                {
                    System.Diagnostics.Debug.WriteLine("Воспроизведение отменено");
                    break;
                }
                
                System.Diagnostics.Debug.WriteLine($"Обработка действия {action.ActionType}, индекс: {currentActionIndex + 1}/{totalActions}");

                // Если пауза включена, фиксируем время начала паузы
                if (_isPaused && pauseStartTime == DateTime.MinValue)
                {
                    pauseStartTime = DateTime.UtcNow;
                }

                // Ждем, пока пауза не будет снята
                while (_isPaused && !_cancellation.Token.IsCancellationRequested)
                {
                    await Task.Delay(100, _cancellation.Token);
                }

                if (_cancellation.Token.IsCancellationRequested)
                {
                    break;
                }

                // Если пауза была снята, добавляем время паузы к totalPauseDuration
                if (!_isPaused && pauseStartTime != DateTime.MinValue)
                {
                    var pauseDuration = (DateTime.UtcNow - pauseStartTime).TotalMilliseconds;
                    totalPauseDuration += (long)pauseDuration;
                    pauseStartTime = DateTime.MinValue;
                }

                var delay = action.TimestampMs - firstTimestamp;
                var adjustedStartTime = startTime.AddMilliseconds(totalPauseDuration);
                var elapsed = (DateTime.UtcNow - adjustedStartTime).TotalMilliseconds;

                if (delay > elapsed)
                {
                    await Task.Delay((int)(delay - elapsed), _cancellation.Token);
                }

                _dispatcher.Invoke(() =>
                {
                    try
                    {
                        // Обрабатываем действие
                        onAction?.Invoke(action);
                        ActionReplayed?.Invoke(this, action);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Ошибка при обработке действия: {ex.Message}");
                    }
                });
                
                // Обновляем прогресс после обработки действия
                currentActionIndex++;
                var progress = (double)currentActionIndex / totalActions * 100.0;
                
                _dispatcher.Invoke(() =>
                {
                    ProgressChanged?.Invoke(this, new PlaybackProgressEventArgs(progress, currentActionIndex, totalActions));
                });
            }
        }
        catch (OperationCanceledException)
        {
            // Игнорируем, если воспроизведение было остановлено
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка при воспроизведении: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"InnerException: {ex.InnerException.Message}");
            }
        }
        finally
        {
            _isPlaying = false;
            // Уведомляем о завершении
            _dispatcher.Invoke(() =>
            {
                if (totalActions > 0)
                {
                    ProgressChanged?.Invoke(this, new PlaybackProgressEventArgs(100, totalActions, totalActions));
                }
            });
        }
    }

    public void PausePlayback()
    {
        if (_isPlaying && !_isPaused)
        {
            _isPaused = true;
            _pauseStartTime = DateTime.UtcNow;
        }
    }

    public void ResumePlayback()
    {
        if (_isPlaying && _isPaused)
        {
            // _isPaused будет сброшен в цикле после обработки паузы
            _isPaused = false;
        }
    }

    public void StopPlayback()
    {
        _cancellation?.Cancel();
        _isPlaying = false;
        _isPaused = false;
    }

    public async Task<bool> DeleteSessionAsync(int sessionId)
    {
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            // Удаляем сессию (действия удалятся каскадно благодаря ON DELETE CASCADE)
            var command = new MySqlCommand(
                "DELETE FROM sessions WHERE id = @sessionId",
                connection);
            command.Parameters.AddWithValue("@sessionId", sessionId);

            var rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка при удалении сессии: {ex.Message}");
            return false;
        }
    }

    private async Task<List<ActionRecord>> LoadSessionActionsAsync(int sessionId)
    {
        var actions = new List<ActionRecord>();

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var command = new MySqlCommand(
                @"SELECT id, session_id, action_type, timestamp_ms, occurred_at, cursor_x, cursor_y,
                         canvas_x, canvas_y, color_index, color_hex, figure_name, button_pressed, raw_x, raw_y, additional_data
                  FROM actions
                  WHERE session_id = @sessionId
                  ORDER BY timestamp_ms ASC",
                connection);

            command.Parameters.AddWithValue("@sessionId", sessionId);

            await using var reader = await command.ExecuteReaderAsync();
            var idOrdinal = reader.GetOrdinal("id");
            var sessionIdOrdinal = reader.GetOrdinal("session_id");
            var actionTypeOrdinal = reader.GetOrdinal("action_type");
            var timestampMsOrdinal = reader.GetOrdinal("timestamp_ms");
            var occurredAtOrdinal = reader.GetOrdinal("occurred_at");
            var cursorXOrdinal = reader.GetOrdinal("cursor_x");
            var cursorYOrdinal = reader.GetOrdinal("cursor_y");
            var canvasXOrdinal = reader.GetOrdinal("canvas_x");
            var canvasYOrdinal = reader.GetOrdinal("canvas_y");
            var colorIndexOrdinal = reader.GetOrdinal("color_index");
            var colorHexOrdinal = reader.GetOrdinal("color_hex");
            var figureNameOrdinal = reader.GetOrdinal("figure_name");
            var buttonPressedOrdinal = reader.GetOrdinal("button_pressed");
            var rawXOrdinal = reader.GetOrdinal("raw_x");
            var rawYOrdinal = reader.GetOrdinal("raw_y");
            var additionalDataOrdinal = reader.GetOrdinal("additional_data");
            
            while (await reader.ReadAsync())
            {
                actions.Add(new ActionRecord
                {
                    Id = reader.GetInt64(idOrdinal),
                    SessionId = reader.GetInt32(sessionIdOrdinal),
                    ActionType = ParseActionType(reader.GetString(actionTypeOrdinal)),
                    TimestampMs = reader.GetInt64(timestampMsOrdinal),
                    OccurredAt = reader.GetDateTime(occurredAtOrdinal),
                    CursorX = reader.IsDBNull(cursorXOrdinal) ? null : reader.GetDouble(cursorXOrdinal),
                    CursorY = reader.IsDBNull(cursorYOrdinal) ? null : reader.GetDouble(cursorYOrdinal),
                    CanvasX = reader.IsDBNull(canvasXOrdinal) ? null : reader.GetDouble(canvasXOrdinal),
                    CanvasY = reader.IsDBNull(canvasYOrdinal) ? null : reader.GetDouble(canvasYOrdinal),
                    ColorIndex = reader.IsDBNull(colorIndexOrdinal) ? null : reader.GetInt32(colorIndexOrdinal),
                    ColorHex = reader.IsDBNull(colorHexOrdinal) ? null : reader.GetString(colorHexOrdinal),
                    FigureName = reader.IsDBNull(figureNameOrdinal) ? null : reader.GetString(figureNameOrdinal),
                    ButtonPressed = reader.IsDBNull(buttonPressedOrdinal) ? null : reader.GetString(buttonPressedOrdinal),
                    RawX = reader.IsDBNull(rawXOrdinal) ? null : reader.GetInt32(rawXOrdinal),
                    RawY = reader.IsDBNull(rawYOrdinal) ? null : reader.GetInt32(rawYOrdinal),
                    AdditionalData = reader.IsDBNull(additionalDataOrdinal) ? null : reader.GetString(additionalDataOrdinal)
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка при загрузке действий: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"InnerException: {ex.InnerException.Message}");
            }
        }

        System.Diagnostics.Debug.WriteLine($"Загружено действий для сессии {sessionId}: {actions.Count}");
        return actions;
    }

    private static ActionType ParseActionType(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "cursormove" => ActionType.CursorMove,
            "colorselect" => ActionType.ColorSelect,
            "fill" => ActionType.Fill,
            "clearfigure" => ActionType.ClearFigure,
            "nextpicture" => ActionType.NextPicture,
            "clearall" => ActionType.ClearAll,
            "initialstate" => ActionType.InitialState,
            _ => throw new ArgumentException($"Unknown action type: {type}")
        };
    }

    public void Dispose()
    {
        StopPlayback();
        _cancellation?.Dispose();
    }
}

public sealed class SessionInfo
{
    public int Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string DrawingKey { get; set; } = string.Empty;
    public long ActionCount { get; set; }
}

public sealed class PlaybackProgressEventArgs : EventArgs
{
    public double Progress { get; }
    public int CurrentAction { get; }
    public int TotalActions { get; }

    public PlaybackProgressEventArgs(double progress, int currentAction, int totalActions)
    {
        Progress = progress;
        CurrentAction = currentAction;
        TotalActions = totalActions;
    }
}

