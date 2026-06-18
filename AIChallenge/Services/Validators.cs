using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AIChallenge.Models;

namespace AIChallenge.Services;

public static partial class Validators
{
    private static readonly Dictionary<string, AddressCatalogEntry> AddressCatalog = LoadAddressCatalog();

    public static bool IsValidCurp(string curp)
    {
        return CurpRegex().IsMatch(Normalize(curp));
    }

    public static bool IsAdult(DateOnly birthDate, DateOnly today)
    {
        int age = today.Year - birthDate.Year;
        if (birthDate > today.AddYears(-age))
        {
            age--;
        }

        return age >= 18;
    }

    public static bool IsKnownAddress(Address address)
    {
        string key = $"{address.PostalCode}|{address.Neighborhood}|{address.Municipality}|{address.State}";
        return AddressCatalog.ContainsKey(key);
    }

    public static bool IsKnownAddress(Address address, IReadOnlyList<Address> addressCatalog)
    {
        return addressCatalog.Any(candidate =>
            string.Equals(candidate.PostalCode, address.PostalCode, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(candidate.Neighborhood, address.Neighborhood, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(candidate.Municipality, address.Municipality, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(candidate.State, address.State, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsValidCardNumber(string cardNumber)
    {
        string digits = DigitsOnly(cardNumber);
        if (digits.Length is < 13 or > 19)
        {
            return false;
        }

        int sum = 0;
        bool alternate = false;
        for (int i = digits.Length - 1; i >= 0; i--)
        {
            int value = digits[i] - '0';
            if (alternate)
            {
                value *= 2;
                if (value > 9)
                {
                    value -= 9;
                }
            }

            sum += value;
            alternate = !alternate;
        }

        return sum % 10 == 0;
    }

    public static bool MatchesCardType(string cardNumber, CardType cardType)
    {
        string digits = DigitsOnly(cardNumber);
        return cardType switch
        {
            CardType.Visa => digits.StartsWith('4') && digits.Length is 13 or 16 or 19,
            CardType.Mastercard => digits.Length == 16 && IsMastercard(digits),
            CardType.Amex => digits.Length == 15 && (digits.StartsWith("34", StringComparison.Ordinal) || digits.StartsWith("37", StringComparison.Ordinal)),
            _ => false
        };
    }

    public static bool IsValidExpiration(string expiration)
    {
        if (!DateTime.TryParseExact(expiration, "MM/yy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
        {
            return false;
        }

        DateOnly lastDay = new(parsed.Year, parsed.Month, DateTime.DaysInMonth(parsed.Year, parsed.Month));
        return lastDay >= DateOnly.FromDateTime(DateTime.UtcNow);
    }

    public static bool IsValidCvv(string cvv, CardType cardType)
    {
        string pattern = cardType == CardType.Amex ? "^[0-9]{4}$" : "^[0-9]{3}$";
        return Regex.IsMatch(cvv, pattern, RegexOptions.CultureInvariant);
    }

    public static string MaskCardNumber(string cardNumber)
    {
        string digits = DigitsOnly(cardNumber);
        return $"**** **** **** {digits[^4..]}";
    }

    public static string FingerprintCardNumber(string cardNumber)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(DigitsOnly(cardNumber)));
        return Convert.ToHexString(bytes);
    }

    public static string Normalize(string value)
    {
        return value.Trim().ToUpperInvariant();
    }

    private static bool IsMastercard(string digits)
    {
        int firstTwo = int.Parse(digits[..2], CultureInfo.InvariantCulture);
        int firstFour = int.Parse(digits[..4], CultureInfo.InvariantCulture);
        return firstTwo is >= 51 and <= 55 || firstFour is >= 2221 and <= 2720;
    }

    private static string DigitsOnly(string value)
    {
        return string.Concat(value.Where(char.IsDigit));
    }

    [GeneratedRegex("^[A-Z][AEIOU][A-Z]{2}[0-9]{2}(0[1-9]|1[0-2])(0[1-9]|[12][0-9]|3[01])[HM](AS|BC|BS|CC|CL|CM|CS|CH|DF|DG|GT|GR|HG|JC|MC|MN|MS|NT|NL|OC|PL|QT|QR|SP|SL|SR|TC|TS|TL|VZ|YN|ZS|NE)[B-DF-HJ-NP-TV-Z]{3}[0-9A-Z][0-9]$")]
    private static partial Regex CurpRegex();

    private sealed record AddressCatalogEntry(string PostalCode, string Neighborhood, string Municipality, string State);

    private static Dictionary<string, AddressCatalogEntry> LoadAddressCatalog()
    {
        Dictionary<string, AddressCatalogEntry> catalog = new(StringComparer.OrdinalIgnoreCase);
        string dataFile = Path.Combine(AppContext.BaseDirectory, "Data", "address-catalog.txt");

        if (!File.Exists(dataFile))
        {
            catalog["06100|Hipódromo|Cuauhtémoc|Ciudad de México"] = new("06100", "Hipódromo", "Cuauhtémoc", "Ciudad de México");
            catalog["06700|Roma Norte|Cuauhtémoc|Ciudad de México"] = new("06700", "Roma Norte", "Cuauhtémoc", "Ciudad de México");
            catalog["03100|Del Valle Centro|Benito Juárez|Ciudad de México"] = new("03100", "Del Valle Centro", "Benito Juárez", "Ciudad de México");
            catalog["11000|Lomas de Chapultepec|Miguel Hidalgo|Ciudad de México"] = new("11000", "Lomas de Chapultepec", "Miguel Hidalgo", "Ciudad de México");
            return catalog;
        }

        foreach (string line in File.ReadAllLines(dataFile))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            string[] parts = line.Split('|');
            if (parts.Length != 4)
            {
                continue;
            }

            string postalCode = parts[0].Trim();
            string neighborhood = parts[1].Trim();
            string municipality = parts[2].Trim();
            string state = parts[3].Trim();
            string key = $"{postalCode}|{neighborhood}|{municipality}|{state}";

            catalog[key] = new(postalCode, neighborhood, municipality, state);
        }

        return catalog;
    }
}
