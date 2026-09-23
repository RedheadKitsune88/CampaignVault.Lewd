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

        var mode = context.ActiveMode;
        if (mode is null || !mode.IsActive ||
            !string.Equals(mode.ModeId, LewdEncounterMode.ModeIdValue, StringComparison.OrdinalIgnoreCase))
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

        if (BindingGraph.BlocksRequiredSites(actor, advance.RequiresFreeSites, out var bindError))
            return ChangeHandlerResult.Failure(bindError!);

        var tone = await IntimacyTone.ResolveAsync(context, ct).ConfigureAwait(false);
        var auth = ConsentGate.AuthorizeAdvance(
            target, advance.ActorId, advance.StimulationType, advance.Tags, tone, out var gateError);

        if (auth == ConsentAuthorizeResult.Fail)
            return ChangeHandlerResult.Failure(gateError!);

        if (auth == ConsentAuthorizeResult.FadeNoStim)
        {
            context.RecordPhysicalStateNudge(
                $"{advance.TargetId} refuses / scene fades the sexual advance from {advance.ActorId}; no stimulation applied.");
            context.RecordMessage(
                $"Lewd advance faded (intimacyTone=fade): {advance.ActorId} → {advance.TargetId}.");
            return ChangeHandlerResult.Ok;
        }

        var kind = string.IsNullOrWhiteSpace(advance.Kind) ? "martial" : advance.Kind.Trim().ToLowerInvariant();
        var wanted = ConsentGate.IsAdvanceWanted(target, advance.ActorId);

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

        context.Characters.TryGetValue(advance.ActorId, out var actorChar);
        context.Characters.TryGetValue(advance.TargetId, out var targetChar);

        var stim = advance.StimulationAmount;
        var stimType = advance.StimulationType;
        var implement = ImplementResolver.Resolve(
            actorChar,
            context.Items,
            advance.ImplementId,
            advance.AnatomyKey,
            advance.StimulationDice,
            advance.StimulationType);

        var maximize = advance.MaximizeStimulation || ConsentGate.GetBool(target, LewdKeys.Edging);

        if (stim <= 0)
        {
            if (implement is null)
            {
                return ChangeHandlerResult.Failure(
                    "stimulationAmount is required when no implement/anatomy/stimulationDice can be resolved.");
            }

            stimType ??= implement.DamageType;
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

        if (stim < 0)
            return ChangeHandlerResult.Failure("stimulationAmount cannot be negative.");

        var tags = advance.Tags ?? [];
        if (implement is not null)
            tags = tags.Concat(implement.Tags).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        stim = ConsentGate.AdjustStimulationForTags(target, stim, stimType, tags);
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
        }

        var beforeCap = stim;
        stim = ConsentGate.CapVerbalStimulation(target, targetChar, kind, tags, stim, flirtBeats);
        if (!verbal)
            stim += ImprintState.SufferingBonus(actorChar, tags, ConsentGate.GetInt(target, LewdKeys.Overstimulation));

        var arousal = targetChar is not null
            ? LewdPoolHelper.EnsurePool(targetChar, LewdKeys.PoolArousal, defaultMax: 10, RecoveryType.Never)
            : new ResourcePool
            {
                Current = ConsentGate.GetInt(target, LewdKeys.ArousalCurrentMirror),
                Max = Math.Max(1, ConsentGate.GetInt(target, LewdKeys.ArousalMaxMirror)),
                Recovery = RecoveryType.Never,
            };
        var numbing = targetChar is not null
            ? LewdPoolHelper.EnsurePool(targetChar, LewdKeys.PoolNumbing, defaultMax: 0, RecoveryType.Never)
            : new ResourcePool { Current = 0, Max = 0, Recovery = RecoveryType.Never };

        if (arousal.Max <= 0)
            arousal.Max = 10;

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
        var successes = ConsentGate.GetInt(target, LewdKeys.ClimaxSuccesses);
        var failures = ConsentGate.GetInt(target, LewdKeys.ClimaxFailures);
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
                               targetChar is not null && BrandState.Has(targetChar, BrandCatalog.Echoes);
                if (selfEcho)
                {
                    if (arousal.Current >= arousal.Max)
                        LewdPoolHelper.SetEdging(target, targetChar, true);
                    climaxNote = " Brand of Echoes: cannot self-climax.";
                }
                else
                {
                    climaxNote = " " + climax.Summary + ApplyClimaxResult(target, targetChar, arousal, climax, advance.ActorId, context, tags);
                }
            }
        }
        else
        {
            LewdPoolHelper.WriteClimaxCounters(target, successes, failures);
        }

        var typeLabel = string.IsNullOrWhiteSpace(stimType) ? "untyped" : stimType;
        var implLabel = implement is null ? "" : $" via {implement.Source}";
        var capNote = stim < beforeCap ? $" Verbal cap {beforeCap}→{stim}." : "";
        context.RecordMessage(
            $"Lewd {kind} advance {advance.ActorId} → {advance.TargetId}{implLabel}: +{stim} {typeLabel} stim " +
            $"(numbing {result.NumbingBefore}→{result.NumbingAfter}, arousal {result.ArousalBefore}→{result.ArousalAfter}/{arousal.Max})." +
            capNote + climaxNote);
        if (!string.Equals(advance.ActorId, advance.TargetId, StringComparison.OrdinalIgnoreCase) &&
            actorChar is not null &&
            BrandState.Has(actorChar, BrandCatalog.Echoes) &&
            stim > 0)
        {
            BrandState.EchoStim(actor, actorChar, stim, context);
        }

        if (!verbal && targetChar is not null)
        {
            var tickTags = tags.ToList();
            if (wasIncap)
                tickTags.Add("incapacitated");
            await ImprintState.AutoAsync(context, target, targetChar, tickTags, wanted, bitchsuit: false, ct)
                .ConfigureAwait(false);
        }

        if (!verbal && actorChar is not null && tags.Any(ImprintMath.IsCruelty))
        {
            await ImprintState.AutoAsync(
                context,
                actor,
                actorChar,
                tags.Where(ImprintMath.IsCruelty),
                ConsentGate.IsAdvanceWanted(actor, advance.ActorId),
                bitchsuit: false,
                ct).ConfigureAwait(false);
        }

        return ChangeHandlerResult.Ok;
    }

    internal static string ApplyClimaxResult(
        ModeParticipantState target,
        Character? character,
        ResourcePool arousal,
        ClimaxSaveResult climax,
        string? sourceId = null,
        IChangeContext? context = null,
        IEnumerable<string>? tags = null)
    {
        var climaxed = climax.Kind is ClimaxOutcomeKind.Climaxed or ClimaxOutcomeKind.InstantClimax;
        var overstim = ConsentGate.GetInt(target, LewdKeys.Overstimulation);
        var wasEdging = ConsentGate.GetBool(target, LewdKeys.Edging);
        ResourcePool? recovery = null;
        var hasRecovery = character?.SystemStats.ResourcePools.TryGetValue(LewdKeys.PoolRecoveryDice, out recovery) == true &&
                          recovery is not null;
        var recoveryCurrent = recovery?.Current ?? 0;

        if (climaxed && BrandState.InterceptClimax(target, character, arousal, context, out var intercepted))
            return intercepted;

        arousal.Current = climax.ArousalAfter;
        LewdPoolHelper.MirrorArousal(target, arousal);
        LewdPoolHelper.WriteClimaxCounters(target, climax.Successes, climax.Failures);

        if (!climaxed)
        {
            LewdPoolHelper.SetEdging(target, character, climax.EdgingAfter);
            return "";
        }

        var wasIncap = ConsentGate.GetBool(target, LewdKeys.ClimaxIncapacitated);
        var streak = ConsentGate.GetInt(target, LewdKeys.ClimaxStreak);
        var tick = OverstimMath.OnClimax(streak, wasIncap, overstim);
        target.State[LewdKeys.ClimaxStreak] = tick.ClimaxStreak;
        target.State[LewdKeys.ClimaxIncapacitated] = true;

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

        var keepEdging = climax.EdgingAfter || level >= 5;
        LewdPoolHelper.SetEdging(target, character, keepEdging);
        StampOverstimCascade(character, level, tick.IncapacitationCondition, sourceId);

        var extra = tick.IncapacitationCondition is null ? "" : $" {tick.IncapacitationCondition}.";
        var osNote = tick.OverstimIncreased ? $" Overstimulation {level}." : "";
        var bad = markedReason is null ? "" : $" Bad-Ended ({markedReason}).";
        var missing = decision.MissingRecoveryPool ? " recovery_dice pool missing; do not assume zero." : "";
        var brandNote = character is null ? "" : BrandState.OnClimax(target, character, tags, context);
        return $" Climax streak {tick.ClimaxStreak}.{extra}{osNote}{bad}{missing}{brandNote}";
    }

    private static void StampOverstimCascade(
        Character? character,
        int level,
        string? incapacitation,
        string? sourceId)
    {
        if (character is null || level <= 0 && incapacitation is null)
            return;

        if (level >= 1)
            LewdPoolHelper.EnsureNamedCondition(character, LewdKeys.ConditionIntoxicated, LewdKeys.ConditionIntoxicated,
                "Overstimulation 1+: Intoxicated.");
        if (level >= 2)
            LewdPoolHelper.EnsureNamedCondition(character, LewdKeys.ConditionHyperaroused, LewdKeys.ConditionHyperaroused,
                "Overstimulation 2+: Hyperaroused.");
        if (level >= 4)
            LewdPoolHelper.EnsureNamedCondition(character, LewdKeys.ConditionInfatuated, LewdKeys.ConditionInfatuated,
                "Overstimulation 4+: Infatuated by the source of overstimulation.");
        if (incapacitation is not null)
            LewdPoolHelper.EnsureNamedCondition(character, incapacitation, incapacitation,
                "Repeated climax while still incapacitated.");

        if (!string.IsNullOrWhiteSpace(sourceId))
        {
            var inf = character.SystemStats.StatusEffects.FirstOrDefault(e =>
                string.Equals(e.Name, LewdKeys.ConditionInfatuated, StringComparison.OrdinalIgnoreCase));
            if (inf is not null && level >= 4)
                inf.AppliedBy = sourceId;
        }
    }
}
