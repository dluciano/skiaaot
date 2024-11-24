using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Internals;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace SkiaAot;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseSkiaSharp()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });
        builder.Services.AddSingleton<IResourceDictionary, ResourceDictionary>();
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddSingleton<MainPage>();
#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}