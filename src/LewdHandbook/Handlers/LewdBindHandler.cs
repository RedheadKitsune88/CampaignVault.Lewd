using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;
using LewdHandbook.Changes;
using LewdHandbook.Mechanics;

namespace LewdHandbook.Handlers;

public sealed class LewdBindHandler : IWorldChangeHandler
{
    /// <summary>Kinds of gear that are sex-scene equipment even outside a lewd_encounter.</summary>
    private static readonly string[] EroticGear = ["bitchsuit", "spreader", "crotch", "vibrat", "chastity", "shibari", "breast"];

    public bool ShouldHandle(WorldChange change) => change is LewdBindChange;

    public async Task<ChangeHandlerResult> ApplyAsync(
        WorldChange change,
        IChangeContext context,
        CancellationToken ct = default)
    {
        var bind = (LewdBindChange)change;
        if (string.IsNullOrWhiteSpace(bind.TargetId))
            return ChangeHandlerResult.Failure("targetId is required.");
        if (!context.Characters.TryGetValue(bind.TargetId, out var targetChar))
            return ChangeHandlerResult.Failure($"Target '{bind.TargetId}' is not in the commit context.");
        if (!AgeGate.TryPassAll(context, out var ageError, bind.ActorId, bind.TargetId))
            return ChangeHandlerResult.Failure(ageError!);
        if (!string.IsNullOrWhiteSpace(bind.AnchorId) &&
            string.Equals(bind.AnchorId.Trim(), bind.TargetId, StringComparison.OrdinalIgnoreCase))
            return ChangeHandlerResult.Failure("anchorId cannot be the bound character itself.");
        if (!string.IsNullOrWhiteSpace(bind.Slack) && !BindingGraph.IsSlack(bind.Slack))
            return ChangeHandlerResult.Failure("slack must be tight or loose.");
        if (!string.IsNullOrWhiteSpace(bind.Quality) && !BondageSlots.IsQuality(bind.Quality))
            return ChangeHandlerResult.Failure($"quality must be one of: {BondageSlots.QualityList}.");
        // An anchor or holder that is a character is a participant like any other: adults only.
        var linked = new[] { bind.AnchorId, bind.HolderId }
            .Where(id => !string.IsNullOrWhiteSpace(id) && context.Characters.ContainsKey(id!.Trim()))
            .Select(id => id!.Trim()).ToArray();
        if (linked.Length > 0 && !AgeGate.TryPassAll(context, out var linkError, linked))
            return ChangeHandlerResult.Failure(linkError!);
        if (!string.IsNullOrWhiteSpace(bind.AnchorId) &&
            (targetChar.SystemStats.Tethers?.Count(t => t.AttachedBy?.StartsWith(Restraint.AppliedBy + ":", StringComparison.Ordinal) != true) ?? 0)
            + (BindingGraph.GetBindings(null, targetChar).Count(b => b.TetherOn)) >= 4)
            return ChangeHandlerResult.Failure("Target already has four tethers; free one first.");

        var target = LewdModeAccess.TryGetParticipant(context, bind.TargetId);
        var actorState = string.IsNullOrWhiteSpace(bind.ActorId) ? null : LewdModeAccess.TryGetParticipant(context, bind.ActorId);
        var (entry, seedProps) = BuildEntry(bind, context);

        var erotic = bind.Erotic ?? (target is not null && actorState is not null) || IsEroticGear(entry, bind.ItemId);
        entry.Erotic = erotic;
        var settings = await LewdSettings.ResolveAsync(context, ct).ConfigureAwait(false);
        var tags = new List<string> { entry.Kind };
        tags.AddRange(entry.Implies);
        tags.AddRange(entry.Materials);
        var selfBind = string.Equals(bind.ActorId, bind.TargetId, StringComparison.OrdinalIgnoreCase);

        bool wanted;
        if (erotic)
        {
            tags.Add("bondage");
            var consentState = target ?? new ModeParticipantState { CharacterId = bind.TargetId };
            if (!ConsentGate.AuthorizeAdvance(consentState, targetChar, bind.ActorId, null, tags, settings, out var consentError))
                return ChangeHandlerResult.Failure(consentError!);
            wanted = selfBind || bind.Willing || ConsentGate.IsAdvanceWanted(consentState, bind.ActorId, targetChar);
        }
        else
        {
            // Plain restraint (a captive, a prisoner) is not a sex act: only the player's hard limits apply.
            if (settings.HitsHardLimit(tags, out var hit))
                return ChangeHandlerResult.Failure($"Campaign hard limit '{hit}' blocks this binding.");
            wanted = selfBind || bind.Willing;
        }

        if (!wanted && Restraint.NotSubdued(targetChar, bind.ActorId) is { } notSubdued)
            return ChangeHandlerResult.Failure(notSubdued);

        // In a scene, tying someone up is the actor's action for the turn.
        if (actorState is not null && target is not null && !LewdActionBudget.TrySpend(actorState, out var budgetError))
            return ChangeHandlerResult.Failure(budgetError + " Move on with mode_transition action=turn.");

        var bindings = BindingGraph.GetBindings(target, targetChar);
        bindings.Add(entry);
        BindingGraph.SetBindings(target, targetChar, bindings);
        SetPosture(target, targetChar, bind.Posture ?? SeedString(seedProps, "posture"));
        Restraint.Sync(targetChar, bindings);

        var sites = entry.Sites.Count == 0 ? "(unspecified sites)" : string.Join(',', entry.Sites);
        var implies = entry.Implies.Count == 0 ? "(none)" : string.Join(',', entry.Implies);
        var orient = string.IsNullOrWhiteSpace(entry.Orientation) ? "unspecified" : entry.Orientation;
        var arms = target is null ? null : ConsentGate.GetString(target, LewdKeys.ArmPosition);
        var legs = target is null ? null : ConsentGate.GetString(target, LewdKeys.LegPosition);
        var pose = arms is null ? "" : $"; arms={arms} legs={legs ?? "free"}";
        var anchor = entry.AnchorId is null ? "" : $" {Restraint.BoundToVerb} {entry.AnchorId} (can't travel unless it comes along)";
        context.RecordMessage(
            $"Bind {bind.ActorId} → {bind.TargetId}: {entry.Kind} id={entry.Id} sites=[{sites}] orientation={orient} implies=[{implies}]" +
            $"{(entry.Locked ? $" locked DC {entry.LockDc}" : "")}{anchor}{(erotic ? "" : " (restraint)")}{pose}.");
        context.RecordPhysicalStateNudge(
            $"{bind.TargetId} is bound: {BondageSlots.Summary(bindings, PregnancyState.Text(targetChar, LewdKeys.TraitPosture), bindings.Any(b => b.AnchorId is not null))} {Restraint.Describe(bindings)}");
        PublishChanged(context, bind.TargetId, "bound", entry, bind.ActorId, null);

        var bitchsuit = erotic && (Contains(bind.ItemId, "bitchsuit") || Contains(entry.Kind, "bitchsuit") ||
                                   entry.Effects.Any(e => Contains(e, "bitchsuit")) ||
                                   entry.Materials.Any(m => Contains(m, "bitchsuit")));
        if (bitchsuit)
        {
            await ImprintState.AutoAsync(
                context,
                target,
                targetChar,
                ["bitchsuit", "training"],
                wanted,
                bitchsuit: true,
                ct,
                anchorId: bind.ActorId).ConfigureAwait(false);
        }

        return ChangeHandlerResult.Ok;
    }

