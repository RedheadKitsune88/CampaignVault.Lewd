using System.Text.Json;
using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

/// <summary>
/// One binding on a character (cuffs, shackles, rope, gag, suit…). Lives on the Character as JSON, in or out of a scene.
/// </summary>
internal sealed class BindingEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Kind { get; set; } = "restraint";
    public List<string> Sites { get; set; } = [];
    public string? Orientation { get; set; }
    public List<string> Links { get; set; } = [];
    public List<string> Implies { get; set; } = [];
    public List<string> Materials { get; set; } = [];
    public List<string> Effects { get; set; } = [];
    public bool Hardened { get; set; }
    public int? HardenAtRound { get; set; }
    public int EscapeDc { get; set; } = 20;
    public int BreakDc { get; set; } = 20;
    public int Hp { get; set; } = 15;
    public string? ItemId { get; set; }

    /// <summary>What this binding ties the character to: another character, an item or fixture, or a named anchor.</summary>
    public string? AnchorId { get; set; }

    /// <summary>Who holds the anchor end when it is not itself a character (a rider with the rope tied to a saddle). Their incapacitation frees the tether.</summary>
    public string? HolderId { get; set; }

    /// <summary>How far they can move from the anchor, in feet. Null = held tight.</summary>
    public int? SlackFeet { get; set; }

    /// <summary>A core tether was attached for this binding; if it later disappears (strained free, holder down) the anchor is dropped too.</summary>
    public bool TetherOn { get; set; }

    /// <summary>tight (default) or loose: how much play the tie leaves. Loose ankles walk at 15 ft instead of 5, loose wrists leave the hands awkwardly usable instead of pinned, and a loose tie is easier to slip.</summary>
    public string? Slack { get; set; }

    /// <summary>Free text on how it is fitted ("wrists to belt, 25 cm chain"). Not mechanics: the DM reads it in the summary and rules on what it allows.</summary>
    public string? Fit { get; set; }

    /// <summary>crude|poor|standard|fine|masterwork|enchanted: shifts the escape, break and lock DCs.</summary>
    public string? Quality { get; set; }

    /// <summary>Locked bindings can't be slipped off by unbinding without the key; pick the lock (DC <see cref="LockDc"/>) or break it.</summary>
    public bool Locked { get; set; }

    public int LockDc { get; set; } = 15;

    /// <summary>The key item that opens it, when there is one.</summary>
    public string? KeyItemId { get; set; }

    /// <summary>Hours this binding has been on, counted by the time observer (only while the character lives through the clock).</summary>
    public double HoursBound { get; set; }

    /// <summary>Who applied it.</summary>
    public string? AppliedById { get; set; }

    /// <summary>Part of a sex scene (lewd consent rules) rather than plain restraint (capture, prisoners).</summary>
    public bool Erotic { get; set; }
}

