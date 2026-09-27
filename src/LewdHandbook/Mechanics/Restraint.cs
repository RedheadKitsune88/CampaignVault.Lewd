using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

/// <summary>
/// What a character's bindings mean on the sheet, in and out of scenes: the bound conditions they imply, one
/// <c>Bound</c> summary status (ids, escape/break/lock DCs, anchors; carries the spell-component blocks), and a hard
/// physical engagement per anchor, which is what makes core refuse <c>travel</c> unless the anchor travels along in
/// the same commit. Everything here is recomputed from the binding list, so it is safe to call after any change.
/// </summary>
internal static class Restraint
{
    public const string AppliedBy = "lewd_bind";
    public const string BoundToVerb = "bound to";
    public const string SummaryName = "Bound";

    // Keys must match host CastingComponentGate constants (the SDK cannot reference host Services).
    private const string BlocksVerbal = "BlocksVerbalComponents";
    private const string BlocksSomatic = "BlocksSomaticComponents";

    /// <summary>Conditions (plugin YAML or core) a binding list implies, with a short reminder each.</summary>
    public static List<(string Name, string Hint)> ImpliedConditions(IReadOnlyList<BindingEntry> bindings)
    {
        var set = BindingGraph.CollectImplied(bindings);
        set.UnionWith(BindingGraph.CollectEffects(bindings));
        bool Has(params string[] tokens) => tokens.Any(set.Contains);

        var wanted = new List<(string, string)>();
        void Add(string name, string hint)
        {
            if (!wanted.Any(w => w.Item1 == name))
                wanted.Add((name, hint));
        }

        var encased = Has("encased");
        var fullTied = Has("full_tied", "full-tied", "hogtie", "hogtied");
        if (encased)
        {
            Add("encased", "Encased: incapacitated, cuffed and hobbled; auto-fails Str/Dex saves.");
            Add("incapacitated", "Encased by a binding.");
        }

        if (fullTied)
        {
            Add("full_tied", "Hog-tied: cuffed, restrained and prone; cannot stand.");
            Add("prone", "Hog-tied; cannot stand up while bound.");
        }

        if (encased || fullTied || Has("cuffed", "cuff", "arms_rear_bound") ||
            bindings.Any(b => b.Orientation is { } o && o != "free" &&
                              (BindingGraph.TouchesArmSites(b.Sites) || BindingGraph.TouchesLegSites(b.Sites))))
            Add("cuffed", "Bound limbs: disadvantage on attacks needing them and on Dex checks/saves.");
        if (Has("limb_bound", "limb-bound"))
            Add("limb_bound", "Bound limbs are unusable.");
        if (Has("mitted", "mitt", "mitts"))
            Add("mitted", "Hands unusable: no manipulation, no somatic components.");
        if (encased || Has("hobbled", "forced_crawl", "no_upright_walk", "shackled_ankles") ||
            bindings.Any(b => BindingGraph.TouchesLegSites(b.Sites)))
            Add("hobbled", "Speed 5 ft at most; disadvantage on Dex checks/saves.");
        if (Has("suspended"))
            Add("suspended", "Hung up: restrained, no foothold.");
        if (fullTied || Has("restrained", "suspended"))
            Add("restrained", "Restrained by a binding.");
        if (Has("leashed", "leash") || bindings.Any(b => !string.IsNullOrWhiteSpace(b.AnchorId)))
            Add("leashed", "Tethered: cannot go farther than the tether allows.");
        if (Has("gagged", "gag", "no_clear_speech", "no_verbal_spellcasting"))
            Add("gagged", "Gagged: no clear speech, no verbal components.");
        if (Has("blinded", "blind", "no_sight"))
            Add("blinded", "Vision blocked by a binding.");
        return wanted;
    }

    public static bool BlocksSomaticComponents(IReadOnlyList<BindingEntry> bindings)
    {
        var set = BindingGraph.CollectImplied(bindings);
        set.UnionWith(BindingGraph.CollectEffects(bindings));
        return new[] { "mitted", "limb_bound", "limb-bound", "encased", "arms_rear_bound", "no_hand_use", "no_somatic_spellcasting", "full_tied" }
            .Any(set.Contains);
    }

    public static bool BlocksVerbalComponents(IReadOnlyList<BindingEntry> bindings)
    {
        var set = BindingGraph.CollectImplied(bindings);
        set.UnionWith(BindingGraph.CollectEffects(bindings));
        return new[] { "gagged", "gag", "no_verbal_spellcasting", "no_clear_speech" }.Any(set.Contains);
    }