    private static (BindingEntry Entry, Dictionary<string, object>? SeedProps) BuildEntry(LewdBindChange bind, IChangeContext context)
    {
        Dictionary<string, object>? seedProps = null;
        if (!string.IsNullOrWhiteSpace(bind.ItemId))
        {
            if (!context.Items.TryGetValue(bind.ItemId, out var item))
            {
                item = context.Items.Values.FirstOrDefault(i =>
                    string.Equals(i.Name, bind.ItemId, StringComparison.OrdinalIgnoreCase) ||
                    i.Id.EndsWith("/" + bind.ItemId, StringComparison.OrdinalIgnoreCase));
            }

            seedProps = item?.Properties;
        }

        // No item: the kind alone still seeds its defaults (a gag gags, manacles lock, shackles hobble).
        var entry = BindingGraph.SeedFromItemProperties(
            bind.ItemId,
            seedProps ?? (string.IsNullOrWhiteSpace(bind.Kind) ? null : new Dictionary<string, object> { ["kind"] = bind.Kind }));
        if (!string.IsNullOrWhiteSpace(bind.Kind))
            entry.Kind = bind.Kind!;
        if (bind.Sites is { Count: > 0 })
            entry.Sites = bind.Sites;
        var orientation = bind.Orientation ?? SeedString(seedProps, "orientation");
        if (!string.IsNullOrWhiteSpace(orientation))
            entry.Orientation = BindingGraph.NormalizeOrientation(orientation);
        if (bind.Links is { Count: > 0 })
            entry.Links = bind.Links;
        if (bind.Implies is { Count: > 0 })
            entry.Implies = bind.Implies;
        if (bind.Materials is { Count: > 0 })
            entry.Materials = bind.Materials;
        if (bind.Effects is { Count: > 0 })
            entry.Effects = bind.Effects;

        if (BindingGraph.NormalizeSlack(bind.Slack) is { } slack)
            entry.Slack = slack;
        if (!string.IsNullOrWhiteSpace(bind.Fit))
            entry.Fit = bind.Fit.Trim();

        // Orientation-derived tags go on after explicit effects so they are never dropped. A loose tie leaves play, so it does
        // not pin the hands (the slot rules then call them awkward instead).
        var pins = entry.Slack != "loose" && BindingGraph.TouchesArmSites(entry.Sites);
        if (entry.Orientation == "behind" && pins)
            AddEffects(entry, "arms_rear_bound", "no_hand_use", "no_somatic_spellcasting");
        if (entry.Orientation == "above" && pins)
            AddEffects(entry, "arms_raised", "no_hand_use");
        if (entry.Orientation is "belt" or "collar" && pins)
            AddEffects(entry, $"hands_at_{entry.Orientation}", "no_hand_use", "no_somatic_spellcasting");
        if (BindingGraph.TouchesLegSites(entry.Sites) && entry.Orientation is "together" or "apart" or null &&
            entry.Implies.Count == 0)
            AddImplies(entry, "hobbled");

        if (bind.Hardened)
            entry.Hardened = true;
        if (bind.HardenAtRound is not null)
            entry.HardenAtRound = bind.HardenAtRound;
        if (bind.EscapeDc is not null)
            entry.EscapeDc = bind.EscapeDc.Value;
        if (bind.BreakDc is not null)
            entry.BreakDc = bind.BreakDc.Value;
        if (bind.Hp is not null)
            entry.Hp = bind.Hp.Value;
        ApplyMaterialAndQuality(entry, bind, seedProps);
        if (entry.Slack == "loose")
            entry.EscapeDc = Math.Max(1, entry.EscapeDc - 4);
        if (bind.Locked is not null)
            entry.Locked = bind.Locked.Value;
        if (bind.LockDc is not null)
            entry.LockDc = bind.LockDc.Value;
        if (!string.IsNullOrWhiteSpace(bind.KeyItemId))
        {
            entry.KeyItemId = bind.KeyItemId.Trim();
            entry.Locked = true;
        }

        if (!string.IsNullOrWhiteSpace(bind.AnchorId))
        {
            entry.AnchorId = bind.AnchorId.Trim();
            // A character anchor holds their own end; anything else is held by the named holder, if any.
            entry.HolderId = !string.IsNullOrWhiteSpace(bind.HolderId)
                ? bind.HolderId.Trim()
                : context.Characters.ContainsKey(entry.AnchorId) ? entry.AnchorId : null;
            entry.SlackFeet = bind.SlackFeet;
        }

        entry.AppliedById = string.IsNullOrWhiteSpace(bind.ActorId) ? null : bind.ActorId;
        if (entry.Hardened)
            Harden(entry);
        return (entry, seedProps);
    }

