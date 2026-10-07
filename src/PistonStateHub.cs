using System;
using System.Diagnostics;

namespace SecretFlasherManaka.ForEveryThing;


// Pure CLR data. ObservedAt uses Stopwatch.GetTimestamp(), never Unity object references.
public enum PistonMode { Unknown = -1, Off = 0, Slow = 1, Medium = 2, Fast = 3 }

public readonly record struct PistonStateSnapshot(bool Active, PistonMode Mode,
    GameStateReason Reason, long Revision, long ObservedAt);

public static class PistonStateHub
{
    public static int ApiMajorVersion => 1;
    private static readonly object Gate = new();
    private static PistonStateSnapshot _current = new(false, PistonMode.Unknown, GameStateReason.SourceUnavailable, 0, 0);
    public static PistonStateSnapshot Current { get { lock (Gate) return _current; } }
    // Fired on Unity's main thread only when meaningful state changes. Keep handlers fast.
    public static event Action<PistonStateSnapshot>? StateChanged;
    // Every fresh frame/lifecycle observation, including unchanged semantic state.
    // The bool is a stop barrier: consumers must not coalesce it away with later positive state.
    // Unity main thread; handlers must only enqueue data and return immediately.
    public static event Action<PistonStateSnapshot, bool>? Observed;
    internal static Action<Exception>? SubscriberError;

    private static void ReportSubscriberError(Exception error)
    {
        try { SubscriberError?.Invoke(error); } catch { }
    }

    internal static void Publish(bool active, PistonMode mode, GameStateReason reason, bool forceStop = false)
    {
        PistonStateSnapshot snapshot;
        bool changed;
        lock (Gate)
        {
            changed = _current.Active != active || _current.Mode != mode || _current.Reason != reason;
            snapshot = new PistonStateSnapshot(active, mode, reason,
                _current.Revision + (changed ? 1 : 0), Stopwatch.GetTimestamp());
            _current = snapshot;
        }
        foreach (Delegate listener in Observed?.GetInvocationList() ?? Array.Empty<Delegate>())
            try { ((Action<PistonStateSnapshot, bool>)listener)(snapshot, forceStop); }
            catch (Exception error) { ReportSubscriberError(error); }
        if (changed)
            foreach (Delegate listener in StateChanged?.GetInvocationList() ?? Array.Empty<Delegate>())
                try { ((Action<PistonStateSnapshot>)listener)(snapshot); }
                catch (Exception error) { ReportSubscriberError(error); }
    }
}
