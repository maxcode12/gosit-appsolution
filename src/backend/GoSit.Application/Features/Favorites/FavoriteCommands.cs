using GoSit.Application.Abstractions;
using GoSit.Application.Common;
using GoSit.Application.Features.CareRecipients.RemoveCareRecipient;
using GoSit.Application.Features.Search;
using GoSit.Application.Features.Subscriptions;
using GoSit.Domain.Subscriptions;

namespace GoSit.Application.Features.Favorites;

/// <summary>
/// Saved caregivers. A favourite is private to the requester who created it: caregivers
/// are never told who saved them, and no count is exposed.
/// </summary>
public sealed record AddFavoriteCommand(Guid CaregiverProfileId) : ICommand<Unit>;

public sealed record RemoveFavoriteCommand(Guid CaregiverProfileId) : ICommand<Unit>;

public sealed record ListFavoritesQuery : IQuery<IReadOnlyList<CaregiverSearchResult>>;

public sealed class AddFavoriteHandler(
    ICareRequesterProfileRepository profiles,
    ICaregiverSearchRepository search,
    IFavoriteRepository favorites,
    EntitlementService entitlements,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : ICommandHandler<AddFavoriteCommand, Unit>
{
    public async Task<Unit> HandleAsync(AddFavoriteCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var profile = await profiles.FindByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Create your care-requester profile first.");

        // Only a listable caregiver can be saved. Without this check the endpoint would
        // confirm whether an arbitrary id belongs to a caregiver account.
        _ = await search.FindListableByIdAsync(command.CaregiverProfileId, cancellationToken)
            ?? throw new NotFoundException("That caregiver is not available.");

        // Saving twice is not an error — the requester's desired end state already holds.
        // Checked before the cap, so someone at the limit can still re-save what they
        // already have rather than being refused for a change that would alter nothing.
        if (await favorites.ExistsAsync(profile.Id, command.CaregiverProfileId, cancellationToken))
        {
            return Unit.Value;
        }

        await EnsureRoomToSaveAsync(userId, profile.Id, cancellationToken);

        favorites.Add(profile.Id, command.CaregiverProfileId, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }

    /// <summary>
    /// The free plan caps saved caregivers; <see cref="SubscriptionFeature.UnlimitedFavourites"/>
    /// removes the cap.
    ///
    /// The count is only fetched for accounts without the feature, so a subscriber pays
    /// no query for a limit that does not apply to them. The refusal names the limit and
    /// what lifts it — "you have reached a limit" with no number is the kind of message
    /// that sends someone to support.
    /// </summary>
    private async Task EnsureRoomToSaveAsync(
        Guid userId,
        Guid careRequesterProfileId,
        CancellationToken cancellationToken)
    {
        if (await entitlements.HasAsync(
                userId, SubscriptionFeature.UnlimitedFavourites, cancellationToken))
        {
            return;
        }

        var saved = await favorites.CountAsync(careRequesterProfileId, cancellationToken);

        if (saved >= EntitlementPolicy.FreeFavouriteLimit)
        {
            throw new ConflictException(
                $"You can save up to {EntitlementPolicy.FreeFavouriteLimit} caregivers on the "
                + "free plan. Remove one, or upgrade to save as many as you like.");
        }
    }
}

public sealed class RemoveFavoriteHandler(
    ICareRequesterProfileRepository profiles,
    IFavoriteRepository favorites,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ICommandHandler<RemoveFavoriteCommand, Unit>
{
    public async Task<Unit> HandleAsync(RemoveFavoriteCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();

        var profile = await profiles.FindByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Create your care-requester profile first.");

        // Scoped to this requester's own favourites, so one requester cannot remove
        // another's. Removing something not saved succeeds silently.
        await favorites.RemoveAsync(profile.Id, command.CaregiverProfileId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
