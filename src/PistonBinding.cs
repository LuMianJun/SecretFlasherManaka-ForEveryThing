using System;
using System.Reflection;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace SecretFlasherManaka.ForEveryThing;

// This is a UI-side cached mode, not proof that the game's underlying actuator is moving.
// No game setters are hooked or guessed. Discovery is limited to one search per second.
internal sealed class PistonBinding
{
    public const string PanelName = "ExposureUnnoticed2.ObjectUI.InGame.VIbeStatePanel.VibeStatePanelView";
    private readonly Type _type;
    private readonly Il2CppSystem.Type _nativeType;
    private readonly ConstructorInfo _wrapper;
    private readonly Func<object, object?> _readMode;
    private MonoBehaviour? _panel;
    private float _nextFindAt;
    private bool _wasActive;
    private int _eligibleAfterFrame = -1;

    public PistonBinding()
    {
        _type = AccessTools.TypeByName(PanelName) ??
            Assembly.Load("Assembly-CSharp").GetType(PanelName, throwOnError: false) ??
            throw new MissingMemberException("The exact piston UI panel type is unavailable.");
        if (!typeof(MonoBehaviour).IsAssignableFrom(_type))
            throw new InvalidOperationException("The piston UI panel is not a compatible MonoBehaviour.");
        _nativeType = Il2CppType.From(_type);
        _wrapper = _type.GetConstructor(new[] { typeof(IntPtr) }) ??
            throw new MissingMethodException("The piston panel interop wrapper constructor is unavailable.");
        _readMode = InstanceReader(_type, "currentPistonMode", out Type valueType);
        // The reference directly returns this member as int?; no game enum type was established.
        if (valueType != typeof(int))
            throw new InvalidOperationException("The cached piston mode is not the expected Int32 member.");
    }

    public void Invalidate()
    {
        _panel = null;
        _wasActive = false;
        _eligibleAfterFrame = Time.frameCount;
        // Do not reset _nextFindAt: rapid scene/resume events must not defeat the search rate limit.
    }

    public bool Observe(int frame, out PistonMode mode, out GameStateReason reason)
    {
        mode = PistonMode.Unknown;
        reason = GameStateReason.SourceUnavailable;
        if (_panel && !_panel.isActiveAndEnabled)
        {
            // Retain a disabled cached panel to detect its re-enable without searching every frame.
            _wasActive = false;
            reason = GameStateReason.SourceDisabled;
        }
        if (!_panel || !_panel.isActiveAndEnabled)
        {
            if (Time.realtimeSinceStartup < _nextFindAt) return false;
            _nextFindAt = Time.realtimeSinceStartup + 1f;
            MonoBehaviour? candidate = null;
            foreach (UnityEngine.Object item in UnityEngine.Object.FindObjectsOfType(_nativeType))
            {
                if (!item) continue;
                // Find returns base Unity wrappers; build the exact generated wrapper once on recovery,
                // so cached FieldInfo/PropertyInfo has a matching CLR target on subsequent frames.
                var panel = (MonoBehaviour)_wrapper.Invoke(new object[] { item.Pointer });
                if (!panel || !panel.isActiveAndEnabled) continue;
                if (candidate) { _panel = null; _wasActive = false; return false; } // Never guess between panels.
                candidate = panel;
            }
            _panel = candidate;
            _wasActive = false;
            if (!_panel) return false;
        }
        if (!_wasActive)
        {
            _wasActive = true;
            _eligibleAfterFrame = frame;
            return false; // Newly found/re-enabled UI must survive into a later observed frame.
        }
        if (frame <= _eligibleAfterFrame) return false;
        object? value = _readMode(_panel);
        if (value is null) { reason = GameStateReason.InvalidValue; return false; }
        int raw = Convert.ToInt32(value);
        mode = raw switch { 0 => PistonMode.Off, 1 => PistonMode.Slow, 2 => PistonMode.Medium, 3 => PistonMode.Fast, _ => PistonMode.Unknown };
        reason = mode == PistonMode.Unknown ? GameStateReason.InvalidValue : GameStateReason.Normal;
        return mode != PistonMode.Unknown;
    }

    private static Func<object, object?> InstanceReader(Type type, string name, out Type valueType)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        PropertyInfo? property = type.GetProperty(name, flags);
        if (property is not null && property.GetIndexParameters().Length == 0 && property.GetGetMethod(true) is { IsStatic: false })
        { valueType = property.PropertyType; return target => property.GetValue(target); }
        FieldInfo? field = type.GetField(name, flags);
        if (field is not null) { valueType = field.FieldType; return target => field.GetValue(target); }
        throw new MissingMemberException("The exact cached piston mode member is unavailable.");
    }
}
