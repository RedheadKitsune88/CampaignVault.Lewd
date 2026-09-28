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
    public static List<(string Name, string Hint)> ImpliedConditions(IReadOnlyList<BindingEntry> bindings, string? posture = null)
    {
        var set = AllTokens(bindings, posture);
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
            Add("hobbled", $"Speed {BondageSlots.SpeedCap(bindings) ?? 5} ft at most; disadvantage on Dex checks/saves.");
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

    /// <summary>Explicit implies and effects plus everything the occupied body slots derive.</summary>
    private static HashSet<string> AllTokens(IReadOnlyList<BindingEntry> bindings, string? posture = null)
    {
        var set = BindingGraph.CollectImplied(bindings);
        set.UnionWith(BindingGraph.CollectEffects(bindings));
        set.UnionWith(BondageSlots.Derive(bindings, posture));
        return set;
    }

    /// <summary>Every condition token the bindings imply or derive (explicit implies and effects plus what the occupied slots give).</summary>
    public static HashSet<string> TokensOf(IReadOnlyList<BindingEntry> bindings, string? posture = null) => AllTokens(bindings, posture);

    public static bool BlocksSomaticComponents(IReadOnlyList<BindingEntry> bindings)
    {
        var set = AllTokens(bindings);
        return new[] { "mitted", "limb_bound", "limb-bound", "encased", "arms_rear_bound", "no_hand_use", "no_somatic_spellcasting", "full_tied" }
            .Any(set.Contains);
    }

    public static bool BlocksVerbalComponents(IReadOnlyList<BindingEntry> bindings)
    {
        var set = AllTokens(bindings);
        return new[] { "gagged", "gag", "no_verbal_spellcasting", "no_clear_speech" }.Any(set.Contains);
    }

    /// <summary>Stamps what the bindings imply and removes <see cref="AppliedBy"/> conditions nothing implies any more.</summary>
    public static void Sync(Character character, IReadOnlyList<BindingEntry> bindings)
    {
        var posture = PregnancyState.Text(character, LewdKeys.TraitPosture);
        ReconcileTethers(character, ref bindings);
        var wanted = bindings.Count == 0 ? [] : ImpliedConditions(bindings, posture);
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

        SyncSummary(character, bindings, posture);
        if (SyncTethers(character, bindings))
            BindingGraph.SetBindings(null, character, bindings.ToList());
    }

    private static void SyncSummary(Character character, IReadOnlyList<BindingEntry> bindings, string? posture)
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
            RecoveryHint = BondageSlots.Summary(bindings, posture, bindings.Any(b => b.AnchorId is not null)) + " " + Describe(bindings) + " Free with lewd_escape (slip/break/pick/unlock/cut) or lewd_unbind.",
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
            var slack = b.Slack == "loose" ? " loose" : "";
            var fit = string.IsNullOrWhiteSpace(b.Fit) ? "" : $" ({b.Fit.Trim()})";
            var lockNote = b.Locked ? $", locked DC {b.LockDc}{(b.KeyItemId is null ? "" : $" key {b.KeyItemId}")}" : "";
            return $"{b.Kind}{sites}{orient}{slack}{fit} [{b.Id}] escape DC {b.EscapeDc}/break DC {b.BreakDc}/hp {b.Hp}{lockNote}{anchor}";
        })) + ".";

    private static string TetherTag(BindingEntry b) => $"{AppliedBy}:{b.Id}";

    private static bool IsOurs(Tether t) => t.AttachedBy?.StartsWith(AppliedBy + ":", StringComparison.Ordinal) == true;

    /// <summary>
    /// A binding whose core tether has since gone (strained free, its holder was put down, the anchor was destroyed) no
    /// longer claims an anchor. Rewrites the stored list when it drops one.
    /// </summary>
    private static void ReconcileTethers(Character character, ref IReadOnlyList<BindingEntry> bindings)
    {
        var live = character.SystemStats.Tethers ?? [];
        var dropped = false;
        foreach (var b in bindings)
        {
            if (!b.TetherOn || live.Any(t => t.AttachedBy == TetherTag(b)))
                continue;
            b.AnchorId = null;
            b.HolderId = null;
            b.TetherOn = false;
            dropped = true;
        }

        if (dropped)
            BindingGraph.SetBindings(null, character, bindings.ToList());
    }

    /// <summary>
    /// One core <see cref="Tether"/> per anchored binding, which is what makes the host refuse <c>travel</c> unless the
    /// anchor (or its holder) travels along in the same commit. Stale tethers of ours are removed. Older saves that used a
    /// "bound to" engagement relation are migrated by dropping it.
    /// </summary>
    private static bool SyncTethers(Character character, IReadOnlyList<BindingEntry> bindings)
    {
        character.SystemStats.EngagementRelations.RemoveAll(r =>
            string.Equals(r.Verb, BoundToVerb, StringComparison.OrdinalIgnoreCase));

        var tethers = character.SystemStats.Tethers ??= [];
        var anchored = bindings.Where(b => !string.IsNullOrWhiteSpace(b.AnchorId)).ToList();
        tethers.RemoveAll(t => IsOurs(t) && !anchored.Any(b => t.AttachedBy == TetherTag(b)));

        var changed = false;
        foreach (var b in anchored)
        {
            var existing = tethers.FindIndex(t => t.AttachedBy == TetherTag(b));
            var tether = new Tether
            {
                AnchorId = b.AnchorId!.Trim(),
                BreakDc = b.BreakDc,
                SlackFeet = b.SlackFeet,
                HolderId = string.IsNullOrWhiteSpace(b.HolderId) ? null : b.HolderId.Trim(),
                Label = $"{b.Kind}{(b.Sites.Count == 0 ? "" : "@" + string.Join(",", b.Sites))}",
                AttachedBy = TetherTag(b),
            };
            if (existing >= 0)
                tethers[existing] = tether;
            else if (tethers.Count < 4)
                tethers.Add(tether);
            else
                continue;
            changed |= !b.TetherOn;
            b.TetherOn = true;
        }

        return changed;
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
