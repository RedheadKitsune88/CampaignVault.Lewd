using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;

namespace GoblinPonyriders.Mechanics;

internal static class TrainingPhases
{
    public const string RaidCamp = "raid_camp";
    public const string Village = "village";
    public const string Assigned = "assigned";

    public static string Get(Character character) =>
        Traits.Get(character, GoblinKeys.TrainingPhase) switch
        {
            var p when string.Equals(p, Village, StringComparison.OrdinalIgnoreCase) => Village,
            var p when string.Equals(p, Assigned, StringComparison.OrdinalIgnoreCase) => Assigned,
            _ => string.IsNullOrWhiteSpace(Traits.Get(character, GoblinKeys.TrainingPhase)) ? "" : RaidCamp,
        };

    public static int Day(Character character) => Math.Max(0, Traits.GetInt(character, GoblinKeys.TrainingDay));

    public static string Advance(Character character, IChangeContext context, string? forcePhase = null)
    {
        if (!string.IsNullOrWhiteSpace(forcePhase))
        {
            var phase = Normalize(forcePhase);
            Traits.Set(character, GoblinKeys.TrainingPhase, phase);
            if (phase == RaidCamp)
                Traits.SetInt(character, GoblinKeys.TrainingDay, Math.Max(1, Day(character)));
            context.RecordMessage($"{character.Id} training phase → {phase} (day {Day(character)}).");
            return phase;
        }

        var current = Get(character);
        var day = Day(character);
        if (current is "" or RaidCamp)
        {
            day = Math.Max(1, day) + 1;
            Traits.SetInt(character, GoblinKeys.TrainingDay, day);
            if (day >= 4)
            {
                Traits.Set(character, GoblinKeys.TrainingPhase, Village);
                Traits.SetInt(character, GoblinKeys.TrainingDay, 4);
                context.RecordMessage(
                    $"{character.Id} raid-camp will-break complete → village structured training. " +
                    "Run cargo/riding pony tests; seed a role with goblin_state action=role.");
                return Village;
            }

            Traits.Set(character, GoblinKeys.TrainingPhase, RaidCamp);
            context.RecordMessage(
                $"{character.Id} raid-camp day {day}/3: rut, gag service, hood when idle; beg windows after use.");
            return RaidCamp;
        }

        if (current == Village)
        {
            day = Math.Max(4, day) + 1;
            Traits.SetInt(character, GoblinKeys.TrainingDay, day);
            if (day >= 8)
            {
                Traits.Set(character, GoblinKeys.TrainingPhase, Assigned);
                context.RecordMessage(
                    $"{character.Id} village training complete → assigned. Confirm role + loyalty tests.");
                return Assigned;
            }

            context.RecordMessage(
                $"{character.Id} village training day {day}: labor/display drills, parade, night pen stimulation.");
            return Village;
        }

        context.RecordMessage($"{character.Id} already assigned; use goblin_state action=role or loyalty notes.");
        return Assigned;
    }

    private static string Normalize(string raw) => raw.Trim().ToLowerInvariant() switch
    {
        "raid" or "raid_camp" or "camp" => RaidCamp,
        "village" or "break" => Village,
        "assigned" or "done" or "role" => Assigned,
        _ => RaidCamp,
    };
}
