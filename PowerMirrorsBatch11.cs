using System;
using CombatSolver;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using CombatSolver.Engine.Common;
using CombatSolver.Engine.InCombat.Mirrors.Hooks.Damage;
using CombatSolver.Engine.InCombat.Mirrors.Hooks.Resources;
using CombatSolver.Engine.InCombat.Mirrors.Hooks.TurnEnd;
using CombatSolver.Engine.InCombat.Mirrors.Hooks.TurnStart;
using CombatSolver.Engine.InCombat.Simulation;
using WatcherMod;

namespace WatcherAdapter;

/// <summary>
/// Batch 11: the per-turn payloads that live on the Watcher's persistent powers and relics.
///
/// Up to batch 10 the card face was covered, but a power's own hook never runs inside the search:
/// an unregistered listener is only a risk line, so 战歌 looked like a card that does nothing and the
/// search routed around it. Each entry below follows the mod's hook body line by line. Where a family
/// has no public registration (AfterDamageReceived), <see cref="MirrorBridge"/> reaches the private
/// registry by field and logs a failure instead of letting that family fall through to mod code.
/// </summary>
internal static class PowerMirrorsBatch11
{
    internal static void RegisterAll()
    {
        RegisterBattleHymn();
        RegisterCollect();
        RegisterDamaru();
        RegisterRestful();
        RegisterTurnEndDeath();
        RegisterEnergyDown();
        RegisterWishPlatedArmor();
        RegisterCloakClasp();
        RegisterBlockReturn();
        RegisterGospel();
        RegisterColdObservation();
    }

    /// <summary>
    /// 冷眼旁观 (ColdObservationPower): every powered move hit the owner deals turns into block equal to the
    /// total damage, and the power only counts down once per card — four hits from one attack are one stack.
    ///
    /// The mod keeps "which card did I already count" in a private field. The search can't read or write that
    /// field, so the same fact lives in a branch state object: it forks with the branch, and the stored card is
    /// remapped so a fork never compares itself against the parent's copy.
    /// </summary>
    private static void RegisterColdObservation() => Guard("ColdObservationPower (damage to block, one stack per card)", () =>
    {
        if (!MirrorBridge.TryRegister<ColdObservationPower, AfterDamageGivenMirrorContext>(
                typeof(AfterDamageGivenMirrors),
                "Registry",
                (power, context) =>
                {
                    if (power.Owner == null || power.Amount <= 0 || context.Dealer != power.Owner)
                    {
                        return;
                    }

                    if (!context.Props.HasFlag(ValueProp.Move) || context.Props.HasFlag(ValueProp.Unpowered))
                    {
                        return;
                    }

                    if (context.Result.TotalDamage <= 0)
                    {
                        return;
                    }

                    var env = StanceEnv.FromParts(context.CombatState, context.Simulator, context.History);
                    env.Simulator.GainBlock(power.Owner, context.Result.TotalDamage, ValueProp.Unpowered);

                    var state = context.StateStore.Get(power, static () => new ColdObservationState());
                    if (ReferenceEquals(state.LastCard, context.Source))
                    {
                        return;
                    }

                    state.LastCard = context.Source;
                    env.Sink.SetPowerAmount(power, Math.Max(0, power.Amount - 1));
                },
                out string error))
        {
            throw new InvalidOperationException("AfterDamageGiven bridge failed: " + error);
        }
    });

    /// <summary>
    /// 格挡回击型的 BlockReturnPower: when its owner takes powered attack damage from someone else, that
    /// attacker's player gains Amount block. AfterDamageGiven is dispatched by the solver but its registry is
    /// private, so this goes through the field bridge.
    /// </summary>
    private static void RegisterBlockReturn() => Guard("BlockReturnPower (attacker gains block)", () =>
    {
        if (!MirrorBridge.TryRegister<BlockReturnPower, AfterDamageGivenMirrorContext>(
                typeof(AfterDamageGivenMirrors),
                "Registry",
                (power, context) =>
                {
                    if (power.Owner == null || context.Target != power.Owner || context.Dealer is null
                        || context.Dealer == power.Owner || context.Source is null)
                    {
                        return;
                    }

                    if (power.Amount <= 0 || context.Result.TotalDamage <= 0)
                    {
                        return;
                    }

                    var attacker = context.Dealer.Player ?? context.Dealer.PetOwner ?? power.Applier?.Player;
                    if (attacker is null)
                    {
                        return;
                    }

                    StanceEnv.FromParts(context.CombatState, context.Simulator, context.History)
                        .Simulator.GainBlock(attacker.Creature, power.Amount, ValueProp.Unpowered);
                },
                out string error))
        {
            throw new InvalidOperationException("AfterDamageGiven bridge failed: " + error);
        }
    });

