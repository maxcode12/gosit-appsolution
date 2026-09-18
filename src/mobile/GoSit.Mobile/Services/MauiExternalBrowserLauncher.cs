using GoSit.Mobile.UI.Abstractions;

namespace GoSit.Mobile.Services;

public sealed class MauiExternalBrowserLauncher : IExternalBrowserLauncher
{
    public Task OpenAsync(Uri url) => Launcher.Default.OpenAsync(url);
}
