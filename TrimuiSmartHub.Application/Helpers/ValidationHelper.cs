using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TrimuiSmartHub.Application.Helpers
{
    public static class ValidationHelper
    {
        public static string Mask(this string value, string mask)
        {
            var maskChar = mask.Count(x => x == '#');

            if (string.IsNullOrEmpty(value) || maskChar != value.Length) return string.Empty;

            var maskedString = string.Empty;
            var lastPosition = 0;
            var specialChars = 0;

            for (var i = 0; i < mask.Length; i++)
            {
                if (mask[i] == '#') continue;

                maskedString += $"{value.Substring(lastPosition, i - lastPosition - specialChars)}{mask[i]}";
                lastPosition = i - specialChars;
                specialChars++;
            }

            maskedString += value.Substring(lastPosition, mask.Length - lastPosition - specialChars);

            return maskedString;
        }

        public static string Length(this string stringValue, int maxLength)
        {
            return stringValue.IsNullOrEmpty() || stringValue.Length <= maxLength ? stringValue : stringValue.Substring(0, maxLength);
        }

        public static string LengthEllipsis(this string stringValue, int maxLength)
        {
            return stringValue.Length <= maxLength ? stringValue : $"{stringValue.Substring(0, maxLength)}...";
        }

        public static string StringValue(this decimal decimalValue)
        {
            return decimalValue.ToString("F", new CultureInfo("en-US"));
        }

        public static float ToFloat(this string stringValue)
        {
            float.TryParse(stringValue, out var parsedValue);

            return parsedValue;
        }

        public static int ToInt(this string stringValue)
        {
            int.TryParse(stringValue, out var parsedValue);

            return parsedValue;
        }

        public static bool ToBool(this string stringValue)
        {
            bool.TryParse(stringValue, out var parsedValue);

            return parsedValue;
        }

        public static long ToLong(this string stringValue)
        {
            long.TryParse(stringValue, out var parsedValue);

            return parsedValue;
        }

        public static int? ToIntNullable(this string stringValue)
        {
            if (int.TryParse(stringValue, out var parsedValue))
            {
                return parsedValue;
            }

            return null;
        }

        public static string NormalizeDecimal<T>(this T curentValue)
        {
            return Regex.Replace(curentValue.ToString(), "[,|.](?=.*[.|,])", string.Empty);
        }

        public static List<T> NewList<T>(this T currentType)
        {
            return new List<T> { currentType };
        }

        public static string ToTitleCase(this string stringValue)
        {
            var textInfo = Thread.CurrentThread.CurrentCulture.TextInfo;

            return textInfo.ToTitleCase(stringValue.ToLower());
        }

        public static string Join(this IEnumerable<string> stringValues, string separator)
        {
            return string.Join(separator, stringValues);
        }

        public static string Join(this List<string> stringValues, string separator)
        {
            return string.Join(separator, stringValues);
        }

        public static List<string> InsertAt(this List<string> stringValues, string contentValue, int contentPosition = -1)
        {
            if (contentPosition == -1) stringValues.Add(contentValue);

            if (contentPosition >= 0) stringValues.Insert(contentPosition, contentValue);

            return stringValues;
        }

        public static string[] Split(this string stringSplit, string separator)
        {
            return stringSplit.Split(new[] { separator }, StringSplitOptions.None);
        }

        public static string ToCurrencyWithSymbol<T>(this T currencyDecimal, string currentCulture = "pt-BR")
        {
            return string.Format(new CultureInfo(currentCulture), "R$ {0:N2}", currencyDecimal);
        }

        public static string ToCurrency<T>(this T currencyDecimal, string currentCulture = "pt-BR")
        {
            return string.Format(new CultureInfo(currentCulture), "{0:N2}", currencyDecimal);
        }

        public static decimal ToCurrency(this string currencyString, string currentCulture = "pt-BR")
        {
            decimal.TryParse(currencyString, NumberStyles.Any, new CultureInfo(currentCulture), out var parsedValue);

            return parsedValue;
        }

        public static string ToFloating<T>(this T currencyDecimal, string currentCulture = "pt-BR", string floatingFormat = "F")
        {
            return string.Format(new CultureInfo(currentCulture), $"{{0:{floatingFormat}}}", currencyDecimal);
        }

        public static string Alphanumeric(this string stringToClean, string replaceWith = "")
        {
            return Regex.Replace(stringToClean, @"[^a-zA-Z0-9]", replaceWith);
        }

        public static string RemoveHidden(this string stringToClean)
        {
            return stringToClean == null ? null : new string(stringToClean.Where(c => !char.IsControl(c)).ToArray());
        }

        public static string AlphanumericAndAccents(this string stringToClean, string replaceWith = "")
        {
            return Regex.Replace(stringToClean, @"[^A-zÀ-ú0-9 ]", replaceWith);
        }

        public static string Letter(this string stringToClean, string replaceWith = "")
        {
            return Regex.Replace(stringToClean, @"[^a-zA-Z]", replaceWith);
        }

        public static string AlphanumericAndSpace(this string stringToClean, string replaceWith = "")
        {
            return stringToClean == null ? null : Regex.Replace(stringToClean, @"[^a-zA-Z0-9 ]", replaceWith);
        }

        public static string AlphanumericSpaceComma(this string stringToClean, string replaceWith = "")
        {
            return stringToClean == null ? null : Regex.Replace(stringToClean, @"[^a-zA-Z0-9 ,]", replaceWith);
        }

        public static string AlphanumericSpaceCommaLine(this string stringToClean, string replaceWith = "")
        {
            if (stringToClean == null) return null;

            stringToClean = stringToClean.Replace("\n", " ");

            return AlphanumericSpaceComma(stringToClean, replaceWith);
        }

        public static string RemoveDiacritics(this string stringWithAccents)
        {
            return stringWithAccents == null ? null : Encoding.ASCII.GetString(Encoding.GetEncoding("Cyrillic").GetBytes(stringWithAccents));
        }

        public static string RemoveAccents(this string text)
        {
            return text.IsNullOrEmpty()
                ? null
                : string.Concat(text.Normalize(NormalizationForm.FormD)
                        .Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark))
                    .Normalize(NormalizationForm.FormC);
        }

        public static string RemoveTag(this string stringWithTag)
        {
            return Regex.Replace(stringWithTag, @"<[^>]+>|&nbsp;", string.Empty);
        }

        public static string Clear(this string stringToClean)
        {
            return stringToClean == null ? null : Regex.Replace(stringToClean, @"[^\d]", string.Empty);
        }

        public static string ClearSpecial(this string stringToClean)
        {
            return stringToClean == null ? null : Regex.Replace(stringToClean, @"[^a-zA-Z0-9]", string.Empty);
        }

        public static bool IsNullOrEmpty(this string stringToVerify)
        {
            return string.IsNullOrEmpty(stringToVerify);
        }

        public static bool IsNotNullOrEmpty(this string stringToVerify)
        {
            return !string.IsNullOrEmpty(stringToVerify);
        }

        public static string Index(this string inlineText, int currentIndex, char splitChar = ',')
        {
            if (inlineText == null) return string.Empty;

            var stringArray = inlineText.Split(splitChar);

            if (!stringArray.Any()) return string.Empty;

            return currentIndex < stringArray.Length ? stringArray[currentIndex] : string.Empty;
        }

        public static string Index(this string[] stringArray, int currentIndex)
        {
            if (stringArray == null || !stringArray.Any()) return string.Empty;

            return currentIndex > -1 && currentIndex < stringArray.Length ? stringArray[currentIndex] : string.Empty;
        }

        public static int[] ArrayValues(this string stringArray)
        {
            if (stringArray.IsNullOrEmpty()) return new int[0];

            return stringArray.Split(',').Select(int.Parse).ToArray();
        }

        public static DateTime ToDateTime(this string dateString)
        {
            DateTime.TryParse(dateString?.Trim(), out var dateTime);

            return dateTime;
        }

        public static DateTime ToUtcDateTime(this string dateString)
        {
            try
            {
                var longDate = dateString.ToLong();

                return new DateTime(longDate, DateTimeKind.Utc);
            }
            catch (Exception)
            {
                return DateTime.MinValue;
            }
        }

        public static DateTime? ToDateTimeNullable(this string dateString)
        {
            if (dateString == null) return null;

            DateTime.TryParse(dateString.Trim(), out var dateTime);

            return dateTime == DateTime.MinValue ? (DateTime?)null : dateTime;
        }

        public static string ClearCurrency(this string stringToClean)
        {
            return string.IsNullOrEmpty(stringToClean) ? string.Empty : Regex.Replace(stringToClean, @"[^\d\,\.]", string.Empty);
        }

        public static T FirstContains<T>(this IEnumerable<T> paramValues, params T[] possibles)
        {
            return paramValues.FirstOrDefault(possibles.Contains);
        }

        public static bool ContainsEquals<T>(this T @this, params T[] possibles)
        {
            return possibles.Contains(@this);
        }

        public static bool ContainsAll<T>(this T[] @this, params T[] possibles)
        {
            return possibles.All(@this.Contains);
        }

        public static bool ContainsAny<T>(this T[] @this, params T[] possibles)
        {
            return possibles.Any(@this.Contains);
        }

        public static bool ContainsAny(this string stringToCheck, params string[] stringArray)
        {
            return stringArray.Any(stringToCheck.Contains);
        }

        public static bool IndexOfDefault(this string stringToCheck, string stringToCompare)
        {
            if (stringToCheck == null) return false;

            return stringToCheck.IndexOf(stringToCompare, StringComparison.InvariantCultureIgnoreCase) > -1;
        }

        public static bool StartsWith(this string stringToCheck, params string[] stringArray)
        {
            return stringArray.Any(stringToCheck.StartsWith);
        }

        public static void RemoveAll<T>(this List<T> originalList, List<T> toRemoveList)
        {
            toRemoveList.ForEach(toRemove => originalList.Remove(toRemove));
        }

        public static int Age(this DateTime birthDate)
        {
            var currentDate = DateTime.Today;

            var insuredAge = currentDate.Year - birthDate.Year;

            if (birthDate > currentDate.AddYears(-insuredAge)) insuredAge--;

            return insuredAge;
        }

        public static DateTime FirstDay(this DateTime currentDateTime)
        {
            return new DateTime(currentDateTime.Year, currentDateTime.Month, 1);
        }

        public static bool ValidateCpf(this string cpfCnpj)
        {
            cpfCnpj = cpfCnpj.Clear();

            if (cpfCnpj.IsNullOrEmpty() || cpfCnpj.Length != 11 || new string(cpfCnpj[0], cpfCnpj.Length) == cpfCnpj) return false;

            var d = new int[14];
            var v = new int[2];
            int i, sum, j;

            for (i = 0; i <= 10; i++) d[i] = Convert.ToInt32(cpfCnpj.Substring(i, 1));

            for (i = 0; i <= 1; i++)
            {
                sum = 0;
                for (j = 0; j <= 8 + i; j++) sum += d[j] * (10 + i - j);

                v[i] = sum * 10 % 11;
                if (v[i] == 10) v[i] = 0;
            }

            return v[0] == d[9] & v[1] == d[10];
        }
        public static bool ValidateDocument(this string cpfCnpj)
        {
            var cleanCpfCnpj = cpfCnpj.Clear();

            if (cleanCpfCnpj.IsNullOrEmpty()) return false;

            var d = new int[14];
            var v = new int[2];
            int j, i, sum;

            if (new string(cleanCpfCnpj[0], cleanCpfCnpj.Length) == cleanCpfCnpj) return false;

            if (cleanCpfCnpj.Length == 11) return ValidateCpf(cleanCpfCnpj);

            if (cleanCpfCnpj.Length != 14) return false;

            const string sequence = "6543298765432";

            for (i = 0; i <= 13; i++) d[i] = Convert.ToInt32(cleanCpfCnpj.Substring(i, 1));
            {
                for (i = 0; i <= 1; i++)
                {
                    sum = 0;
                    for (j = 0; j <= 11 + i; j++)
                        sum += d[j] * Convert.ToInt32(sequence.Substring(j + 1 - i, 1));

                    v[i] = (sum * 10) % 11;
                    if (v[i] == 10) v[i] = 0;
                }
            }

            return (v[0] == d[12] & v[1] == d[13]);
        }
        public static bool ValidatePhone(this string number)
        {
            return number != null && Regex.Match(number, @"^([(][0-9]{2}[)][ ])?[0-9]{4,5}[- ][0-9]{3,4}[- ]?([0-9]{4})?$").Success;
        }

        public static bool ValidateChassi(this string chassiNumber)
        {
            chassiNumber = chassiNumber?.ToUpper();

            return string.IsNullOrEmpty(chassiNumber) || chassiNumber.Length == 17 && !Regex.IsMatch(chassiNumber, "^0| |^.{4,}([0-9A-Z])\\1{6,}|[iIoOqQ]") && Regex.IsMatch(chassiNumber, "[0-9]{4}$");
        }

        public static bool ValidatePlate(this string plateNumber)
        {
            return string.IsNullOrEmpty(plateNumber?.Alphanumeric()) || Regex.IsMatch(plateNumber, "^[a-zA-Z]{3}-?[0-9]{1}[0-9a-zA-z]{2}[0-9]{1}$");
        }

        public static string GetOperatingSystemName()
        {
            try
            {
                return (from x in new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem").Get().OfType<ManagementObject>()
                        select x.GetPropertyValue("Caption")).FirstOrDefault()?.ToString() ?? "Unknown";
            }
            catch (Exception)
            {
                return "Unknown";
            }
        }

        public static string IsPropertyExist(dynamic settings, string name)
        {
            JToken jToken = settings;

            return jToken[name]?.ToString();
        }

        public static IEnumerable<T> DistinctBy<T, TKey>(this IEnumerable<T> items, Func<T, TKey> property)
        {
            return items.GroupBy(property).Select(x => x.First());
        }

        public static string ToStringDate(this DateTime? dateTime, string dateFormat = "dd/MM/yyyy")
        {
            return dateTime?.ToString(dateFormat) ?? string.Empty;
        }

        public static string ToStringDate(this DateTime dateTime)
        {
            return dateTime.ToString("dd/MM/yyyy");
        }

        public static string TrimNonAscii(this string valueToTrim)
        {
            return Regex.Replace(valueToTrim, @"[^\t\r\n -~]", string.Empty).Trim();
        }

        public static DateTime NormalizeTime(this DateTime dateTime)
        {
            var timeZoneOffset = TimeZoneInfo.Local.BaseUtcOffset.Negate();

            return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc).Add(timeZoneOffset);
        }

        public static IEnumerable<T> RangedEnumeration<T>(int min, int max, int step)
        {
            return Enumerable.Range(min, max - min + 1).Where(i => (i - min) % step == 0).Select(x => (T)Convert.ChangeType(x, typeof(T)));
        }

        public static string[] RegexSplit(this string text, string pattern = null)
        {
            pattern = pattern ?? @"\D+";

            return Regex.Split(text.Trim(), pattern);
        }

        public static bool ValidateEmail(this string email)
        {
            email = email?.TrimNonAscii();

            return Regex.IsMatch(email, @"^((?!\.)[\w\-_.]*[^.])(@\w+)(\.\w+(\.\w+)?[^.\W])$");
        }
    }
}