internal static class BindingGraph
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Bindings are stored as JSON on the Character (<see cref="LewdKeys.TraitBindings"/>) so restraints outlast the
    /// scene; the participant holds a JSON mirror for the model. Falls back to the participant when no character.
    /// </summary>
    public static List<BindingEntry> GetBindings(ModeParticipantState? participant, Character? character)
    {
        var stored = PregnancyState.Text(character, LewdKeys.TraitBindings);
        if (!string.IsNullOrWhiteSpace(stored))
            return Parse(stored);
        return participant is null ? [] : GetBindings(participant);
    }

    public static List<BindingEntry> GetBindings(ModeParticipantState participant)
    {
        if (!participant.State.TryGetValue(LewdKeys.Bindings, out var raw) || raw is null)
            return [];

        return raw switch
        {
            List<BindingEntry> typed => typed,
            string s => Parse(s),
            JsonElement je => Parse(je.GetRawText()),
            _ => Parse(raw.ToString()),
        };
    }

    /// <summary>Writes the list to the character (source of truth) and refreshes every participant mirror.</summary>
    public static void SetBindings(ModeParticipantState? participant, Character? character, List<BindingEntry> bindings)
    {
        var json = JsonSerializer.Serialize(bindings, Json);
        if (character is not null)
        {
            var traits = character.SystemStats.Traits;
            traits.Remove(LewdKeys.LegacyModeTraitBindings);
            if (bindings.Count == 0)
                traits.Remove(LewdKeys.TraitBindings);
            else
                traits[LewdKeys.TraitBindings] = json;
        }

        if (participant is null)
            return;
        participant.State[LewdKeys.Bindings] = json;
        participant.State["binding_implies"] = CollectImplied(bindings).OrderBy(x => x).ToList();
        participant.State["binding_effects"] = CollectEffects(bindings).OrderBy(x => x).ToList();
        RefreshLimbPositions(participant, bindings);
    }

    private static List<BindingEntry> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<BindingEntry>>(json, Json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static HashSet<string> CollectImplied(IEnumerable<BindingEntry> bindings) =>
        Collect(bindings.SelectMany(b => b.Implies));

    public static HashSet<string> CollectEffects(IEnumerable<BindingEntry> bindings) =>
        Collect(bindings.SelectMany(b => b.Effects));

    private static HashSet<string> Collect(IEnumerable<string> values)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                set.Add(v.Trim());
        }

        return set;
    }

    private static readonly HashSet<string> ArmSites = new(StringComparer.OrdinalIgnoreCase)
    {
        "wrists", "wrist", "arms", "arm", "hands", "hand", "elbows", "elbow", "forearms", "forearm",
    };

    private static readonly HashSet<string> LegSites = new(StringComparer.OrdinalIgnoreCase)
    {
        "ankles", "ankle", "legs", "leg", "feet", "foot", "knees", "knee", "thighs", "thigh", "calves", "calf",
    };

    public static bool IsSlack(string? raw) => raw is not null && NormalizeSlack(raw) is not null;

    /// <summary>"tight" or "loose"; null when nothing (or nothing recognisable) was said, which means tight.</summary>
    public static string? NormalizeSlack(string? raw) => raw?.Trim().ToLowerInvariant() switch
    {
        "tight" or "snug" or "short" => "tight",
        "loose" or "slack" or "long" => "loose",
        _ => null,
    };

    public static bool TouchesArmSites(IEnumerable<string>? sites) =>
        sites is not null && sites.Any(s => !string.IsNullOrWhiteSpace(s) && ArmSites.Contains(s.Trim()));

    public static bool TouchesLegSites(IEnumerable<string>? sites) =>
        sites is not null && sites.Any(s => !string.IsNullOrWhiteSpace(s) && LegSites.Contains(s.Trim()));

    /// <summary>
    /// Normalize orientation tokens for cuffs/ties: front|behind|above|together|apart|crossed|folded|hogtie.
    /// </summary>
    public static string NormalizeOrientation(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "free";
        var o = raw.Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
        return o switch
        {
            "in_front" or "infront" or "forward" or "front" => "front",
            "behind" or "back" or "rear" or "behind_back" or "behind_the_back" => "behind",
            "above" or "overhead" or "over_head" or "raised" or "up" => "above",
            "together" or "joined" or "bound_together" => "together",
            "apart" or "spread" or "forced_spread" => "apart",
            "crossed" or "cross" => "crossed",
            "folded" or "bent" => "folded",
            "belt" or "waist" or "hip" or "hips" or "to_belt" or "at_belt" => "belt",
            "collar" or "neck" or "to_collar" => "collar",
            "hogtie" or "hog_tied" or "hog-tied" => "hogtie",
            "free" or "none" or "unbound" => "free",
            _ => o,
        };
    }

    /// <summary>
    /// Recompute participant arm_position / leg_position from structured bindings (last matching bind wins).
    /// </summary>
    public static void RefreshLimbPositions(ModeParticipantState participant, IReadOnlyList<BindingEntry> bindings)
    {
        string? arms = null;
        string? legs = null;
        foreach (var b in bindings)
        {
            if (string.IsNullOrWhiteSpace(b.Orientation))
                continue;
            var orient = NormalizeOrientation(b.Orientation);
            if (TouchesArmSites(b.Sites))
                arms = orient;
            if (TouchesLegSites(b.Sites))
                legs = orient;
        }

        // Suit / encasement without explicit orientation still implies folded limbs.
        if (arms is null || legs is null)
        {
            foreach (var b in bindings)
            {
                var implies = b.Implies.Select(i => i.ToLowerInvariant()).ToHashSet();
                var effects = b.Effects.Select(i => i.ToLowerInvariant()).ToHashSet();
                if (arms is null && (implies.Contains("encased") || effects.Contains("all_fours") || effects.Contains("arms_rear_bound")))
                    arms = effects.Contains("arms_rear_bound") || implies.Contains("limb_bound") ? "behind" : "folded";
                if (legs is null && (implies.Contains("encased") || effects.Contains("forced_crawl") || effects.Contains("all_fours")))
                    legs = "folded";
                if (legs is null && (implies.Contains("hobbled") || effects.Contains("forced_spread")))
                    legs = effects.Contains("forced_spread") ? "apart" : "together";
            }
        }

        participant.State[LewdKeys.ArmPosition] = arms ?? "free";
        participant.State[LewdKeys.LegPosition] = legs ?? "free";
    }

    /// <summary>True when either character is chained or tethered to the other.</summary>
    public static bool AreLinked(Character a, Character b) =>
        GetBindings(null, a).Any(x => string.Equals(x.AnchorId, b.Id, StringComparison.OrdinalIgnoreCase)) ||
        GetBindings(null, b).Any(x => string.Equals(x.AnchorId, a.Id, StringComparison.OrdinalIgnoreCase));

    public static HashSet<string> CollectBoundSites(IEnumerable<BindingEntry> bindings) =>
        Collect(bindings.SelectMany(b => b.Sites));

    /// <summary>
    /// Returns true when structured bindings block the required free sites for an advance.
    /// </summary>
    public static bool BlocksRequiredSites(
        ModeParticipantState actor,
        Character? actorChar,
        IEnumerable<string>? requiresFreeSites,
        out string? reason)
    {
        reason = null;
        if (requiresFreeSites is null)
            return false;

        var required = requiresFreeSites
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToList();
        if (required.Count == 0)
            return false;

        var bindings = GetBindings(actor, actorChar);
        var bound = CollectBoundSites(bindings);
        var implied = CollectImplied(bindings);

        // Limb-disabling implies block hand/arm/leg advances even if site list is coarse.
        var limbDisabled = implied.Contains("limb_bound") ||
                           implied.Contains("limb-bound") ||
                           implied.Contains("mitted") ||
                           implied.Contains("encased") ||
                           implied.Contains("full_tied") ||
                           implied.Contains("full-tied");

        var occupied = BondageSlots.Occupied(bindings);
        foreach (var site in required)
        {
            if (bound.Contains(site) || BondageSlots.SlotsOf(site).Any(occupied.ContainsKey))
            {
                reason = $"Actor '{actor.CharacterId}' site '{site}' is bound; advance requires free site.";
                return true;
            }

            if (limbDisabled && site is "hands" or "hand" or "arms" or "arm" or "wrists" or "legs" or "leg")
            {
                reason = $"Actor '{actor.CharacterId}' limb-disabling bind blocks required site '{site}'.";
                return true;
            }
        }

        return false;
    }

    public static BindingEntry SeedFromItemProperties(string? itemId, Dictionary<string, object>? props)
    {
        var entry = new BindingEntry
        {
            ItemId = itemId,
            Kind = GetString(props, "kind") ?? GetString(props, "category") ?? "restraint",
            EscapeDc = GetInt(props, "escapeDc", 20),
            BreakDc = GetInt(props, "breakDc", 20),
            Hp = GetInt(props, "hp", 15),
            Hardened = GetBool(props, "hardened"),
            HardenAtRound = GetIntNullable(props, "hardenAtRound"),
            Orientation = GetString(props, "orientation"),
            Slack = NormalizeSlack(GetString(props, "slack")),
            Fit = GetString(props, "fit"),
        };

        entry.Sites = GetList(props, "sites");
        entry.Implies = GetList(props, "implies");
        if (entry.Implies.Count == 0)
            entry.Implies = GetList(props, "seedsConditions");
        entry.Materials = GetList(props, "materials");
        if (entry.Hp == 15)
        {
            var hp = GetIntNullable(props, "hitPoints");
            if (hp is not null) entry.Hp = hp.Value;
        }
        entry.Effects = GetList(props, "effects");
        entry.Links = GetList(props, "links");
        entry.Locked = GetBool(props, "locked") || GetBool(props, "lockable");
        entry.LockDc = GetInt(props, "lockDc", 15);
        entry.KeyItemId = GetString(props, "keyItemId");

        if (entry.Implies.Count == 0)
            entry.Implies = GetList(props, "seedsConditions");

        if (entry.Sites.Count == 0 && entry.Implies.Count == 0)
        {
            // Sensible defaults by kind/name / lewdCategory
            var lewdCat = GetString(props, "lewdCategory");
            var kind = (lewdCat ?? itemId ?? entry.Kind).ToLowerInvariant();
            if (kind.Contains("gag"))
            {
                entry.Sites = ["mouth"];
                entry.Implies = ["gagged"];
                entry.Effects = ["no_clear_speech", "no_verbal_spellcasting", "jaw_forced_open"];
            }
            else if (kind.Contains("blind"))
            {
                entry.Sites = ["eyes"];
                entry.Implies = ["blinded"];
            }
            else if (kind.Contains("hood"))
            {
                entry.Sites = ["head", "eyes", "mouth"];
                entry.Implies = ["gagged", "blinded"];
                entry.Effects = ["no_sight", "no_clear_speech", "no_verbal_spellcasting"];
            }
            else if (kind.Contains("bitchsuit") || kind.Contains("encase"))
            {
                entry.Sites = ["torso", "arms", "legs", "head"];
                entry.Implies = ["encased", "cuffed", "hobbled"];
                entry.Effects = ["forced_crawl", "bent_knees_elbows", "all_fours", "no_upright_walk", "no_hand_use", "no_somatic_spellcasting"];
            }
            else if (kind.Contains("armbinder"))
            {
                entry.Sites = ["arms", "wrists"];
                entry.Implies = ["cuffed", "limb_bound"];
                entry.Links = ["arm-to-arm"];
                entry.Effects = ["arms_rear_bound", "no_hand_use", "no_somatic_spellcasting"];
            }
            else if (kind.Contains("manacle"))
            {
                entry.Sites = ["wrists"];
                entry.Implies = ["cuffed"];
                entry.Locked = true;
            }
            else if (kind.Contains("shackle") || kind.Contains("fetter") || kind.Contains("leg_iron") || kind.Contains("leg iron"))
            {
                entry.Sites = ["ankles"];
                entry.Implies = ["hobbled"];
                entry.Locked = true;
            }
            else if (kind.Contains("collar"))
            {
                entry.Sites = ["neck"];
                entry.Locked = kind.Contains("lock");
            }
            else if (kind.Contains("leash") || kind.Contains("tether") || kind.Contains("chain"))
            {
                entry.Sites = ["neck"];
                entry.Implies = ["leashed"];
            }
            else if (kind.Contains("cuff") || kind.Contains("rope"))
            {
                entry.Sites = entry.Sites.Count > 0 ? entry.Sites : ["wrists"];
                entry.Implies = ["cuffed"];
                // orientation MUST come from lewd_bind / Properties — behind|front|above|…
            }
            else if (kind.Contains("spreader"))
            {
                entry.Sites = ["ankles"];
                entry.Implies = ["hobbled"];
                entry.Effects = ["forced_spread"];
            }
            else if (kind.Contains("tar") || kind.Contains("bandage"))
            {
                entry.Sites = ["torso"];
                entry.Materials = ["bandage", "tar"];
                entry.Implies = ["cuffed"];
            }
        }

        return entry;
    }

    private static string? GetString(Dictionary<string, object>? props, string key)
    {
        if (props is null) return null;
        foreach (var (k, v) in props)
        {
            if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                return v?.ToString();
        }

        return null;
    }

    private static int GetInt(Dictionary<string, object>? props, string key, int fallback)
    {
        var n = GetIntNullable(props, key);
        return n ?? fallback;
    }

    private static int? GetIntNullable(Dictionary<string, object>? props, string key)
    {
        if (props is null) return null;
        foreach (var (k, v) in props)
        {
            if (!string.Equals(k, key, StringComparison.OrdinalIgnoreCase) || v is null)
                continue;
            try { return Convert.ToInt32(v); }
            catch { return null; }
        }

        return null;
    }

    private static bool GetBool(Dictionary<string, object>? props, string key)
    {
        if (props is null) return false;
        foreach (var (k, v) in props)
        {
            if (!string.Equals(k, key, StringComparison.OrdinalIgnoreCase) || v is null)
                continue;
            if (v is bool b) return b;
            if (bool.TryParse(v.ToString(), out var p)) return p;
        }

        return false;
    }

    private static List<string> GetList(Dictionary<string, object>? props, string key)
    {
        if (props is null) return [];
        foreach (var (k, v) in props)
        {
            if (!string.Equals(k, key, StringComparison.OrdinalIgnoreCase) || v is null)
                continue;
            if (v is IEnumerable<object> objs)
                return objs.Select(o => o?.ToString() ?? "").Where(s => s.Length > 0).ToList();
            if (v is IEnumerable<string> strs)
                return strs.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            var s = v.ToString();
            if (string.IsNullOrWhiteSpace(s)) return [];
            return s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }

        return [];
    }
}
