using GoSit.Application.Abstractions;
using GoSit.Domain.Subscriptions;
using Microsoft.Extensions.Options;

namespace GoSit.Infrastructure.Payments;

public sealed class WebAppOptions
{
    public const string SectionName = "WebApp";

    /// <summary>
    /// Where the web app is served from, without a trailing slash. Configured per
    /// environment; defaults to the local dev server so checkout works out of the box
    /// once Stripe keys are supplied.
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:3000";
}

/// <summary>
/// Builds the URLs the provider returns someone to.
///
/// Each side of the marketplace goes back to its own plan screen. The success URL is
/// only a place to land — it proves nothing, and the page it opens reads real state from
/// the API rather than believing the redirect.
/// </summary>
public sealed class BillingReturnUrls(IOptions<WebAppOptions> options) : IBillingReturnUrls
{
    private string Base => options.Value.BaseUrl.TrimEnd('/');

    private static string PlanPath(PlanAudience audience) =>
        audience == PlanAudience.Caregiver ? "/caregiver/plan" : "/account/plan";

    public string CheckoutSuccessUrl(PlanAudience audience) =>
        $"{Base}{PlanPath(audience)}?checkout=complete";

    public string CheckoutCancelUrl(PlanAudience audience) =>
        $"{Base}{PlanPath(audience)}?checkout=cancelled";

    public string BillingPortalReturnUrl(PlanAudience audience) =>
        $"{Base}{PlanPath(audience)}";
}

/// <summary>
/// Where the provider returns a caregiver after hosted payout onboarding.
///
/// Both land on the earnings screen, which reads the caregiver's real standing from the
/// API — returning here proves nothing, and someone whose link expired needs to be
/// offered a fresh one rather than told they failed.
/// </summary>
public sealed class PayoutReturnUrls(IOptions<WebAppOptions> options) : IPayoutReturnUrls
{
    private string Base => options.Value.BaseUrl.TrimEnd('/');

    public string OnboardingReturnUrl() => $"{Base}/caregiver/earnings?payouts=complete";

    public string OnboardingRefreshUrl() => $"{Base}/caregiver/earnings?payouts=refresh";
}
