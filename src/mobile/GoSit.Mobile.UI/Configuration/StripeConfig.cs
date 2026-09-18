namespace GoSit.Mobile.UI.Configuration;

/// <summary>
/// Stripe's publishable key. Not a secret — it identifies the account to Stripe.js and is
/// meant to reach the client, the same key the web app embeds. See <c>lib/env.ts</c> on
/// the web side for the twin of this file.
/// </summary>
public static class StripeConfig
{
    public const string PublishableKey =
        "pk_test_51UGKvbECTiHFhT6w9z6pd60eZnblGKDmCNZrLEZhVrM24WfQNTjDMvl6uOo47ggiSy21cgMSLcEk9R9Cep08EQja00Aq7GO0TV";
}
