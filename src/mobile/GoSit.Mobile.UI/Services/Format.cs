using System.Globalization;

namespace GoSit.Mobile.UI.Services;

/// <summary>
/// Dates and money, in the reader's own timezone.
///
/// The API works in UTC throughout, which is right for storage and wrong for a screen: a
/// caregiver told their shift starts at 20:00 when their phone says 16:00 will either
/// miss it or lose faith in the app. Everything here converts before it formats.
/// </summary>
public static class Format
{
    private static readonly CultureInfo Canada = CultureInfo.GetCultureInfo("en-CA");

    /// <summary>"Wed 3 Sep, 4:00–7:00 p.m." — or with the date dropped when it is today.</summary>
    public static string Window(MobileBooking booking)
    {
        var start = booking.StartAtUtc.ToLocalTime();
        var end = booking.EndAtUtc.ToLocalTime();

        var day = start.Date == DateTime.Now.Date
            ? "Today"
            : start.Date == DateTime.Now.Date.AddDays(1)
                ? "Tomorrow"
                : start.ToString("ddd d MMM", Canada);

        // A stay, or care past midnight, needs the end day too.
        if (end.Date != start.Date)
        {
            return $"{day}, {Time(start)} – {end.ToString("ddd d MMM", Canada)}, {Time(end)}";
        }

        return $"{day}, {Time(start)}–{Time(end)}";
    }

    public static string Time(DateTime local) =>
        local.ToString("h:mm tt", Canada).ToLowerInvariant();

    public static string LocalTimeOf(DateTime utc) => Time(utc.ToLocalTime());

    public static string Money(decimal amount) =>
        amount.ToString("C", Canada);

    /// <summary>
    /// What a status is called on screen. The API's own names are for the state machine,
    /// and "InProgress" is not a phrase anyone says.
    /// </summary>
    public static string Status(string status) => status switch
    {
        "Pending" => "Awaiting your reply",
        "Accepted" => "Confirmed",
        "InProgress" => "In progress",
        "Completed" => "Finished",
        "Declined" => "Declined",
        "Cancelled" => "Cancelled",
        "Disputed" => "Disputed",
        _ => status,
    };

    /// <summary>
    /// The short form, for a pill beside a title.
    ///
    /// Separate from <see cref="Status"/> because a pill has one line and about twelve
    /// characters before it wraps — "Awaiting your reply" wrapped to two lines and pushed
    /// the card out of shape. The long form still reads on the detail screen, where there
    /// is room for it.
    /// </summary>
    public static string StatusShort(string status) => status switch
    {
        "Pending" => "Reply",
        "Accepted" => "Confirmed",
        "InProgress" => "Working",
        "Completed" => "Finished",
        "Cancelled" => "Cancelled",
        "Declined" => "Declined",
        "Disputed" => "Disputed",
        _ => status,
    };

    /// <summary>
    /// A payout status in a caregiver's words.
    ///
    /// "Queued" is a word from our database, not from anyone's vocabulary — it appeared
    /// verbatim on the earnings statement, where the only question being asked is "have I
    /// been paid".
    /// </summary>
    public static string PayoutStatus(string status) => status switch
    {
        "Queued" => "Not sent yet",
        "InTransit" => "On its way",
        "Paid" => "Paid",
        "Failed" => "Failed",
        _ => status,
    };

    /// <summary>
    /// Why money that has been earned has not arrived, or null once it has.
    ///
    /// A caregiver waits out the family's dispute window before a payout moves. That is a
    /// deliberate policy — before it, a dispute arrived after the money had gone — but a
    /// balance sitting still with no explanation reads as a bug, and the person it reads
    /// that way to is the one who has already done the work.
    /// </summary>
    public static string? Availability(
        string status,
        DateTime availableAtUtc,
        bool isHeldForDispute)
    {
        if (status is "Paid" or "InTransit")
        {
            return null;
        }

        if (isHeldForDispute)
        {
            return "On hold while the family's dispute is looked at.";
        }

        var available = availableAtUtc.ToLocalTime();

        return available <= DateTime.Now
            ? "Ready to be sent."
            : $"Available {available.ToString("d MMM", Canada)}.";
    }

    public static string PayoutPill(string status) => status switch
    {
        "Paid" => "pill pill--done",
        "InTransit" => "pill pill--live",
        "Failed" => "pill pill--wait",
        _ => "pill pill--off",
    };

    public static string StatusPill(string status) => status switch
    {
        "InProgress" => "pill pill--live",
        "Pending" => "pill pill--wait",
        "Completed" => "pill pill--done",
        _ => "pill pill--off",
    };

    /// <summary>
    /// Whether the location recorded at check-in said anything.
    ///
    /// Null for "not provided" and "not comparable" — neither is news. One means the
    /// phone could not fix a location, the other that the address was never geocoded, and
    /// presenting either as "unconfirmed" invites a caregiver to think they have done
    /// something wrong.
    /// </summary>
    public static string? Arrival(string verification) => verification switch
    {
        "AtTheAddress" => "Location confirmed at the address.",
        "AwayFromTheAddress" => "The location recorded was not at the address.",
        _ => null,
    };

    public static string Address(MobileAddress address) =>
        string.Join(", ", new[]
        {
            address.Line1,
            address.Line2,
            address.City,
            $"{address.Province} {address.PostalCode}".Trim(),
        }.Where(part => !string.IsNullOrWhiteSpace(part)));

    /// <summary>"large dog, Labrador" — what a walker wants to know at a glance.</summary>
    public static string Pet(MobilePet pet)
    {
        var size = pet.Size switch
        {
            "Small" => "small",
            "Medium" => "medium",
            "Large" => "large",
            "ExtraLarge" => "extra large",
            _ => null,
        };

        var species = pet.Species switch
        {
            "Dog" => "dog",
            "Cat" => "cat",
            "SmallAnimal" => "small animal",
            "Bird" => "bird",
            _ => "pet",
        };

        var kind = size is null ? species : $"{size} {species}";

        return string.IsNullOrWhiteSpace(pet.Breed) ? kind : $"{kind}, {pet.Breed}";
    }
}
