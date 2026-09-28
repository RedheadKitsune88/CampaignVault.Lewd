using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;
using GoblinPonyriders.Changes;
using GoblinPonyriders.Events;
using GoblinPonyriders.Mechanics;

namespace GoblinPonyriders.Handlers;

public sealed class GoblinStateHandler : IWorldChangeHandler
{
    public bool ShouldHandle(WorldChange change) => change is GoblinStateChange;

    public async Task<ChangeHandlerResult> ApplyAsync(
        WorldChange change,
        IChangeContext context,
        CancellationToken ct = default)
    {
        var req = (GoblinStateChange)change;
        var action = (req.Action ?? "status").Trim().ToLowerInvariant();

        if (action == "seed_faction")
            return SeedFaction(context, req);

        if (string.IsNullOrWhiteSpace(req.CharacterId))
            return ChangeHandlerResult.Failure("characterId is required (except seed_faction).");
        if (!context.Characters.TryGetValue(req.CharacterId, out var character))
            return ChangeHandlerResult.Failure($"Character '{req.CharacterId}' is not in the commit context.");

        var settings = await GoblinSettings.ResolveAsync(context, ct).ConfigureAwait(false);
        if (!settings.Enabled && action is not ("status" or "seed_faction"))
            return ChangeHandlerResult.Failure(
                "goblinClans is off. Player must enable it (playerRequest) before Unified Clans capture/training verbs run.");

        if (action is not ("status" or "seed_faction") &&
            settings.HitsHardLimit(GoblinKeys.HardLimitProbe, out var hit))
            return ChangeHandlerResult.Failure($"Hard limit '{hit}' blocks goblin_state.");

        return action switch
        {
            "capture" => Capture(req, character, context),
            "release" => Release(req, character, context),
            "defiance" => Defiance(req, character, context),
            "mark" => Mark(req, character, context),
            "role" => Role(req, character, context),
            "train" => Train(req, character, context),
            "name" => Name(req, character, context),
            "status" => Status(character, context),
            _ => ChangeHandlerResult.Failure(
                "action must be seed_faction, capture, release, defiance, mark, role, train, name, or status."),
        };
    }

    private static ChangeHandlerResult SeedFaction(IChangeContext context, GoblinStateChange req)
    {
        var id = string.IsNullOrWhiteSpace(req.FactionId) ? GoblinKeys.FactionId : req.FactionId.Trim();
        if (!string.Equals(id, GoblinKeys.FactionId, StringComparison.OrdinalIgnoreCase))
            return ChangeHandlerResult.Failure($"This plugin seeds only '{GoblinKeys.FactionId}'.");

        if (GoblinFaction.RefreshIfPresent(context, out var message))
        {
            context.RecordMessage(message);
            return ChangeHandlerResult.Ok;
        }

        return ChangeHandlerResult.Failure(message);
    }

    private static ChangeHandlerResult Capture(GoblinStateChange req, Character character, IChangeContext context)
    {
        CaptureState.Begin(character, context, req.AssignedName, startingDefiance: req.Absolute ? Math.Clamp(req.Delta, 0, 10) : 7);
        context.Publish(GoblinEvents.Capture, new
        {
            characterId = character.Id,
            action = "capture",
            assignedName = Traits.Get(character, GoblinKeys.AssignedName),
            factionId = GoblinKeys.FactionId,
        });
        return ChangeHandlerResult.Ok;
    }

    private static ChangeHandlerResult Release(GoblinStateChange req, Character character, IChangeContext context)
    {
        if (!CaptureState.IsCaptured(character) && !req.TerrorRelease)
            return ChangeHandlerResult.Failure($"{character.Id} is not in Capture State.");

        CaptureState.End(character, context, req.TerrorRelease, req.Reason);
        context.Publish(GoblinEvents.Release, new
        {
            characterId = character.Id,
            action = req.TerrorRelease ? "terror_release" : "release",
            reason = req.Reason,
            factionId = GoblinKeys.FactionId,
        });
        return ChangeHandlerResult.Ok;
    }

