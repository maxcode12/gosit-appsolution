using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.CareRecipients.RemoveCareRecipient;
using GoSit.Domain.Caregivers;
using Microsoft.Extensions.Logging;

namespace GoSit.Application.Features.Caregivers.SubmitInsurance;

/// <summary>
/// Attaches proof of insurance to a policy the caregiver has already submitted. Mirrors
/// <see cref="Caregivers.SubmitCertification.UploadCertificationDocumentCommand"/> —
/// same two-step reasoning, same size and content-type limits, same document store.
/// </summary>
public sealed record UploadInsuranceDocumentCommand(
    Guid CaregiverInsurancePolicyId,
    Stream Content,
    string FileName,
    string ContentType,
    long Length) : ICommand<Unit>;

public sealed class UploadInsuranceDocumentHandler(
    ICaregiverProfileRepository profiles,
    IDocumentStore documents,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    ILogger<UploadInsuranceDocumentHandler> logger)
    : ICommandHandler<UploadInsuranceDocumentCommand, Unit>
{
    public const long MaximumBytes = 10 * 1024 * 1024;

    public static readonly IReadOnlySet<string> AllowedContentTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/heic",
            "image/webp",
            "application/pdf",
        };

    private static RequestValidationException Invalid(string message) =>
        new(new Dictionary<string, string[]> { ["file"] = [message] });

    public async Task<Unit> HandleAsync(UploadInsuranceDocumentCommand command, CancellationToken cancellationToken)
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
                + "A photo of the policy declaration page is usually enough.");
        }

        if (!AllowedContentTypes.Contains(command.ContentType))
        {
            throw Invalid("Upload a photo or a PDF of the policy — JPEG, PNG, HEIC, WebP or PDF.");
        }

        var profile = await profiles.FindByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Create your caregiver profile first.");

        var policy = profile.InsurancePolicies
            .SingleOrDefault(p => p.Id == command.CaregiverInsurancePolicyId)
            ?? throw new NotFoundException("That insurance policy does not exist.");

        var previousKey = policy.DocumentReference;

        var key = await documents.StoreAsync(
            command.Content, command.FileName, command.ContentType, cancellationToken);

        profile.AttachInsuranceDocument(policy.Id, key, clock.UtcNow);

        audit.Record(nameof(CaregiverInsurancePolicy), policy.Id, "InsuranceDocumentUploaded", userId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(previousKey) && previousKey != key)
        {
            await documents.DeleteAsync(previousKey, cancellationToken);
        }

        logger.LogInformation("Insurance policy {CaregiverInsurancePolicyId} document replaced", policy.Id);

        return Unit.Value;
    }
}
