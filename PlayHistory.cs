using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;
using CombatSolver.Engine.InCombat.Simulation;

namespace WatcherAdapter;

/// <summary>
/// Reads the branch's play history. The mod's "previous card type" helper looks at the live play pile;
/// the branch records every finished play, so the last finished entry is the same thing without touching
/// live state.
/// </summary>
internal static class PlayHistory
{
    internal static CardType? PreviousPlayedType(CombatPredictionHistory history)
    {
        try
        {
            var last = history.OfType<CombatPredictionCardPlayFinishedEntry>().LastOrDefault();
            return last?.Card.Type;
        }
        catch
        {
            return null;
        }
    }
}