    /// <summary>Stamps what the bindings imply and removes <see cref="AppliedBy"/> conditions nothing implies any more.</summary>
    public static void Sync(Character character, IReadOnlyList<BindingEntry> bindings)
    {
        var wanted = bindings.Count == 0 ? [] : ImpliedConditions(bindings);
        var effects = character.SystemStats.StatusEffects;
        effects.RemoveAll(e =>
            string.Equals(e.AppliedBy, AppliedBy, StringComparison.Ordinal) &&
            e.Name != SummaryName &&
            !wanted.Any(w => string.Equals(w.Name, e.Name, StringComparison.OrdinalIgnoreCase)));

        foreach (var (name, hint) in wanted)
        {
            if (effects.Any(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(e.ConditionName, name, StringComparison.OrdinalIgnoreCase)))
                continue;
            effects.Add(new StatusEffect
            {
                Name = name,
                Category = "Condition",
                ConditionName = name,
                RecoveryHint = hint,
                AppliedBy = AppliedBy,
            });
        }

        SyncSummary(character, bindings);
        SyncAnchors(character, bindings);
    }

    private static void SyncSummary(Character character, IReadOnlyList<BindingEntry> bindings)
    {
        var effects = character.SystemStats.StatusEffects;
        effects.RemoveAll(e => e.Name == SummaryName && string.Equals(e.AppliedBy, AppliedBy, StringComparison.Ordinal));
        if (bindings.Count == 0)
            return;

        var summary = new StatusEffect
        {
            Name = SummaryName,
            Category = "Restraint",
            AppliedBy = AppliedBy,
            RecoveryHint = Describe(bindings) + " Free with lewd_escape (slip/break/pick/unlock/cut) or lewd_unbind.",
        };
        if (BlocksSomaticComponents(bindings))
            summary.StatModifiers[BlocksSomatic] = 1f;
        if (BlocksVerbalComponents(bindings))
            summary.StatModifiers[BlocksVerbal] = 1f;
        effects.Add(summary);
    }

    public static string Describe(IReadOnlyList<BindingEntry> bindings) =>
        string.Join("; ", bindings.Select(b =>
        {
            var sites = b.Sites.Count == 0 ? "" : "@" + string.Join(",", b.Sites);
            var orient = string.IsNullOrWhiteSpace(b.Orientation) || b.Orientation == "free" ? "" : $" {b.Orientation}";
            var anchor = string.IsNullOrWhiteSpace(b.AnchorId) ? "" : $", {BoundToVerb} {b.AnchorId}";
            var lockNote = b.Locked ? $", locked DC {b.LockDc}{(b.KeyItemId is null ? "" : $" key {b.KeyItemId}")}" : "";
            return $"{b.Kind}{sites}{orient} [{b.Id}] escape DC {b.EscapeDc}/break DC {b.BreakDc}/hp {b.Hp}{lockNote}{anchor}";
        })) + ".";

    /// <summary>One hard physical engagement per anchor (verb <see cref="BoundToVerb"/>); stale ones removed.</summary>
    private static void SyncAnchors(Character character, IReadOnlyList<BindingEntry> bindings)
    {
        var anchors = bindings
            .Select(b => b.AnchorId?.Trim())
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Select(a => a!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var relations = character.SystemStats.EngagementRelations;
        relations.RemoveAll(r =>
            string.Equals(r.Verb, BoundToVerb, StringComparison.OrdinalIgnoreCase) &&
            !anchors.Contains(r.TargetId, StringComparer.OrdinalIgnoreCase));
        foreach (var anchor in anchors)
        {
            if (relations.Any(r => string.Equals(r.TargetId, anchor, StringComparison.OrdinalIgnoreCase) &&
                                   string.Equals(r.Verb, BoundToVerb, StringComparison.OrdinalIgnoreCase)))
                continue;
            relations.Add(new EngagementRelation
            {
                TargetId = anchor,
                Category = EngagementCategory.Physical,
                Verb = BoundToVerb,
                RestrictionLevel = EngagementRestrictionLevel.Hard,
            });
        }
    }

    private static readonly string[] Subdued =
    [
        "grappled", "restrained", "incapacitated", "unconscious", "paralyzed", "stunned", "petrified",
        "cuffed", "limb_bound", "encased", "full_tied", "mitted",
    ];

    /// <summary>
    /// Tying up someone who does not want it takes a subdued target: grappled (by anyone, or held by the actor),
    /// incapacitated, unconscious, paralyzed, stunned, restrained or already bound, or at 0 HP. Returns null when allowed.
    /// </summary>
    public static string? NotSubdued(Character target, string? actorId)
    {
        if (target.MaxHp > 0 && target.CurrentHp <= 0)
            return null;
        if (target.SystemStats.StatusEffects.Any(e =>
                Subdued.Any(s => string.Equals(e.ConditionName, s, StringComparison.OrdinalIgnoreCase) ||
                                 (e.Name?.StartsWith(s, StringComparison.OrdinalIgnoreCase) ?? false))))
            return null;
        if (!string.IsNullOrWhiteSpace(actorId) &&
            target.SystemStats.EngagementRelations.Any(r =>
                r.Category == EngagementCategory.Physical &&
                string.Equals(r.TargetId, actorId, StringComparison.OrdinalIgnoreCase)))
            return null;
        return $"'{target.Id}' is not subdued: an unwilling character must be grappled, restrained, incapacitated, unconscious " +
               "(or already bound) before they can be tied. Grapple first, or set willing=true if they submit.";
    }
}
