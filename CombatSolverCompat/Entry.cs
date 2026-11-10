using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace CombatSolverCompat;

/// <summary>
/// Best-effort compatibility layer for Combat Solver (战斗路线求解器).
/// Combat Solver refuses to run when it detects any foreign "gameplay" mod that
/// Harmony-patches the game's card / monster / hook surface. This mod makes those
/// audits non-fatal so the solver can still run in a best-effort mode.
/// </summary>
[ModInitializer("Initialize")]
public static class Entry
{
    internal const string Version = "1.4.0";
    internal const string HarmonyId = "chensiwu.combatsolvercompat";

    internal static readonly Harmony Harmony = new Harmony(HarmonyId);
    internal static CompatConfig Config = new CompatConfig();
    internal static string UserDir = "";
    internal static string LogPath = "";
    private static bool _applied;

    private static readonly ConcurrentDictionary<MethodBase, Func<object, bool>> HookGates = new ConcurrentDictionary<MethodBase, Func<object, bool>>();
    private static readonly HashSet<string> SwallowedHooks = new HashSet<string>(StringComparer.Ordinal);
    private static readonly object SwallowLock = new object();
    private static readonly Func<object, bool> AllowAll = _ => true;

    public static void Initialize()
    {
        try
        {
            UserDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SlayTheSpire2");
            LogPath = Path.Combine(UserDir, "logs", "CombatSolverCompat.log");
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            Config = CompatConfig.Load(Path.Combine(UserDir, "combat_solver_compat.json"));
        }
        catch (Exception ex)
        {
            Log("init logging failed: " + ex);
        }

        Log($"=== CombatSolverCompat {Version} initializing ===");
        Log("config = " + Config.ToJson());

        var asm = FindCombatSolver();
        if (asm != null)
        {
            Apply(asm);
        }
        else
        {
            Log("CombatSolver assembly not loaded yet - waiting for AssemblyLoad event.");
            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
        }
    }

    private static void OnAssemblyLoad(object? sender, AssemblyLoadEventArgs e)
    {
        if (string.Equals(e.LoadedAssembly.GetName().Name, "CombatSolver", StringComparison.OrdinalIgnoreCase))
        {
            AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
            Apply(e.LoadedAssembly);
        }
    }

