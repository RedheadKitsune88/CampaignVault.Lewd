using CampaignVault.Data;
using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;
using LewdHandbook.Changes;
using LewdHandbook.Mechanics;

namespace LewdHandbook.Handlers;

public sealed class LewdAdvanceHandler : IWorldChangeHandler
{
    public bool ShouldHandle(WorldChange change) => change is LewdAdvanceChange;

    public async Task<ChangeHandlerResult> ApplyAsync(
        WorldChange change,
        IChangeContext context,
        CancellationToken ct = default)
    {
        var advance = (LewdAdvanceChange)change;
        if (string.IsNullOrWhiteSpace(advance.ActorId) || string.IsNullOrWhiteSpace(advance.TargetId))
            return ChangeHandlerResult.Failure("actorId and targetId are required.");

        var mode = LewdModeAccess.TryGetActive(context);
        if (mode is null)
        {
            return ChangeHandlerResult.Failure(
                "lewd_advance requires an active lewd_encounter mode. Enter via mode_transition first.");
        }

        var actor = mode.Participants.FirstOrDefault(p =>
            string.Equals(p.CharacterId, advance.ActorId, StringComparison.OrdinalIgnoreCase));
        if (actor is null)
            return ChangeHandlerResult.Failure($"Actor '{advance.ActorId}' is not in the lewd encounter.");

        var target = mode.Participants.FirstOrDefault(p =>
            string.Equals(p.CharacterId, advance.TargetId, StringComparison.OrdinalIgnoreCase));
        if (target is null)
            return ChangeHandlerResult.Failure($"Target '{advance.TargetId}' is not in the lewd encounter.");

        if (!AgeGate.TryPassAll(context, out var ageError, advance.ActorId, advance.TargetId))
            return ChangeHandlerResult.Failure(ageError!);
        var actorChar = context.Characters[advance.ActorId];
        var targetChar = context.Characters[advance.TargetId];

        if (BindingGraph.BlocksRequiredSites(actor, actorChar, advance.RequiresFreeSites, out var bindError))
            return ChangeHandlerResult.Failure(bindError!);

        var settings = await LewdSettings.ResolveAsync(context, ct).ConfigureAwait(false);
        var kind = string.IsNullOrWhiteSpace(advance.Kind) ? "martial" : advance.Kind.Trim().ToLowerInvariant();
        var stimType = advance.StimulationType;

        if (ImplementResolver.CheckId(actorChar, context.Items, advance.ImplementId) is { } implementError)
            return ChangeHandlerResult.Failure(implementError);

        // Resolve the implement before the consent check so its tags count against hard limits too.
        var stim = advance.StimulationAmount;
        var implement = ImplementResolver.Resolve(
            actorChar,
            context.Items,
            advance.ImplementId,
            advance.AnatomyKey,
            advance.StimulationDice,
            advance.StimulationType,
            allowGuess: stim <= 0);
        var tags = advance.Tags ?? [];
        if (implement is not null)
            tags = tags.Concat(implement.Tags).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        stimType ??= implement?.DamageType;

        if (!ConsentGate.AuthorizeAdvance(target, targetChar, advance.ActorId, stimType, tags, settings, out var gateError))
            return ChangeHandlerResult.Failure(gateError!);

        var wanted = ConsentGate.IsAdvanceWanted(target, advance.ActorId, targetChar);

        // Resolve hit for martial
        var hit = advance.Hit;
        if (kind is "martial" && hit is null)
        {
            if (wanted)
            {
                hit = true;
            }
            else if (context.Rolls is not null && advance.AttackBonus is not null && advance.TargetAc is not null)
            {
                var attack = await context.Rolls.RollAsync(
                    new RollRequest
                    {
                        Tag = "lewd_martial_attack",
                        Expression = "1d20",
                        Bonus = advance.AttackBonus.Value,
                    },
                    ct).ConfigureAwait(false);
                hit = attack.Result >= advance.TargetAc.Value;
                advance.IsCritical = advance.IsCritical || attack.HasCritical;
                context.RecordMessage(
                    $"Lewd martial attack roll: {attack.Summary} vs AC {advance.TargetAc} → {(hit.Value ? "hit" : "miss")}.");
            }
            else
            {
                return ChangeHandlerResult.Failure(
                    "Unwilling martial advance requires hit=true|false (or attackBonus+targetAc with Rolls).");
            }
        }

        if (kind is "martial" && hit == false)
        {
            context.RecordMessage($"Lewd martial advance miss: {advance.ActorId} → {advance.TargetId}.");
            return ChangeHandlerResult.Ok;
        }

        var arousal = LewdPoolHelper.Arousal(targetChar);
        if (arousal.Max <= 0)
        {
            // Handbook: an arousal maximum of 0 or less is a bad end, not a pool to quietly refill.
            BadEndState.Apply(target, targetChar, BadEndMath.ArousalMax, advance.ActorId, context: context, settings: settings);
            context.RecordMessage($"{advance.TargetId} arousal maximum is {arousal.Max}; no stimulation applies.");
            return ChangeHandlerResult.Ok;
        }

        var maximize = advance.MaximizeStimulation || ConsentGate.GetBool(target, LewdKeys.Edging);
        if (stim <= 0)
        {
            if (implement is null)
            {
                return ChangeHandlerResult.Failure(
                    "stimulationAmount is required when no implement/anatomy/stimulationDice can be resolved.");
            }

            if (maximize)
            {
                stim = ImplementResolver.MaximizeDice(implement.DiceExpression) + advance.AbilityBonus;
            }
            else if (context.Rolls is not null)
            {
                var roll = await context.Rolls.RollAsync(
                    new RollRequest
                    {
                        Tag = "lewd_stimulation",
                        Expression = implement.DiceExpression,
                        Bonus = advance.AbilityBonus,
                    },
                    ct).ConfigureAwait(false);
                stim = roll.Result;
                context.RecordMessage($"Lewd stim roll ({implement.Source}): {roll.Summary}.");
            }
            else
            {
                return ChangeHandlerResult.Failure(
                    "stimulationAmount required when Rolls is unavailable; or supply a pre-resolved amount.");
            }
        }
        else if (maximize && implement is not null)
        {
            var maxed = ImplementResolver.MaximizeDice(implement.DiceExpression) + advance.AbilityBonus;
            if (maxed > stim)
                stim = maxed;
        }

        // A low roll with a negative ability modifier is a weak advance, not an invalid commit.
        stim = Math.Max(0, stim);

        stim = ConsentGate.AdjustStimulationForTags(target, targetChar, stim, stimType, tags);
        var flirtBeats = ConsentGate.GetInt(target, LewdKeys.FlirtBeats);
        var verbal = ConsentGate.IsVerbalOrNonContact(kind, tags);
        if (verbal)
        {
            flirtBeats++;
            target.State[LewdKeys.FlirtBeats] = flirtBeats;
        }
        else
        {
            target.State[LewdKeys.HadPhysical] = true;
            target.State[LewdKeys.FlirtBeats] = 0;
            BrandState.RecordStimulation(targetChar, tags);
            ConsentGate.RecordStimulatedBy(targetChar, advance.ActorId);
        }

        var beforeCap = stim;
        stim = ConsentGate.CapVerbalStimulation(target, targetChar, kind, tags, stim, flirtBeats);
        var capNote = stim < beforeCap ? $" Verbal cap {beforeCap}→{stim}." : "";
        if (!verbal)
            stim += ImprintState.SufferingBonus(actorChar, tags, ConsentGate.GetInt(target, LewdKeys.Overstimulation));

        var numbing = LewdPoolHelper.Numbing(targetChar);
        var result = StimulationMath.Apply(
            arousal.Current,
            arousal.Max,
            numbing.Current,
            stim,
            advance.IsCritical);

        numbing.Current = result.NumbingAfter;
        arousal.Current = result.ArousalAfter;
        LewdPoolHelper.MirrorArousal(target, arousal);

        var wasIncap = ConsentGate.GetBool(target, LewdKeys.ClimaxIncapacitated);
        var (successes, failures) = LewdPoolHelper.ClimaxCounters(target, targetChar);
        var climaxNote = "";

        if (result.InstantClimax || result.AutoClimaxFailures > 0)
        {
            var blockVerbal = ConsentGate.BlocksVerbalClimax(target, targetChar, kind, tags);
            if (blockVerbal && result.InstantClimax)
            {
                LewdPoolHelper.SetEdging(target, targetChar, true);
                climaxNote = " Verbal/non-contact climax blocked (inexperienced history, no prior physical scene); edging held.";
            }
            else
            {
                var climax = ClimaxMath.ApplyAutoFailures(
                    result.AutoClimaxFailures,
                    successes,
                    failures,
                    arousal.Current,
                    arousal.Max,
                    result.InstantClimax && !blockVerbal);
                var selfEcho = string.Equals(advance.ActorId, advance.TargetId, StringComparison.OrdinalIgnoreCase) &&
                               BrandState.Has(targetChar, BrandCatalog.Echoes);
                if (selfEcho)
                {
                    if (arousal.Current >= arousal.Max)
                        LewdPoolHelper.SetEdging(target, targetChar, true);
                    climaxNote = " Brand of Echoes: cannot self-climax.";
                }
                else
                {
                    var ruin = await BrandState.PreRollRuinAsync(context, targetChar, ct).ConfigureAwait(false);
                    climaxNote = " " + climax.Summary + ApplyClimaxResult(
                        target, targetChar, arousal, climax, advance.ActorId, context, tags, forced: false, ruin: ruin,
                        nowDays: await LewdClock.NowDaysAsync(context).ConfigureAwait(false));
                }
            }
        }
        else
        {
            LewdPoolHelper.WriteClimaxCounters(target, successes, failures, targetChar);
        }

        var typeLabel = string.IsNullOrWhiteSpace(stimType) ? "untyped" : stimType;
        var implLabel = implement is null ? "" : $" via {implement.Source}";
        context.RecordMessage(
            $"Lewd {kind} advance {advance.ActorId} → {advance.TargetId}{implLabel}: +{stim} {typeLabel} stim " +
            $"(numbing {result.NumbingBefore}→{result.NumbingAfter}, arousal {result.ArousalBefore}→{result.ArousalAfter}/{arousal.Max})." +
            capNote + climaxNote);
        settings.Narrate(context);
        if (!string.Equals(advance.ActorId, advance.TargetId, StringComparison.OrdinalIgnoreCase) &&
            BrandState.Has(actorChar, BrandCatalog.Echoes) &&
            stim > 0)
        {
            BrandState.EchoStim(actor, actorChar, stim, context);
        }

        if (!verbal)
        {
            var tickTags = tags.ToList();
            if (wasIncap)
                tickTags.Add("incapacitated");
            await ImprintState.AutoAsync(context, target, targetChar, tickTags, wanted, bitchsuit: false, ct, anchorId: advance.ActorId)
                .ConfigureAwait(false);
        }

        if (!verbal && tags.Any(ImprintMath.IsCruelty))
        {
            // The actor chose the cruelty, so their own imprint is willing.
            await ImprintState.AutoAsync(
                context,
                actor,
                actorChar,
                tags.Where(ImprintMath.IsCruelty),
                wanted: true,
                bitchsuit: false,
                ct).ConfigureAwait(false);
        }

        if (!verbal && wanted && !wasIncap && !tags.Any(ImprintMath.IsCruelty))
        {
            var day = await ImprintState.DayAsync(context, ct).ConfigureAwait(false);
            var now = await LewdClock.NowDaysAsync(context).ConfigureAwait(false) ?? day;
            foreach (var (who, partner) in new[] { (actorChar, advance.TargetId), (targetChar, advance.ActorId) })
            {
                if (LewdMood.TryGrant(who, LewdMood.WarmGlow, partner, day, now, settings) is { } mood)
                    context.RecordMessage(mood);
            }
        }

        return ChangeHandlerResult.Ok;
    }

