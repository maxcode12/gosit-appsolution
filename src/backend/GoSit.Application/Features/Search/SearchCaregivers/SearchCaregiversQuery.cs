using FluentValidation;
using GoSit.Application.Abstractions;

namespace GoSit.Application.Features.Search.SearchCaregivers;

/// <summary>
/// Caregiver search. Every filter is optional; with none supplied it returns listable
/// caregivers ranked by the MVP order in PLAN.md.
/// </summary>
public sealed record SearchCaregiversQuery(
    string? ServiceCode,
    string? City,
    string? PostalCode,
    int? RadiusKm,
    DayOfWeek? Day,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    decimal? MaxHourlyRate,
    int? MinYearsOfExperience,
    decimal? MinRating,
    IReadOnlyList<string>? RequiredCertificationCodes,
    int Page = 1,
    int PageSize = 20) : IQuery<CaregiverSearchResponse>;

public sealed class SearchCaregiversValidator : AbstractValidator<SearchCaregiversQuery>
{
    public const int MaxPageSize = 50;

    public SearchCaregiversValidator()
    {
        RuleFor(x => x.ServiceCode).MaximumLength(50);
        RuleFor(x => x.City).MaximumLength(100);
        RuleFor(x => x.PostalCode).MaximumLength(7);
        RuleFor(x => x.RadiusKm).InclusiveBetween(1, 100).When(x => x.RadiusKm.HasValue);
        RuleFor(x => x.MaxHourlyRate).GreaterThan(0).When(x => x.MaxHourlyRate.HasValue);
        RuleFor(x => x.MinYearsOfExperience).InclusiveBetween(0, 70).When(x => x.MinYearsOfExperience.HasValue);
        RuleFor(x => x.MinRating).InclusiveBetween(1m, 5m).When(x => x.MinRating.HasValue);

        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, MaxPageSize);

        RuleForEach(x => x.RequiredCertificationCodes).NotEmpty().MaximumLength(50);

        // A time window is only meaningful as a pair.
        RuleFor(x => x.EndTime)
            .NotNull()
            .When(x => x.StartTime.HasValue)
            .WithMessage("Supply both a start time and an end time.");

        RuleFor(x => x.StartTime)
            .NotNull()
            .When(x => x.EndTime.HasValue)
            .WithMessage("Supply both a start time and an end time.");

        // An end before the start searches across midnight — 20:00 to 07:00 finds someone
        // for an overnight. Refusing it would hide the caregivers a family looking for
        // overnight care most needs to find.
        RuleFor(x => x.EndTime)
            .NotEqual(x => x.StartTime!.Value)
            .When(x => x.StartTime.HasValue && x.EndTime.HasValue)
            .WithMessage("The start and end times must differ.");

        RuleFor(x => x.Day)
            .NotNull()
            .When(x => x.StartTime.HasValue)
            .WithMessage("Supply a day when filtering by time.");
    }
}
