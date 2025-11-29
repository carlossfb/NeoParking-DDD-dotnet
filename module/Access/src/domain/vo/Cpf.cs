using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using src.domain.exception;
using System.Text.RegularExpressions;

namespace src.domain.vo
{
    public sealed record Cpf
  {
        public string Document { get; }

        private Cpf(string document)
        {
            Document = ValidateAndNormalize(document);
        }

        public static Cpf Create(string document) => new Cpf(document);
        

        private string ValidateAndNormalize(string document)
        {
            var digits = Regex.Replace(document ?? "", "[^0-9]", "");

            if (string.IsNullOrWhiteSpace(digits))
                throw new DomainException("CPF cannot be empty");

            if (digits.Length != 11)
                throw new DomainException("CPF must contain 11 digits");

            if (digits.All(d => d == digits[0]))
                throw new DomainException("Invalid CPF");

            var numbers = digits.Select(n => int.Parse(n.ToString())).ToList();


            int firstSum = 0;
            for (int i = 0; i < 9; i++)
                firstSum += numbers[i] * (10 - i);

            Math.DivRem(firstSum, 11, out int firstRest);
            int firstDigit = firstRest < 2 ? 0 : 11 - firstRest;

            if (numbers[9] != firstDigit)
                throw new DomainException("Invalid CPF");

            int secondSum = 0;
            for (int i = 0; i < 10; i++)
                secondSum += numbers[i] * (11 - i);

            Math.DivRem(secondSum, 11, out int secondRest);
            int secondDigit = secondRest < 2 ? 0 : 11 - secondRest;

            if (numbers[10] != secondDigit)
                throw new DomainException("Invalid CPF");

            return digits;
        }
        public override string ToString()
        {
            if (Document.Length == 11)
                return $"{Document[..3]}.{Document.Substring(3, 3)}.{Document.Substring(6, 3)}-{Document[9..]}";

            return Document;
        }
    }
}