using System;
using MegaCrit.Sts2.Core.ValueProps;
using CombatSolver.Engine.InCombat.Mirrors.Hooks.TurnEnd;
using CombatSolver.Engine.InCombat.Mirrors.Hooks.TurnStart;
using CombatSolver.Engine.InCombat.Mirrors.Hooks.Resources;
using WatcherMod;

namespace WatcherAdapter;

/// <summary>
/// Mirrors for the Watcher's persistent powers.
///
/// A power whose effect never materialises in the branch makes its card look worthless, so the search
/// routes around it. These entries give the powers their per-turn behaviour back. Each one follows the
/// mod's own hook body line by line; where the solver has no matching entry point (the regular
/// AfterSideTurnEnd pass) the effect is attached to the late pass and the branch is flagged.
/// </summary>
internal static class PowerMirrors
{
    internal static void RegisterAll()
    {
        RegisterLikeWater();
        RegisterSimmeringFury();
        RegisterDevaForm();
        RegisterDevotion();
        LogUnavailableSideTurnEndEffects();
    }

    /// <summary>Like Water: at the end of its owner's side turn, gain Amount block while in Calm.</summary>
    private static void RegisterLikeWater() => Guard("LikeWaterPower (block at side turn end while in Calm)", () =>
        BeforeSideTurnEndMirrors.Registry.Register<LikeWaterPower>((power, context) =>
        {
            if (power.Amount <= 0 || power.Owner == null || context.Side != power.Owner.Side)
            {
                return;
            }

            var env = StanceEnv.From(context);
            if (!StanceOps.IsInStance(env, power.Owner, StanceOps.Calm))
            {
                return;
            }

            env.Simulator.GainBlock(power.Owner, power.Amount, ValueProp.Unpowered);
        }));

    /// <summary>Simmering Fury: at its owner's turn start, enter Wrath, draw Amount, then remove itself.</summary>
    private static void RegisterSimmeringFury() => Guard("SimmeringFuryPower (Wrath + draw + remove at turn start)", () =>
        AfterPlayerTurnStartMirrors.Register<SimmeringFuryPower>((power, context) =>
        {
            if (power.Owner?.Player == null || context.Player != power.Owner.Player)
            {
                return;
            }

            var env = StanceEnv.From(context);
            int draw = Math.Max(0, power.Amount);

            StanceOps.EnterFromEnv(env, context.Player, StanceOps.Wrath);
            if (draw > 0)
            {
                env.Simulator.Draw(context.Player, draw);
            }

            env.Sink.SetPowerAmount(power, 0);
        }));

    /// <summary>
    /// Deva Form: after the turn's energy reset, gain one energy per instance, and every instance grows
    /// by one (DevaPower.AfterEnergyReset). The branch keeps a single instance, so a second Deva Form is
    /// approximated by the same growing counter rather than by two independent ones.
    /// </summary>
    private static void RegisterDevaForm() => Guard("DevaPower (energy after reset, growing counter)", () =>
        AfterEnergyResetMirrors.Registry.Register<DevaPower>((power, context) =>
        {
            if (power.Owner?.Player == null || context.Player != power.Owner.Player)
            {
                return;
            }

            var env = StanceEnv.FromParts(context.CombatState, context.Simulator, context.History);
            int gained = Math.Max(1, power.Amount);
            env.Simulator.GainEnergy(context.Player, gained);
            env.Sink.SetPowerAmount(power, gained + 1);
        }));

    /// <summary>
    /// Devotion: at its owner's turn start, gain Amount Mantra, and every full 10 Mantra turns into
    /// Divinity (Mantra.AfterPowerAmountChangedCompat). That threshold hook is third-party code the
    /// search never runs, so the conversion is reproduced here.
    /// </summary>
    private static void RegisterDevotion() => Guard("DevotionPower (mantra each turn + Divinity at 10)", () =>
        AfterPlayerTurnStartMirrors.Register<DevotionPower>((power, context) =>
        {
            if (power.Owner?.Player == null || context.Player != power.Owner.Player)
            {
                return;
            }

            var env = StanceEnv.From(context);
            int amount = Math.Max(0, power.Amount);
            if (amount == 0)
            {
                return;
            }

            MantraOps.Gain(env, context.Player, amount, null);
        }));

    /// <summary>
    /// Study and Omega both act in the mod's REGULAR AfterSideTurnEnd pass. Combat Solver only opens the
    /// late pass, and that registry rejects a type which does not override AfterSideTurnEndLate, so both
    /// are unavailable rather than approximated: the solver will not run their end-of-turn effect and the
    /// workshop text says so. Revisit when the author opens the regular turn-end pass.
    /// </summary>
    private static void LogUnavailableSideTurnEndEffects()
    {
        Entry.Log("power mirror unavailable: StudyPower / OmegaPower hook the regular AfterSideTurnEnd pass, "
                  + "which CombatSolver 0.50.1 does not expose for third-party registration (late pass only). "
                  + "Their end-of-turn effect is NOT modelled.");
    }

    private static void Guard(string name, Action register)
    {
        try
        {
            register();
            Entry.Log($"power mirror registered: {name}");
        }
        catch (Exception ex)
        {
            Entry.Log($"FAILED to register {name}: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
