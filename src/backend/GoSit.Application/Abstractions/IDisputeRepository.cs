using GoSit.Application.Features.Administration.Disputes;

namespace GoSit.Application.Abstractions;

/// <summary>
/// The staff view of disputed bookings.
///
/// A query-side repository, like the certification review queue: the booking aggregate
/// loads one at a time, which is right for a transition and useless for a list that spans
/// every family and caregiver on the platform.
/// </summary>
public interface IDisputeRepository
{
    /// <summary>
    /// Disputes nobody has settled, oldest first.
    ///
    /// Oldest first because a dispute sitting here is a caregiver not being paid and a
    /// family not being answered at the same time — the wait costs both of them.
    /// </summary>
    Task<IReadOnlyList<OpenDispute>> ListOpenAsync(int take, CancellationToken cancellationToken);
}
