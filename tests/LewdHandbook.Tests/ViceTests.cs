using CampaignVault.Data;
using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;
using LewdHandbook.Changes;
using LewdHandbook.Handlers;
using LewdHandbook.Mechanics;
using LewdHandbook.Observers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LewdHandbook.Tests;

public class ViceTests
{
    [Fact]
    public async Task Consume_sets_last_hours_and_clears_withdrawal()
    {
        var character = Character("bob");
        character.SystemStats.Traits[ViceState.AddictedKey("sex")] = "true";
        character.SystemStats.Traits[ViceState.WithdrawalKey("sex")] = "true";
        character.SystemStats.Attributes[ViceState.DcKey("sex")] = 8;
        character.SystemStats.Attributes[ViceState.BaseDcKey("sex")] = 8;
        character.SystemStats.Attributes[ViceState.LastHoursKey("sex")] = 0;
        var ctx = new Recorder(character)
        {
            Time = new CampaignTime { TotalDaysElapsed = 2, Hour = 6 },
        };

        var result = await new LewdViceHandler().ApplyAsync(new LewdViceChange
        {
            CharacterId = "bob",
            Action = "consume",
            ViceId = "sex",
            D20 = 20,
        }, ctx);

        Assert.True(result.Success);
        Assert.Equal(2 * 24 + 6, character.SystemStats.Attributes[ViceState.LastHoursKey("sex")]);
        Assert.False(character.SystemStats.Traits.ContainsKey(ViceState.WithdrawalKey("sex")));
        Assert.Contains(character.SystemStats.StatusEffects, e => e.ConditionName == "vice_sex");
    }

    [Fact]
    public async Task Save_failure_sets_addicted_and_second_partake_raises_dc()
    {
        var character = Character("bob");
        var ctx = new Recorder(character);
        var handler = new LewdViceHandler();

        var failed = await handler.ApplyAsync(new LewdViceChange
        {
            CharacterId = "bob",
            Action = "consume",
            ViceId = "sex",
            D20 = 1,
            AbilityMod = 0,
        }, ctx);
        Assert.True(failed.Success);
        Assert.Equal("true", character.SystemStats.Traits[ViceState.AddictedKey("sex")]);
        Assert.Equal(9, character.SystemStats.Attributes[ViceState.DcKey("sex")]);
        Assert.Contains(character.SystemStats.StatusEffects, e => e.ConditionName == "vice_sex");
        Assert.Contains(character.SystemStats.StatusEffects, e => e.ConditionName == LewdKeys.ConditionHyperaroused);

        await handler.ApplyAsync(new LewdViceChange
        {
            CharacterId = "bob",
            Action = "consume",
            ViceId = "sex",
            D20 = 20,
        }, ctx);
        Assert.Equal(10, character.SystemStats.Attributes[ViceState.DcKey("sex")]);
        Assert.Equal(2, character.SystemStats.Attributes[ViceState.WeekCountKey("sex")]);
    }

    [Fact]
    public async Task Gap_from_stamps_marks_withdrawal_without_background_tick()
    {
        var character = Character("bob");
        character.SystemStats.Traits[ViceState.AddictedKey("sex")] = "true";
        character.SystemStats.Attributes[ViceState.DcKey("sex")] = 8;
        character.SystemStats.Attributes[ViceState.BaseDcKey("sex")] = 8;
        character.SystemStats.Attributes[ViceState.LastHoursKey("sex")] = 0;
        var ctx = new Recorder(character)
        {
            Time = new CampaignTime { TotalDaysElapsed = 1, Hour = 1 },
        };

        var result = await new LewdViceHandler().ApplyAsync(new LewdViceChange
        {
            CharacterId = "bob",
            Action = "note_presence",
            ViceId = "sex",
            InPresence = true,
        }, ctx);

        Assert.True(result.Success);
        Assert.Equal("true", character.SystemStats.Traits[ViceState.WithdrawalKey("sex")]);
        Assert.Contains("vice:sex", character.SystemStats.Traits[LewdKeys.IntrusiveThoughts]);
        Assert.Contains(ctx.Messages, m => m.Contains("temptation"));
    }

    [Fact]
    public async Task Sex_withdrawal_failure_stamps_overstimulation_not_exhaustion()
    {
        var character = Character("bob");
        character.SystemStats.Traits[ViceState.AddictedKey("sex")] = "true";
        character.SystemStats.Traits[ViceState.WithdrawalKey("sex")] = "true";
        character.SystemStats.Attributes[ViceState.DcKey("sex")] = 8;
        character.SystemStats.Attributes[ViceState.BaseDcKey("sex")] = 8;
        character.SystemStats.Attributes[ViceState.LastHoursKey("sex")] = 0;
        var ctx = new Recorder(character)
        {
            Time = new CampaignTime { TotalDaysElapsed = 2, Hour = 0 },
        };

        var result = await new LewdViceHandler().ApplyAsync(new LewdViceChange
        {
            CharacterId = "bob",
            Action = "rest",
            ViceId = "sex",
            D20 = 1,
        }, ctx);

        Assert.True(result.Success);
        Assert.Contains(character.SystemStats.StatusEffects, e =>
            e.ConditionName == LewdKeys.ConditionOverstimulation && e.Name == "Overstimulation 2");
        Assert.DoesNotContain(character.SystemStats.StatusEffects, e =>
            e.ConditionName == "exhaustion" || (e.Name?.StartsWith("Exhaustion") ?? false));
    }

