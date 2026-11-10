using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace CombatSolverCompat;

internal static class Patches
{
    /// <summary>
    /// Replaces the body of CombatSolver.PredictionModPatchAudit.TryDescribeForeignPatch with
    /// "return default" (an empty Nullable&lt;ForeignPatch&gt;), which makes the whole foreign
    /// patch audit report "no foreign patch" for every mod.
    /// </summary>
    public static IEnumerable<CodeInstruction> NoForeignPatch(IEnumerable<CodeInstruction> instructions, MethodBase original)
    {
        Type returnType = (original as MethodInfo)?.ReturnType ?? typeof(object);
        Entry.Log($"transpiler: replacing body of {original.DeclaringType?.FullName}.{original.Name} (returns {returnType})");
        return new List<CodeInstruction>
        {
            new CodeInstruction(OpCodes.Ldnull),
            new CodeInstruction(OpCodes.Unbox_Any, returnType),
            new CodeInstruction(OpCodes.Ret)
        };
    }

    /// <summary>Skips a pure-validation method (prefix returning false = do not run the original).</summary>
    public static bool SkipVoid() => false;

    /// <summary>
    /// Tolerates ModHelper subscribers registered by other gameplay mods.
    /// Loadout keeps its original strict handling because Combat Solver ships a real adapter for it.
    /// </summary>
    public static bool SubscriberGate(object __0, string __1)
    {
        Type? type = __0?.GetType();
        string name = type?.FullName ?? "<null>";
        if (Entry.Config.KeepLoadoutStrict && name.StartsWith("Loadout.", StringComparison.Ordinal))
        {
            return true;
        }

        Entry.Log($"IGNORED ModHelper subscriber {name} (scope={__1}) - its effects are not modelled by the solver");
        return false;
    }

    /// <summary>
    /// Gate for the private "InvokeOriginal*" helpers: they run the REAL implementation of a hook for
    /// models that have no mirror (base behaviour for vanilla, arbitrary code for mods). Foreign code
    /// is not safe to run inside the parallel search (thread-unsafe pools / Godot / UI), so for
    /// non-vanilla models we return the unchanged value instead of executing it.
    /// </summary>
    public static bool OriginalDecimalGate(object __0, object __1, ref decimal __result, MethodBase __originalMethod)
    {
        if (__0 == null || Entry.IsVanilla(__0))
        {
            return true;
        }

        decimal identity = IdentityFor(__originalMethod, __1);
        __result = identity;

        Entry.ReportSwallowedHook(
            __originalMethod.DeclaringType?.Name + "." + __originalMethod.Name + "(identity=" + identity + ")",
            __0.GetType().FullName);
        return false;
    }

    /// <summary>
    /// The value a swallowed listener must return so the surrounding hook pass continues unchanged.
    /// Read off the solver's own call sites in HookMirrors:
    ///   * "*Multiplicative*" passes multiply the running value:  damage *= result  -> 1
    ///   * "ModifyDamageMirrors.*Additive*" adds a delta:         damage += result  -> 0
    ///   * every other decimal pass REPLACES the running value:   context.X = result -> the current X
    /// Getting this wrong is silent: an additive pass fed its own running value doubles the number once
    /// per skipped listener (a 13-damage enemy attack became 13 * 2^22), and a replacing pass fed 0
    /// zeroes the number (8-damage attack became 0).
    /// </summary>
    private static decimal IdentityFor(MethodBase method, object context)
    {
        string name = method.Name;
        if (name.Contains("Multiplicative", StringComparison.Ordinal))
        {
            return 1m;
        }

        if (name.Contains("Additive", StringComparison.Ordinal))
        {
            return 0m;
        }

        // Replacing passes: the context property that holds "the current value" differs per family.
        decimal known = Entry.ReadNumber(context, "Amount", "HitCount", "PlayCount", "Cost", "TriggerCount", "Count", "HpLost", "Damage", "Block");
        if (known != 0m)
        {
            return known;
        }

        // Last resort for a family whose context property we do not know by name: take its first
        // numeric property. Returning 0 there would silently zero the value (an 8-damage attack
        // became 0 in testing), so a best-effort read beats a confident zero.
        return Entry.ReadFirstNumeric(context);
    }

    /// <summary>Same as <see cref="OriginalDecimalGate"/> for int-returning helpers.</summary>
    public static bool OriginalIntGate(object __0, object __1, ref int __result, MethodBase __originalMethod)
    {
        if (__0 == null || Entry.IsVanilla(__0))
        {
            return true;
        }

        __result = (int)IdentityFor(__originalMethod, __1);
        Entry.ReportSwallowedHook(__originalMethod.DeclaringType?.Name + "." + __originalMethod.Name, __0.GetType().FullName);
        return false;
    }

    /// <summary>
    /// Gate for the "XxxMirrors" hook dispatchers (e.g. Invoke, Modify, AfterGain).
    /// The original throws IncompatibleGameplayModException (via ForContent) whenever a model type
    /// has no registered mirror - which is every relic/power/modifier added by another mod.
    /// Registered (vanilla / adapted) handlers still run untouched; only the "no mirror" case is
    /// swallowed and logged once per model type.
    /// </summary>
    public static bool HookInvokeGate(object __0, MethodBase __originalMethod)
    {
        Type? declaring = __originalMethod.DeclaringType;
        if (declaring == null || __0 == null)
        {
            return true;
        }

        Func<object, bool> gate = Entry.GetGate(__originalMethod);
        if (gate(__0))
        {
            return true;
        }

        Entry.ReportSwallowedHook(declaring.Name + "." + __originalMethod.Name, __0.GetType().FullName);
        return false;
    }

    /// <summary>
    /// Same gate for dispatchers that return a decimal. The substituted value must be the IDENTITY of
    /// that particular dispatch pass, not "whatever the context says":
    ///   - a multiplicative damage pass multiplies the amount by the listener result, so the identity is 1;
    ///   - an additive pass adds the listener result, so the identity is the incoming amount;
    ///   - anything else keeps the amount unchanged by returning 0.
    /// Returning <c>__1.Amount</c> for every decimal dispatcher (the old behaviour) fed the running
    /// damage amount back in as a multiplier, which is how a 13-damage enemy attack could be predicted
    /// as 13 * 2^22 in combat logs.
    /// </summary>
    public static bool HookAmountGate(object[] __args, ref decimal __result, MethodBase __originalMethod)
    {
        Type? declaring = __originalMethod.DeclaringType;
        if (declaring == null || __args.Length == 0 || __args[0] == null)
        {
            return true;
        }

        object model = __args[0];
        Func<object, bool> gate = Entry.GetGate(__originalMethod);
        if (gate(model))
        {
            return true;
        }

        object context = __args[__args.Length - 1];
        decimal identity = IdentityFor(__originalMethod, context);
        __result = identity;
        Entry.ReportSwallowedHook($"{declaring.Name}.{__originalMethod.Name}(identity={identity})", model.GetType().FullName);
        return false;
    }

}
