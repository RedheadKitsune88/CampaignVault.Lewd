using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;
using LewdHandbook.Changes;
using LewdHandbook.Mechanics;

namespace LewdHandbook.Handlers;

public sealed class LewdHumiliateHandler : IWorldChangeHandler
{
    public bool ShouldHandle(WorldChange change) => change is LewdHumiliateChange;

    public async Task<ChangeHandlerResult> ApplyAsync(
        WorldChange change,
        IChangeContext context,
        CancellationToken ct = default)
    {
        var req = (LewdHumiliateChange)change;
        if (string.IsNullOrWhiteSpace(req.CharacterId))
            return ChangeHandlerResult.Failure("characterId is required.");
        // sourceId may be a crowd/ritual label; only age-gate when it names a loaded character.
        var ageIds = new List<string?> { req.CharacterId };
        if (!string.IsNullOrWhiteSpace(req.SourceId) && context.Characters.ContainsKey(req.SourceId))
            ageIds.Add(req.SourceId);
        if (!AgeGate.TryPassAll(context, out var ageError, ageIds.ToArray()))
            return ChangeHandlerResult.Failure(ageError!);
        if (!context.Characters.TryGetValue(req.CharacterId, out var character))
            return ChangeHandlerResult.Failure($"Character '{req.CharacterId}' is not in the commit context.");

        var severity = Math.Clamp(req.Severity <= 0 ? 1 : req.Severity, 1, 3);
        var tags = req.Tags ?? [];
        var probe = Humiliation.HardLimitTags.Concat(tags).Concat(["humiliate"]);
        var settings = await LewdSettings.ResolveAsync(context, ct).ConfigureAwait(false);
        if (!settings.Humiliation)
            return ChangeHandlerResult.Failure("lewdHumiliation is off.");

        var participant = LewdModeAccess.TryGetParticipant(context, req.CharacterId);
        if (!ConsentGate.AuthorizeEffect(
                participant, character, req.CharacterId, req.SourceId, probe, settings, out _, out var gateError))
            return ChangeHandlerResult.Failure(gateError!);

        var day = await ImprintState.DayAsync(context, ct).ConfigureAwait(false);
        var now = await LewdClock.NowDaysAsync(context).ConfigureAwait(false) ?? day;
        var scope = Humiliation.ScopeKey(req.SourceId, tags);
        var shame = Humiliation.TryApplyShame(character, severity, scope, day, now, settings, out var drained);
        if (shame is null)
        {
            context.RecordMessage(
                $"{req.CharacterId}: humiliation skipped (daily cap, duplicate source+tags today, or a stronger shame still live).");
            return ChangeHandlerResult.Ok;
        }

        context.RecordMessage(shame + (string.IsNullOrWhiteSpace(req.Reason) ? "" : $" Reason: {req.Reason}."));
        Humiliation.MarkRecent(character, day);

        var ordeal = ImprintState.Level(character, "ordeal");
        var arousalDelta = 0;
        if (severity >= 2 && ordeal >= 1)
        {
            var die = await LewdDice.RollAsync(
                context, "lewd_humiliate_arousal", 1, 4,
                supplied: req.ArousalD4 is >= 1 and <= 4 ? [req.ArousalD4] : null,
                ct).ConfigureAwait(false);
            arousalDelta = Humiliation.ArousalDelta(severity, ordeal, die.Faces.FirstOrDefault());
            if (arousalDelta > 0)
            {
                var arousal = LewdPoolHelper.Arousal(character);
                if (arousal.Max > 0)
                {
                    var before = arousal.Current;
                    arousal.Current = Math.Min(arousal.Max, arousal.Current + arousalDelta);
                    if (participant is not null)
                        LewdPoolHelper.MirrorArousal(participant, arousal);
                    context.RecordMessage(
                        $"{req.CharacterId}: Ordeal {ordeal} converts shame → +{arousalDelta} arousal ({before}→{arousal.Current}/{arousal.Max}; {die.Summary}).");
                }
                else
                    arousalDelta = 0;
            }
        }

        context.Publish(
            Events.LewdEvents.Humiliated,
            new Dictionary<string, object?>
            {
                [Events.LewdEvents.Fields.CharacterId] = req.CharacterId,
                [Events.LewdEvents.Fields.Severity] = severity,
                [Events.LewdEvents.Fields.SourceId] = string.IsNullOrWhiteSpace(req.SourceId) ? null : req.SourceId.Trim(),
                [Events.LewdEvents.Fields.Reason] = string.IsNullOrWhiteSpace(req.Reason) ? null : req.Reason.Trim(),
                [Events.LewdEvents.Fields.WillpowerDrained] = drained,
                [Events.LewdEvents.Fields.Ordeal] = ordeal,
                [Events.LewdEvents.Fields.ArousalDelta] = arousalDelta,
                ["tags"] = tags.ToArray(),
            });

        settings.Narrate(context);
        return ChangeHandlerResult.Ok;
    }
}
