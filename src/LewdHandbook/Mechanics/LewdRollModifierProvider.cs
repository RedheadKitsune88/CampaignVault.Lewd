using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

/// <summary>
/// What the handbook's conditions and restraints do to rolls, so they stop being reminders for the DM: intoxicated, nymphomanic and
/// flustered characters roll mental saves at disadvantage, and bound limbs, a blindfold or hobbles cost advantage on the rolls they
/// obviously affect. Situational advantage only; timed numbers (restraint aftermath) live on status effects, so nothing counts twice.
/// A roll tagged <c>escape</c> skips the restraint rules: the restraint is what is being escaped and its own DC already says how hard.
/// </summary>
public sealed class LewdRollModifierProvider : IRollModifierProvider
{
    private static readonly HashSet<string> DexSubjects = new(StringComparer.Ordinal)
    {
        "dexterity", "dex", "acrobatics", "sleightofhand", "stealth",
    };

    public IEnumerable<RollModifier> Modifiers(RollQuery query)
    {
        var actor = query.Actor;
        if (actor.SystemStats is null)
            yield break;

        var subject = Normalize(query.Subject);
        if (query.Kind == RollKinds.Save && subject is "wisdom" or "wis" or "intelligence" or "int" or "charisma" or "cha")
        {
            var ability = subject[..3];
            if (SaveConditions.Disadvantage(actor, ability))
                yield return new RollModifier("lewd", 0, AdvantageEffect.Disadvantage, "Intoxicated or flustered: disadvantage");
        }

        var bindings = BindingGraph.GetBindings(null, actor);
        if (bindings.Count == 0)
            yield break;

        var escaping = query.Tags.Contains("escape", StringComparer.OrdinalIgnoreCase);
        var tokens = Restraint.TokensOf(bindings, PregnancyState.Text(actor, LewdKeys.TraitPosture));

        if (query.Kind == RollKinds.Speed)
        {
            var stats = actor.SystemStats;
            if (tokens.Contains("encased") || BondageSlots.SpeedCap(bindings) == 5)
            {
                // "Speed 5 ft at most": a large enough penalty; the pipeline floors movement at 5.
                yield return new RollModifier("lewd", -999, AdvantageEffect.None, "");
            }
            else if (BondageSlots.SpeedCap(bindings) is { } cap && stats.Movement is { } move)
            {
                // Loose hobbles: a shuffle, 15 ft at most. Only the excess over the cap is taken off.
                var excess = (int)Math.Floor(move + stats.MovementModifier) - cap;
                if (excess > 0)
                    yield return new RollModifier("lewd", -excess, AdvantageEffect.None, "");
            }

            yield break;
        }

        if (escaping)
            yield break;

        if (query.Kind == RollKinds.Attack && Restraint.BlocksSomaticComponents(bindings))
            yield return new RollModifier("lewd", 0, AdvantageEffect.Disadvantage, "Hands bound: disadvantage on attacks");
        else if (query.Kind == RollKinds.Attack && tokens.Contains("awkward_hands"))
            yield return new RollModifier("lewd", 0, AdvantageEffect.Disadvantage, "Hands awkwardly bound: disadvantage on attacks");
        else if (query.Kind == RollKinds.Attack && (tokens.Contains("blinded") || tokens.Contains("no_sight")))
            yield return new RollModifier("lewd", 0, AdvantageEffect.Disadvantage, "Blindfolded: disadvantage on attacks");

        if (query.Kind == RollKinds.Check && subject == "perception" && (tokens.Contains("blinded") || tokens.Contains("no_sight")))
            yield return new RollModifier("lewd", 0, AdvantageEffect.Disadvantage, "Blindfolded: disadvantage on Perception");

        if (query.Kind is RollKinds.Check or RollKinds.Save && DexSubjects.Contains(subject) &&
            (tokens.Contains("cuffed") || tokens.Contains("hobbled") || tokens.Contains("encased")))
            yield return new RollModifier("lewd", 0, AdvantageEffect.Disadvantage, "Bound limbs: disadvantage on Dex");
    }

    private static string Normalize(string? name) =>
        string.Concat((name ?? "").Where(char.IsLetterOrDigit)).ToLowerInvariant();
}