    /// <summary>
    /// 福音 (GospelPower): the owner gains Amount mantra on every unblocked, powered, non-Unpowered hit.
    /// The mod's multiplayer branch (DoomPower on all enemies) does not apply in a solo run.
    /// </summary>
    private static void RegisterGospel() => Guard("GospelPower (mantra on unblocked damage)", () =>
    {
        if (!MirrorBridge.TryRegister<GospelPower, AfterDamageGivenMirrorContext>(
                typeof(AfterDamageGivenMirrors),
                "Registry",
                (power, context) =>
                {
                    if (power.Owner?.Player == null || context.Dealer != power.Owner)
                    {
                        return;
                    }

                    if (!context.Props.HasFlag(ValueProp.Move) || context.Props.HasFlag(ValueProp.Unpowered))
                    {
                        return;
                    }

                    if (context.Result.UnblockedDamage <= 0 || power.Amount <= 0)
                    {
                        return;
                    }

                    MantraOps.Gain(
                        StanceEnv.FromParts(context.CombatState, context.Simulator, context.History),
                        power.Owner.Player,
                        power.Amount,
                        null);
                },
                out string error))
        {
            throw new InvalidOperationException("AfterDamageGiven bridge failed: " + error);
        }
    });

    /// <summary>战歌: at its owner's turn start, add Amount 惩戒 (Smite) to hand.</summary>
    private static void RegisterBattleHymn() => Guard("BattleHymnPower (Smite into hand each turn)", () =>
        AfterPlayerTurnStartMirrors.Register<BattleHymnPower>((power, context) =>
        {
            if (power.Owner?.Player == null || context.Player != power.Owner.Player)
            {
                return;
            }

            int count = Math.Max(0, power.Amount);
            if (count == 0)
            {
                return;
            }

            StanceEnv.From(context).Simulator
                .CreateAndAddGeneratedCardsToCombat<WatcherSmite>(context.Player, PileType.Hand, count, context.Player);
        }));

    /// <summary>
    /// 收集: at its owner's turn start, add one upgraded 奇迹 (Miracle) to hand, then count down and
    /// drop the power at zero (CollectPower.AfterPlayerTurnStart).
    /// </summary>
    private static void RegisterCollect() => Guard("CollectPower (upgraded Miracle each turn, counting down)", () =>
        AfterPlayerTurnStartMirrors.Register<CollectPower>((power, context) =>
        {
            if (power.Owner?.Player == null || context.Player != power.Owner.Player)
            {
                return;
            }

            var env = StanceEnv.From(context);
            var miracle = PredictedCard.Create(CanonicalModels.Card<WatcherMiracle>(), context.Player);
            miracle.Upgrade();
            env.Simulator.AddGeneratedCardsToCombat(
                [miracle], PileType.Hand, context.Player, CardPilePosition.Bottom, CardGenerationResultKind.Fixed);

            int remaining = Math.Max(0, power.Amount - 1);
            env.Sink.SetPowerAmount(power, remaining);
        }));

    /// <summary>手摇鼓: one mantra at its owner's turn start (Damaru.AfterPlayerTurnStart).</summary>
    private static void RegisterDamaru() => Guard("Damaru (one mantra each turn)", () =>
        AfterPlayerTurnStartMirrors.Register<Damaru>((relic, context) =>
        {
            if (relic.Owner == null || context.Player != relic.Owner)
            {
                return;
            }

            int amount = relic.DynamicVars["Mantra"].IntValue;
            if (amount <= 0)
            {
                return;
            }

            MantraOps.Gain(StanceEnv.From(context), context.Player, amount, null);
        }));

    /// <summary>
    /// 宁静's 休闲 (RestfulPower): while its owner is in Calm, incoming damage is reduced by Amount percent
    /// (floor, at least 1, never below zero), and the power removes itself at the owner's next turn start.
    /// </summary>
    private static void RegisterRestful() => Guard("RestfulPower (damage reduction in Calm, one turn)", () =>
    {
        AfterPlayerTurnStartMirrors.RegisterEarly<RestfulPower>((power, context) =>
        {
            if (power.Owner?.Player == null || context.Player != power.Owner.Player)
            {
                return;
            }

            StanceEnv.From(context).Sink.SetPowerAmount(power, 0);
        });

        ModifyDamageMirrors.MultiplicativeRegistry.Register<RestfulPower>((power, context) =>
        {
            if (power.Amount <= 0 || context.Amount <= 0m)
            {
                return 1m;
            }

            if (context.Target != power.Owner || context.Dealer == power.Owner)
            {
                return 1m;
            }

            var env = StanceEnv.FromParts(context.CombatState, context.Simulator, context.History);
            if (!StanceOps.IsInStance(env, power.Owner, StanceOps.Calm))
            {
                return 1m;
            }

            decimal percent = Math.Clamp(power.Amount, 0, 100) / 100m;
            return Math.Max(1m, Math.Floor(context.Amount * (1m - percent))) / context.Amount;
        });
    });

