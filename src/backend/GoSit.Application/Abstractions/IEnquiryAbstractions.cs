using GoSit.Domain.Enquiries;

namespace GoSit.Application.Abstractions;

public interface IEnquiryRepository
{
    void Add(Enquiry enquiry);

    Task<Enquiry?> FindByIdAsync(Guid enquiryId, CancellationToken cancellationToken);

    /// <summary>
    /// The staff queue, oldest first so nobody who wrote in early waits behind later arrivals.
    /// When <paramref name="includeClosed"/> is false only new enquiries are returned.
    /// </summary>
    Task<IReadOnlyList<Enquiry>> ListAsync(
        bool includeClosed,
        int take,
        CancellationToken cancellationToken);
}

/// <summary>
/// Tells the team a message has arrived. Never throws and never blocks the sender: the
/// enquiry is already saved by the time this runs, so a mail failure must not turn into an
/// error on the public form.
/// </summary>
public interface IEnquiryAlert
{
    Task NotifyAsync(Guid enquiryId, EnquiryKind kind, CancellationToken cancellationToken);
}
