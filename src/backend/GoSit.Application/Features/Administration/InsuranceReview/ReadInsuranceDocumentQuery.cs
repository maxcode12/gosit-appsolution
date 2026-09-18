using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Domain.Caregivers;
using Microsoft.Extensions.Logging;

namespace GoSit.Application.Features.Administration.InsuranceReview;

/// <summary>
/// Streams a proof-of-insurance document to the staff member reviewing it. Mirrors
/// <see cref="Administration.CertificationReview.ReadCertificationDocumentHandler"/>
/// exactly — streamed rather than linked, audited before the read, for the same reason:
/// a link to someone's insurance policy is a bearer token for whoever holds it.
/// </summary>
public sealed record ReadInsuranceDocumentQuery(Guid CaregiverInsurancePolicyId) : IQuery<StoredDocument>;

public sealed class ReadInsuranceDocumentHandler(
    IInsuranceReviewRepository insurance,
    ICaregiverProfileRepository profiles,
    IDocumentStore documents,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    ICurrentUser currentUser,
    ILogger<ReadInsuranceDocumentHandler> logger) : IQueryHandler<ReadInsuranceDocumentQuery, StoredDocument>
{
    public async Task<StoredDocument> HandleAsync(ReadInsuranceDocumentQuery query, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var profile = await ApproveInsuranceHandler.LoadOwnerAsync(
            profiles, insurance, query.CaregiverInsurancePolicyId, cancellationToken);

        var policy = profile.InsurancePolicies
            .SingleOrDefault(p => p.Id == query.CaregiverInsurancePolicyId)
            ?? throw new NotFoundException("That insurance policy does not exist.");

        if (policy.DocumentReference is not { } key)
        {
            throw new NotFoundException("No document has been uploaded for that policy.");
        }

        audit.Record(nameof(CaregiverInsurancePolicy), policy.Id, "InsuranceDocumentViewed", userId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var document = await documents.ReadAsync(key, cancellationToken)
            ?? throw new NotFoundException("That document is no longer available.");

        logger.LogInformation(
            "Insurance document {CaregiverInsurancePolicyId} read by staff {UserId}", policy.Id, userId);

        return document;
    }
}
