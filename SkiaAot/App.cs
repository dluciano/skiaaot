using Microsoft.Maui.Controls.Internals;

namespace SkiaAot;

internal class App : Application
{
    public App(IResourceDictionary resourceDictionary)
    {
        Resources = (ResourceDictionary)resourceDictionary;
        var mergedDictionaries = Resources.MergedDictionaries;
        mergedDictionaries.Clear();
        mergedDictionaries.Add(new global::SkiaAot.Resources.Styles.Colors());
        mergedDictionaries.Add(new Resources.Styles.Styles());
        var x = resourceDictionary.TryGetValue("Headline", out var test);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        ArgumentNullException.ThrowIfNull(activationState);
        return new Window(activationState.Context.Services.GetRequiredService<AppShell>());
    }
}