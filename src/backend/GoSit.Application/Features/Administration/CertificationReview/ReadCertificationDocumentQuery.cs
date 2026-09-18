using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using Microsoft.Extensions.Logging;

namespace GoSit.Application.Features.Administration.CertificationReview;

/// <summary>
/// Streams a certification document to the staff member reviewing it.
///
/// Streamed through the API rather than handed out as a link. A shared access signature
/// would be less code and would also be a bearer token for someone's vulnerable-sector
/// check — valid for anyone who ends up holding the URL, including a browser history, a
/// proxy log, or a support ticket someone pasted it into.
///
/// Every read is audited. This is the most sensitive thing GoSit stores, and "which
/// staff member opened whose criminal record check, and when" is a question that has to
/// have an answer.
/// </summary>
public sealed record ReadCertificationDocumentQuery(Guid CaregiverCertificationId)
    : IQuery<StoredDocument>;

public sealed class ReadCertificationDocumentHandler(
    ICertificationReviewRepository certifications,
    ICaregiverProfileRepository profiles,
    IDocumentStore documents,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    ICurrentUser currentUser,
    ILogger<ReadCertificationDocumentHandler> logger)
    : IQueryHandler<ReadCertificationDocumentQuery, StoredDocument>
{
    public async Task<StoredDocument> HandleAsync(
        ReadCertificationDocumentQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var profile = await ApproveCertificationHandler.LoadOwnerAsync(
            profiles, certifications, query.CaregiverCertificationId, cancellationToken);

        var certification = profile.Certifications
            .SingleOrDefault(c => c.Id == query.CaregiverCertificationId)
            ?? throw new NotFoundException("That certification does not exist.");

        if (certification.DocumentReference is not { } key)
        {
            throw new NotFoundException("No document has been uploaded for that certification.");
        }

        /*
         * Recorded before the read, and saved before the stream is handed back.
         *
         * An audit row written afterwards is a row that never appears if the download
         * fails halfway or the process restarts — and the access still happened. For this
         * particular record the log matters more than the convenience.
         */
        audit.Record(
            "CaregiverCertification",
            certification.Id,
            "DocumentViewed",
            userId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var document = await documents.ReadAsync(key, cancellationToken)
            ?? throw new NotFoundException("That document is no longer available.");

        // The certification id, never the key or the file name: a log line about a
        // criminal record check should not describe the criminal record check.
        logger.LogInformation(
            "Certification document {CaregiverCertificationId} read by staff {UserId}",
            certification.Id,
            userId);

        return document;
    }
}
