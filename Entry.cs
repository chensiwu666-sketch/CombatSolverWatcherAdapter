using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using CombatSolver.Engine.InCombat.Mirrors.Cards.OnPlay;
using MegaCrit.Sts2.Core.Modding;

namespace WatcherAdapter;

[ModInitializer("Initialize")]
public static class Entry
{
    internal const string Version = "0.11.0-batch10";
    // Built and verified against this exact CombatSolver build. A version change means the internal
    // mirror APIs we bind to may have moved, so we fail closed instead of predicting nonsense.
    internal const string ExpectedSolverVersion = "0.50.1.";
    private static readonly object LogLock = new object();
    internal static string LogPath;

    public static void Initialize()
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SlayTheSpire2", "logs");
            Directory.CreateDirectory(dir);
            LogPath = Path.Combine(dir, "WatcherAdapter.log");
        }
        catch
        {
            // ignore - logging is best effort
        }

        Log($"=== WatcherAdapter {Version} initializing ===");

        // All registration must finish before the solver captures its first combat root: the dispatch
        // cache never invalidates, so a late Register silently does nothing.
        try
        {
            Assembly solver = typeof(CardOnPlayMirrors).Assembly;
            string solverVersion = solver.GetName().Version?.ToString() ?? "<unknown>";
            Log($"CombatSolver assembly: {solverVersion} @ {solver.Location}");

            if (!solverVersion.StartsWith(ExpectedSolverVersion, StringComparison.Ordinal))
            {
                Log($"DISABLED: this adapter is built for CombatSolver {ExpectedSolverVersion}x, found {solverVersion}. "
                    + "Registration skipped on purpose: the internal mirror API may have changed and a partial "
                    + "adapter would produce confidently wrong routes.");
                return;
            }

            Log($"registry resolved: {CardOnPlayMirrors.Registry.GetType().FullName}");
            Capabilities.Probe();

            HookMirrors.RegisterAll();
            Cards.RegisterAll();
            CardsBatch3.RegisterAll();
            CardsBatch4.RegisterAll();
            CardsBatch5.RegisterAll();
            CardsBatch6.RegisterAll();
            CardsBatch7.RegisterAll();
            CardsBatch8.RegisterAll();
            CardsBatch9.RegisterAll();
            CardsBatch10.RegisterAll();
            DrawMirrors.RegisterAll();
            Playability.RegisterAll();
            PowerMirrors.RegisterAll();
            Prewarm.RegisterAll();

            VerifyRegistrations();
            Log("registration finished");
        }
        catch (Exception ex)
        {
            Log("FATAL during registration: " + ex);
        }
    }


    /// <summary>
    /// Reads the registry back and counts the mirrors actually registered for Watcher types. A swallowed
    /// <c>Register</c> (duplicate type, unresolvable override) would otherwise leave a card looking
    /// mirrored while it silently is not.
    /// </summary>
    private static void VerifyRegistrations()
    {
        try
        {
            var descriptor = CardOnPlayMirrors.Registry.DescribeMirrorSupport();
            int watcher = 0;
            foreach (var registration in descriptor.Registrations)
            {
                if (registration.ReceiverType.Namespace == "WatcherMod")
                {
                    watcher++;
                }
            }

            Log($"self-check: {watcher} Watcher card mirror(s) present in the registry");
            if (watcher < 75)
            {
                Log($"SELF-CHECK WARNING: expected 75 card mirrors, registry reports {watcher}.");
            }
        }
        catch (Exception ex)
        {
            Log("self-check failed to read the registry: " + ex.Message);
        }
    }

    internal static readonly HashSet<string> LoggedOnce = new HashSet<string>(StringComparer.Ordinal);

    internal static void LogOnce(string key, string message)
    {
        lock (LoggedOnce)
        {
            if (!LoggedOnce.Add(key))
            {
                return;
            }
        }
        Log(message);
    }

    internal static void Log(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        try
        {
            if (!string.IsNullOrEmpty(LogPath))
            {
                lock (LogLock)
                {
                    File.AppendAllText(LogPath, line + Environment.NewLine, Encoding.UTF8);
                }
            }
        }
        catch
        {
            // ignore
        }

        try
        {
            MegaCrit.Sts2.Core.Logging.Log.Info("[WatcherAdapter] " + message);
        }
        catch
        {
            // ignore
        }
    }
}
