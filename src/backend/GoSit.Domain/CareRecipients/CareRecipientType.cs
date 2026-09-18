namespace GoSit.Domain.CareRecipients;

/// <summary>
/// Who the care is for. Special needs are NOT a recipient type — they are described
/// in <see cref="CareNeedsProfile"/>, because a recipient of any age may have them.
/// </summary>
public enum CareRecipientType
{
    Child = 1,
    Adult = 2,
    Senior = 3,
}
