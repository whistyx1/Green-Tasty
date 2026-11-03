using Microsoft.Playwright;
using Reqnroll.BoDi;
using Restaurant_Aqa_Framework.Core;
using Restaurant_Aqa_Framework.Screenplay;
using Restaurant_Aqa_Framework.Screenplay.Abilities;
using Restaurant_Aqa_Framework.Support.GroupOrders;

namespace Restaurant_Aqa_Framework.Hooks;

/// <summary>Capture a masked browser screenshot before test data or contexts are removed.
/// Raw level-one traces are discarded because they can contain passwords/session tokens.</summary>
[Binding]
public sealed class LevelOneArtifactHooks(IObjectContainer container, ScenarioContext scenario)
{
    private bool _captured;

    [AfterScenario("level1", Order = -200)]
    [AfterScenario("level2", Order = -200)]
    [AfterScenario("level3", Order = -200)]
    public async Task CaptureFailureAsync(IReqnrollOutputHelper output)
    {
        if (_captured || !container.IsRegistered<Actor>())
            return;

        var actor = container.Resolve<Actor>();
        var cleanupFailed = actor.TryRecall<GroupOrderCleanupStatus>(out var cleanup) && cleanup.FailureCount > 0;
        if (!cleanupFailed && scenario.TestError is null or TUnit.Core.Exceptions.SkipTestException)
            return;
        if (!actor.Can<IBrowseTheWeb>())
            return;

        var page = scenario.ScenarioInfo.CombinedTags.Any(tag => tag is "level2" or "level3")
            && actor.TryRecall<GroupOrderBrowserState>(out var state)
            && state.WaiterPage is not null
                ? state.WaiterPage
                : actor.LookingAtThePage();
        if (page.IsClosed)
            return;

        var directory = Directory.CreateDirectory(Path.Combine(ArtifactPaths.Root, "screenshots"));
        var path = Path.Combine(directory.FullName, $"group-order-failed-{Guid.NewGuid():N}.png");
        try
        {
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = path,
                FullPage = true,
                Mask = [page.Locator("input, textarea, .invite-field, a[href*='/join/']")],
            });
            output.AddAttachment(path);
            _captured = true;
        }
        catch (PlaywrightException)
        {
            // Preserve the original test failure; do not log a sensitive browser URL.
            output.WriteLine("A masked failure screenshot could not be captured.");
        }
    }

    [AfterScenario("level1", Order = -50)]
    [AfterScenario("level2", Order = -50)]
    [AfterScenario("level3", Order = -50)]
    public Task CaptureCleanupFailureAsync(IReqnrollOutputHelper output) => CaptureFailureAsync(output);
}