    private static Assembly? FindCombatSolver()
        => AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => string.Equals(a.GetName().Name, "CombatSolver", StringComparison.OrdinalIgnoreCase));

    private static void Apply(Assembly asm)
    {
        if (_applied) return;
        _applied = true;
        Log($"CombatSolver assembly found: {asm.GetName().Version} @ {asm.Location}");

        try
        {
            var auditType = asm.GetType("CombatSolver.PredictionModPatchAudit", throwOnError: false);
            var hookType = asm.GetType("CombatSolver.PredictionModHookSubscriberCapture", throwOnError: false);
            if (auditType == null)
            {
                Log("ERROR: CombatSolver.PredictionModPatchAudit not found. This Combat Solver build changed its internals; compat patches were not applied.");
                return;
            }

            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var describe = auditType.GetMethod("TryDescribeForeignPatch", flags);
            var monsters = auditType.GetMethod("ValidateMonsterModels", flags);
            var reject = auditType.GetMethod("RejectForeignPatches", flags);
            var validateCards = auditType.GetMethod("ValidateCardOnPlay", flags);
            var validateSubscriber = hookType?.GetMethod("ValidateSubscriber", flags);

            var transpiler = new HarmonyMethod(typeof(Patches).GetMethod(nameof(Patches.NoForeignPatch), BindingFlags.Static | BindingFlags.Public));
            var skip = new HarmonyMethod(typeof(Patches).GetMethod(nameof(Patches.SkipVoid), BindingFlags.Static | BindingFlags.Public));
            var subscriberPrefix = new HarmonyMethod(typeof(Patches).GetMethod(nameof(Patches.SubscriberGate), BindingFlags.Static | BindingFlags.Public));

            TryPatch("TryDescribeForeignPatch", describe, Config.IgnoreForeignCardPatches,
                m => Harmony.Patch(m, transpiler: transpiler),
                "foreign Harmony patches are ignored by the card audit");
            TryPatch("ValidateMonsterModels", monsters, Config.AllowModdedMonsters,
                m => Harmony.Patch(m, prefix: skip),
                "modded / patched monster AI no longer blocks the solver");
            TryPatch("RejectForeignPatches", reject, Config.IgnoreForeignCardPatches,
                m => Harmony.Patch(m, prefix: skip),
                "foreign patches on intent / move-state-machine code are ignored");
            TryPatch("ValidateCardOnPlay", validateCards, Config.IgnoreForeignCardPatches,
                m => Harmony.Patch(m, prefix: skip),
                "standalone card OnPlay validation is skipped");
            TryPatch("ValidateSubscriber", validateSubscriber, Config.AllowForeignHookSubscribers,
                m => Harmony.Patch(m, prefix: subscriberPrefix),
                $"ModHelper subscribers from other gameplay mods are tolerated (Loadout stays strict: {Config.KeepLoadoutStrict})");

            PatchHookMirrors(asm);

            PatchOriginalCallSites(asm);

            ReportForeignPatches();

            AuditVanillaPatches();

            if (describe != null)
            {
                SelfTest(describe);
            }
        }
        catch (Exception ex)
        {
            Log("ERROR while applying patches: " + ex);
        }
    }

    private static void TryPatch(string label, MethodInfo? target, bool enabled, Action<MethodInfo> patch, string successNote)
    {
        if (target == null)
        {
            Log($"WARN: {label} not found (Combat Solver internals changed?) - this gate stays active.");
            return;
        }
        if (!enabled)
        {
            Log($"skip (disabled in config): {label}");
            return;
        }
        try
        {
            patch(target);
            Log($"patched {Name(target)} -> {successNote}");
        }
        catch (Exception ex)
        {
            Log($"ERROR patching {label}: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string Name(MethodBase m) => (m.DeclaringType?.FullName ?? "?") + "." + m.Name;

    /// <summary>Verifies that the transpiler really replaced the audit body.</summary>
    private static void SelfTest(MethodInfo describe)
    {
        try
        {
            object? result = describe.Invoke(null, new object?[] { null, null });
            if (result == null)
            {
                Log("SELFTEST OK: TryDescribeForeignPatch(null, null) returned no patch -> audit gate is neutralized.");
            }
            else
            {
                Log($"SELFTEST FAIL: TryDescribeForeignPatch returned {result} (gate still active?).");
            }
        }
        catch (TargetInvocationException tie)
        {
            Log("SELFTEST FAIL: gate not neutralized, original body still runs: " + tie.InnerException);
        }
        catch (Exception ex)
        {
            Log("SELFTEST FAIL: " + ex);
        }
    }

    /// <summary>Logs which game methods are patched by which mod, so results can be audited.</summary>
    private static void ReportForeignPatches()
    {
        try
        {
            var owners = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var method in Harmony.GetAllPatchedMethods())
            {
                var info = Harmony.GetPatchInfo(method);
                if (info == null) continue;
                foreach (var patch in info.Owners ?? new System.Collections.ObjectModel.ReadOnlyCollection<string>(new List<string>()))
                {
                    if (patch.StartsWith(HarmonyId, StringComparison.OrdinalIgnoreCase)) continue;
                    if (patch.StartsWith("CombatSolver", StringComparison.OrdinalIgnoreCase)) continue;
                    owners.TryGetValue(patch, out int c);
                    owners[patch] = c + 1;
                }
            }

            if (owners.Count == 0)
            {
                Log("foreign patch report: no other mods patch game code.");
                return;
            }

            Log($"foreign patch report: {owners.Count} patching mod(s) detected; their effects are invisible to Combat Solver in this mode:");
            foreach (var kv in owners.OrderByDescending(k => k.Value).Take(40))
            {
                Log($"  - {kv.Key}: {kv.Value} patched method(s)");
            }
        }
        catch (Exception ex)
        {
            Log("foreign patch report failed: " + ex.Message);
        }
    }

    /// <summary>
    /// Patches every hook mirror dispatcher (class named *Mirrors with a static Registry field and a
    /// void Invoke(TModel, TContext) method) so that "this model type has no mirror" is logged
    /// instead of aborting the whole search.
    /// </summary>
    private static void PatchHookMirrors(Assembly asm)
    {
        if (!Config.AllowUnmirroredHooks)
        {
            Log("skip (disabled in config): hook mirror gates");
            return;
        }

        int patched = 0;
        int failed = 0;
        var voidPrefix = new HarmonyMethod(typeof(Patches).GetMethod(nameof(Patches.HookInvokeGate), BindingFlags.Static | BindingFlags.Public));
        var amountPrefix = new HarmonyMethod(typeof(Patches).GetMethod(nameof(Patches.HookAmountGate), BindingFlags.Static | BindingFlags.Public));

        foreach (Type type in asm.GetTypes())
        {
            if (!type.Name.EndsWith("Mirrors", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                bool returnsVoid = method.ReturnType == typeof(void);
                bool returnsDecimal = method.ReturnType == typeof(decimal);
                if (!returnsVoid && !returnsDecimal)
                {
                    continue;
                }

                ParameterInfo[] ps = method.GetParameters();
                if (ps.Length < 2 || ps.Length > 3 || ps[0].ParameterType.IsValueType || ps[0].ParameterType.IsPrimitive)
                {
                    continue;
                }

                FieldInfo? registry = ResolveRegistry(type, method.Name);
                if (registry == null || !registry.FieldType.IsGenericType || registry.FieldType.GetGenericArguments().Length != 2)
                {
                    continue;
                }

                // Only dispatchers whose first argument is a model handled by that registry
                // (this skips e.g. CardOnPlayMirrors.Invoke(simulator, card, play)).
                Type baseType = registry.FieldType.GetGenericArguments()[0];
                if (!baseType.IsAssignableFrom(ps[0].ParameterType))
                {
                    continue;
                }

                try
                {
                    Harmony.Patch(method, prefix: returnsVoid ? voidPrefix : amountPrefix);
                    patched++;
                }
                catch (Exception ex)
                {
                    failed++;
                    Log($"ERROR patching hook mirror {Name(method)}: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        Log($"patched {patched} hook mirror dispatcher(s) -> unmirrored (modded) relics/powers/modifiers are skipped instead of aborting the search"
            + (failed > 0 ? $", {failed} failed" : ""));
    }

    private static FieldInfo? ResolveRegistry(Type mirrorType, string methodName)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        return mirrorType.GetField(methodName + "Registry", flags)
            ?? mirrorType.GetField("Registry", flags);
    }

    internal static Func<object, bool> GetGate(MethodBase target) => HookGates.GetOrAdd(target, BuildGate);

    /// <summary>Builds "does this registry have a handler for this model type" as a compiled delegate.</summary>
    private static Func<object, bool> BuildGate(MethodBase target)
    {
        try
        {
            Type? mirrorType = target.DeclaringType;
            if (mirrorType == null)
            {
                return AllowAll;
            }

            FieldInfo? registryField = ResolveRegistry(mirrorType, target.Name);
            if (registryField == null)
            {
                return AllowAll;
            }

            Type registryType = registryField.FieldType;
            if (!registryType.IsGenericType || registryType.GetGenericArguments().Length != 2)
            {
                return AllowAll;
            }

            MethodInfo? hasHandler = registryType
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == "HasRegisteredHandler" && m.GetParameters().Length == 1);
            if (hasHandler == null)
            {
                // No cheap predicate: fall back to letting the original run (it may still throw).
                return AllowAll;
            }

            ParameterExpression receiver = Expression.Parameter(typeof(object), "listener");
            Expression call = Expression.Call(
                Expression.Convert(Expression.Field(null, registryField), registryType),
                hasHandler,
                Expression.Convert(receiver, hasHandler.GetParameters()[0].ParameterType));
            return Expression.Lambda<Func<object, bool>>(call, receiver).Compile();
        }
        catch (Exception ex)
        {
            Log($"WARN: could not build hook gate for {target.DeclaringType?.Name}.{target.Name}: {ex.Message}");
            return AllowAll;
        }
    }

    private static Assembly? _gameAssembly;

    internal static Assembly? GameAssembly =>
        _gameAssembly ??= AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => string.Equals(a.GetName().Name, "sts2", StringComparison.OrdinalIgnoreCase));

    /// <summary>True when the model comes from the vanilla game assembly (safe to execute real code).</summary>
    internal static bool IsVanilla(object instance)
    {
        Assembly? game = GameAssembly;
        return game != null && instance.GetType().Assembly == game;
    }

    /// <summary>
    /// Patches the private "InvokeOriginal*" helpers so a non-vanilla model never executes its own code
    /// inside the simulator. Those helpers are only reached when no mirror is registered, i.e. exactly
    /// the mod-content case, and running third-party code there crashes the search (thread-unsafe
    /// caches, Godot/UI access) or silently diverges.
    /// </summary>
    private static void PatchOriginalCallSites(Assembly asm)
    {
        if (!Config.AllowUnmirroredHooks)
        {
            Log("skip (disabled in config): original-implementation call sites");
            return;
        }

        var decimalPrefix = new HarmonyMethod(typeof(Patches).GetMethod(nameof(Patches.OriginalDecimalGate), BindingFlags.Static | BindingFlags.Public));
        var intPrefix = new HarmonyMethod(typeof(Patches).GetMethod(nameof(Patches.OriginalIntGate), BindingFlags.Static | BindingFlags.Public));

        int patched = 0;
        int skipped = 0;
        foreach (Type type in asm.GetTypes())
        {
            if (!type.Name.EndsWith("Mirrors", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!method.Name.StartsWith("InvokeOriginal", StringComparison.Ordinal) || method.GetParameters().Length != 2)
                {
                    continue;
                }

                try
                {
                    if (method.ReturnType == typeof(decimal))
                    {
                        Harmony.Patch(method, prefix: decimalPrefix);
                        patched++;
                    }
                    else if (method.ReturnType == typeof(int))
                    {
                        Harmony.Patch(method, prefix: intPrefix);
                        patched++;
                    }
                    else
                    {
                        skipped++;
                        Log($"NOTE: {Name(method)} returns {method.ReturnType.Name}; not gated - third-party code may still run there.");
                    }
                }
                catch (Exception ex)
                {
                    Log($"ERROR patching original call site {Name(method)}: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        Log($"patched {patched} original-implementation call site(s) -> third-party hook code is no longer executed inside the search (vanilla code still runs unchanged)"
            + (skipped > 0 ? $", {skipped} left as-is" : ""));
    }

    /// <summary>Reads the first readable numeric property/field from a context object.</summary>
    /// <summary>
    /// Best-effort read of the first decimal/int property on a hook context. Used only when the hook
    /// family is unknown, to avoid substituting a confident 0 for a value we simply could not name.
    /// </summary>
    internal static decimal ReadFirstNumeric(object? source)
    {
        if (source == null)
        {
            return 0m;
        }

        try
        {
            foreach (PropertyInfo property in source.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!property.CanRead || property.GetIndexParameters().Length != 0)
                {
                    continue;
                }

                Type t = property.PropertyType;
                if (t != typeof(decimal) && t != typeof(int) && t != typeof(long) && t != typeof(float) && t != typeof(double))
                {
                    continue;
                }
                try
                {
                    object? value = property.GetValue(source);
                    if (value != null)
                    {
                        return Convert.ToDecimal(value);
                    }
                }
                catch
                {
                    // try the next property
                }
            }
        }
        catch
        {
            // ignore
        }

        return 0m;
    }

    internal static decimal ReadNumber(object? source, params string[] names)
    {
        if (source == null)
        {
            return 0m;
        }

        Type type = source.GetType();
        foreach (string name in names)
        {
            try
            {
                PropertyInfo? property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null && property.CanRead)
                {
                    object? value = property.GetValue(source);
                    if (value != null)
                    {
                        return Convert.ToDecimal(value);
                    }
                }

                FieldInfo? field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    object? value = field.GetValue(source);
                    if (value != null)
                    {
                        return Convert.ToDecimal(value);
                    }
                }
            }
            catch
            {
                // try the next candidate
            }
        }

        return 0m;
    }

    private static readonly string[] KnownLibraryOwners =
    {
        "CombatSolver", "CombatSolverCompat", "WatcherAdapter", "BaseLib", "RitsuLib", "OLC."
    };

    /// <summary>
    /// Answers "is this test environment clean?": lists mods that Harmony-patch VANILLA game code.
    /// Those changes are invisible to the solver (we skip foreign patches), so they produce wrong
    /// predictions that would be misread as adapter bugs during verification.
    /// </summary>
    private static void AuditVanillaPatches()
    {
        try
        {
            var byOwner = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (MethodBase method in Harmony.GetAllPatchedMethods())
            {
                Type? declaring = method.DeclaringType;
                if (declaring == null)
                {
                    continue;
                }

                if (!string.Equals(declaring.Assembly.GetName().Name, "sts2", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var info = Harmony.GetPatchInfo(method);
                if (info == null)
                {
                    continue;
                }

                var owners = new List<string>();
                if (info.Prefixes != null) foreach (Patch p in info.Prefixes) owners.Add(p.owner ?? "");
                if (info.Postfixes != null) foreach (Patch p in info.Postfixes) owners.Add(p.owner ?? "");
                if (info.Transpilers != null) foreach (Patch p in info.Transpilers) owners.Add(p.owner ?? "");
                if (info.Finalizers != null) foreach (Patch p in info.Finalizers) owners.Add(p.owner ?? "");

                foreach (string owner in owners)
                {
                    if (string.IsNullOrEmpty(owner))
                    {
                        continue;
                    }

                    bool known = false;
                    foreach (string prefix in KnownLibraryOwners)
                    {
                        // owner ids are not always prefixed with the mod name (e.g. "com.ritsukage.sts2-RitsuLib.framework-core")
                        if (owner.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            known = true;
                            break;
                        }
                    }
                    if (known)
                    {
                        continue;
                    }

                    byOwner.TryGetValue(owner, out int count);
                    byOwner[owner] = count + 1;
                }
            }

            if (byOwner.Count == 0)
            {
                Log("VANILLA AUDIT: CLEAN - no non-library mod patches vanilla combat code. Test results are trustworthy.");
                return;
            }

            Log($"VANILLA AUDIT: WARNING - {byOwner.Count} non-library mod(s) patch VANILLA game code; the solver ignores those changes, so predictions may be off in a way that looks like an adapter bug:");
            foreach (KeyValuePair<string, int> entry in byOwner.OrderByDescending(k => k.Value).Take(20))
            {
                Log($"  - {entry.Key}: {entry.Value} vanilla method(s)");
            }
        }
        catch (Exception ex)
        {
            Log("vanilla audit failed: " + ex.Message);
        }
    }

    internal static void ReportSwallowedHook(string hookClass, string? modelType)
    {
        string key = hookClass + "|" + modelType;
        bool first;
        lock (SwallowLock)
        {
            first = SwallowedHooks.Add(key);
        }
        if (first)
        {
            Log($"IGNORED hook {hookClass} for {modelType} - no prediction mirror, effect omitted (search continues)");
        }
    }

    internal static void Log(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        try
        {
            if (LogPath.Length > 0) File.AppendAllText(LogPath, line + Environment.NewLine, Encoding.UTF8);
        }
        catch
        {
            // ignored
        }

        try
        {
            MegaCrit.Sts2.Core.Logging.Log.Info("[CombatSolverCompat] " + message);
        }
        catch
        {
            // ignored
        }
    }
}
