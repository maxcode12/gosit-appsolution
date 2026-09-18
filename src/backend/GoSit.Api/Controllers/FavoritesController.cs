using GoSit.Api.Authentication;
using GoSit.Application.Abstractions;
using GoSit.Application.Features.CareRecipients.RemoveCareRecipient;
using GoSit.Application.Features.Favorites;
using GoSit.Application.Features.Search;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoSit.Api.Controllers;

/// <summary>
/// Saved caregivers for the authenticated care requester.
///
/// Scoped to the caller's own profile, so one requester can neither read nor change
/// another's saved list. Caregivers are never told who has saved them.
/// </summary>
[ApiController]
[Route("api/v1/care-requesters/me/favorites")]
[Authorize(Policy = AuthorizationPolicies.CareRequesterOnly)]
public sealed class FavoritesController(
    ICommandHandler<AddFavoriteCommand, Unit> add,
    ICommandHandler<RemoveFavoriteCommand, Unit> remove,
    IQueryHandler<ListFavoritesQuery, IReadOnlyList<CaregiverSearchResult>> list) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CaregiverSearchResult>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CaregiverSearchResult>>> List(
        CancellationToken cancellationToken) =>
        Ok(await list.HandleAsync(new ListFavoritesQuery(), cancellationToken));

    /// <summary>Saving an already-saved caregiver succeeds without changing anything.</summary>
    [HttpPut("{caregiverProfileId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Add(Guid caregiverProfileId, CancellationToken cancellationToken)
    {
        await add.HandleAsync(new AddFavoriteCommand(caregiverProfileId), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{caregiverProfileId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(Guid caregiverProfileId, CancellationToken cancellationToken)
    {
        await remove.HandleAsync(new RemoveFavoriteCommand(caregiverProfileId), cancellationToken);
        return NoContent();
    }
}
