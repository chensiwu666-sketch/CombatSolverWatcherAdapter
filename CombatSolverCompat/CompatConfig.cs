using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CombatSolverCompat;

/// <summary>
/// User editable switches. Written to %APPDATA%\SlayTheSpire2\combat_solver_compat.json on first run.
/// </summary>
public sealed class CompatConfig
{
    public bool IgnoreForeignCardPatches { get; set; } = true;
    public bool AllowModdedMonsters { get; set; } = true;
    public bool AllowForeignHookSubscribers { get; set; } = true;
    public bool AllowUnmirroredHooks { get; set; } = true;
    public bool KeepLoadoutStrict { get; set; } = true;

    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static CompatConfig Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var loaded = JsonSerializer.Deserialize<CompatConfig>(File.ReadAllText(path), Options);
                if (loaded != null) return loaded;
            }
            var fresh = new CompatConfig();
            File.WriteAllText(path, JsonSerializer.Serialize(fresh, Options));
            return fresh;
        }
        catch (Exception ex)
        {
            Entry.Log("config load failed, using defaults: " + ex.Message);
            return new CompatConfig();
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, Options).Replace(Environment.NewLine, " ");
}
