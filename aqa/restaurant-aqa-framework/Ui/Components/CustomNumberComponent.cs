using System.Globalization;
using Microsoft.Playwright;

namespace Restaurant_Aqa_Framework.Ui.Components;

/// <summary>The app's numeric stepper, located through its current accessible button names.</summary>
public sealed class CustomNumberComponent(ILocator root)
{
    public ILocator Value => root.Locator(".counter .value");

    public Task IncrementAsync() => root.GetByRole(AriaRole.Button, new() { Name = "Increase value", Exact = true }).ClickAsync();
    public Task DecrementAsync() => root.GetByRole(AriaRole.Button, new() { Name = "Decrease value", Exact = true }).ClickAsync();

    public async Task SetAsync(int target)
    {
        var current = int.Parse((await Value.TextContentAsync())!.Trim(), CultureInfo.InvariantCulture);
        while (current < target)
        {
            await IncrementAsync();
            current++;
        }

        while (current > target)
        {
            await DecrementAsync();
            current--;
        }
    }
}
