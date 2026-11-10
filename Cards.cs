using System;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using CombatSolver.Engine.InCombat.Mirrors.Cards.OnPlay;
using CombatSolver.Engine.InCombat.Simulation;

namespace WatcherAdapter;

/// <summary>
/// Batch 1: cards whose OnPlay is a plain sequence of already-modelled commands.
///
/// Each entry follows the mod's own OnPlay body line by line — attack, then stance change, then power
/// application, in that order. Cards whose OnPlay needs a mechanic that is not modelled yet are
/// deliberately absent rather than approximated.
/// </summary>
internal static class Cards
{
    internal static void RegisterAll()
    {
        // --- batch 0: attack / block only (proven working on 2026-11-09) ---------------
        Register<WatcherMod.WatcherStrike_P>("WatcherStrike_P", (c, ctx) => Ops.Attack(c, ctx));
        Register<WatcherMod.WatcherFlurryOfBlows>("WatcherFlurryOfBlows", (c, ctx) => Ops.Attack(c, ctx));
        Register<WatcherMod.WatcherSandsOfTime>("WatcherSandsOfTime", (c, ctx) => Ops.Attack(c, ctx));
        Register<WatcherMod.WatcherSmite>("WatcherSmite", (c, ctx) => Ops.Attack(c, ctx));
        Register<WatcherMod.WatcherWeave>("WatcherWeave", (c, ctx) => Ops.Attack(c, ctx));
        Register<WatcherMod.WatcherWindmillStrike>("WatcherWindmillStrike", (c, ctx) => Ops.Attack(c, ctx));
        Register<WatcherMod.WatcherDefend_P>("WatcherDefend_P", (c, ctx) => Ops.Block(c, ctx));
        Register<WatcherMod.WatcherPerseverance>("WatcherPerseverance", (c, ctx) => Ops.Block(c, ctx));
        Register<WatcherMod.WatcherProtect>("WatcherProtect", (c, ctx) => Ops.Block(c, ctx));
        Register<WatcherMod.WatcherSafety>("WatcherSafety", (c, ctx) => Ops.Block(c, ctx));

        // --- damage only ---------------------------------------------------------------
        Register<WatcherMod.WatcherConsecrate>("WatcherConsecrate", (c, ctx) => Ops.Attack(c, ctx));                 // all enemies
        Register<WatcherMod.WatcherExpunger>("WatcherExpunger", (c, ctx) => Ops.Attack(c, ctx));                     // hit count from Repeat
        Register<WatcherMod.WatcherThroughViolence>("WatcherThroughViolence", (c, ctx) => Ops.Attack(c, ctx));       // single target
        Register<WatcherMod.WatcherSignatureMove>("WatcherSignatureMove", (c, ctx) => Ops.Attack(c, ctx));           // single target
        Register<WatcherMod.WatcherFlyingSleeves>("WatcherFlyingSleeves", (c, ctx) => Ops.AttackSingle(ctx, 2));
        Register<WatcherMod.WatcherBowlingBash>("WatcherBowlingBash", (c, ctx) =>
            Ops.AttackSingle(ctx, ctx.CombatState.HittableEnemies.Count));

        // --- damage + power on the target ---------------------------------------------
        Register<WatcherMod.WatcherTalkToTheHand>("WatcherTalkToTheHand", (c, ctx) =>
        {
            Ops.Attack(c, ctx);
            if (Ops.Pending(ctx))
            {
                return;
            }

            Ops.Power(c, ctx, typeof(WatcherMod.BlockReturnPower), ctx.Target, Ops.Var(c, ctx, "MagicNumber"));
        });

        // --- stance entry only --------------------------------------------------------
        Register<WatcherMod.WatcherCrescendo>("WatcherCrescendo", (c, ctx) => StanceOps.Enter(c, ctx, StanceOps.Wrath));
        Register<WatcherMod.WatcherTranquility>("WatcherTranquility", (c, ctx) => StanceOps.Enter(c, ctx, StanceOps.Calm));

        // --- damage + stance entry ----------------------------------------------------
        Register<WatcherMod.WatcherEruption_P>("WatcherEruption_P", (c, ctx) =>
        {
            Ops.Attack(c, ctx);
            if (Ops.Pending(ctx))
            {
                return;
            }

            StanceOps.Enter(c, ctx, StanceOps.Wrath);
        });

        Register<WatcherMod.WatcherTantrum>("WatcherTantrum", (c, ctx) =>
        {
            // Tantrum also shuffles itself back into the draw pile from AfterCardPlayed. The branch has no
            // "move one card to the draw pile" entry point, so that part is reported instead of assumed.
            ctx.History.RecordRisk(CombatSolver.Engine.Common.PredictionRiskReason.MethodMirrorIncomplete);
            Ops.AttackSingle(ctx, Ops.Var(c, ctx, "MagicNumber"));
            if (Ops.Pending(ctx))
            {
                return;
            }

            StanceOps.Enter(c, ctx, StanceOps.Wrath);
        });

        Register<WatcherMod.WatcherCataclysm>("WatcherCataclysm", (c, ctx) =>
        {
            Ops.AttackAll(ctx, Ops.Var(c, ctx, "Repeat"));
            if (Ops.Pending(ctx))
            {
                return;
            }

            StanceOps.Enter(c, ctx, StanceOps.Wrath);
        });

        // --- powers on self -----------------------------------------------------------
        Register<WatcherMod.WatcherColdObservation>("WatcherColdObservation", (c, ctx) =>
            Ops.PowerSelf(c, ctx, typeof(WatcherMod.ColdObservationPower), Ops.Var(c, ctx, "MagicNumber")));

        Register<WatcherMod.WatcherCollect>("WatcherCollect", (c, ctx) =>
        {
            int energyX = (int)ctx.Card.ResolveEnergyXValue(ctx.State);
            if (c.IsUpgraded)
            {
                energyX++;
            }

            Ops.PowerSelf(c, ctx, typeof(WatcherMod.CollectPower), energyX);
        });

        Register<WatcherMod.WatcherDevaForm>("WatcherDevaForm", (c, ctx) =>
            Ops.PowerSelf(c, ctx, typeof(WatcherMod.DevaPower), 1));

        Register<WatcherMod.WatcherEstablishment>("WatcherEstablishment", (c, ctx) =>
            Ops.PowerSelf(c, ctx, typeof(WatcherMod.EstablishmentPower), Ops.Var(c, ctx, "MagicNumber")));

        Register<WatcherMod.WatcherForesight>("WatcherForesight", (c, ctx) =>
            Ops.PowerSelf(c, ctx, typeof(WatcherMod.ForesightPower), Ops.Var(c, ctx, "MagicNumber")));

        Register<WatcherMod.WatcherLikeWater>("WatcherLikeWater", (c, ctx) =>
            Ops.PowerSelf(c, ctx, typeof(WatcherMod.LikeWaterPower), Ops.Var(c, ctx, "MagicNumber")));

        Register<WatcherMod.WatcherMasterReality>("WatcherMasterReality", (c, ctx) =>
            Ops.PowerSelf(c, ctx, typeof(WatcherMod.MasterRealityPower), 1));

        Register<WatcherMod.WatcherMentalFortress>("WatcherMentalFortress", (c, ctx) =>
            Ops.PowerSelf(c, ctx, typeof(WatcherMod.MentalFortressPower), Ops.Var(c, ctx, "MagicNumber")));

        Register<WatcherMod.WatcherNirvana>("WatcherNirvana", (c, ctx) =>
            Ops.PowerSelf(c, ctx, typeof(WatcherMod.NirvanaPower), Ops.Var(c, ctx, "MagicNumber")));

        Register<WatcherMod.WatcherOmega>("WatcherOmega", (c, ctx) =>
            Ops.PowerSelf(c, ctx, typeof(WatcherMod.OmegaPower), Ops.Var(c, ctx, "MagicNumber")));

        Register<WatcherMod.WatcherRushdown>("WatcherRushdown", (c, ctx) =>
            Ops.PowerSelf(c, ctx, typeof(WatcherMod.RushdownPower), Ops.Var(c, ctx, "MagicNumber")));

        Register<WatcherMod.WatcherSimmeringFury>("WatcherSimmeringFury", (c, ctx) =>
            Ops.PowerSelf(c, ctx, typeof(WatcherMod.SimmeringFuryPower), Ops.Var(c, ctx, "MagicNumber")));

        Register<WatcherMod.WatcherStudy>("WatcherStudy", (c, ctx) =>
            Ops.PowerSelf(c, ctx, typeof(WatcherMod.StudyPower), 1));

        Register<WatcherMod.WatcherWaveOfTheHand>("WatcherWaveOfTheHand", (c, ctx) =>
            Ops.PowerSelf(c, ctx, typeof(WatcherMod.WaveOfTheHandPower), Ops.Var(c, ctx, "MagicNumber")));

        Register<WatcherMod.WatcherWreathOfFlame>("WatcherWreathOfFlame", (c, ctx) =>
            Ops.PowerSelf(c, ctx, typeof(VigorPower), Ops.Var(c, ctx, "MagicNumber")));

        Register<WatcherMod.WatcherFasting2>("WatcherFasting2", (c, ctx) =>
        {
            Ops.PowerSelf(c, ctx, typeof(StrengthPower), Ops.Var(c, ctx, "MagicNumber"));
            Ops.PowerSelf(c, ctx, typeof(DexterityPower), Ops.Var(c, ctx, "MagicNumber"));
            Ops.PowerSelf(c, ctx, typeof(WatcherMod.EnergyDownPower), 1);
        });

        Register<WatcherMod.WatcherPreach>("WatcherPreach", (c, ctx) =>
        {
            Ops.PowerSelf(c, ctx, typeof(WatcherMod.DevotionPower), 2);
            Ops.PowerSelf(c, ctx, typeof(WatcherMod.GospelPower), 1);
        });
    }

    private static void Register<T>(string name, Action<T, CardOnPlayMirrorContext> handler)
        where T : CardModel
    {
        try
        {
            CardOnPlayMirrors.Registry.Register(handler);
            Entry.Log($"mirror registered: {name}");
        }
        catch (Exception ex)
        {
            Entry.Log($"FAILED to register {name}: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