    private static ChangeHandlerResult Defiance(GoblinStateChange req, Character character, IChangeContext context)
    {
        var level = req.Absolute
            ? DefianceClock.Set(character, req.Delta, context, req.Reason)
            : DefianceClock.Adjust(character, req.Delta == 0 ? 1 : req.Delta, context, req.Reason);
        context.Publish(GoblinEvents.Defiance, new
        {
            characterId = character.Id,
            action = "defiance",
            level,
            delta = req.Delta,
            reason = req.Reason,
        });
        context.RecordMessage($"{character.Id} defiance telegraph: {DefianceClock.Telegraph(level)}.");
        return ChangeHandlerResult.Ok;
    }

    private static ChangeHandlerResult Mark(GoblinStateChange req, Character character, IChangeContext context)
    {
        var level = req.Absolute
            ? ClanMark.Set(character, req.Delta, context, req.Reason)
            : ClanMark.Raise(character, req.Delta == 0 ? 1 : req.Delta, context, req.Reason);
        context.Publish(GoblinEvents.ClanMark, new
        {
            characterId = character.Id,
            action = "mark",
            level,
            reason = req.Reason,
        });
        return ChangeHandlerResult.Ok;
    }

    private static ChangeHandlerResult Role(GoblinStateChange req, Character character, IChangeContext context)
    {
        if (!RoleCatalog.TryGet(req.RoleId, out var role))
            return ChangeHandlerResult.Failure($"roleId must be one of: {RoleCatalog.ListIds()}.");

        Traits.Set(character, GoblinKeys.Role, role.Id);
        if (TrainingPhases.Get(character) is "" or TrainingPhases.RaidCamp or TrainingPhases.Village)
            Traits.Set(character, GoblinKeys.TrainingPhase, TrainingPhases.Assigned);

        context.RecordMessage(
            $"{character.Id} assigned role {role.DisplayName} ({role.Id}). {role.Summary} " +
            $"Lewd bind hint: {role.LewdBindHint}");
        context.Publish(GoblinEvents.Role, new
        {
            characterId = character.Id,
            action = "role",
            role = role.Id,
            reason = req.Reason,
        });
        return ChangeHandlerResult.Ok;
    }

    private static ChangeHandlerResult Train(GoblinStateChange req, Character character, IChangeContext context)
    {
        if (!CaptureState.IsCaptured(character) && TrainingPhases.Get(character) is "")
            return ChangeHandlerResult.Failure("train requires Capture State or an existing training phase.");

        var phase = TrainingPhases.Advance(character, context, req.Phase);
        context.Publish(GoblinEvents.Training, new
        {
            characterId = character.Id,
            action = "train",
            phase,
            day = TrainingPhases.Day(character),
            reason = req.Reason,
        });
        return ChangeHandlerResult.Ok;
    }

    private static ChangeHandlerResult Name(GoblinStateChange req, Character character, IChangeContext context)
    {
        if (string.IsNullOrWhiteSpace(req.AssignedName))
            return ChangeHandlerResult.Failure("assignedName is required for action=name.");
        Traits.Set(character, GoblinKeys.AssignedName, req.AssignedName.Trim());
        context.RecordMessage(
            $"{character.Id} assigned clan name '{req.AssignedName.Trim()}'. Tattoo during public naming if in village; iron ear notch follows.");
        context.Publish(GoblinEvents.Name, new
        {
            characterId = character.Id,
            action = "name",
            assignedName = req.AssignedName.Trim(),
            factionId = GoblinKeys.FactionId,
        });
        return ChangeHandlerResult.Ok;
    }

    private static ChangeHandlerResult Status(Character character, IChangeContext context)
    {
        context.RecordMessage(
            $"{character.Id} goblin status: capture={CaptureState.IsCaptured(character)} " +
            $"defiance={DefianceClock.Get(character)}/10 ({DefianceClock.Telegraph(DefianceClock.Get(character))}) " +
            $"clan_mark={ClanMark.Get(character)}/5 role={Traits.Get(character, GoblinKeys.Role) switch { "" => "none", var r => r }} " +
            $"phase={TrainingPhases.Get(character) switch { "" => "none", var p => p }} " +
            $"day={TrainingPhases.Day(character)} name={Traits.Get(character, GoblinKeys.AssignedName) switch { "" => "none", var n => n }} " +
            $"terror_released={Traits.Flag(character, GoblinKeys.TerrorReleased)}.");
        return ChangeHandlerResult.Ok;
    }
}
