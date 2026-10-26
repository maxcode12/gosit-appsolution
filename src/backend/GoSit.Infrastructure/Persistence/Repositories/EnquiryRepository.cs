using GoSit.Application.Abstractions;
using GoSit.Domain.Enquiries;
using Microsoft.EntityFrameworkCore;

namespace GoSit.Infrastructure.Persistence.Repositories;

public sealed class EnquiryRepository(GoSitDbContext context) : IEnquiryRepository
{
    public void Add(Enquiry enquiry) => context.Enquiries.Add(enquiry);

    public Task<Enquiry?> FindByIdAsync(Guid enquiryId, CancellationToken cancellationToken) =>
        context.Enquiries.SingleOrDefaultAsync(e => e.Id == enquiryId, cancellationToken);

    public async Task<IReadOnlyList<Enquiry>> ListAsync(
        bool includeClosed,
        int take,
        CancellationToken cancellationToken) =>
        await context.Enquiries
            .AsNoTracking()
            .Where(e => includeClosed || e.Status == EnquiryStatus.New)
            .OrderBy(e => e.CreatedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);
}
