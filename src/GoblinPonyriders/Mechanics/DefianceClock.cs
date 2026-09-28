using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;

namespace GoblinPonyriders.Mechanics;

/// <summary>
/// 0–10 Defiance Clock for captives. High defiance = resistance / intrusive rebellion;
/// low = broken compliance. Syncs narratively with willpower drain (Lewd owns the numbers).
/// </summary>
internal static class DefianceClock
{
    public const int Min = 0;
    public const int Max = 10;

    public static int Get(Character character) =>
        Math.Clamp(Traits.GetInt(character, GoblinKeys.Defiance, 5), Min, Max);

    public static int Set(Character character, int value, IChangeContext? context = null, string? reason = null)
    {
        var clamped = Math.Clamp(value, Min, Max);
        Traits.SetInt(character, GoblinKeys.Defiance, clamped);
        context?.RecordMessage(
            $"{character.Id} Defiance Clock → {clamped}/10{(reason is null ? "" : $" ({reason})")}.");
        return clamped;
    }

    public static int Adjust(Character character, int delta, IChangeContext? context = null, string? reason = null) =>
        Set(character, Get(character) + delta, context, reason);

    public static string Telegraph(int level) => level switch
    {
        <= 2 => "broken — compliance easy, intrusive obedience",
        <= 4 => "frayed — still resists in small ways",
        <= 6 => "defiant — bargaining and sabotage",
        <= 8 => "rising — sharp tongue, escape plotting",
        _ => "peak defiance — will fight or sabotage at any opening",
    };
}
