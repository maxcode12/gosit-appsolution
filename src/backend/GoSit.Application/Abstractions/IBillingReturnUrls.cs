using GoSit.Domain.Subscriptions;

namespace GoSit.Application.Abstractions;

/// <summary>
/// Where the provider sends someone after hosted checkout.
///
/// An abstraction rather than a configuration read inside the handler, because these are
/// web-app URLs and the application layer has no business knowing the shape of the front
/// end. It also keeps the two sides of the marketplace apart: a caregiver who finishes
/// checkout should land back in the caregiver portal, not on a family's screen.
///
/// Note what these URLs are *not*: proof of anything. Someone can type the success URL
/// directly. The page it lands on says the subscription is being confirmed and reads the
/// real state from the API, which only the webhook can change.
/// </summary>
public interface IBillingReturnUrls
{
    string CheckoutSuccessUrl(PlanAudience audience);

    string CheckoutCancelUrl(PlanAudience audience);

    string BillingPortalReturnUrl(PlanAudience audience);
}

/// <summary>
/// Where the provider returns a caregiver after hosted payout onboarding.
///
/// Same reasoning as <see cref="IBillingReturnUrls"/>: these are web-app URLs, and the
/// application layer has no business knowing the shape of the front end. And as there,
/// returning to one of these proves nothing — the page reads the caregiver's real
/// standing from the API.
/// </summary>
public interface IPayoutReturnUrls
{
    string OnboardingReturnUrl();

    /// <summary>Where the provider sends someone whose link expired before they finished.</summary>
    string OnboardingRefreshUrl();
}