    /// <summary>
    /// 亵渎's death mark: the mod sets the applied turn in AfterApplied and kills the owner at the NEXT
    /// turn start (EndTurnDeathPower.AfterPlayerTurnStart). That hook never runs in the search and the
    /// private field never gets written, so the first turn start with the power present is the kill —
    /// which is the same turn the mod dies on. Getting this wrong in the other direction is the dangerous
    /// one: an unmodelled death mark makes 亵渎 look like a free Divinity.
    /// </summary>
    private static void RegisterTurnEndDeath() => Guard("EndTurnDeathPower (owner dies at next turn start)", () =>
        AfterPlayerTurnStartMirrors.Register<EndTurnDeathPower>((power, context) =>
        {
            if (power.Owner?.Player == null || context.Player != power.Owner.Player)
            {
                return;
            }

            var env = StanceEnv.From(context);
            if (!context.State.GetCreature(power.Owner).IsAlive)
            {
                env.Sink.SetPowerAmount(power, 0);
                return;
            }

            // 99999 unblockable / unpowered, exactly like CreatureCmd.Damage in the mod: it must go through
            // the damage pipeline so death and AfterDamageReceived behave normally, not a bare Kill().
            env.Simulator.Damage(power.Owner, 99999m, ValueProp.Unblockable | ValueProp.Unpowered, null);
            env.Sink.SetPowerAmount(power, 0);
        }));

    /// <summary>斋戒's energy cost: EnergyDownPower.AfterEnergyReset loses Amount energy every turn.</summary>
    private static void RegisterEnergyDown() => Guard("EnergyDownPower (energy loss after the reset)", () =>
        AfterEnergyResetMirrors.Registry.Register<EnergyDownPower>((power, context) =>
        {
            if (power.Owner?.Player == null || context.Player != power.Owner.Player)
            {
                return;
            }

            int amount = Math.Max(0, power.Amount);
            if (amount == 0)
            {
                return;
            }

            StanceEnv.FromParts(context.CombatState, context.Simulator, context.History)
                .Simulator.LoseEnergy(context.Player, amount);
        }));

    /// <summary>
    /// 许愿 → 甲胄 (WishPlatedArmorPower): block equal to Amount at the owner's side turn end, and one
    /// stack consumed by every hit that got through block. The decrement lives on AfterDamageReceived,
    /// whose registry is private — without it the armor never wears off and block is overestimated.
    /// </summary>
    private static void RegisterWishPlatedArmor() => Guard("WishPlatedArmorPower (block at side turn end, wears off)", () =>
    {
        BeforeSideTurnEndMirrors.Registry.Register<WishPlatedArmorPower>((power, context) =>
        {
            if (power.Amount <= 0 || power.Owner == null || context.Side != power.Owner.Side)
            {
                return;
            }

            StanceEnv.From(context).Simulator.GainBlock(power.Owner, power.Amount, ValueProp.Unpowered);
        });

        if (!MirrorBridge.TryRegister<WishPlatedArmorPower, AfterDamageReceivedMirrorContext>(
                typeof(AfterDamageReceivedMirrors),
                "Registry",
                (power, context) =>
                {
                    if (power.Owner == null || context.Target != power.Owner || context.Result.UnblockedDamage <= 0)
                    {
                        return;
                    }

                    var env = StanceEnv.FromParts(context.CombatState, context.Simulator, context.History);
                    env.Sink.SetPowerAmount(power, Math.Max(0, power.Amount - 1));
                },
                out string error))
        {
            throw new InvalidOperationException("AfterDamageReceived bridge failed: " + error);
        }
    });

    /// <summary>斗篷扣: block equal to the hand size at its owner's side turn end (CloakClasp_P).</summary>
    private static void RegisterCloakClasp() => Guard("CloakClasp_P (block equal to hand size)", () =>
        BeforeSideTurnEndMirrors.Registry.Register<CloakClasp_P>((relic, context) =>
        {
            if (relic.Owner == null || context.Side != relic.Owner.Creature.Side)
            {
                return;
            }

            int count = context.State.GetPlayerCombatState(relic.Owner).Hand.Cards.Count;
            if (count <= 0)
            {
                return;
            }

            StanceEnv.From(context).Simulator.GainBlock(relic.Owner.Creature, count, ValueProp.Unpowered);
        }));

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

/// <summary>
/// Branch-local replacement for ColdObservationPower's private "card already counted" field.
/// </summary>
/// <remarks>
/// The stored card is a branch object, so <see cref="Fork"/> has to remap it; a MemberwiseClone would leave the
/// fork pointing at the parent's PredictedCard and every fork would count the same card a second time.
/// </remarks>
internal sealed class ColdObservationState : IPredictionStateForkable
{
    internal PredictedCard? LastCard;

    public object Fork(PredictionForkContext context) =>
        new ColdObservationState { LastCard = LastCard is null ? null : context.RemapOrSelf(LastCard) };
}