    [Fact]
    public async Task Locked_vice_ignores_successful_clean_save()
    {
        var character = Character("bob");
        ViceState.LockSexualFluids(character, null);
        character.SystemStats.Traits[ViceState.WithdrawalKey("sexual_fluids")] = "true";
        character.SystemStats.Attributes[ViceState.LastHoursKey("sexual_fluids")] = 0;
        var ctx = new Recorder(character)
        {
            Time = new CampaignTime { TotalDaysElapsed = 3, Hour = 0 },
        };

        var result = await new LewdViceHandler().ApplyAsync(new LewdViceChange
        {
            CharacterId = "bob",
            Action = "rest",
            ViceId = "sexual_fluids",
            D20 = 20,
        }, ctx);

        Assert.True(result.Success);
        Assert.Equal("true", character.SystemStats.Traits[ViceState.AddictedKey("sexual_fluids")]);
        Assert.Equal("true", character.SystemStats.Traits[ViceState.LockedKey("sexual_fluids")]);
        Assert.Equal(18, character.SystemStats.Attributes[ViceState.DcKey("sexual_fluids")]);
        Assert.Contains(ctx.Messages, m => m.Contains("locked"));
    }

    [Fact]
    public async Task Observer_applies_pending_bad_end_vice_on_rest_without_mode()
    {
        var character = Character("bob");
        character.SystemStats.Traits[LewdKeys.BadEndViceId] = "alcohol";
        var ctx = new Recorder(character) { ActiveMode = null };
        var observer = new LewdViceObserver();
        Assert.True(observer.IsInterestedIn(new RestChange
        {
            CharacterId = "bob",
            LocationId = "loc",
            RestType = RestType.LongRest,
            IntendedHours = 8,
        }, ctx));

        await observer.OnCommittedAsync(new RestChange
        {
            CharacterId = "bob",
            LocationId = "loc",
            RestType = RestType.LongRest,
            IntendedHours = 8,
        }, ctx);

        Assert.False(character.SystemStats.Traits.ContainsKey(LewdKeys.BadEndViceId));
        Assert.Equal("true", character.SystemStats.Traits[ViceState.AddictedKey("alcohol")]);
        Assert.Equal(10, character.SystemStats.Attributes[ViceState.DcKey("alcohol")]);
        Assert.Contains(character.SystemStats.StatusEffects, e => e.ConditionName == "vice_alcohol");
    }

    [Fact]
    public async Task Bad_end_pending_is_consumed_on_first_lewd_vice()
    {
        var character = Character("bob");
        character.SystemStats.Traits[LewdKeys.BadEndViceId] = "sex";
        var ctx = new Recorder(character);
        var result = await new LewdViceHandler().ApplyAsync(new LewdViceChange
        {
            CharacterId = "bob",
            Action = "note_presence",
            ViceId = "sex",
        }, ctx);
        Assert.True(result.Success);
        Assert.False(character.SystemStats.Traits.ContainsKey(LewdKeys.BadEndViceId));
        Assert.Equal("true", character.SystemStats.Traits[ViceState.AddictedKey("sex")]);
    }

[Fact]
    public async Task Travel_with_elapsed_minutes_syncs_withdrawal_clock()
    {
        var character = Character("bob");
        character.SystemStats.Traits[ViceState.AddictedKey("sex")] = "true";
        character.SystemStats.Attributes[ViceState.DcKey("sex")] = 8;
        character.SystemStats.Attributes[ViceState.BaseDcKey("sex")] = 8;
        character.SystemStats.Attributes[ViceState.LastHoursKey("sex")] = 0;
        var ctx = new Recorder(character)
        {
            Time = new CampaignTime { TotalDaysElapsed = 2, Hour = 0 },
            ActiveMode = null,
        };
        var travel = new TravelChange
        {
            CharacterId = "bob",
            DestinationLocationId = "loc/tavern",
            MinutesElapsed = 60,
        };
        var observer = new LewdViceObserver();
        Assert.True(observer.IsInterestedIn(travel, ctx));
        await observer.OnCommittedAsync(travel, ctx);

        Assert.Equal("true", character.SystemStats.Traits[ViceState.WithdrawalKey("sex")]);
        Assert.Contains(ctx.Messages, m => m.Contains("temptation"));
    }

