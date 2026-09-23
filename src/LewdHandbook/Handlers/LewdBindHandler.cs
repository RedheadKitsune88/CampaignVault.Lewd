using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;
using LewdHandbook.Changes;
using LewdHandbook.Mechanics;

namespace LewdHandbook.Handlers;

public sealed class LewdBindHandler : IWorldChangeHandler
{
    public bool ShouldHandle(WorldChange change) => change is LewdBindChange;

    public async Task<ChangeHandlerResult> ApplyAsync(
        WorldChange change,
        IChangeContext context,
        CancellationToken ct = default)
    {
        var bind = (LewdBindChange)change;
        if (string.IsNullOrWhiteSpace(bind.TargetId))
            return ChangeHandlerResult.Failure("targetId is required.");

        var mode = context.ActiveMode;
        if (mode is null || !mode.IsActive ||
            !string.Equals(mode.ModeId, LewdEncounterMode.ModeIdValue, StringComparison.OrdinalIgnoreCase))
        {
            return ChangeHandlerResult.Failure(
                "lewd_bind requires an active lewd_encounter mode.");
        }

        var target = mode.Participants.FirstOrDefault(p =>
            string.Equals(p.CharacterId, bind.TargetId, StringComparison.OrdinalIgnoreCase));
        if (target is null)
            return ChangeHandlerResult.Failure($"Target '{bind.TargetId}' is not in the lewd encounter.");

        Dictionary<string, object>? seedProps = null;
        if (!string.IsNullOrWhiteSpace(bind.ItemId) &&
            context.Items.TryGetValue(bind.ItemId, out var item))
        {
            seedProps = item.Properties;
        }
        else if (!string.IsNullOrWhiteSpace(bind.ItemId))
        {
            // Match by name
            item = context.Items.Values.FirstOrDefault(i =>
                string.Equals(i.Name, bind.ItemId, StringComparison.OrdinalIgnoreCase) ||
                i.Id.EndsWith("/" + bind.ItemId, StringComparison.OrdinalIgnoreCase));
            seedProps = item?.Properties;
        }

        var entry = BindingGraph.SeedFromItemProperties(bind.ItemId, seedProps);
        if (!string.IsNullOrWhiteSpace(bind.Kind))
            entry.Kind = bind.Kind!;
        if (bind.Sites is { Count: > 0 })
            entry.Sites = bind.Sites;
        if (!string.IsNullOrWhiteSpace(bind.Orientation))
            entry.Orientation = BindingGraph.NormalizeOrientation(bind.Orientation);
        else if (seedProps is not null)
        {
            foreach (var (k, v) in seedProps)
            {
                if (string.Equals(k, "orientation", StringComparison.OrdinalIgnoreCase) && v is not null)
                {
                    entry.Orientation = BindingGraph.NormalizeOrientation(v.ToString());
                    break;
                }
            }
        }

        // Wrists/arms behind → somatic/hand tags on the binding effects list.
        if (string.Equals(entry.Orientation, "behind", StringComparison.OrdinalIgnoreCase) &&
            BindingGraph.TouchesArmSites(entry.Sites))
        {
            foreach (var tag in new[] { "arms_rear_bound", "no_hand_use", "no_somatic_spellcasting" })
            {
                if (!entry.Effects.Contains(tag, StringComparer.OrdinalIgnoreCase))
                    entry.Effects.Add(tag);
            }
        }

        if (string.Equals(entry.Orientation, "above", StringComparison.OrdinalIgnoreCase) &&
            BindingGraph.TouchesArmSites(entry.Sites))
        {
            foreach (var tag in new[] { "arms_raised", "no_hand_use" })
            {
                if (!entry.Effects.Contains(tag, StringComparer.OrdinalIgnoreCase))
                    entry.Effects.Add(tag);
            }
        }
        if (bind.Links is { Count: > 0 })
            entry.Links = bind.Links;
        if (bind.Implies is { Count: > 0 })
            entry.Implies = bind.Implies;
        if (bind.Materials is { Count: > 0 })
            entry.Materials = bind.Materials;
        if (bind.Effects is { Count: > 0 })
            entry.Effects = bind.Effects;
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

        if (entry.Hardened)
        {
            entry.EscapeDc = Math.Max(entry.EscapeDc, 25);
            entry.BreakDc = Math.Max(entry.BreakDc, 25);
            entry.Hp = Math.Max(entry.Hp, 25);
        }

        var bindings = BindingGraph.GetBindings(target);
        bindings.Add(entry);
        BindingGraph.SetBindings(target, bindings);

        // Prefer explicit commit posture; else seed from item Properties["posture"].
        var posture = bind.Posture;
        if (string.IsNullOrWhiteSpace(posture) && seedProps is not null)
        {
            foreach (var (k, v) in seedProps)
            {
                if (string.Equals(k, "posture", StringComparison.OrdinalIgnoreCase) && v is not null)
                {
                    posture = v.ToString();
                    break;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(posture))
            target.State[LewdKeys.Posture] = posture!;

        // Stamp implied condition names into a mirror list for LLM/status tooling.
        var implied = BindingGraph.CollectImplied(target).OrderBy(x => x).ToList();
        target.State["binding_implies"] = implied;

        var effectTags = BindingGraph.CollectEffects(target).OrderBy(x => x).ToList();
        target.State["binding_effects"] = effectTags;

        // StatusEffects on character — include casting StatModifiers so host CastingComponentGate enforces V/S blocks.
        if (context.Characters.TryGetValue(bind.TargetId, out var targetChar))
        {
            StampRestraintEffects(targetChar, implied, effectTags);
        }

        BindingGraph.RefreshLimbPositions(target);

        var sites = entry.Sites.Count == 0 ? "(unspecified sites)" : string.Join(',', entry.Sites);
        var implies = entry.Implies.Count == 0 ? "(none)" : string.Join(',', entry.Implies);
        var orient = string.IsNullOrWhiteSpace(entry.Orientation) ? "unspecified" : entry.Orientation;
        var arms = ConsentGate.GetString(target, LewdKeys.ArmPosition) ?? "free";
        var legs = ConsentGate.GetString(target, LewdKeys.LegPosition) ?? "free";
        context.RecordMessage(
            $"Lewd bind {bind.ActorId} → {bind.TargetId}: {entry.Kind} id={entry.Id} sites=[{sites}] orientation={orient} implies=[{implies}]; arms={arms} legs={legs}.");
        context.RecordPhysicalStateNudge(
            $"{bind.TargetId} is bound ({entry.Kind}) at {sites} ({orient}); arms {arms}, legs {legs}; implied: {implies}.");

        var bitchsuit = Contains(bind.ItemId, "bitchsuit") || Contains(entry.Kind, "bitchsuit") ||
                        entry.Effects.Any(e => Contains(e, "bitchsuit")) ||
                        entry.Materials.Any(m => Contains(m, "bitchsuit"));
        if (bitchsuit && context.Characters.TryGetValue(bind.TargetId, out var imprintChar))
        {
            await ImprintState.AutoAsync(
                context,
                target,
                imprintChar,
                ["bitchsuit", "training"],
                ConsentGate.IsAdvanceWanted(target, bind.ActorId),
                bitchsuit: true,
                ct).ConfigureAwait(false);
        }

        return ChangeHandlerResult.Ok;
    }

    private static bool Contains(string? value, string needle) =>
        value?.Contains(needle, StringComparison.OrdinalIgnoreCase) == true;

    internal static void StampRestraintEffects(
        Character character,
        IEnumerable<string> implied,
        IEnumerable<string> effectTags)
    {
        var set = new HashSet<string>(implied, StringComparer.OrdinalIgnoreCase);
        foreach (var e in effectTags)
        {
            if (!string.IsNullOrWhiteSpace(e))
                set.Add(e.Trim());
        }

        void Ensure(string name, string summary, string? conditionName, Action<Dictionary<string, float>>? mods = null)
        {
            var existing = character.SystemStats.StatusEffects.FirstOrDefault(e =>
                string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                mods?.Invoke(existing.StatModifiers);
                if (conditionName is not null && string.IsNullOrWhiteSpace(existing.ConditionName))
                    existing.ConditionName = conditionName;
                return;
            }

            var effect = new StatusEffect
            {
                Name = name,
                Category = "Condition",
                ConditionName = conditionName,
                RecoveryHint = summary,
                AppliedBy = "lewd_bind",
            };
            mods?.Invoke(effect.StatModifiers);
            character.SystemStats.StatusEffects.Add(effect);
        }

        // Keys must match host CastingComponentGate constants (Sdk cannot reference host Services).
        const string blocksVerbal = "BlocksVerbalComponents";
        const string blocksSomatic = "BlocksSomaticComponents";

        if (set.Contains("blinded") || set.Contains("blind") || set.Contains("no_sight"))
            Ensure("blinded", "Vision blocked by hood/blindfold binding.", "blinded");

        if (set.Contains("gagged") || set.Contains("gag") ||
            set.Contains("no_verbal_spellcasting") || set.Contains("no_clear_speech"))
        {
            Ensure(
                "gagged",
                "Mouth bound; no clear speech; verbal spell components blocked.",
                "gagged",
                m => m[blocksVerbal] = 1f);
        }

        if (set.Contains("mitted") || set.Contains("limb_bound") || set.Contains("limb-bound") ||
            set.Contains("no_hand_use") || set.Contains("no_somatic_spellcasting") ||
            set.Contains("arms_rear_bound"))
        {
            Ensure(
                "mitted",
                "Hands/arms bound; fine manipulation and somatic components blocked.",
                "mitted",
                m => m[blocksSomatic] = 1f);
        }

        if (set.Contains("restrained") || set.Contains("full_tied") || set.Contains("full-tied") ||
            set.Contains("suspended") || set.Contains("encased"))
        {
            Ensure("restrained", "Restrained by bondage graph.", "restrained");
        }

        if (set.Contains("hobbled") || set.Contains("forced_crawl") || set.Contains("no_upright_walk"))
        {
            Ensure(
                "hobbled",
                "Movement limited by bondage (hobble, crawl-suit, spreader).",
                "hobbled");
        }
    }
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

        var mode = context.ActiveMode;
        if (mode is null || !mode.IsActive ||
            !string.Equals(mode.ModeId, LewdEncounterMode.ModeIdValue, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(ChangeHandlerResult.Failure(
                "lewd_unbind requires an active lewd_encounter mode."));
        }

        var target = mode.Participants.FirstOrDefault(p =>
            string.Equals(p.CharacterId, unbind.TargetId, StringComparison.OrdinalIgnoreCase));
        if (target is null)
            return Task.FromResult(ChangeHandlerResult.Failure($"Target '{unbind.TargetId}' is not in the lewd encounter."));

        var bindings = BindingGraph.GetBindings(target);
        var before = bindings.Count;

        if (unbind.RemoveAll)
        {
            bindings.Clear();
        }
        else if (!string.IsNullOrWhiteSpace(unbind.BindingId))
        {
            bindings.RemoveAll(b => string.Equals(b.Id, unbind.BindingId, StringComparison.OrdinalIgnoreCase));
        }
        else if (!string.IsNullOrWhiteSpace(unbind.ItemId))
        {
            bindings.RemoveAll(b => string.Equals(b.ItemId, unbind.ItemId, StringComparison.OrdinalIgnoreCase));
        }
        else if (bindings.Count > 0)
        {
            bindings.RemoveAt(bindings.Count - 1);
        }

        BindingGraph.SetBindings(target, bindings);
        var implied = BindingGraph.CollectImplied(target).OrderBy(x => x).ToList();
        target.State["binding_implies"] = implied;
        target.State["binding_effects"] = BindingGraph.CollectEffects(target).OrderBy(x => x).ToList();
        BindingGraph.RefreshLimbPositions(target);

        var arms = ConsentGate.GetString(target, LewdKeys.ArmPosition) ?? "free";
        var legs = ConsentGate.GetString(target, LewdKeys.LegPosition) ?? "free";
        context.RecordMessage(
            $"Lewd unbind {unbind.ActorId} → {unbind.TargetId}: removed {before - bindings.Count} binding(s); {bindings.Count} remain; arms={arms} legs={legs}.");
        context.RecordPhysicalStateNudge(
            $"{unbind.TargetId} bindings updated; arms {arms}, legs {legs}; remaining implies: {(implied.Count == 0 ? "none" : string.Join(',', implied))}.");

        return Task.FromResult(ChangeHandlerResult.Ok);
    }
}
