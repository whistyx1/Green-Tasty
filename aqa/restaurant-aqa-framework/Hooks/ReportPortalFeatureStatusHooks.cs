using System.Collections.Concurrent;
using ReportPortal.Client.Abstractions.Models;
using ReportPortal.ReqnrollPlugin;
using Reqnroll;

namespace Restaurant_Aqa_Framework.Hooks;

/// <summary>
/// Let ReportPortal derive a feature suite's status from its scenarios.
/// The Reqnroll agent otherwise finishes every feature suite as Skipped.
/// </summary>
[Binding]
public static class ReportPortalFeatureStatusHooks
{
    private static readonly ConcurrentDictionary<string, Status> ScenarioOutcomes = new();
    private static int _registered;

    [BeforeTestRun(Order = -21000)]
    public static void Register()
    {
        ScenarioOutcomes.Clear();
        if (Interlocked.Exchange(ref _registered, 1) == 1)
        {
            return;
        }

        ReportPortalAddin.AfterScenarioFinished += (_, args) =>
        {
            var outcome = args.FinishTestItemRequest.Status switch
            {
                Status.Passed => Status.Passed,
                Status.Skipped => Status.Skipped,
                _ => Status.Failed
            };
            ScenarioOutcomes.AddOrUpdate(
                args.FeatureContext.FeatureInfo.Title,
                outcome,
                (_, previous) => previous == Status.Failed || outcome == Status.Failed
                    ? Status.Failed
                    : previous == Status.Skipped || outcome == Status.Skipped
                        ? Status.Skipped
                        : Status.Passed);
        };

        ReportPortalAddin.BeforeFeatureFinished += (_, args) =>
        {
            if (ScenarioOutcomes.TryRemove(args.FeatureContext.FeatureInfo.Title, out var outcome))
            {
                args.FinishTestItemRequest.Status = outcome;
            }
        };
    }
}
