namespace GoSit.Domain.Common;

/// <summary>
/// Normalizes North American phone numbers to E.164 (+1XXXXXXXXXX) so the same
/// number entered in different formats is stored identically.
/// </summary>
public static class PhoneNumberRules
{
    public static string Normalize(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            throw new DomainException("A phone number is required.");
        }

        var digits = new string(phoneNumber.Where(char.IsAsciiDigit).ToArray());

        // Accept a leading country code, then require the 10 national digits.
        if (digits.Length == 11 && digits[0] == '1')
        {
            digits = digits[1..];
        }

        if (digits.Length != 10)
        {
            throw new DomainException("Enter a 10-digit Canadian phone number.");
        }

        // NANP: area code and exchange code both start with 2-9.
        if (digits[0] < '2' || digits[3] < '2')
        {
            throw new DomainException("That phone number is not valid.");
        }

        return $"+1{digits}";
    }
}
