using System.Net;
using System.Text;
using System.Web;
using GoSit.Application.Abstractions;
using GoSit.Infrastructure.Payments;
using Microsoft.Extensions.Options;
using Xunit;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// What the real Stripe provider actually puts on the wire.
///
/// Everything else in the billing suite substitutes a fake at the
/// <see cref="ISubscriptionBillingProvider"/> seam, which proves GoSit's own logic but
/// never runs a line of <see cref="StripeSubscriptionBillingProvider"/>. The risk that
/// leaves is precise: an option named wrongly, a mode misspelt, metadata that never
/// leaves the process. None of that fails a build, and all of it fails in production.
///
/// So these point the genuine provider — the real Stripe SDK, serializing the real
/// options objects — at a local stub speaking Stripe's wire format, and assert on the
/// requests that arrive. What is still unproven is whether Stripe *accepts* them; that
/// needs a test-mode account, and is called out in docs/DEVELOPMENT.md.
/// </summary>
public sealed class StripeApiStub : IAsyncDisposable
{
    private readonly HttpListener _listener = new();
    private readonly Task _loop;

    public sealed record Captured(string Method, string Path, string Body, string? IdempotencyKey)
    {
        /// <summary>Stripe takes form-encoded bodies, including its bracketed nesting.</summary>
        public string? Field(string name) => HttpUtility.ParseQueryString(Body)[name];
    }

    public List<Captured> Requests { get; } = [];

    public string BaseUrl { get; }

    public StripeApiStub()
    {
        BaseUrl = LoopbackPort.StartOnFreePort(_listener);

        _loop = Task.Run(ServeAsync);
    }

    private async Task ServeAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;

            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (HttpListenerException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();

            var path = context.Request.Url?.AbsolutePath ?? string.Empty;

            Requests.Add(new Captured(
                context.Request.HttpMethod,
                path,
                body,
                context.Request.Headers["Idempotency-Key"]));

            var payload = Encoding.UTF8.GetBytes(ResponseFor(path));

            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            await context.Response.OutputStream.WriteAsync(payload);
            context.Response.Close();
        }
    }

    /// <summary>Just enough of a Stripe object for the SDK to deserialize.</summary>
    private static string ResponseFor(string path) => path switch
    {
        "/v1/customers" => """{"id":"cus_stub_1","object":"customer"}""",
        "/v1/checkout/sessions" =>
            """{"id":"cs_stub_1","object":"checkout.session","url":"https://checkout.stripe.test/cs_stub_1"}""",
        "/v1/billing_portal/sessions" =>
            """{"id":"bps_stub_1","object":"billing_portal.session","url":"https://billing.stripe.test/bps_stub_1"}""",
        _ => """{"id":"sub_stub_1","object":"subscription","status":"active"}""",
    };

    public async ValueTask DisposeAsync()
    {
        _listener.Stop();
        _listener.Close();

        await Task.WhenAny(_loop, Task.Delay(TimeSpan.FromSeconds(2)));
    }
}

public class StripeApiContractTests
{
    private static readonly IReadOnlyDictionary<string, string> Metadata =
        new Dictionary<string, string>
        {
            [SubscriptionMetadataKeys.UserId] = "11111111-1111-1111-1111-111111111111",
            [SubscriptionMetadataKeys.PlanCode] = "PLUS_REQUESTER",
        };

    private static StripeSubscriptionBillingProvider ProviderFor(StripeApiStub stub) =>
        new(Options.Create(new StripeOptions
        {
            SecretKey = "sk_test_stub",
            ApiBase = stub.BaseUrl,
        }));

    [Fact]
    public async Task Creating_a_customer_sends_the_email_name_metadata_and_idempotency_key()
    {
        await using var stub = new StripeApiStub();

        var customerId = await ProviderFor(stub).EnsureCustomerAsync(
            existingCustomerId: null,
            "ada@example.com",
            "Ada Lovelace",
            "customer:abc",
            Metadata,
            CancellationToken.None);

        Assert.Equal("cus_stub_1", customerId);

        var request = Assert.Single(stub.Requests);

        Assert.Equal("POST", request.Method);
        Assert.Equal("/v1/customers", request.Path);
        Assert.Equal("ada@example.com", request.Field("email"));
        Assert.Equal("Ada Lovelace", request.Field("name"));

        // A retry must not leave one account with two customers.
        Assert.Equal("customer:abc", request.IdempotencyKey);

        Assert.Equal(
            "11111111-1111-1111-1111-111111111111",
            request.Field($"metadata[{SubscriptionMetadataKeys.UserId}]"));
    }

