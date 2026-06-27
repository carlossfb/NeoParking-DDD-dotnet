namespace NeoParking.Access.Domain;

using PhoneNumbers;
using NeoParking.Shared.Kernel.Exceptions;

public sealed record PhoneNumber
{
    public string Value { get; }
    public string CountryCode { get; }
    public string NationalNumber { get; }

    private PhoneNumber(string value, string countryCode, string nationalNumber)
    {
        Value = value;
        CountryCode = countryCode;
        NationalNumber = nationalNumber;
    }

    public static PhoneNumber Create(string rawNumber, string defaultRegion = "BR") =>
        TryCreate(rawNumber, defaultRegion, out var phone)
            ? phone
            : throw new DomainException("Invalid phone number");

    public static PhoneNumber CreateBR(string rawNumber) => Create(rawNumber, "BR");

    private static bool TryCreate(string rawNumber, string region, out PhoneNumber phone)
    {
        phone = null!;
        var util = PhoneNumberUtil.GetInstance();

        try
        {
            var parsed = util.Parse(rawNumber, region);

            if (!util.IsValidNumber(parsed))
                return false;

            phone = new PhoneNumber(
                util.Format(parsed, PhoneNumberFormat.E164),
                $"+{parsed.CountryCode}",
                parsed.NationalNumber.ToString());

            return true;
        }
        catch
        {
            return false;
        }
    }
}