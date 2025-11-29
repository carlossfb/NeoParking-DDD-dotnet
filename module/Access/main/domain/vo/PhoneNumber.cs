using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PhoneNumbers;
using main.domain.exception;

namespace main.domain.vo
{
    public class PhoneNumber : IEquatable<PhoneNumber>
    {

        public string Value { get; private set; }
        public string CountryCode { get; private set; }
        public string NationalNumber { get; private set; }



        private PhoneNumber(string value, string countryCode, string nationalNumber)
        {
            Value = value;
            CountryCode = countryCode;
            NationalNumber = nationalNumber;
        }


        public static PhoneNumber Create(string rawNumber, string defaultRegion ="BR")
        {
            return TryCreate(rawNumber, defaultRegion, out PhoneNumber phone) ? phone : throw new DomainException("Invalid phone number");

        }

        private static bool TryCreate (string rawNumber, string region, out PhoneNumber phone)
        {
            phone = null;
            var util = PhoneNumberUtil.GetInstance();

            try
            {
                var parsed = util.Parse(rawNumber, region);

                if (!util.IsValidNumber(parsed))
                    return false;
            

                var formatted = util.Format(parsed, PhoneNumberFormat.E164);

                phone = new PhoneNumber(
                    formatted,
                    $"+{parsed.CountryCode}",
                    parsed.NationalNumber.ToString()
                );

                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool Equals(PhoneNumber? other)
        {
            if (other is null) return false;
            return Value == other.Value;
        }

        public override string ToString() => Value;
        public override bool Equals(object? obj) => Equals(obj as PhoneNumber);
        public override int GetHashCode() => Value.GetHashCode();


    }
}