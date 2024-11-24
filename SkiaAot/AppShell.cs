namespace SkiaAot;

public class AppShell : Shell
{
    public AppShell()
    {
        Title = "SkiaAot";
        FlyoutBehavior = FlyoutBehavior.Flyout;
        var shellContent = new ShellContent()
        {
            Title = "Home",
            ContentTemplate = new(() =>
            {
                var mainPage = Handler.MauiContext.Services.GetRequiredService<MainPage>();
                return mainPage;
            }),
            Route = "MainPage"
        };
        Items.Add(shellContent);
    }
}