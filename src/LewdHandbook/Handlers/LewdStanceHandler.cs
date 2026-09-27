using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;
using LewdHandbook.Changes;
using LewdHandbook.Mechanics;

namespace LewdHandbook.Handlers;

public sealed class LewdStanceHandler : IWorldChangeHandler
{
    private static readonly string[] Stances =
    [
        LewdKeys.ConsentWilling, LewdKeys.ConsentSelective, LewdKeys.ConsentUnwilling, LewdKeys.ConsentRevoked,
    ];

    public bool ShouldHandle(WorldChange change) => change is LewdStanceChange;

    public Task<ChangeHandlerResult> ApplyAsync(WorldChange change, IChangeContext context, CancellationToken ct = default)
    {
        var req = (LewdStanceChange)change;
        if (string.IsNullOrWhiteSpace(req.CharacterId))
            return Task.FromResult(ChangeHandlerResult.Failure("characterId is required."));
        if (!AgeGate.TryPassAll(context, out var ageError, req.CharacterId))
            return Task.FromResult(ChangeHandlerResult.Failure(ageError!));
        var character = context.Characters[req.CharacterId];

        var stance = req.Stance?.Trim().ToLowerInvariant();
        if (stance is not null && !Stances.Contains(stance))
            return Task.FromResult(ChangeHandlerResult.Failure("stance must be willing, selective, unwilling, or revoked."));

        var participant = LewdModeAccess.TryGetParticipant(context, req.CharacterId);
        var scope = req.Scope?.Trim().ToLowerInvariant() ?? (participant is null ? "default" : "scene");
        if (scope is not ("scene" or "default"))
            return Task.FromResult(ChangeHandlerResult.Failure("scope must be scene or default."));
        if (scope == "scene" && participant is null)
            return Task.FromResult(ChangeHandlerResult.Failure(
                $"'{req.CharacterId}' is not in an active lewd_encounter; use scope=default for lasting defaults."));

        var written = new List<string>();
        if (scope == "scene")
        {
            var state = participant!.State;
            if (stance is not null) { state[LewdKeys.Consent] = stance; written.Add($"stance={stance}"); }
            SetList(state, LewdKeys.AllowedPartners, req.AllowedPartners, written);
            SetList(state, LewdKeys.HardLimits, req.HardLimits, written);
            SetList(state, LewdKeys.SoftLimits, req.SoftLimits, written);
            SetList(state, LewdKeys.Kinks, req.Kinks, written);
            if (req.Inhibition is { } inhib) { state[LewdKeys.Inhibition] = inhib; written.Add($"inhibition={inhib}"); }
        }
        else
        {
            var traits = character.SystemStats.Traits;
            if (stance is not null) { traits[LewdKeys.TraitStance] = stance; written.Add($"stance={stance}"); }
            SetTrait(traits, LewdKeys.TraitAllowedPartners, req.AllowedPartners, written);
            SetTrait(traits, LewdKeys.TraitHardLimits, req.HardLimits, written);
            SetTrait(traits, LewdKeys.TraitSoftLimits, req.SoftLimits, written);
            SetTrait(traits, LewdKeys.TraitKinks, req.Kinks, written);
            if (req.Inhibition is { } inhib) { traits[LewdKeys.TraitInhibition] = inhib.ToString(); written.Add($"inhibition={inhib}"); }
        }

        if (written.Count == 0)
            return Task.FromResult(ChangeHandlerResult.Failure("lewd_stance has nothing to set."));

        context.RecordMessage($"{req.CharacterId} lewd stance ({scope}): {string.Join(", ", written)}.");
        if (stance == LewdKeys.ConsentRevoked)
            context.RecordPhysicalStateNudge($"{req.CharacterId} revoked consent. Stop: no further lewd verbs touch them.");
        return Task.FromResult(ChangeHandlerResult.Ok);
    }

    private static void SetList(Dictionary<string, object> state, string key, List<string>? values, List<string> written)
    {
        if (values is null)
            return;
        state[key] = Clean(values);
        written.Add($"{key}=[{string.Join(",", Clean(values))}]");
    }

    private static void SetTrait(Dictionary<string, string> traits, string key, List<string>? values, List<string> written)
    {
        if (values is null)
            return;
        var clean = Clean(values);
        if (clean.Count == 0)
            traits.Remove(key);
        else
            traits[key] = string.Join(",", clean);
        written.Add($"{key}=[{string.Join(",", clean)}]");
    }

    private static List<string> Clean(IEnumerable<string> values) =>
        values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}
