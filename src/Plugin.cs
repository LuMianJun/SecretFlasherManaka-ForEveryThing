using System;
using System.Reflection;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SecretFlasherManaka.ForEveryThing;

[BepInPlugin(Id, "SecretFlasherManaka For EveryThing", "1.1.0")]
public sealed class Plugin : BasePlugin
{
    // Stable technical ID: display/assembly renaming must not change this dependency key.
    public const string Id = "local.seleka.gamesignals";
    internal static Plugin? Current;
    private GameBinding? _binding;
    private PistonBinding? _pistonBinding;
    private string _lastPistonDiagnostic = "";
    private Harmony? _harmony;
    private GameSignalLifecycle? _lifecycle;
    private MonoBehaviour? _observedLeader;
    private int _observedFrame = -1, _scene, _blockedThroughFrame = -1;
    private GameStrength _observedStrength = GameStrength.Off;
    private bool _closing, _paused;
    private bool _sourceEnabled = true;
    private long _hookCalls, _acceptedHookCalls, _lateTicks;
    private int _lastHookFrame = -1;
    private float _nextDiagnosticAt;
    private string _lastGameDiagnostic = "";

    public override void Load()
    {
        if (Current is not null) throw new InvalidOperationException("A game-signal plugin instance is already active.");
        try
        {
            // Incompatible game versions fail closed; this plugin never opens hardware.
            _binding = new GameBinding();
            try { _pistonBinding = new PistonBinding(); }
            catch (Exception error)
            {
                // Piston compatibility is independent: an unavailable panel must not disable vibration.
                Log.LogWarning("Piston UI binding unavailable: " + error.GetType().Name + ". Stretch stays invalid/stopped.");
            }
            _scene = SceneManager.GetActiveScene().handle;
            Current = this;
            GameStateHub.SubscriberError = error => Log.LogWarning("Game state subscriber failed: " + error.GetType().Name);
            PistonStateHub.SubscriberError = error => Log.LogWarning("Piston state subscriber failed: " + error.GetType().Name);
            _harmony = new Harmony(Id);
            MethodInfo postfix = typeof(Plugin).GetMethod(nameof(AfterControllerUpdate), BindingFlags.Static | BindingFlags.NonPublic)!;
            _harmony.Patch(_binding.Update, postfix: new HarmonyMethod(postfix));
            _lifecycle = AddComponent<GameSignalLifecycle>();
            _lifecycle.Bind(this);
            SetSourceEnabled(_lifecycle.isActiveAndEnabled);
            Log.LogInfo("SecretFlasherManaka For EveryThing 1.1.0: actual Leader Update hook retained; cached piston UI observed in the same LateUpdate. StateChanged is change-only; Observed is a freshness heartbeat.");
        }
        catch (Exception error)
        {
            Log.LogError("Game signal load failed: " + error.GetType().Name + ". No hardware is owned by this plugin.");
            BeginShutdown();
            throw;
        }
    }

    // Harmony's object instance avoids a compile-time dependency on redistributed game assemblies.
    private static void AfterControllerUpdate(object __instance)
    {
        Plugin? plugin = Current;
        if (plugin is null || plugin._closing || plugin._binding is null) return;
        try
        {
            plugin._hookCalls++;
            plugin._lastHookFrame = Time.frameCount;
            if (plugin._hookCalls == 1)
                plugin.Log.LogInfo("[Game] Update postfix reached; CLR instance type=" + (__instance?.GetType().FullName ?? "null"));
            MonoBehaviour? instance = __instance as MonoBehaviour;
            MonoBehaviour? leader = plugin._binding.Leader;
            if (!instance || !leader || instance != leader || !leader.isActiveAndEnabled) return;
            plugin._acceptedHookCalls++;
            plugin._observedLeader = leader;
            plugin._observedFrame = Time.frameCount;
            plugin._observedStrength = plugin._binding.ActualStrength();
        }
        catch (Exception error) { plugin.FailClosed(error); }
    }

    // Unity calls this on the main thread after Update. Background code receives primitives only.
    internal void Tick()
    {
        if (_closing || _binding is null) return;
        try
        {
            _lateTicks++;
            if (_lateTicks == 1) Log.LogInfo("[Game] Lifecycle LateUpdate reached.");
            int frame = Time.frameCount;
            int scene = SceneManager.GetActiveScene().handle;
            if (scene != _scene)
            {
                _scene = scene;
                _blockedThroughFrame = frame;
                _observedFrame = -1;
                _pistonBinding?.Invalidate();
                PublishGlobalStop(GameStateReason.SceneChanged);
                return;
            }
            MonoBehaviour? leader = _binding.Leader;
            bool valid = _sourceEnabled && !_paused && frame > _blockedThroughFrame && _observedFrame == frame && leader &&
                leader.isActiveAndEnabled && _observedLeader && leader == _observedLeader;
            GameStateHub.Publish(valid && _observedStrength != GameStrength.Unknown, valid ? _observedStrength : GameStrength.Unknown,
                valid ? (_observedStrength == GameStrength.Unknown ? GameStateReason.InvalidValue : GameStateReason.Normal) : (!_sourceEnabled ? GameStateReason.SourceDisabled : (_paused ? GameStateReason.Paused : GameStateReason.LeaderUnavailable)));
            ObservePiston(scene, frame);
            ReportGameObservation(valid, leader && leader.isActiveAndEnabled);
        }
        catch (Exception error) { FailClosed(error); }
    }

