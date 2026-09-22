using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;
using LewdHandbook.Changes;
using LewdHandbook.Mechanics;

namespace LewdHandbook.Handlers;

public sealed class LewdBindHandler : IWorldChangeHandler
{
    public bool ShouldHandle(WorldChange change) => change is LewdBindChange;

    public Task<ChangeHandlerResult> ApplyAsync(
        WorldChange change,
        IChangeContext context,
        CancellationToken ct = default)
    {
        var bind = (LewdBindChange)change;
        if (string.IsNullOrWhiteSpace(bind.TargetId))
            return Task.FromResult(ChangeHandlerResult.Failure("targetId is required."));

        var mode = context.ActiveMode;
        if (mode is null || !mode.IsActive ||
            !string.Equals(mode.ModeId, LewdEncounterMode.ModeIdValue, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(ChangeHandlerResult.Failure(
                "lewd_bind requires an active lewd_encounter mode."));
        }

        var target = mode.Participants.FirstOrDefault(p =>
            string.Equals(p.CharacterId, bind.TargetId, StringComparison.OrdinalIgnoreCase));
        if (target is null)
            return Task.FromResult(ChangeHandlerResult.Failure($"Target '{bind.TargetId}' is not in the lewd encounter."));

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
            entry.Orientation = bind.Orientation;
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

        if (!string.IsNullOrWhiteSpace(bind.Posture))
            target.State[LewdKeys.Posture] = bind.Posture!;

        // Stamp implied condition names into a mirror list for LLM/status tooling.
        var implied = BindingGraph.CollectImplied(target).OrderBy(x => x).ToList();
        target.State["binding_implies"] = implied;

        // Sensory StatusEffects on character when present
        if (context.Characters.TryGetValue(bind.TargetId, out var targetChar))
        {
            StampSensoryEffects(targetChar, implied);
        }

        var sites = entry.Sites.Count == 0 ? "(unspecified sites)" : string.Join(',', entry.Sites);
        var implies = entry.Implies.Count == 0 ? "(none)" : string.Join(',', entry.Implies);
        context.RecordMessage(
            $"Lewd bind {bind.ActorId} → {bind.TargetId}: {entry.Kind} id={entry.Id} sites=[{sites}] implies=[{implies}].");
        context.RecordPhysicalStateNudge(
            $"{bind.TargetId} is bound ({entry.Kind}) at {sites}; implied: {implies}.");

        return Task.FromResult(ChangeHandlerResult.Ok);
    }

    internal static void StampSensoryEffects(Character character, IEnumerable<string> implied)
    {
        var set = new HashSet<string>(implied, StringComparer.OrdinalIgnoreCase);
        void Ensure(string name, string summary)
        {
            if (character.SystemStats.StatusEffects.Any(e =>
                    string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase)))
                return;
            character.SystemStats.StatusEffects.Add(new StatusEffect
            {
                Name = name,
                Category = "Condition",
                RecoveryHint = summary,
            });
        }

        if (set.Contains("blinded") || set.Contains("blind"))
            Ensure("blinded", "Vision blocked by hood/blindfold binding.");
        if (set.Contains("gagged") || set.Contains("gag"))
            Ensure("gagged", "Mouth bound; no verbal spell components / clear speech.");
        if (set.Contains("restrained") || set.Contains("full_tied") || set.Contains("full-tied") ||
            set.Contains("suspended") || set.Contains("encased"))
            Ensure("restrained", "Restrained by bondage graph.");
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

        context.RecordMessage(
            $"Lewd unbind {unbind.ActorId} → {unbind.TargetId}: removed {before - bindings.Count} binding(s); {bindings.Count} remain.");
        context.RecordPhysicalStateNudge(
            $"{unbind.TargetId} bindings updated; remaining implies: {(implied.Count == 0 ? "none" : string.Join(',', implied))}.");

        return Task.FromResult(ChangeHandlerResult.Ok);
    }
}