    /// <summary>
    /// Material sets the base DCs only when nobody stated any (no explicit DC, none on the item); quality then shifts
    /// escape, break and lock together.
    /// </summary>
    private static void ApplyMaterialAndQuality(BindingEntry entry, LewdBindChange bind, Dictionary<string, object>? seedProps)
    {
        var statedEscape = bind.EscapeDc is not null || HasProp(seedProps, "escapeDc");
        var statedBreak = bind.BreakDc is not null || HasProp(seedProps, "breakDc");
        if (BondageSlots.MaterialBase(entry.Materials) is { } material)
        {
            if (!statedEscape)
                entry.EscapeDc = material.Escape;
            if (!statedBreak)
                entry.BreakDc = material.Break;
        }

        if (!string.IsNullOrWhiteSpace(bind.Quality))
        {
            var delta = BondageSlots.QualityDelta(bind.Quality);
            entry.Quality = bind.Quality.Trim().ToLowerInvariant();
            entry.EscapeDc += delta;
            entry.BreakDc += delta;
            entry.LockDc += delta;
        }
    }

    private static bool HasProp(Dictionary<string, object>? props, string key) =>
        props is not null && props.Keys.Any(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));

    private static bool IsEroticGear(BindingEntry entry, string? itemId) =>
        EroticGear.Any(g => Contains(itemId, g) || Contains(entry.Kind, g) ||
                            entry.Effects.Any(e => Contains(e, g)) || entry.Materials.Any(m => Contains(m, g)));

    private static void SetPosture(ModeParticipantState? participant, Character character, string? posture)
    {
        if (string.IsNullOrWhiteSpace(posture))
            return;
        character.SystemStats.Traits[LewdKeys.TraitPosture] = posture.Trim();
        character.SystemStats.Traits.Remove(LewdKeys.LegacyModeTraitPosture);
        if (participant is not null)
            participant.State[LewdKeys.Posture] = posture.Trim();
    }

    private static string? SeedString(Dictionary<string, object>? props, string key) =>
        props?.FirstOrDefault(kv => string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase)).Value?.ToString();

    internal static void Harden(BindingEntry entry)
    {
        entry.Hardened = true;
        entry.EscapeDc = Math.Max(entry.EscapeDc, 25);
        entry.BreakDc = Math.Max(entry.BreakDc, 25);
        entry.Hp = Math.Max(entry.Hp, 25);
    }

    private static void AddEffects(BindingEntry entry, params string[] tags)
    {
        foreach (var tag in tags)
        {
            if (!entry.Effects.Contains(tag, StringComparer.OrdinalIgnoreCase))
                entry.Effects.Add(tag);
        }
    }

    private static void AddImplies(BindingEntry entry, params string[] tags)
    {
        foreach (var tag in tags)
        {
            if (!entry.Implies.Contains(tag, StringComparer.OrdinalIgnoreCase))
                entry.Implies.Add(tag);
        }
    }

    private static bool Contains(string? value, string needle) =>
        value?.Contains(needle, StringComparison.OrdinalIgnoreCase) == true;

    internal static void PublishChanged(IChangeContext context, string characterId, string action, BindingEntry entry, string? actorId, string? method) =>
        context.Publish(
            Events.LewdEvents.BindingChanged,
            new { characterId, action, bindingId = entry.Id, kind = entry.Kind, anchorId = entry.AnchorId, actorId, method });
}

