namespace WpfClient.Models;

public enum ActionType
{
    CursorMove,
    ColorSelect,
    Fill,
    ClearFigure,
    NextPicture,
    ClearAll,
    InitialState
}

public sealed class ActionRecord
{
    public long Id { get; set; }
    public int SessionId { get; set; }
    public ActionType ActionType { get; set; }
    public long TimestampMs { get; set; }
    public DateTime OccurredAt { get; set; }
    public double? CursorX { get; set; }
    public double? CursorY { get; set; }
    public double? CanvasX { get; set; }
    public double? CanvasY { get; set; }
    public int? ColorIndex { get; set; }
    public string? ColorHex { get; set; }
    public string? FigureName { get; set; }
    public string? ButtonPressed { get; set; }
    public int? RawX { get; set; }
    public int? RawY { get; set; }
    public string? AdditionalData { get; set; }
}





