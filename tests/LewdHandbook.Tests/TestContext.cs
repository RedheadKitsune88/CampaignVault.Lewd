using CampaignVault.Data;
using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;
using LewdHandbook.Mechanics;
using Microsoft.Extensions.Logging.Abstractions;

namespace LewdHandbook.Tests;

/// <summary>Minimal IChangeContext for handler tests: characters, one optional mode, options, fixed dice.</summary>
internal sealed class TestContext : IChangeContext
{
    private readonly Dictionary<string, Character> _characters = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Item> _items = new(StringComparer.OrdinalIgnoreCase);

    public TestContext(ModeEncounter? mode = null, params Character[] characters)
    {
        ActiveMode = mode;
        foreach (var c in characters)
            _characters[c.Id] = c;
    }

    public static Character Adult(string id) =>
        new() { Id = id, Name = id, LifeStage = LifeStage.Adult, SystemStats = new SystemExtension() };

    public Dictionary<string, string> Options { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Messages { get; } = [];
    public List<string> Nudges { get; } = [];
    public List<(string Topic, object? Data)> Published { get; } = [];
    public CampaignTime Time { get; set; } = new();

    public void Add(Character c) => _characters[c.Id] = c;
    public void Add(Item i) => _items[i.Id] = i;

    public IReadOnlyDictionary<string, Character> Characters => _characters;
    public IReadOnlyDictionary<string, Item> Items => _items;
    public IReadOnlyDictionary<string, Location> Locations { get; } = new Dictionary<string, Location>();
    public IReadOnlyDictionary<string, Faction> Factions { get; } = new Dictionary<string, Faction>();
    public IReadOnlyDictionary<string, Quest> Quests { get; } = new Dictionary<string, Quest>();
    public Microsoft.Extensions.Logging.ILogger Logger { get; } = NullLogger.Instance;
    public CombatEncounter? ActiveCombat => null;
    public ModeEncounter? ActiveMode { get; set; }
    public IReadOnlyDictionary<string, ModeEncounter> ActiveModes =>
        ActiveMode is { IsActive: true } m
            ? new Dictionary<string, ModeEncounter>(StringComparer.OrdinalIgnoreCase) { [m.ModeId] = m }
            : new Dictionary<string, ModeEncounter>(StringComparer.OrdinalIgnoreCase);
    public CampaignConfig? Config => null;
    public IRollService? Rolls { get; set; }
    public string? CampaignName => "test";
    public HashSet<string> InvolvedEntities { get; } = [];
    public IReadOnlyList<WorldChange>? Batch => null;
    public int BatchIndex => 0;
    public Func<Task<CampaignTime>> GetCurrentTimeAsync => () => Task.FromResult(Time);
    public Func<Task<Dictionary<string, string>>> GetSystemOptionsAsync => () => Task.FromResult(Options);
    public Func<Event, Task> LogEventAsync { get; } = _ => Task.CompletedTask;
    public void RegisterNewLocation(Location loc) { }
    public void RegisterNewCharacter(Character c) { }
    public void RegisterNewItem(Item i) { }
    public void RegisterNewFaction(Faction f) { }
    public void RegisterNewQuest(Quest q) { }
    public void Publish(string topic, object? data = null) => Published.Add((topic, data));
    /// <summary>Providers folded by <see cref="ResolveRollModifiers"/>; the plugin's own by default. Core's layers (status effects,
    /// willpower) are the host's and are covered by the host tests.</summary>
    public List<IRollModifierProvider> Providers { get; } = [new LewdRollModifierProvider()];

    public RollResolution ResolveRollModifiers(RollQuery query, int baseBonus, AdvantageEffect explicitAdvantage = AdvantageEffect.None)
    {
        var bonus = baseBonus;
        var adv = explicitAdvantage == AdvantageEffect.Advantage;
        var dis = explicitAdvantage == AdvantageEffect.Disadvantage;
        var notes = new List<string>();
        foreach (var m in Providers.SelectMany(p => p.Modifiers(query)))
        {
            bonus += m.Bonus;
            adv |= m.Advantage == AdvantageEffect.Advantage;
            dis |= m.Advantage == AdvantageEffect.Disadvantage;
            if (!string.IsNullOrWhiteSpace(m.Reason))
                notes.Add(m.Reason);
        }

        return new RollResolution(bonus, adv == dis ? AdvantageEffect.None : adv ? AdvantageEffect.Advantage : AdvantageEffect.Disadvantage, notes);
    }

    public void RecordMessage(string message) => Messages.Add(message);
    public void RecordPhysicalStateNudge(string message) => Nudges.Add(message);
    public void RecordFailure() { }
    public void RecordEntityCollision(string entityId, string message) { }
    public void RecordCommittedId(string id) { }
    public Task<string?> SuggestLocationMatchAsync(string? nameQuery) => Task.FromResult<string?>(null);
    public Task<string?> SuggestCharacterMatchAsync(string? nameQuery) => Task.FromResult<string?>(null);
    public Task<string?> SuggestItemMatchAsync(string? nameQuery) => Task.FromResult<string?>(null);
    public Task<string?> SuggestFactionMatchAsync(string? nameQuery) => Task.FromResult<string?>(null);
    public Task<string?> SuggestQuestMatchAsync(string? nameQuery) => Task.FromResult<string?>(null);

    /// <summary>Published payload field, read the way a subscriber would see it.</summary>
    public string? Field(string topic, string field)
    {
        foreach (var (_, data) in Published.Where(p => p.Topic == topic).Reverse())
        {
            if (data is null)
                continue;
            if (data is System.Collections.IDictionary map)
            {
                foreach (System.Collections.DictionaryEntry entry in map)
                {
                    if (string.Equals(entry.Key?.ToString(), field, StringComparison.OrdinalIgnoreCase))
                        return entry.Value?.ToString();
                }
            }

            var prop = data.GetType().GetProperty(field) ??
                       data.GetType().GetProperties()
                           .FirstOrDefault(pr => string.Equals(pr.Name, field, StringComparison.OrdinalIgnoreCase));
            if (prop is not null)
                return prop.GetValue(data)?.ToString();
        }

        return null;
    }
}

internal sealed class FixedRolls(int face) : IRollService
{
    public Task<RollOutcome> RollAsync(RollRequest request, CancellationToken ct = default) =>
        Task.FromResult(new RollOutcome
        {
            Tag = request.Tag,
            Result = face + request.Bonus,
            IndividualDice = [face],
            Summary = $"[{face}]+{request.Bonus}",
        });

    public async Task<IReadOnlyList<RollOutcome>> RollBatchAsync(IEnumerable<RollRequest> requests, CancellationToken ct = default)
    {
        var list = new List<RollOutcome>();
        foreach (var r in requests)
            list.Add(await RollAsync(r, ct));
        return list;
    }
}