    private void ObservePiston(int scene, int frame)
    {
        bool valid = false;
        PistonMode mode = PistonMode.Unknown;
        GameStateReason reason = !_sourceEnabled ? GameStateReason.SourceDisabled :
            _paused ? GameStateReason.Paused : GameStateReason.SourceUnavailable;
        if (_sourceEnabled && !_paused && frame > _blockedThroughFrame && _pistonBinding is not null)
        {
            try { valid = _pistonBinding.Observe(frame, out mode, out reason); }
            catch (Exception error)
            {
                Log.LogWarning("Piston observation failed: " + error.GetType().Name + ". Disabling piston observation for this session.");
                _pistonBinding = null;
                reason = GameStateReason.SourceUnavailable;
            }
        }
        PistonStateHub.Publish(valid, valid ? mode : PistonMode.Unknown, reason);
        string diagnostic = $"active={valid}, pistonMode={mode}, reason={reason}";
        if (diagnostic != _lastPistonDiagnostic)
        {
            _lastPistonDiagnostic = diagnostic;
            Log.LogInfo("[Game/Piston UI cache] " + diagnostic);
        }
    }

    private void PublishGlobalStop(GameStateReason reason)
    {
        GameStateHub.Publish(false, GameStrength.Unknown, reason, forceStop: true);
        PistonStateHub.Publish(false, PistonMode.Unknown, reason, forceStop: true);
    }

    private void ReportGameObservation(bool valid, bool enabledLeader)
    {
        string state = $"active={valid}, enabledLeader={enabledLeader}, lastObserved={_observedStrength}, paused={_paused}";
        float now = Time.realtimeSinceStartup;
        if (state == _lastGameDiagnostic && now < _nextDiagnosticAt) return;
        _lastGameDiagnostic = state;
        _nextDiagnosticAt = now + 5f;
        string hookGap = _lastHookFrame < 0 ? "never" : (Time.frameCount - _lastHookFrame).ToString();
        string acceptedGap = _observedFrame < 0 ? "never" : (Time.frameCount - _observedFrame).ToString();
        Log.LogInfo($"[Game] {state}; postfix={_hookCalls}, leaderAccepted={_acceptedHookCalls}, late={_lateTicks}, hookFrameGap={hookGap}, acceptedFrameGap={acceptedGap}");
    }

    internal void Pause(bool paused)
    {
        if (_closing) return;
        _paused = paused;
        _observedFrame = -1;
        _blockedThroughFrame = Time.frameCount;
        _pistonBinding?.Invalidate();
        if (paused) PublishGlobalStop(GameStateReason.Paused);
    }

    internal void SetSourceEnabled(bool enabled)
    {
        if (_closing) return;
        _sourceEnabled = enabled;
        _observedFrame = -1;
        _blockedThroughFrame = Time.frameCount;
        _pistonBinding?.Invalidate();
        if (!enabled) PublishGlobalStop(GameStateReason.SourceDisabled);
    }

    private void FailClosed(Exception error)
    {
        Log.LogError("Game observation failed: " + error.GetType().Name + ". Disabling this game-signal session.");
        BeginShutdown();
    }

    internal void BeginShutdown()
    {
        if (_closing) return;
        _closing = true;
        _pistonBinding?.Invalidate();
        PublishGlobalStop(GameStateReason.ShuttingDown);
        Current = null;
        try { _harmony?.UnpatchSelf(); } catch { Log.LogWarning("Hook cleanup could not be confirmed."); }
        if (_lifecycle) UnityEngine.Object.Destroy(_lifecycle);
        GameStateHub.SubscriberError = null;
        PistonStateHub.SubscriberError = null;
    }

    public override bool Unload()
    {
        BeginShutdown();
        return true; // No device I/O or background work belongs to the game plugin.
    }

}

public sealed class GameSignalLifecycle : MonoBehaviour
{
    private Plugin? _owner;
    public GameSignalLifecycle(IntPtr pointer) : base(pointer) { }
    [HideFromIl2Cpp]
    internal void Bind(Plugin owner) => _owner = owner;
    public void LateUpdate() => _owner?.Tick();
    public void OnEnable() => _owner?.SetSourceEnabled(true);
    public void OnDisable() => _owner?.SetSourceEnabled(false);
    public void OnApplicationPause(bool paused) => _owner?.Pause(paused);
    public void OnApplicationQuit() => _owner?.BeginShutdown();
    public void OnDestroy() => _owner?.BeginShutdown();
}
