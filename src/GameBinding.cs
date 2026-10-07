using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SecretFlasherManaka.ForEveryThing;

// IL2CPP-generated interop fields may be exposed as properties. Validate actual metadata at load.
internal sealed class GameBinding
{
    public const string ControllerName = "ExposureUnnoticed2.Object3D.AdultGoods.CommonVibratorController";
    private readonly Func<object?> _leader;
    private readonly Func<object?> _strength;
    public MethodInfo Update { get; }

    public GameBinding()
    {
        Type? type = AccessTools.TypeByName(ControllerName);
        if (type is null)
        {
            // Ask the existing loader for its generated interop assembly, never load arbitrary DLL paths.
            type = Assembly.Load("Assembly-CSharp").GetType(ControllerName, throwOnError: false);
        }
        if (type is null) throw new MissingMemberException("The exact game controller type is unavailable.");
        if (!typeof(MonoBehaviour).IsAssignableFrom(type))
            throw new InvalidOperationException("The game controller is not a compatible MonoBehaviour interop type.");
        _leader = StaticReader(type, "Leader", out Type leaderType);
        if (leaderType != type) throw new InvalidOperationException("Leader has an unexpected type.");
        _strength = StaticReader(type, "VibrationStrength", out Type strengthType);
        if (!strengthType.IsEnum || strengthType.FullName != "ExposureUnnoticed2.Master.AdultGoods.VibrationModeType")
            throw new InvalidOperationException("VibrationStrength has an unexpected enum type.");
        string[] names = { "Off", "Low", "High", "Random" };
        for (int n = 0; n < names.Length; n++)
            if (!Enum.IsDefined(strengthType, names[n]) || Convert.ToInt32(Enum.Parse(strengthType, names[n])) != n)
                throw new InvalidOperationException("The game enum mapping differs from the supplied source.");
        Update = type.GetMethod("Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null, types: Type.EmptyTypes, modifiers: null) ?? throw new MissingMethodException("The game Update method was not found.");
        if (Update.IsStatic || Update.ReturnType != typeof(void)) throw new InvalidOperationException("Unexpected Update signature.");
    }

    public MonoBehaviour? Leader => _leader() as MonoBehaviour;
    public GameStrength ActualStrength()
    {
        // UpdateRandomMode already resolves the global strength. Do not randomize or map Random to High.
        int value = Convert.ToInt32(_strength());
        return value switch { 0 => GameStrength.Off, 1 => GameStrength.Low, 2 => GameStrength.High, _ => GameStrength.Unknown };
    }

    private static Func<object?> StaticReader(Type type, string name, out Type valueType)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        PropertyInfo? property = type.GetProperty(name, flags);
        if (property is not null && property.GetIndexParameters().Length == 0 && property.GetGetMethod(true) is { IsStatic: true })
        { valueType = property.PropertyType; return () => property.GetValue(null); }
        FieldInfo? field = type.GetField(name, flags);
        if (field is not null) { valueType = field.FieldType; return () => field.GetValue(null); }
        throw new MissingMemberException("Required static game member is unavailable: " + name);
    }
}
