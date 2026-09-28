using System.Text.Json;
using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

/// <summary>Something seated in a receptive orifice. Source of truth is character Trait JSON.</summary>
internal sealed class OccupancyEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>pussy | ass | mouth (normalized).</summary>
    public string Orifice { get; set; } = "ass";

    /// <summary>phallic | plug | beads | wand | partner</summary>
    public string Kind { get; set; } = "phallic";

    /// <summary>open | plugged | beaded — how much it holds fluids in.</summary>
    public string Seal { get; set; } = LewdKeys.SealOpen;

    public string? ItemId { get; set; }
    public string? SourceId { get; set; }
    public string? Label { get; set; }
    public int BeadStages { get; set; }
    public int BeadStage { get; set; }
    public double HoursIn { get; set; }
    public string? AppliedById { get; set; }
}

/// <summary>Internal fluid left after an inside finish. Leaks into core soil when the seal opens or time passes.</summary>
internal sealed class InternalDeposit
{
    public string Orifice { get; set; } = "pussy";
    public int Severity { get; set; } = 1;
    public string? SourceId { get; set; }
    public int AppliedDay { get; set; }
}

internal static class OccupancyGraph
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static List<OccupancyEntry> Get(ModeParticipantState? participant, Character? character)
    {
        var stored = PregnancyState.Text(character, LewdKeys.TraitOccupied);
        if (!string.IsNullOrWhiteSpace(stored))
            return ParseOccupancy(stored);
        return participant is null ? [] : Get(participant);
    }

    public static List<OccupancyEntry> Get(ModeParticipantState participant)
    {
        if (!participant.State.TryGetValue(LewdKeys.Occupied, out var raw) || raw is null)
            return [];
        return raw switch
        {
            List<OccupancyEntry> typed => typed,
            string s => ParseOccupancy(s),
            JsonElement je => ParseOccupancy(je.GetRawText()),
            _ => ParseOccupancy(raw.ToString()),
        };
    }

    public static void Set(ModeParticipantState? participant, Character? character, List<OccupancyEntry> entries)
    {
        var json = JsonSerializer.Serialize(entries, Json);
        if (character is not null)
        {
            var traits = character.SystemStats.Traits;
            if (entries.Count == 0)
                traits.Remove(LewdKeys.TraitOccupied);
            else
                traits[LewdKeys.TraitOccupied] = json;
        }

        if (participant is not null)
            participant.State[LewdKeys.Occupied] = json;
    }

    public static List<InternalDeposit> GetDeposits(Character? character)
    {
        var stored = PregnancyState.Text(character, LewdKeys.TraitInternalDeposits);
        return ParseDeposits(stored);
    }

    public static void SetDeposits(Character? character, List<InternalDeposit> deposits)
    {
        if (character is null)
            return;
        var traits = character.SystemStats.Traits;
        if (deposits.Count == 0)
            traits.Remove(LewdKeys.TraitInternalDeposits);
        else
            traits[LewdKeys.TraitInternalDeposits] = JsonSerializer.Serialize(deposits, Json);
    }

    public static OccupancyEntry? At(IEnumerable<OccupancyEntry> entries, string orifice) =>
        entries.FirstOrDefault(e => string.Equals(e.Orifice, NormalizeOrifice(orifice), StringComparison.OrdinalIgnoreCase));

    public static string SealFor(IEnumerable<OccupancyEntry> entries, string orifice) =>
        At(entries, orifice)?.Seal ?? LewdKeys.SealOpen;

    public static bool HoldsFluids(string seal) =>
        seal is LewdKeys.SealPlugged or LewdKeys.SealBeaded;

    public static string NormalizeOrifice(string? raw)
    {
        var key = (raw ?? "").Trim().ToLowerInvariant();
        return key switch
        {
            "pussy" or "vagina" or "cunt" or "womb" => "pussy",
            "ass" or "anus" or "butt" or "rear" => "ass",
            "mouth" or "throat" or "oral" => "mouth",
            _ => string.IsNullOrWhiteSpace(key) ? "ass" : key,
        };
    }

    public static string NormalizeKind(string? raw, string? itemName = null)
    {
        var key = (raw ?? "").Trim().ToLowerInvariant();
        if (key is "plug" or "plugged" or "anal_plug" or "butt_plug")
            return LewdKeys.OccupancyPlug;
        if (key is "beads" or "beaded" or "anal_beads")
            return LewdKeys.OccupancyBeads;
        if (key is "wand" or "vibe" or "vibrating" or "vibrating_wand" or "glass_wand")
            return LewdKeys.OccupancyWand;
        if (key is "partner" or "cock" or "phallus" or "penis")
            return LewdKeys.OccupancyPartner;
        if (key is "phallic" or "dildo" or "toy")
            return LewdKeys.OccupancyPhallic;

        var item = (itemName ?? "").ToLowerInvariant();
        if (item.Contains("bead"))
            return LewdKeys.OccupancyBeads;
        if (item.Contains("plug"))
            return LewdKeys.OccupancyPlug;
        if (item.Contains("wand") || item.Contains("vibe"))
            return LewdKeys.OccupancyWand;
        if (item.Contains("dildo") || item.Contains("phallic"))
            return LewdKeys.OccupancyPhallic;
        return string.IsNullOrWhiteSpace(key) ? LewdKeys.OccupancyPhallic : key;
    }

    public static string SealForKind(string kind) => kind switch
    {
        LewdKeys.OccupancyPlug => LewdKeys.SealPlugged,
        LewdKeys.OccupancyBeads => LewdKeys.SealBeaded,
        _ => LewdKeys.SealOpen,
    };

    public static void AddOrReplace(List<OccupancyEntry> entries, OccupancyEntry entry)
    {
        entry.Orifice = NormalizeOrifice(entry.Orifice);
        entry.Kind = NormalizeKind(entry.Kind);
        if (string.IsNullOrWhiteSpace(entry.Seal))
            entry.Seal = SealForKind(entry.Kind);
        entries.RemoveAll(e => string.Equals(e.Orifice, entry.Orifice, StringComparison.OrdinalIgnoreCase));
        entries.Add(entry);
    }

    public static void AddDeposit(List<InternalDeposit> deposits, string orifice, int amount, string? sourceId, int day)
    {
        orifice = NormalizeOrifice(orifice);
        amount = Math.Clamp(amount, DirtMark.MinSeverity, DirtMark.MaxSeverity);
        var existing = deposits.FirstOrDefault(d => string.Equals(d.Orifice, orifice, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            deposits.Add(new InternalDeposit
            {
                Orifice = orifice,
                Severity = amount,
                SourceId = sourceId,
                AppliedDay = day,
            });
            return;
        }

        existing.Severity = Math.Min(DirtMark.MaxSeverity, existing.Severity + amount);
        existing.AppliedDay = day;
        if (!string.IsNullOrWhiteSpace(sourceId))
            existing.SourceId = sourceId;
    }

    private static List<OccupancyEntry> ParseOccupancy(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<OccupancyEntry>>(json, Json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static List<InternalDeposit> ParseDeposits(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<InternalDeposit>>(json, Json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