public sealed class LewdUnbindHandler : IWorldChangeHandler
{
    public bool ShouldHandle(WorldChange change) => change is LewdUnbindChange;

    public Task<ChangeHandlerResult> ApplyAsync(
        WorldChange change,
        IChangeContext context,
        CancellationToken ct = default)
    {
        var unbind = (LewdUnbindChange)change;
        if (string.IsNullOrWhiteSpace(unbind.TargetId))
            return Task.FromResult(ChangeHandlerResult.Failure("targetId is required."));

        // Freeing someone never needs a lewd scene: bindings live on the Character.
        var target = LewdModeAccess.TryGetParticipant(context, unbind.TargetId);
        context.Characters.TryGetValue(unbind.TargetId, out var targetChar);
        if (target is null && targetChar is null)
            return Task.FromResult(ChangeHandlerResult.Failure($"Target '{unbind.TargetId}' is not in the commit context."));

        var bindings = BindingGraph.GetBindings(target, targetChar);
        var selected = unbind.RemoveAll
            ? bindings.ToList()
            : !string.IsNullOrWhiteSpace(unbind.BindingId)
                ? bindings.Where(b => string.Equals(b.Id, unbind.BindingId, StringComparison.OrdinalIgnoreCase)).ToList()
                : !string.IsNullOrWhiteSpace(unbind.ItemId)
                    ? bindings.Where(b => string.Equals(b.ItemId, unbind.ItemId, StringComparison.OrdinalIgnoreCase)).ToList()
                    : bindings.TakeLast(1).ToList();

        if (bindings.Count > 0 && selected.Count == 0)
        {
            var ids = string.Join(", ", bindings.Select(b => $"{b.Id} ({b.Kind})"));
            return Task.FromResult(ChangeHandlerResult.Failure(
                $"No binding on '{unbind.TargetId}' matched. Current bindings: {ids}."));
        }

        var needsKey = selected.FirstOrDefault(b =>
            b.Locked && b.KeyItemId is not null &&
            !string.Equals(b.KeyItemId, unbind.KeyItemId, StringComparison.OrdinalIgnoreCase));
        if (needsKey is not null)
        {
            return Task.FromResult(ChangeHandlerResult.Failure(
                $"{needsKey.Kind} [{needsKey.Id}] is locked; its key is {needsKey.KeyItemId}. Pass keyItemId, or use lewd_escape (pick/break/cut)."));
        }

        bindings.RemoveAll(selected.Contains);
        BindingGraph.SetBindings(target, targetChar, bindings);
        if (targetChar is not null)
        {
            Restraint.Sync(targetChar, bindings);
            if (bindings.Count == 0)
            {
                targetChar.SystemStats.Traits.Remove(LewdKeys.TraitPosture);
                targetChar.SystemStats.Traits.Remove(LewdKeys.LegacyModeTraitPosture);
            }
        }

        foreach (var removed in selected)
            LewdBindHandler.PublishChanged(context, unbind.TargetId, "unbound", removed, unbind.ActorId, null);

        var implied = BindingGraph.CollectImplied(bindings).OrderBy(x => x).ToList();
        context.RecordMessage(
            $"Unbind {unbind.ActorId} → {unbind.TargetId}: removed {selected.Count} binding(s); {bindings.Count} remain.");
        context.RecordPhysicalStateNudge(bindings.Count == 0
            ? $"{unbind.TargetId} is free of bindings."
            : $"{unbind.TargetId} still bound: {BondageSlots.Summary(bindings, targetChar is null ? null : PregnancyState.Text(targetChar, LewdKeys.TraitPosture), bindings.Any(b => b.AnchorId is not null))} {Restraint.Describe(bindings)} Implied: {(implied.Count == 0 ? "none" : string.Join(',', implied))}.");

        return Task.FromResult(ChangeHandlerResult.Ok);
    }
}

