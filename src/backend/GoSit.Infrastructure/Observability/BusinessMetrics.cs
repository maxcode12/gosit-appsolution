using System.Diagnostics.Metrics;

namespace GoSit.Infrastructure.Observability;

/// <summary>
/// The marketplace health metrics listed in CLAUDE.md.
///
/// Derived from domain events rather than instrumented into each handler: every
/// meaningful state change already raises one, so this is a single place that cannot
/// drift out of step with the business logic.
///
/// Deliberately dimension-free beyond an event name. Attaching a user id, an email or a
/// city would turn a metrics backend into a second, less protected copy of the personal
/// data — and metrics are widely readable inside an organisation. Counts only.
/// </summary>
public sealed class BusinessMetrics : IDisposable
{
    public const string MeterName = "GoSit.Business";

    private readonly Meter _meter;
    private readonly Counter<long> _domainEvents;
    private readonly Counter<long> _registrations;
    private readonly Counter<long> _searches;
    private readonly Counter<long> _logins;

    public BusinessMetrics()
    {
        _meter = new Meter(MeterName);

        // One counter keyed by event name covers bookings, payments, messages, reviews
        // and payouts without a new metric per feature.
        _domainEvents = _meter.CreateCounter<long>(
            "gosit.domain_events",
            unit: "{event}",
            description: "Domain events raised, by event type.");

        _registrations = _meter.CreateCounter<long>(
            "gosit.registrations",
            unit: "{account}",
            description: "Accounts registered, by role.");

        _searches = _meter.CreateCounter<long>(
            "gosit.searches",
            unit: "{search}",
            description: "Caregiver searches performed.");

        _logins = _meter.CreateCounter<long>(
            "gosit.logins",
            unit: "{login}",
            description: "Login attempts, by outcome.");
    }

    /// <summary>
    /// Records a domain event by type name. The name is a compile-time class name, never
    /// anything a user supplied.
    /// </summary>
    public void RecordDomainEvent(string eventTypeName) =>
        _domainEvents.Add(1, new KeyValuePair<string, object?>("event", eventTypeName));

    public void RecordRegistration(string role) =>
        _registrations.Add(1, new KeyValuePair<string, object?>("role", role));

    /// <summary>
    /// Counts a search. Deliberately takes no filters: a city or postal code would make
    /// this a record of where individual families are looking for care.
    /// </summary>
    public void RecordSearch(bool hadResults) =>
        _searches.Add(1, new KeyValuePair<string, object?>("had_results", hadResults));

    public void RecordLogin(bool succeeded) =>
        _logins.Add(1, new KeyValuePair<string, object?>("outcome", succeeded ? "success" : "failure"));

    public void Dispose() => _meter.Dispose();
}
