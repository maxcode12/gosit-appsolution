using GoSit.Mobile.Services;
using GoSit.Mobile.UI.Abstractions;
using GoSit.Mobile.UI.Services;
using Microsoft.Extensions.Logging;

namespace GoSit.Mobile;

public static class MauiProgram
{
    /// <summary>
    /// Where the API lives.
    ///
    /// A phone is not the machine running the API, so "localhost" is the device itself
    /// and never the developer's laptop. 10.0.2.2 is the Android emulator's route to the
    /// host; everything else falls back to localhost, which is right for the Windows and
    /// Mac Catalyst heads. A real deployment replaces this with the public URL.
    /// </summary>
    private static string ApiBaseUrl =>
        DeviceInfo.Platform == DevicePlatform.Android && DeviceInfo.DeviceType == DeviceType.Virtual
            ? "http://10.0.2.2:5199"
            : "http://localhost:5199";

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();
        ConfigureMediaPermissions();

        // Device capabilities are registered behind interfaces so shared UI stays testable.
        builder.Services.AddSingleton<ISecureTokenStore, MauiSecureTokenStore>();
        builder.Services.AddSingleton<IConnectivityService, MauiConnectivityService>();
        builder.Services.AddSingleton<IDeviceLocationService, MauiDeviceLocationService>();
        builder.Services.AddSingleton<IExternalBrowserLauncher, MauiExternalBrowserLauncher>();

        builder.Services.AddSingleton<SessionStore>();
        builder.Services.AddScoped<BookingService>();
        builder.Services.AddScoped<InterviewService>();
        builder.Services.AddScoped<CaregiverProfileService>();
        builder.Services.AddScoped<NotificationService>();

        /*
         * A short timeout on purpose. The default is 100 seconds, which on a phone with
         * one bar means a button that looks stuck for over a minute — and a stuck button
         * gets pressed again. Ten seconds is long enough for a slow connection and short
         * enough to say "no connection" while someone is still looking at the screen.
         */
        builder.Services.AddSingleton(_ => new HttpClient
        {
            BaseAddress = new Uri(ApiBaseUrl),
            Timeout = TimeSpan.FromSeconds(10),
        });

        builder.Services.AddScoped<GoSitApiClient>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    /// <summary>
    /// Grants the BlazorWebView's own camera and microphone permission requests without a
    /// system prompt.
    ///
    /// LiveKit's <c>getUserMedia</c> call runs inside the WebView, not as a MAUI feature —
    /// so the OS-level permission MAUI requests in the manifest is necessary but not
    /// enough; the WebView engine itself asks a second time, and each platform's engine
    /// asks differently. The interview's own consent screen is where a person agrees to
    /// being recorded; this only stops the OS re-asking underneath that screen.
    /// </summary>
    private static void ConfigureMediaPermissions()
    {
#if WINDOWS
        Microsoft.AspNetCore.Components.WebView.Maui.BlazorWebViewHandler.BlazorWebViewMapper
            .AppendToMapping("GoSitMediaPermissions", (handler, _) =>
            {
                handler.PlatformView.CoreWebView2Initialized += (webView, _) =>
                {
                    webView.CoreWebView2.PermissionRequested += (_, args) =>
                    {
                        if (args.PermissionKind is Microsoft.Web.WebView2.Core.CoreWebView2PermissionKind.Camera
                            or Microsoft.Web.WebView2.Core.CoreWebView2PermissionKind.Microphone)
                        {
                            args.State = Microsoft.Web.WebView2.Core.CoreWebView2PermissionState.Allow;
                        }
                    };
                };
            });
#elif ANDROID
        Microsoft.AspNetCore.Components.WebView.Maui.BlazorWebViewHandler.BlazorWebViewMapper
            .AppendToMapping("GoSitMediaPermissions", (handler, _) =>
            {
                handler.PlatformView.SetWebChromeClient(new GoSitWebChromeClient());
            });
#endif
    }
}

#if ANDROID
/// <summary>
/// Grants an Android WebView's own camera/microphone permission prompt. See remarks on
/// ConfigureMediaPermissions.
///
/// Not <c>file</c>-scoped: Android's Java Callable Wrapper generator turns this class
/// into a .java file named after its compiled type name, and a file-scoped type's
/// compiler-generated name contains "&lt;&gt;" characters that are illegal in a Windows
/// file path — building for Android failed outright until this was a plain internal
/// class.
/// </summary>
internal sealed class GoSitWebChromeClient : Android.Webkit.WebChromeClient
{
    public override void OnPermissionRequest(Android.Webkit.PermissionRequest? request) =>
        request?.Grant(request.GetResources());
}
#endif
