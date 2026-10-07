using System;
using System.Diagnostics;

namespace SecretFlasherManaka.ForEveryThing;

public enum GameStrength { Unknown = -1, Off = 0, Low = 1, High = 2 }
public enum GameStateReason { Normal, LeaderUnavailable, SceneChanged, Paused, ShuttingDown, SourceDisabled, SourceUnavailable, InvalidValue }

// Pure CLR data. ObservedAt uses Stopwatch.GetTimestamp(), never Unity object references.
public readonly record struct GameStateSnapshot(bool Active, GameStrength Strength,
    GameStateReason Reason, long Revision, long ObservedAt);

public static class GameStateHub
{
    public static int ApiMajorVersion => 1;
    private static readonly object Gate = new();
    private static GameStateSnapshot _current = new(false, GameStrength.Unknown, GameStateReason.LeaderUnavailable, 0, 0);
    public static GameStateSnapshot Current { get { lock (Gate) return _current; } }
    // Fired on Unity's main thread only when meaningful state changes. Keep handlers fast.
    public static event Action<GameStateSnapshot>? StateChanged;
    // Every fresh frame/lifecycle observation, including unchanged semantic state.
    // The bool is a stop barrier: consumers must not coalesce it away with later positive state.
    // Unity main thread; handlers must only enqueue data and return immediately.
    public static event Action<GameStateSnapshot, bool>? Observed;
    internal static Action<Exception>? SubscriberError;

    private static void ReportSubscriberError(Exception error)
    {
        try { SubscriberError?.Invoke(error); } catch { }
    }

    internal static void Publish(bool active, GameStrength strength, GameStateReason reason, bool forceStop = false)
    {
        GameStateSnapshot snapshot;
        bool changed;
        lock (Gate)
        {
            changed = _current.Active != active || _current.Strength != strength || _current.Reason != reason;
            snapshot = new GameStateSnapshot(active, strength, reason,
                _current.Revision + (changed ? 1 : 0), Stopwatch.GetTimestamp());
            _current = snapshot;
        }
        foreach (Delegate listener in Observed?.GetInvocationList() ?? Array.Empty<Delegate>())
            try { ((Action<GameStateSnapshot, bool>)listener)(snapshot, forceStop); }
            catch (Exception error) { ReportSubscriberError(error); }
        if (changed)
            foreach (Delegate listener in StateChanged?.GetInvocationList() ?? Array.Empty<Delegate>())
                try { ((Action<GameStateSnapshot>)listener)(snapshot); }
                catch (Exception error) { ReportSubscriberError(error); }
    }
}