public sealed class LewdEscapeHandler : IWorldChangeHandler
{
    public bool ShouldHandle(WorldChange change) => change is LewdEscapeChange;

    public async Task<ChangeHandlerResult> ApplyAsync(WorldChange change, IChangeContext context, CancellationToken ct = default)
    {
        var req = (LewdEscapeChange)change;
        if (!context.Characters.TryGetValue(req.CharacterId ?? "", out var bound))
            return ChangeHandlerResult.Failure($"Character '{req.CharacterId}' is not in the commit context.");

        var participant = LewdModeAccess.TryGetParticipant(context, bound.Id);
        var bindings = BindingGraph.GetBindings(participant, bound);
        if (bindings.Count == 0)
            return ChangeHandlerResult.Failure($"{bound.Id} is not bound.");
        var binding = string.IsNullOrWhiteSpace(req.BindingId)
            ? bindings[^1]
            : bindings.FirstOrDefault(b => string.Equals(b.Id, req.BindingId, StringComparison.OrdinalIgnoreCase));
        if (binding is null)
            return ChangeHandlerResult.Failure(
                $"No binding '{req.BindingId}' on {bound.Id}. Current: {string.Join(", ", bindings.Select(b => $"{b.Id} ({b.Kind})"))}.");

        var helperId = string.IsNullOrWhiteSpace(req.ActorId) || string.Equals(req.ActorId, bound.Id, StringComparison.OrdinalIgnoreCase)
            ? null
            : req.ActorId;
        Character? helper = null;
        if (helperId is not null && !context.Characters.TryGetValue(helperId, out helper))
            return ChangeHandlerResult.Failure($"Actor '{helperId}' is not in the commit context.");
        var worker = helper ?? bound;
        var handsBound = helper is null && Restraint.BlocksSomaticComponents(bindings);
        var method = (req.Method ?? "slip").Trim().ToLowerInvariant();

        // An attempt by someone in a lewd scene is their action for the turn.
        var workerState = LewdModeAccess.TryGetParticipant(context, worker.Id);
        if (workerState is not null && !LewdActionBudget.TrySpend(workerState, out var budgetError))
            return ChangeHandlerResult.Failure(budgetError + " Move on with mode_transition action=turn.");

        string outcome;
        bool? freed;
        switch (method)
        {
            case "slip":
            {
                if (helper is not null)
                    return Refund(workerState, "slip is the bound character's own attempt; a helper uses pick, unlock, cut or break.");
                var ability = AbilityScores.Normalize(req.Ability) is "str" ? "str" : "dex";
                var cramped = BindingGraph.CollectImplied(bindings).Overlaps(["encased", "full_tied", "suspended"]);
                (freed, outcome) = await CheckAsync(context, "lewd_escape_slip", req, worker, ability, binding.EscapeDc, cramped, ct).ConfigureAwait(false);
                break;
            }
            case "break":
                (freed, outcome) = await CheckAsync(context, "lewd_escape_break", req, worker, "str", binding.BreakDc, false, ct).ConfigureAwait(false);
                break;
            case "pick":
                if (!binding.Locked)
                    return Refund(workerState, $"{binding.Kind} [{binding.Id}] has no lock; slip, break or cut it, or lewd_unbind.");
                if (handsBound)
                    return Refund(workerState, $"{bound.Id}'s hands are bound: someone else has to pick the lock (actorId).");
                (freed, outcome) = await CheckAsync(context, "lewd_escape_pick", req, worker, "dex", binding.LockDc, false, ct).ConfigureAwait(false);
                break;
            case "unlock":
                if (!binding.Locked)
                    return Refund(workerState, $"{binding.Kind} [{binding.Id}] has no lock; use lewd_unbind.");
                if (handsBound)
                    return Refund(workerState, $"{bound.Id}'s hands are bound: someone else has to use the key (actorId).");
                if (binding.KeyItemId is null || !string.Equals(binding.KeyItemId, req.KeyItemId, StringComparison.OrdinalIgnoreCase))
                    return Refund(workerState, binding.KeyItemId is null
                        ? "No key is recorded for this lock; pick it, break it, or lewd_unbind."
                        : $"That is not the key; this lock opens with {binding.KeyItemId}.");
                (freed, outcome) = (true, $"unlocked with {binding.KeyItemId}");
                break;
            case "cut":
            case "damage":
                if (req.Amount <= 0)
                    return Refund(workerState, "cut needs amount (damage dealt to the binding).");
                binding.Hp -= req.Amount;
                freed = binding.Hp <= 0;
                outcome = binding.Hp <= 0 ? $"{req.Amount} damage destroys it" : $"{req.Amount} damage, {binding.Hp} hp left";
                break;
            default:
                return Refund(workerState, "method must be slip, break, pick, unlock or cut.");
        }

        if (freed is null)
            return Refund(workerState, outcome);
        if (freed == true)
            bindings.Remove(binding);
        BindingGraph.SetBindings(participant, bound, bindings);
        Restraint.Sync(bound, bindings);
        if (freed == true)
            LewdBindHandler.PublishChanged(context, bound.Id, "escaped", binding, worker.Id, method);

        var who = helper is null ? bound.Id : $"{helper.Id} (for {bound.Id})";
        context.RecordMessage(
            $"Escape {who}: {method} {binding.Kind} [{binding.Id}] — {outcome}. {(freed == true ? "Free of it." : "Still bound.")}" +
            (freed == true && bindings.Count > 0 ? $" Remaining: {Restraint.Describe(bindings)}" : ""));
        return ChangeHandlerResult.Ok;
    }

    private static async Task<(bool? Freed, string Outcome)> CheckAsync(
        IChangeContext context, string tag, LewdEscapeChange req, Character worker, string ability, int dc, bool disadvantage, CancellationToken ct)
    {
        var mod = AbilityScores.Mod(worker, ability) + req.Bonus;
        var roll = await SaveDice.RollAsync(
            context, tag, req.D20, mod, disadvantage, ct, who: worker, subject: ability, kind: RollKinds.Check, tags: ["escape"]).ConfigureAwait(false);
        if (roll.Error is not null)
            return (null, roll.Error);
        var ok = roll.Total >= dc;
        return (ok, $"{ability} {roll.Summary} vs DC {dc}{(disadvantage ? " (disadvantage)" : "")}: {(ok ? "success" : "failure")}");
    }

    private static ChangeHandlerResult Refund(ModeParticipantState? workerState, string error)
    {
        if (workerState is not null)
            workerState.ActionBudget[LewdActionBudget.Action] = workerState.ActionBudget.GetValueOrDefault(LewdActionBudget.Action) + 1;
        return ChangeHandlerResult.Failure(error);
    }
}
