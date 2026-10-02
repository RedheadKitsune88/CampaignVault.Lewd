using Xunit;
using CampaignVault.Events;
using CampaignVault.Models;
using LewdHandbook.Handlers;

namespace LewdHandbook.Tests;

public class InterruptHandlerTests
{
    private static TestContext Scene(params string[] ids)
    {
        var mode = new LewdEncounterMode().StateMachine.CreateEncounter("inn", ids);
        return new TestContext(mode);
    }

    private static DomainEvent Died(string id) =>
        DomainEvent.Create(CoreEvents.CharacterDied, new Dictionary<string, object?> { [CoreEvents.Fields.CharacterId] = id });

    [Fact]
    public async Task A_death_in_the_scene_ends_it()
    {
        var changes = await new LewdInterruptHandler().HandleAsync(Died("bob"), Scene("alice", "bob"));

        var exit = Assert.IsType<ModeTransitionChange>(Assert.Single(changes));
        Assert.Equal("exit", exit.Action);
    }

    [Fact]
    public async Task A_death_elsewhere_leaves_the_scene_alone()
    {
        Assert.Empty(await new LewdInterruptHandler().HandleAsync(Died("carol"), Scene("alice", "bob")));
    }

    [Fact]
    public void The_handler_subscribes_to_deaths()
    {
        Assert.Contains(CoreEvents.CharacterDied, new LewdInterruptHandler().Topics);
    }
}
