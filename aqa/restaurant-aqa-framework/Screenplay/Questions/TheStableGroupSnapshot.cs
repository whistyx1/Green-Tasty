using System.Diagnostics;
using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;
using Restaurant_Aqa_Framework.Configuration;
using Restaurant_Aqa_Framework.Support.GroupOrders;

namespace Restaurant_Aqa_Framework.Screenplay.Questions;

/// <summary>Condition-based read-only polling for the backend's eventually consistent session
/// index. Never retries a mutation or a failed HTTP response; no blind fixed wait.</summary>
public sealed class TheStableGroupSnapshot(int expectedCount, decimal expectedTotal, int expectedSubmitted = 1) : IQuestion<GroupSessionsSnapshot>
{
    public async Task<GroupSessionsSnapshot> AnsweredByAsync(Actor actor)
    {
        var scenario = actor.Recall<GroupOrderScenario>();
        var groups = new GroupOrdersApi(actor.UsingTheApi());
        var elapsed = Stopwatch.StartNew();
        using var polling = new PeriodicTimer(TimeSpan.FromMilliseconds(200));
        while (true)
        {
            var snapshot = await GroupOrderScenario.ReadRequiredAsync<GroupSessionsSnapshot>(
                await groups.GetParticipantsAsync(scenario.GroupId, scenario.Owner.IdToken), "Read participant snapshot");
            if ((snapshot.Participants.Count == expectedCount && snapshot.Total == expectedTotal
                && snapshot.Participants.Select(person => person.Id).Order().SequenceEqual(scenario.Guests.Select(guest => guest.SessionId).Order())
                && snapshot.Participants.Count(person => person.IsSubmitted) == expectedSubmitted)
                || elapsed.ElapsedMilliseconds >= Settings.Current.DefaultTimeoutMs)
                return snapshot;
            await polling.WaitForNextTickAsync();
        }
    }
}
