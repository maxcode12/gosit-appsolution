using System.Security.Cryptography;
using FluentValidation;
using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.Authentication.AccountRecovery;
using GoSit.Application.Features.CareRecipients.RemoveCareRecipient;
using GoSit.Domain.Users;
using Microsoft.Extensions.Logging;

namespace GoSit.Application.Features.Administration.Staff;

/// <summary>
/// Creating staff accounts, by a super administrator.
///
/// A staff account is created with a password nobody knows, then the person is emailed a link
/// to choose their own. The password is never chosen, shown or sent by someone else, so no
/// administrator ever holds a colleague's credentials. Every invitation is written to the
/// audit trail with who made it.
/// </summary>
public sealed record StaffMember(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool IsSuperAdmin,
    string Status,
    DateTime CreatedAtUtc);

public sealed record ListStaffQuery : IQuery<IReadOnlyList<StaffMember>>;

public sealed record InviteStaffCommand(string Email, string FirstName, string LastName)
    : ICommand<StaffMember>;

public sealed class InviteStaffValidator : AbstractValidator<InviteStaffCommand>
{
    public InviteStaffValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(320).EmailAddress();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
    }
}

public sealed class ListStaffHandler(IUserRepository users)
    : IQueryHandler<ListStaffQuery, IReadOnlyList<StaffMember>>
{
    public async Task<IReadOnlyList<StaffMember>> HandleAsync(
        ListStaffQuery query,
        CancellationToken cancellationToken)
    {
        var admins = await users.ListAdministratorsAsync(cancellationToken);

        return admins.Select(StaffMapping.ToStaffMember).ToList();
    }
}

public sealed class InviteStaffHandler(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IAccountRecoveryNotifier notifier,
    ISecureTokenGenerator tokens,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    ILogger<InviteStaffHandler> logger) : ICommandHandler<InviteStaffCommand, StaffMember>
{
    public async Task<StaffMember> HandleAsync(
        InviteStaffCommand command,
        CancellationToken cancellationToken)
    {
        var inviterId = currentUser.UserId
            ?? throw new ForbiddenException("You must be signed in.");

        var inviter = await users.FindByIdAsync(inviterId, cancellationToken);

        if (inviter is null || inviter.Role != UserRole.Admin || !inviter.IsSuperAdmin || !inviter.CanAuthenticate)
        {
            // The policy already refused anyone else; this is the same rule at the door
            // that matters, so a mistake in routing cannot let someone through.
            throw new ForbiddenException("Only a super administrator can add staff.");
        }

        var normalized = User.Normalize(command.Email);

        if (await users.EmailExistsAsync(normalized, cancellationToken))
        {
            throw new ConflictException(
                "An account with that email address already exists. Staff accounts use their own address.");
        }

        var utcNow = clock.UtcNow;

        // A password nobody has seen: random, long, and hashed like any other. The person
        // replaces it through the emailed link before they can sign in.
        var unknownPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

        var staff = User.ProvisionStaff(
            command.Email,
            passwordHasher.Hash(unknownPassword),
            command.FirstName,
            command.LastName,
            utcNow);

        var (token, hash) = tokens.Generate();
        staff.IssueToken(UserTokenPurpose.PasswordReset, hash, utcNow, RecoveryTokenLifetimes.StaffInvitation);

        users.Add(staff);
        audit.Record(nameof(User), staff.Id, "StaffInvited", inviterId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notifier.SendStaffInvitationAsync(
            staff.Email,
            staff.FirstName,
            $"{inviter.FirstName} {inviter.LastName}".Trim(),
            token,
            cancellationToken);

        // Identifiers only, never the address or name.
        logger.LogInformation("Staff account {UserId} invited by {InviterId}", staff.Id, inviterId);

        return staff.ToStaffMember();
    }
}

internal static class StaffMapping
{
    public static StaffMember ToStaffMember(this User user) =>
        new(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.IsSuperAdmin,
            user.Status.ToString(),
            user.CreatedAtUtc);
}
