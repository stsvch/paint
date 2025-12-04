using System;

namespace WpfClient;

public sealed class TimedGameResultEventArgs : EventArgs
{
    public TimedGameResultEventArgs(
        bool success,
        TimeSpan roundDuration,
        TimeSpan elapsed,
        int filledRegions,
        int totalRegions,
        int fillActions,
        TimeSpan activeTime,
        TimeSpan idleTime)
    {
        Success = success;
        RoundDuration = roundDuration;
        Elapsed = elapsed;
        FilledRegions = filledRegions;
        TotalRegions = totalRegions;
        FillActions = fillActions;
        ActiveTime = activeTime;
        IdleTime = idleTime;
    }

    public bool Success { get; }

    public TimeSpan RoundDuration { get; }

    public TimeSpan Elapsed { get; }

    public int FilledRegions { get; }

    public int TotalRegions { get; }

    public int FillActions { get; }

    public TimeSpan ActiveTime { get; }

    public TimeSpan IdleTime { get; }

    public double CompletionPercent =>
        TotalRegions > 0 ? (double)FilledRegions / TotalRegions * 100 : 0;

    public double ActionsPerMinute =>
        Elapsed.TotalMinutes > 0 ? FillActions / Elapsed.TotalMinutes : 0;
}


