using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.CareRecipients.RemoveCareRecipient;
using GoSit.Domain.Caregivers;
using Microsoft.Extensions.Logging;

namespace GoSit.Application.Features.Caregivers.SubmitCertification;

/// <summary>
/// Attaches the evidence for a certification the caregiver has already submitted.
///
/// Two steps rather than one multipart request that carries both the dates and the file:
/// an upload that fails halfway leaves the certification intact and retryable, and the
/// far more common case — replacing a photo that came out unreadable — does not mean
/// re-entering the certificate's details.
/// </summary>
public sealed record UploadCertificationDocumentCommand(
    Guid CaregiverCertificationId,
    Stream Content,
    string FileName,
    string ContentType,
    long Length) : ICommand<Unit>;

public sealed class UploadCertificationDocumentHandler(
    ICaregiverProfileRepository profiles,
    IDocumentStore documents,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    ILogger<UploadCertificationDocumentHandler> logger)
    : ICommandHandler<UploadCertificationDocumentCommand, Unit>
{
    /// <summary>
    /// Ten megabytes. A phone photograph of a certificate is one to four; a scan is less.
    /// Enough that nobody has to compress anything, small enough that the endpoint is not
    /// a way to fill a storage account.
    /// </summary>
    public const long MaximumBytes = 10 * 1024 * 1024;

    /// <summary>
    /// What a certificate can be.
    ///
    /// An allow-list, and a short one. The file is opened by staff, so anything a browser
    /// might execute — SVG, HTML — is excluded regardless of what it is called. Blob
    /// Storage also serves these with `Content-Disposition: attachment`, so this is the
    /// first of two independent reasons a script never runs in an admin's session.
    /// </summary>
    public static readonly IReadOnlySet<string> AllowedContentTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/heic",
            "image/webp",
            "application/pdf",
        };

    /// <summary>Field-scoped so the message lands on the file input rather than the form.</summary>
    private static RequestValidationException Invalid(string message) =>
        new(new Dictionary<string, string[]> { ["file"] = [message] });

    public async Task<Unit> HandleAsync(
        UploadCertificationDocumentCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        if (command.Length <= 0)
        {
            throw Invalid("Choose a file to upload.");
        }

        if (command.Length > MaximumBytes)
        {
            throw Invalid(
                $"That file is larger than {MaximumBytes / (1024 * 1024)} MB. "
                + "A photo of the certificate is usually enough.");
        }

        if (!AllowedContentTypes.Contains(command.ContentType))
        {
            throw Invalid(
                "Upload a photo or a PDF of the certificate — JPEG, PNG, HEIC, WebP or PDF.");
        }

        var profile = await profiles.FindByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Create your caregiver profile first.");

        // Resolved through the caregiver's own profile, so a certification belonging to
        // someone else is simply not found — the id cannot be used to attach a document
        // to another caregiver's record, or to discover that the id exists.
        var certification = profile.Certifications
            .SingleOrDefault(c => c.Id == command.CaregiverCertificationId)
            ?? throw new NotFoundException("That certification does not exist.");

        var previousKey = certification.DocumentReference;

        var key = await documents.StoreAsync(
            command.Content, command.FileName, command.ContentType, cancellationToken);

        // Through the profile, which also sends the certification back for review — a
        // document swapped after approval must not keep the approval.
        profile.AttachCertificationDocument(certification.Id, key, clock.UtcNow);

        audit.Record(
            nameof(CaregiverCertification),
            certification.Id,
            "CertificationDocumentUploaded",
            userId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        /*
         * The old document is deleted only after the new key is safely committed.
         *
         * The other order is faster and loses evidence: a delete that succeeds followed
         * by a save that fails leaves a certification pointing at a blob that no longer
         * exists, which staff would meet as an approval request with no document and no
         * way to tell whether one was ever provided.
         */
        if (!string.IsNullOrWhiteSpace(previousKey) && previousKey != key)
        {
            await documents.DeleteAsync(previousKey, cancellationToken);
        }

        logger.LogInformation(
            "Certification {CaregiverCertificationId} document replaced", certification.Id);

        return Unit.Value;
    }
}
