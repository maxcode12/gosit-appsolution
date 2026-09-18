namespace GoSit.Domain.Reviews;

/// <summary>
/// Review rules that are policy rather than invariants — the numbers a product owner
/// would expect to tune.
/// </summary>
public static class ReviewPolicy
{
    /// <summary>How long after a booking completes either party may review.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromDays(14);

    public const int MinimumRating = 1;
    public const int MaximumRating = 5;
    public const int MaxCommentLength = 2000;
}
