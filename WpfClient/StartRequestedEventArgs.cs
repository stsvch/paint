using System;

namespace WpfClient;

public enum StartMode
{
    Regular,
    TimedGame
}

public sealed class StartRequestedEventArgs : EventArgs
{
    private StartRequestedEventArgs(StartMode mode)
    {
        Mode = mode;
    }

    public StartMode Mode { get; }

    public static StartRequestedEventArgs Create(StartMode mode) => new(mode);
}