    [Fact]
    public async Task Rest_observer_uses_sheet_ability_mod_and_disadvantage()
    {
        var character = new Character
        {
            Id = "bob",
            Name = "bob",
            SystemStats = new Dnd5eExtension { Charisma = 16 },
        };
        character.SystemStats.Traits[ViceState.AddictedKey("sex")] = "true";
        character.SystemStats.Traits[ViceState.WithdrawalKey("sex")] = "true";
        character.SystemStats.Attributes[ViceState.DcKey("sex")] = 8;
        character.SystemStats.Attributes[ViceState.BaseDcKey("sex")] = 8;
        character.SystemStats.Attributes[ViceState.LastHoursKey("sex")] = 0;
        var ctx = new Recorder(character)
        {
            Time = new CampaignTime { TotalDaysElapsed = 1, Hour = 0 },
            ActiveMode = null,
            Rolls = new FixedRolls(face: 6),
        };
        var rolls = (FixedRolls)ctx.Rolls!;
        await new LewdViceObserver().OnCommittedAsync(new RestChange
        {
            CharacterId = "bob",
            LocationId = "loc",
            RestType = RestType.LongRest,
            IntendedHours = 8,
        }, ctx);

        // Face 6 + Cha +3 = 9 ≥ DC 8; at base DC a success clears addicted unless locked.
        Assert.Contains(rolls.Seen, r => r.Mechanic == DiceMechanic.Disadvantage && r.Bonus == 3);
        Assert.Contains(ctx.Messages, m => m.Contains("withdrawal save") && m.Contains("DC 8"));
        Assert.NotEqual("true", character.SystemStats.Traits.GetValueOrDefault(ViceState.AddictedKey("sex")));
    }

    private static Character Character(string id) => new()
    {
        Id = id,
        Name = id,
        SystemStats = new SystemExtension(),
    };

private sealed class FixedRolls : IRollService
    {
        private readonly int _face;
        public FixedRolls(int face) => _face = face;
        public List<RollRequest> Seen { get; } = [];

        public Task<RollOutcome> RollAsync(RollRequest request, CancellationToken ct = default)
        {
            Seen.Add(request);
            return Task.FromResult(new RollOutcome
            {
                Tag = request.Tag,
                Result = _face + request.Bonus,
                IndividualDice = [_face],
                Summary = $"[{_face}]+{request.Bonus}={_face + request.Bonus}",
            });
        }

        public async Task<IReadOnlyList<RollOutcome>> RollBatchAsync(
            IEnumerable<RollRequest> requests, CancellationToken ct = default)
        {
            var list = new List<RollOutcome>();
            foreach (var r in requests)
                list.Add(await RollAsync(r, ct));
            return list;
        }
    }

    private sealed class Recorder : IChangeContext
    {
        public Recorder(Character character)
        {
            Characters = new Dictionary<string, Character> { [character.Id] = character };
        }

        public List<string> Messages { get; } = [];
        public CampaignTime Time { get; set; } = new();
        public ModeEncounter? ActiveMode { get; set; }
        public IReadOnlyDictionary<string, Character> Characters { get; }
        public IReadOnlyDictionary<string, Item> Items { get; } = new Dictionary<string, Item>();
        public IReadOnlyDictionary<string, Location> Locations { get; } = new Dictionary<string, Location>();
        public IReadOnlyDictionary<string, Faction> Factions { get; } = new Dictionary<string, Faction>();
        public IReadOnlyDictionary<string, Quest> Quests { get; } = new Dictionary<string, Quest>();
        public Microsoft.Extensions.Logging.ILogger Logger { get; } = NullLogger.Instance;
        public CombatEncounter? ActiveCombat => null;
        public CampaignConfig? Config => null;
        public IRollService? Rolls { get; set; }
        public string? CampaignName => "test";
        public HashSet<string> InvolvedEntities { get; } = [];
        public IReadOnlyList<WorldChange>? Batch => null;
        public int BatchIndex => 0;
        public Func<Task<CampaignTime>> GetCurrentTimeAsync => () => Task.FromResult(Time);
        public Func<Task<Dictionary<string, string>>> GetSystemOptionsAsync =>
            () => Task.FromResult(new Dictionary<string, string>());
        public Func<Event, Task> LogEventAsync { get; } = _ => Task.CompletedTask;
        public void RegisterNewLocation(Location loc) { }
        public void RegisterNewCharacter(Character c) { }
        public void RegisterNewItem(Item i) { }
        public void RegisterNewFaction(Faction f) { }
        public void RegisterNewQuest(Quest q) { }
        public void RecordMessage(string message) => Messages.Add(message);
        public void RecordPhysicalStateNudge(string message) => Messages.Add(message);
        public void RecordFailure() { }
        public void RecordEntityCollision(string entityId, string message) { }
        public void RecordCommittedId(string id) { }
        public Task<string?> SuggestLocationMatchAsync(string? nameQuery) => Task.FromResult<string?>(null);
        public Task<string?> SuggestCharacterMatchAsync(string? nameQuery) => Task.FromResult<string?>(null);
        public Task<string?> SuggestItemMatchAsync(string? nameQuery) => Task.FromResult<string?>(null);
        public Task<string?> SuggestFactionMatchAsync(string? nameQuery) => Task.FromResult<string?>(null);
        public Task<string?> SuggestQuestMatchAsync(string? nameQuery) => Task.FromResult<string?>(null);
    }
}
