namespace GoSit.Mobile.UI.Abstractions;

/// <summary>
/// Opens a URL in the device's own browser, outside the app.
///
/// Stripe's Connect onboarding is a hosted flow collecting identity and bank details —
/// exactly the kind of page this app must never render itself, in an embedded WebView a
/// person cannot verify is really Stripe.
/// </summary>
public interface IExternalBrowserLauncher
{
    Task OpenAsync(Uri url);
}