    /// <summary>An account that already has a customer must not get a second one.</summary>
    [Fact]
    public async Task An_existing_customer_is_reused_without_calling_stripe()
    {
        await using var stub = new StripeApiStub();

        var customerId = await ProviderFor(stub).EnsureCustomerAsync(
            existingCustomerId: "cus_existing",
            "ada@example.com",
            "Ada Lovelace",
            "customer:abc",
            Metadata,
            CancellationToken.None);

        Assert.Equal("cus_existing", customerId);
        Assert.Empty(stub.Requests);
    }

    /// <summary>
    /// The most consequential request in the integration. Mode, price, and — critically —
    /// metadata on *both* the session and the subscription it creates, since the second
    /// is what lets a renewal a year from now be traced back to an account.
    /// </summary>
    [Fact]
    public async Task Checkout_is_created_as_a_subscription_with_metadata_on_both_objects()
    {
        await using var stub = new StripeApiStub();

        var session = await ProviderFor(stub).StartCheckoutAsync(
            new BillingCheckoutRequest(
                "cus_stub_1",
                "price_test_plus",
                "https://gosit.test/account/plan?checkout=complete",
                "https://gosit.test/account/plan?checkout=cancelled",
                "checkout:abc",
                Metadata),
            CancellationToken.None);

        Assert.Equal("https://checkout.stripe.test/cs_stub_1", session.Url);

        var request = Assert.Single(stub.Requests);

        Assert.Equal("/v1/checkout/sessions", request.Path);
        Assert.Equal("subscription", request.Field("mode"));
        Assert.Equal("cus_stub_1", request.Field("customer"));
        Assert.Equal("price_test_plus", request.Field("line_items[0][price]"));
        Assert.Equal("1", request.Field("line_items[0][quantity]"));

        Assert.Equal(
            "https://gosit.test/account/plan?checkout=complete", request.Field("success_url"));
        Assert.Equal(
            "https://gosit.test/account/plan?checkout=cancelled", request.Field("cancel_url"));

        Assert.Equal("checkout:abc", request.IdempotencyKey);

        // On the session…
        Assert.Equal(
            "PLUS_REQUESTER",
            request.Field($"metadata[{SubscriptionMetadataKeys.PlanCode}]"));

        // …and on the subscription Stripe will create from it.
        Assert.Equal(
            "PLUS_REQUESTER",
            request.Field($"subscription_data[metadata][{SubscriptionMetadataKeys.PlanCode}]"));
        Assert.Equal(
            "11111111-1111-1111-1111-111111111111",
            request.Field($"subscription_data[metadata][{SubscriptionMetadataKeys.UserId}]"));
    }

    /// <summary>
    /// Cancelling must set cancel_at_period_end rather than deleting the subscription —
    /// someone who has paid for a month gets the month.
    /// </summary>
    [Fact]
    public async Task Cancelling_sets_cancel_at_period_end_rather_than_deleting()
    {
        await using var stub = new StripeApiStub();

        await ProviderFor(stub).CancelAtPeriodEndAsync("sub_stub_1", CancellationToken.None);

        var request = Assert.Single(stub.Requests);

        Assert.Equal("POST", request.Method);
        Assert.Equal("/v1/subscriptions/sub_stub_1", request.Path);
        Assert.Equal("true", request.Field("cancel_at_period_end"));
    }

    [Fact]
    public async Task The_billing_portal_is_opened_for_the_customer_with_a_return_url()
    {
        await using var stub = new StripeApiStub();

        var url = await ProviderFor(stub).CreateBillingPortalUrlAsync(
            "cus_stub_1", "https://gosit.test/account/plan", CancellationToken.None);

        Assert.Equal("https://billing.stripe.test/bps_stub_1", url);

        var request = Assert.Single(stub.Requests);

        Assert.Equal("/v1/billing_portal/sessions", request.Path);
        Assert.Equal("cus_stub_1", request.Field("customer"));
        Assert.Equal("https://gosit.test/account/plan", request.Field("return_url"));
    }
}
