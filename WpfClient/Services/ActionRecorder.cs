using System;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;
using WpfClient.Models;

namespace WpfClient.Services;

public sealed class ActionRecorder : IDisposable
{
    private readonly string _connectionString;
    private int? _currentSessionId;
    private DateTime _sessionStartTime;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public ActionRecorder(string connectionString)
    {
        _connectionString = connectionString;
    }

    public bool IsRecording { get; private set; }

    public async Task StartSessionAsync(string drawingKey)
    {
        if (IsRecording)
        {
            return;
        }

        await _semaphore.WaitAsync();
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var insertCommand = new MySqlCommand(
                "INSERT INTO sessions (drawing_key, started_at) VALUES (@drawingKey, @startedAt);" +
                "SELECT LAST_INSERT_ID();",
                connection);

            insertCommand.Parameters.AddWithValue("@drawingKey", drawingKey);
            // Сохраняем локальное время, чтобы оно совпадало с вашим часовым поясом
            insertCommand.Parameters.AddWithValue("@startedAt", DateTime.Now);

            var sessionId = Convert.ToInt32(await insertCommand.ExecuteScalarAsync());
            _currentSessionId = sessionId;
            _sessionStartTime = DateTime.UtcNow;
            IsRecording = true;
        }
        catch (Exception ex)
        {
            // Логирование ошибки, но не прерываем работу приложения
            System.Diagnostics.Debug.WriteLine($"Ошибка при запуске записи сессии: {ex.Message}");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task StopSessionAsync()
    {
        if (!IsRecording || _currentSessionId is null)
        {
            return;
        }

        await _semaphore.WaitAsync();
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var updateCommand = new MySqlCommand(
                "UPDATE sessions SET ended_at = @endedAt WHERE id = @sessionId",
                connection);

            updateCommand.Parameters.AddWithValue("@endedAt", DateTime.UtcNow);
            updateCommand.Parameters.AddWithValue("@sessionId", _currentSessionId.Value);

            await updateCommand.ExecuteNonQueryAsync();
            _currentSessionId = null;
            IsRecording = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка при остановке записи сессии: {ex.Message}");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task RecordCursorMoveAsync(double x, double y, int? rawX = null, int? rawY = null)
    {
        if (!IsRecording || _currentSessionId is null)
        {
            return;
        }

        await RecordActionAsync(new ActionRecord
        {
            SessionId = _currentSessionId.Value,
            ActionType = ActionType.CursorMove,
            TimestampMs = GetTimestampMs(),
            OccurredAt = DateTime.UtcNow,
            CursorX = x,
            CursorY = y,
            RawX = rawX,
            RawY = rawY
        });
    }

    public async Task RecordColorSelectAsync(int colorIndex, string colorHex, double cursorX, double cursorY)
    {
        if (!IsRecording || _currentSessionId is null)
        {
            return;
        }

        await RecordActionAsync(new ActionRecord
        {
            SessionId = _currentSessionId.Value,
            ActionType = ActionType.ColorSelect,
            TimestampMs = GetTimestampMs(),
            OccurredAt = DateTime.UtcNow,
            ColorIndex = colorIndex,
            ColorHex = colorHex,
            CursorX = cursorX,
            CursorY = cursorY
        });
    }

    public async Task RecordFillAsync(double canvasX, double canvasY, string figureName, int colorIndex, string colorHex, double cursorX, double cursorY)
    {
        if (!IsRecording || _currentSessionId is null)
        {
            return;
        }

        await RecordActionAsync(new ActionRecord
        {
            SessionId = _currentSessionId.Value,
            ActionType = ActionType.Fill,
            TimestampMs = GetTimestampMs(),
            OccurredAt = DateTime.UtcNow,
            CanvasX = canvasX,
            CanvasY = canvasY,
            FigureName = figureName,
            ColorIndex = colorIndex,
            ColorHex = colorHex,
            CursorX = cursorX,
            CursorY = cursorY
        });
    }

    public async Task RecordClearFigureAsync(double canvasX, double canvasY, string figureName, double cursorX, double cursorY)
    {
        if (!IsRecording || _currentSessionId is null)
        {
            return;
        }

        await RecordActionAsync(new ActionRecord
        {
            SessionId = _currentSessionId.Value,
            ActionType = ActionType.ClearFigure,
            TimestampMs = GetTimestampMs(),
            OccurredAt = DateTime.UtcNow,
            CanvasX = canvasX,
            CanvasY = canvasY,
            FigureName = figureName,
            CursorX = cursorX,
            CursorY = cursorY
        });
    }

    public async Task RecordNextPictureAsync(string buttonPressed)
    {
        if (!IsRecording || _currentSessionId is null)
        {
            return;
        }

        await RecordActionAsync(new ActionRecord
        {
            SessionId = _currentSessionId.Value,
            ActionType = ActionType.NextPicture,
            TimestampMs = GetTimestampMs(),
            OccurredAt = DateTime.UtcNow,
            ButtonPressed = buttonPressed
        });
    }

    public async Task RecordClearAllAsync(string buttonPressed)
    {
        if (!IsRecording || _currentSessionId is null)
        {
            return;
        }

        await RecordActionAsync(new ActionRecord
        {
            SessionId = _currentSessionId.Value,
            ActionType = ActionType.ClearAll,
            TimestampMs = GetTimestampMs(),
            OccurredAt = DateTime.UtcNow,
            ButtonPressed = buttonPressed
        });
    }

    private Task RecordActionAsync(ActionRecord action)
    {
        // Записываем асинхронно без блокировки UI
        return Task.Run(async () =>
        {
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var command = new MySqlCommand(
                    @"INSERT INTO actions (session_id, action_type, timestamp_ms, occurred_at, cursor_x, cursor_y, 
                        canvas_x, canvas_y, color_index, color_hex, figure_name, button_pressed, raw_x, raw_y, additional_data)
                      VALUES (@sessionId, @actionType, @timestampMs, @occurredAt, @cursorX, @cursorY, 
                        @canvasX, @canvasY, @colorIndex, @colorHex, @figureName, @buttonPressed, @rawX, @rawY, @additionalData)",
                    connection);

                command.Parameters.AddWithValue("@sessionId", action.SessionId);
                command.Parameters.AddWithValue("@actionType", action.ActionType.ToString().ToLowerInvariant());
                command.Parameters.AddWithValue("@timestampMs", action.TimestampMs);
                command.Parameters.AddWithValue("@occurredAt", action.OccurredAt);
                command.Parameters.AddWithValue("@cursorX", action.CursorX.HasValue ? (object)action.CursorX.Value : DBNull.Value);
                command.Parameters.AddWithValue("@cursorY", action.CursorY.HasValue ? (object)action.CursorY.Value : DBNull.Value);
                command.Parameters.AddWithValue("@canvasX", action.CanvasX.HasValue ? (object)action.CanvasX.Value : DBNull.Value);
                command.Parameters.AddWithValue("@canvasY", action.CanvasY.HasValue ? (object)action.CanvasY.Value : DBNull.Value);
                command.Parameters.AddWithValue("@colorIndex", action.ColorIndex.HasValue ? (object)action.ColorIndex.Value : DBNull.Value);
                command.Parameters.AddWithValue("@colorHex", (object?)action.ColorHex ?? DBNull.Value);
                command.Parameters.AddWithValue("@figureName", (object?)action.FigureName ?? DBNull.Value);
                command.Parameters.AddWithValue("@buttonPressed", (object?)action.ButtonPressed ?? DBNull.Value);
                command.Parameters.AddWithValue("@rawX", action.RawX.HasValue ? (object)action.RawX.Value : DBNull.Value);
                command.Parameters.AddWithValue("@rawY", action.RawY.HasValue ? (object)action.RawY.Value : DBNull.Value);
                command.Parameters.AddWithValue("@additionalData", (object?)action.AdditionalData ?? DBNull.Value);

                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при записи действия: {ex.Message}");
            }
        });
    }

    private long GetTimestampMs()
    {
        return (long)(DateTime.UtcNow - _sessionStartTime).TotalMilliseconds;
    }

    public void Dispose()
    {
        StopSessionAsync().Wait(TimeSpan.FromSeconds(2));
        _semaphore?.Dispose();
    }
}

