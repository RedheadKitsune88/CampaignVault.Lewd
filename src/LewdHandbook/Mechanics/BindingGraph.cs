using System.Text.Json;
using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

/// <summary>
/// Structured bondage state on ModeParticipantState — source of truth (no prose keyword scan).
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
}

internal static class BindingGraph
{
    public static List<BindingEntry> GetBindings(ModeParticipantState participant)
    {
        if (!participant.State.TryGetValue(LewdKeys.Bindings, out var raw) || raw is null)
            return [];

        try
        {
            if (raw is List<BindingEntry> typed)
                return typed;
            if (raw is JsonElement je)
                return JsonSerializer.Deserialize<List<BindingEntry>>(je.GetRawText()) ?? [];
            if (raw is string s)
                return JsonSerializer.Deserialize<List<BindingEntry>>(s) ?? [];

            // object graphs from deserializer (List<object> / Dictionary)
            var json = JsonSerializer.Serialize(raw);
            return JsonSerializer.Deserialize<List<BindingEntry>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void SetBindings(ModeParticipantState participant, List<BindingEntry> bindings) =>
        participant.State[LewdKeys.Bindings] = bindings;

    public static HashSet<string> CollectImplied(ModeParticipantState participant)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var b in GetBindings(participant))
        {
            foreach (var i in b.Implies)
            {
                if (!string.IsNullOrWhiteSpace(i))
                    set.Add(i.Trim());
            }
        }

        return set;
    }

    public static HashSet<string> CollectEffects(ModeParticipantState participant)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var b in GetBindings(participant))
        {
            foreach (var e in b.Effects)
            {
                if (!string.IsNullOrWhiteSpace(e))
                    set.Add(e.Trim());
            }
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
            "hogtie" or "hog_tied" or "hog-tied" => "hogtie",
            "free" or "none" or "unbound" => "free",
            _ => o,
        };
    }

    /// <summary>
    /// Recompute participant arm_position / leg_position from structured bindings (last matching bind wins).
    /// </summary>
    public static void RefreshLimbPositions(ModeParticipantState participant)
    {
        string? arms = null;
        string? legs = null;
        foreach (var b in GetBindings(participant))
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
            foreach (var b in GetBindings(participant))
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

    public static HashSet<string> CollectBoundSites(ModeParticipantState participant)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var b in GetBindings(participant))
        {
            foreach (var s in b.Sites)
            {
                if (!string.IsNullOrWhiteSpace(s))
                    set.Add(s.Trim());
            }
        }

        return set;
    }

    /// <summary>
    /// Returns true when structured bindings block the required free sites for an advance.
    /// </summary>
    public static bool BlocksRequiredSites(
        ModeParticipantState actor,
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

        var bound = CollectBoundSites(actor);
        var implied = CollectImplied(actor);

        // Limb-disabling implies block hand/arm/leg advances even if site list is coarse.
        var limbDisabled = implied.Contains("limb_bound") ||
                           implied.Contains("limb-bound") ||
                           implied.Contains("mitted") ||
                           implied.Contains("encased") ||
                           implied.Contains("full_tied") ||
                           implied.Contains("full-tied");

        foreach (var site in required)
        {
            if (bound.Contains(site))
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