    /// <summary>
    /// Applies a resolved climax save (or instant/forced climax) and publishes <c>climax.v1</c> with what actually
    /// happened: <c>climax</c>, <c>denied</c> / <c>ruined</c> (a brand intercepted it), <c>held</c> (three successes
    /// or a natural 20: edging cleared) or <c>edging</c> (still pending).
    /// </summary>
    internal static string ApplyClimaxResult(
        ModeParticipantState target,
        Character? character,
        ResourcePool arousal,
        ClimaxSaveResult climax,
        string? sourceId = null,
        IChangeContext? context = null,
        IEnumerable<string>? tags = null,
        bool forced = false,
        BrandState.RuinDice? ruin = null,
        double? nowDays = null)
    {
        var climaxed = climax.Kind is ClimaxOutcomeKind.Climaxed or ClimaxOutcomeKind.InstantClimax;
        var overstim = ConsentGate.GetInt(target, LewdKeys.Overstimulation);
        var wasEdging = ConsentGate.GetBool(target, LewdKeys.Edging);
        ResourcePool? recovery = null;
        var hasRecovery = character?.SystemStats.ResourcePools.TryGetValue(LewdKeys.PoolRecoveryDice, out recovery) == true &&
                          recovery is not null;
        var recoveryCurrent = recovery?.Current ?? 0;

        if (climaxed && BrandState.InterceptClimax(target, character, arousal, context, out var intercepted, ruin, tags, forced))
        {
            var blockedBy = BrandState.BlocksClimax(character!) || intercepted.Contains("Fertility") ? "denied" : "ruined";
            PublishClimax(context, target, blockedBy, forced);
            return intercepted;
        }

        arousal.Current = climax.ArousalAfter;
        LewdPoolHelper.MirrorArousal(target, arousal);
        LewdPoolHelper.WriteClimaxCounters(target, climax.Successes, climax.Failures, character);

        if (!climaxed)
        {
            LewdPoolHelper.SetEdging(target, character, climax.EdgingAfter);
            PublishClimax(context, target, climax.Kind == ClimaxOutcomeKind.HeldEdge ? "held" : "edging", forced);
            return "";
        }

        var wasIncap = ConsentGate.GetBool(target, LewdKeys.ClimaxIncapacitated);
        var streak = ConsentGate.GetInt(target, LewdKeys.ClimaxStreak);
        var tick = OverstimMath.OnClimax(streak, wasIncap, overstim);
        target.State[LewdKeys.ClimaxStreak] = tick.ClimaxStreak;
        target.State[LewdKeys.ClimaxIncapacitated] = true;
        // Until the end of this participant's next turn; each climax while still incapacitated adds a turn.
        target.State[LewdKeys.ClimaxIncapTurns] = wasIncap ? ConsentGate.GetInt(target, LewdKeys.ClimaxIncapTurns) + 1 : 1;

        var level = tick.OverstimulationAfter;
        if (tick.OverstimIncreased)
            LewdPoolHelper.SetOverstimulation(target, character, level, sourceId, context);

        var decision = BadEndMath.Evaluate(new BadEndProbe(
            BadEndState.IsMarked(target, character),
            level,
            Climaxed: true,
            wasEdging,
            hasRecovery,
            recoveryCurrent,
            HasArousalPool: false,
            ArousalMax: 1,
            CurrentHp: null));
        var markedReason = decision.Reason;
        if (decision.Mark && decision.Reason is not null)
            BadEndState.Apply(target, character, decision.Reason, sourceId, context: context);
        else if (BadEndState.IsMarked(target, character) && level >= OverstimMath.MaxLevel)
            markedReason = PregnancyState.Text(character, LewdKeys.BadEndReason) ?? BadEndMath.Overstim;

        if (character is not null && BadEndRescue.IsPending(character))
        {
            PublishClimax(context, target, "climax", forced);
            return $" {character.Id} blacks out (fade to black); the rescue is resolved next.";
        }

        var keepEdging = climax.EdgingAfter || level >= 5;
        LewdPoolHelper.SetEdging(target, character, keepEdging);
        if (character is not null)
        {
            LewdPoolHelper.SyncOverstimCascade(character, level, sourceId);
            LewdPoolHelper.StampClimaxIncapacitation(character, LewdKeys.ConditionIncapacitated, nowDays);
            if (tick.IncapacitationCondition is { } incapacitation)
                LewdPoolHelper.StampClimaxIncapacitation(character, incapacitation, nowDays);
        }

        var concentration = "";
        if (character is not null && nowDays is { } climaxDay)
        {
            var hours = (float)(climaxDay * 24);
            ClimaxLog.Record(character, hours);
            // Handbook: Concentration check on each climax, DC 15 + climaxes in the past minute (this one included).
            concentration = $" Concentration check DC {15 + ClimaxLog.Count(character, hours, 0)} for anything they concentrate on that is not a sexual advance.";
        }

        PublishClimax(context, target, "climax", forced);
        if (character is not null && hasRecovery && recoveryCurrent > 0 && arousal.Max > 0)
            RecoveryWindow.Open(character, RecoveryWindow.Climax, 0);
        var extra = tick.IncapacitationCondition is null ? "" : $" {tick.IncapacitationCondition}.";
        var osNote = tick.OverstimIncreased ? $" Overstimulation {level}." : "";
        var bad = markedReason is null ? "" : $" Bad-Ended ({markedReason}).";
        var missing = decision.MissingRecoveryPool ? " recovery_dice pool missing; do not assume zero." : "";
        var brandNote = character is null ? "" : BrandState.OnClimax(target, character, tags, context);
        var spend = character is not null && RecoveryWindow.Kind(character) == RecoveryWindow.Climax
            ? $" May spend up to {ArousalMath.ProficiencyBonus(character)} recovery dice now (lewd_recover), before the next turn."
            : "";
        return $" Climax streak {tick.ClimaxStreak}.{extra}{osNote}{bad}{missing}{brandNote}{concentration}{spend}";
    }

    private static void PublishClimax(IChangeContext? context, ModeParticipantState target, string outcome, bool forced)
    {
        if (context is null)
            return;
        var inEncounter = LewdModeAccess.TryGetParticipant(context, target.CharacterId) is not null;
        context.Publish(
            Events.LewdEvents.Climax,
            new { characterId = target.CharacterId, outcome, forced, inEncounter });
    }
}
