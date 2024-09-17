using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TrimuiSmartHub.Application.Helpers
{
    public static class KeyValueHelper
    {
        public static List<KeyValuePair<T, T2>> AddValue<T, T2>(this List<KeyValuePair<T, T2>> keyValuePairs, T name, T2 value, bool condition = true)
        {
            if (name == null || name.ToString() == string.Empty || !condition) return keyValuePairs;

            if (keyValuePairs == null)
            {
                keyValuePairs = AddValue(name, value);

                return keyValuePairs;
            }

            keyValuePairs.Add(new KeyValuePair<T, T2>(name, value));

            return keyValuePairs;
        }

        public static List<KeyValuePair<T, T2>> AddValueConditional<T, T2>(this List<KeyValuePair<T, T2>> keyValuePairs, T name, T2 value, Func<KeyValuePair<T, T2>, bool> keySelector)
        {
            var newPair = new KeyValuePair<T, T2>(name, value);

            var pairResult = keySelector(newPair);

            return !pairResult ? keyValuePairs : keyValuePairs.AddValue(name, value);
        }

        public static List<KeyValuePair<T, T2>> AddValue<T, T2>(T name, T2 value, bool condition = true)
        {
            if (name == null || name.ToString() == string.Empty || !condition) return new List<KeyValuePair<T, T2>>();

            var keyValuePairs = new List<KeyValuePair<T, T2>>
            {
                new KeyValuePair<T, T2>(name, value)
            };

            return keyValuePairs;
        }

        public static List<KeyValuePair<string, string>> AddValueOrNothing(this List<KeyValuePair<string, string>> keyValuePairs, string name, string value)
        {
            return string.IsNullOrEmpty(value) || string.IsNullOrEmpty(name) ? keyValuePairs : AddValue(keyValuePairs, name, value);
        }

        public static List<KeyValuePair<string, string>> AddValue()
        {
            return new List<KeyValuePair<string, string>>();
        }

        public static List<KeyValuePair<T, T2>> Create<T, T2>()
        {
            return new List<KeyValuePair<T, T2>>();
        }

        public static List<KeyValuePair<T1, T2>> CopyValues<T1, T2>(this List<KeyValuePair<T1, T2>> keyValuePairs)
        {
            var newKeyValuePairs = new List<KeyValuePair<T1, T2>>();

            newKeyValuePairs.AddRange(keyValuePairs);

            return newKeyValuePairs;
        }

        public static List<KeyValuePair<string, string>> SetDateValueNotExists(this List<KeyValuePair<string, string>> keyValuePairs, string name, DateTime? dateTime, string dateFormat = null)
        {
            dateFormat = dateFormat ?? "dd/MM/yyyy";

            var dateString = dateTime?.ToString(dateFormat) ?? string.Empty;

            return keyValuePairs.SetValueNotExists(name, dateString);
        }

        public static List<KeyValuePair<string, string>> SetDateValue(this List<KeyValuePair<string, string>> keyValuePairs, string name, DateTime? dateTime, string dateFormat = null, bool condition = true)
        {
            if (!condition) return keyValuePairs;

            dateFormat = dateFormat ?? "dd/MM/yyyy";

            var dateString = dateTime?.ToString(dateFormat) ?? string.Empty;

            return keyValuePairs.SetValue(name, dateString);
        }

        public static List<KeyValuePair<string, string>> SetDateValueExact(this List<KeyValuePair<string, string>> keyValuePairs, string name, DateTime? dateTime, string dateFormat = null)
        {
            dateFormat = dateFormat ?? "dd/MM/yyyy";

            var dateString = dateTime?.ToString(dateFormat) ?? string.Empty;

            return keyValuePairs.SetValueExact(name, dateString);
        }

        public static List<KeyValuePair<string, string>> SetAllDateValue(this List<KeyValuePair<string, string>> keyValuePairs, string name, DateTime? dateTime, string dateFormat = null)
        {
            dateFormat = dateFormat ?? "dd/MM/yyyy";

            var dateString = dateTime?.ToString(dateFormat) ?? string.Empty;

            return keyValuePairs.SetAllValue(name, dateString);
        }

        public static List<KeyValuePair<string, string>> SetCurrencyValue<T>(this List<KeyValuePair<string, string>> keyValuePairs, string name, T decimalValue, bool condition = true)
        {
            return keyValuePairs.SetValue(name, decimalValue.ToCurrency(), condition);
        }

        public static List<KeyValuePair<string, string>> SetAllCurrencyValue<T>(this List<KeyValuePair<string, string>> keyValuePairs, string name, T decimalValue, bool condition = true)
        {
            return keyValuePairs.SetAllValue(name, decimalValue.ToCurrency(), condition);
        }

        public static List<KeyValuePair<string, string>> SetDecimalFormattedExact(this List<KeyValuePair<string, string>> keyValuePairs, string name, decimal decimalValue, string stringFormat, string cultureInfo = "pt-BR", bool condition = true)
        {
            return keyValuePairs.SetValueExact(name, decimalValue.ToString(stringFormat, new CultureInfo(cultureInfo)), condition);
        }

        public static List<KeyValuePair<string, string>> SetAllDecimalFormatted(this List<KeyValuePair<string, string>> keyValuePairs, string name, decimal decimalValue, string stringFormat, string cultureInfo = "pt-BR", bool condition = true)
        {
            return keyValuePairs.SetAllValue(name, decimalValue.ToString(stringFormat, new CultureInfo(cultureInfo)), condition);
        }

        public static List<KeyValuePair<string, string>> SetCurrencyValueExists<T>(this List<KeyValuePair<string, string>> keyValuePairs, string name, T decimalValue, bool condition = true)
        {
            return keyValuePairs.SetValueExists(name, decimalValue.ToCurrency(), condition);
        }

        public static List<KeyValuePair<string, string>> SetCurrencyValueExact<T>(this List<KeyValuePair<string, string>> keyValuePairs, string name, T decimalValue, bool condition = true)
        {
            return keyValuePairs.SetValueExact(name, decimalValue.ToCurrency(), condition);
        }

        public static List<KeyValuePair<string, string>> SetValue<T>(this List<KeyValuePair<string, string>> keyValuePairs, string name, T value, bool condition = true)
        {
            if (!condition) return keyValuePairs;

            var keyIndex = keyValuePairs.FindIndex(x => x.Key.Contains(name));

            keyValuePairs.UpdateValue(name, value?.ToString(), keyIndex);

            return keyValuePairs;
        }

        public static List<KeyValuePair<string, string>> SetValueWithKey(this List<KeyValuePair<string, string>> keyValuePairs, string name, string nameToFind)
        {
            var findValue = keyValuePairs.GetValueExact(nameToFind);

            keyValuePairs.SetValue(name, findValue);

            return keyValuePairs;
        }

        public static List<KeyValuePair<string, string>> SetValueCleared(this List<KeyValuePair<string, string>> keyValuePairs, string name, string value, bool condition = true)
        {
            return keyValuePairs.SetValue(name, value.Clear(), condition);
        }

        public static List<KeyValuePair<string, string>> SetValueEndsWith<T>(this List<KeyValuePair<string, string>> keyValuePairs, string name, T value, bool condition = true)
        {
            if (!condition) return keyValuePairs;

            var keyIndex = keyValuePairs.FindIndex(x => x.Key.EndsWith(name));

            keyValuePairs.UpdateValue(name, value?.ToString(), keyIndex);

            return keyValuePairs;
        }

        public static List<KeyValuePair<string, string>> SetValueIndex<T>(this List<KeyValuePair<string, string>> keyValuePairs, string name, T value, int index = 0, bool condition = true)
        {
            if (!condition || value == null || name == null) return keyValuePairs;

            var indexData = value.ToString().Index(index).Trim();

            return keyValuePairs.SetValue(name, indexData);
        }

        public static List<KeyValuePair<string, string>> SetValueIndexExactOrNothing<T>(this List<KeyValuePair<string, string>> keyValuePairs, string name, T value, int index = 0, bool condition = true)
        {
            if (!condition || value == null || name == null) return keyValuePairs;

            var indexData = value.ToString().Index(index).Trim();

            return keyValuePairs.SetValueExactOrNothing(name, indexData);
        }

        public static List<KeyValuePair<string, string>> SetValueIndex(this List<KeyValuePair<string, string>> keyValuePairs, string name, string[] value, int index, bool condition = true)
        {
            if (!condition) return keyValuePairs;

            var indexData = value.Index(index).Trim();

            return keyValuePairs.SetValue(name, indexData);
        }

        public static List<KeyValuePair<string, string>> SetAllValueIndex(this List<KeyValuePair<string, string>> keyValuePairs, string name, string[] value, int index = 0, bool condition = true)
        {
            if (!condition) return keyValuePairs;

            var indexData = value.Index(index).Trim();

            return keyValuePairs.SetAllValue(name, indexData);
        }

        public static List<KeyValuePair<string, string>> SetAllValueIndex<T>(this List<KeyValuePair<string, string>> keyValuePairs, string name, T value, int index = 0, bool condition = true)
        {
            if (!condition || value == null) return keyValuePairs;

            var indexData = value.ToString().Index(index).Trim();

            return keyValuePairs.SetAllValue(name, indexData);
        }

        public static List<KeyValuePair<string, string>> SetValueEncoded<T>(this List<KeyValuePair<string, string>> keyValuePairs, string name, T value, Encoding textEncoding = null)
        {
            var currentEncoding = textEncoding ?? Encoding.GetEncoding("Windows-1252");

            var currentValue = HttpUtility.UrlEncode(value.ToString(), currentEncoding);

            return SetValue(keyValuePairs, name, currentValue);
        }

        public static List<KeyValuePair<string, string>> Clear(this List<KeyValuePair<string, string>> keyValuePairs, string name)
        {
            var keyIndex = keyValuePairs.FindIndex(x => x.Key.Contains(name));

            keyValuePairs.UpdateValue(name, string.Empty, keyIndex);

            return keyValuePairs;
        }

        public static List<KeyValuePair<string, string>> ClearAll(this List<KeyValuePair<string, string>> keyValuePairs, string name)
        {
            for (var index = 0; index < keyValuePairs.Count; index++)
            {
                if (!keyValuePairs[index].Key.Contains(name) || keyValuePairs[index].Value == string.Empty) continue;

                keyValuePairs.UpdateValue(name, string.Empty, index);

                index--;
            }

            return keyValuePairs;
        }

        public static List<KeyValuePair<string, string>> SetValueRange(this List<KeyValuePair<string, string>> keyValuePairs, List<KeyValuePair<string, string>> keyValuePairsUpdate)
        {
            keyValuePairsUpdate.ForEach(update =>
            {
                keyValuePairs.SetValue(update.Key, update.Value);
            });

            return keyValuePairs;
        }

        public static List<KeyValuePair<string, string>> SetValueRangeExact(this List<KeyValuePair<string, string>> keyValuePairs, List<KeyValuePair<string, string>> keyValuePairsUpdate)
        {
            keyValuePairsUpdate.ForEach(update =>
            {
                keyValuePairs.SetValueExact(update.Key, update.Value);
            });

            return keyValuePairs;
        }

        public static List<KeyValuePair<string, string>> SetMultipleValue(this List<KeyValuePair<string, string>> keyValuePairs, string[] names, string value, bool condition = true)
        {
            if (!condition) return keyValuePairs;

            foreach (var name in names)
            {
                var keyIndex = keyValuePairs.FindIndex(x => x.Key.Contains(name));

                keyValuePairs.UpdateValue(name, value, keyIndex);
            }

            return keyValuePairs;
        }

        public static List<KeyValuePair<string, string>> SetMultipleValueExact(this List<KeyValuePair<string, string>> keyValuePairs, string[] names, string value)
        {
            foreach (var name in names)
            {
                var keyIndex = keyValuePairs.FindIndex(x => x.Key == name);

                keyValuePairs.UpdateValue(name, value, keyIndex);
            }

            return keyValuePairs;
        }

        public static List<KeyValuePair<string, string>> SetTokenValue(this List<KeyValuePair<string, string>> keyValuePairs, JToken jToken, string tokenName, params string[] names)
        {
            var tokenValue = jToken[tokenName].ToString();

            foreach (var name in names)
            {
                var keyIndex = keyValuePairs.FindIndex(x => x.Key.Contains(name));

                keyValuePairs.UpdateValue(name, tokenValue, keyIndex);
            }

            return keyValuePairs;
        }

        public static List<KeyValuePair<string, string>> SetMultipleValue(this List<KeyValuePair<string, string>> keyValuePairs, string value, params string[] names)
        {
            foreach (var name in names)
            {
                var keyIndex = keyValuePairs.FindIndex(x => x.Key.Contains(name));

                keyValuePairs.UpdateValue(name, value, keyIndex);
            }

            return keyValuePairs;
        }

        public static List<KeyValuePair<string, string>> SetValueIndexOrNothing(this List<KeyValuePair<string, string>> keyValuePairs, string name, string[] value, int index = 0, bool condition = true)
        {
            var indexData = value.Index(index).Trim();

            return keyValuePairs.SetValueOrNothing(name, indexData, condition);
        }

        public static List<KeyValuePair<string, string>> SetValueOrNothing<T>(this List<KeyValuePair<string, string>> keyValuePairs, string name, T value, bool condition = true)
        {
            var stringValue = value?.ToString();

            return !condition || string.IsNullOrEmpty(stringValue) || string.IsNullOrEmpty(name) ? keyValuePairs : SetValue(keyValuePairs, name, stringValue);
        }

        public static List<KeyValuePair<string, string>> SetCurrencyValueOrNothing<T>(this List<KeyValuePair<string, string>> keyValuePairs, string name, T decimalValue, bool condition = true)
        {
            return keyValuePairs.SetValueOrNothing(name, decimalValue.ToCurrency(), condition);
        }

        public static List<KeyValuePair<string, string>> SetAllValueOrNothing(this List<KeyValuePair<string, string>> keyValuePairs, string name, string value, bool condition = true)
        {
            return !condition || string.IsNullOrEmpty(value) || string.IsNullOrEmpty(name) ? keyValuePairs : SetAllValue(keyValuePairs, name, value);
        }

        public static List<KeyValuePair<string, string>> SetValueExistsOrNothing(this List<KeyValuePair<string, string>> keyValuePairs, string name, string value, bool condition = true)
        {
            return !condition || string.IsNullOrEmpty(value) || string.IsNullOrEmpty(name) ? keyValuePairs : keyValuePairs.SetValueExists(name, value);
        }

        public static List<KeyValuePair<string, string>> SetValueExactOrNothing(this List<KeyValuePair<string, string>> keyValuePairs, string name, string value, bool condition = true)
        {
            if (!condition || string.IsNullOrEmpty(value) || string.IsNullOrEmpty(name)) return keyValuePairs;

            return keyValuePairs.SetValueExact(name, value);
        }

        public static List<KeyValuePair<string, string>> SetValueOrDefault(this List<KeyValuePair<string, string>> keyValuePairs, string name, string value, string defaultValue)
        {
            return string.IsNullOrEmpty(value) ? SetValue(keyValuePairs, name, defaultValue) : SetValue(keyValuePairs, name, value);
        }

        public static List<KeyValuePair<string, string>> Remove(this List<KeyValuePair<string, string>> keyValuePairs, string name, bool condition = true)
        {
            if (!condition || name == null) return keyValuePairs;

            var keyIndex = keyValuePairs.FindIndex(x => x.Key.Contains(name));

            if (keyIndex == -1) return keyValuePairs;

            keyValuePairs.RemoveAt(keyIndex);

            return keyValuePairs;
        }

        public static List<KeyValuePair<string, string>> RemoveExact(this List<KeyValuePair<string, string>> keyValuePairs, string name, bool condition = true)
        {
            if (!condition) return keyValuePairs;

            var keyIndex = keyValuePairs.FindIndex(x => x.Key == name);

            if (keyIndex == -1) return keyValuePairs;

            keyValuePairs.RemoveAt(keyIndex);

            return keyValuePairs;
        }

        public static List<KeyValuePair<string, string>> RemoveAll(this List<KeyValuePair<string, string>> keyValuePairs, string name, bool condition = true)
        {
            if (!condition) return keyValuePairs;

            keyValuePairs.RemoveAll(x => x.Key != null && x.Key.Contains(name));

            return keyValuePairs;
        }

        public static List<KeyValuePair<string, string>> RemoveAllEmpty(this List<KeyValuePair<string, string>> keyValuePairs, string name)
        {
            keyValuePairs.RemoveAll(x => x.Key != null && x.Key.Contains(name) && x.Value.IsNullOrEmpty());

            return keyValuePairs;
        }

        public static List<KeyValuePair<string, string>> RemoveAllEmpty(this List<KeyValuePair<string, string>> keyValuePairs)
        {
            keyValuePairs.RemoveAll(x => x.Key != null && x.Value.IsNullOrEmpty());

            return keyValuePairs;
        }

        public static List<KeyValuePair<string, string>> SetAllValue<T>(this List<KeyValuePair<string, string>> keyValuePairs, string name, T value, bool condition = true)
        {
            if (!condition) return keyValuePairs;

            var stringValue = value?.ToString();

            for (var index = 0; index < keyValuePairs.Count; index++)
            {
                if (!keyValuePairs[index].Key.Contains(name) || keyValuePairs[index].Value == stringValue) continue;

                keyValuePairs.UpdateValue(name, stringValue, index);

                index--;
            }

            return keyValuePairs;
        }

        public static List<KeyValuePair<string, string>> SetAllValueExists(this List<KeyValuePair<string, string>> keyValuePairs, string name, string value, bool condition = true)
        {
            if (!condition || name == null) return keyValuePairs;

            var keyExists = keyValuePairs.Any(x => x.Key.Contains(name));

            if (!keyExists) return keyValuePairs;

            return keyValuePairs.SetAllValue(name, value);
        }

        public static List<KeyValuePair<string, string>> SetAllValueNotExists<T>(this List<KeyValuePair<string, string>> keyValuePairs, string name, T value, string emptyValue = null)
        {
            var keyValueFiltered = keyValuePairs.Where(x => x.Key.Contains(name)).ToList();

            foreach (var keyValuePair in keyValueFiltered)
            {
                var keyIndex = keyValuePairs.FindIndex(x => x.Key == keyValuePair.Key);

                keyValuePairs.UpdateValue(keyValuePair.Key, value?.ToString(), keyIndex, emptyValue);
            }

            return keyValuePairs;
        }

        public static List<KeyValuePair<string, string>> SetValueIfEmpty<T>(this List<KeyValuePair<string, string>> keyValuePairs, string name, T value, string valueToCompare, bool condition = true)
        {
            if (!condition) return keyValuePairs;

            var valueNotEmpty = !valueToCompare.IsNullOrEmpty();

            return valueNotEmpty ? keyValuePairs : keyValuePairs.SetValueExact(name, value);
        }

        public static List<KeyValuePair<string, string>> SetValueExact<T>(this List<KeyValuePair<string, string>> keyValuePairs, string name, T value, bool condition = true)
        {
            if (!condition) return keyValuePairs;

            var keyIndex = keyValuePairs.FindIndex(x => x.Key == name);

            keyValuePairs.UpdateValue(name, value?.ToString(), keyIndex);

            return keyValuePairs;
        }

        public static List<KeyValuePair<string, string>> SetValueExactExists<T>(this List<KeyValuePair<string, string>> keyValuePairs, string name, T value, bool condition = true)
        {
            if (!condition) return keyValuePairs;

            var keyIndex = keyValuePairs.FindIndex(x => x.Key == name);

            if (keyIndex == -1) return keyValuePairs;

            keyValuePairs.UpdateValue(name, value?.ToString(), keyIndex);

            return keyValuePairs;
        }

        public static bool Exists(this List<KeyValuePair<string, string>> keyValuePairs, string name, bool condition = true)
        {
            if (!condition || name == null) return false;

            var keyIndex = keyValuePairs.FindIndex(x => x.Key.Contains(name));

            return keyIndex != -1;
        }

        public static List<KeyValuePair<string, string>> SetValueExists<T>(this List<KeyValuePair<string, string>> keyValuePairs, string name, T value, bool condition = true)
        {
            if (!condition || name == null || value == null) return keyValuePairs;

            var keyIndex = keyValuePairs.FindIndex(x => x.Key.Contains(name));

            if (keyIndex == -1) return keyValuePairs;

            keyValuePairs.UpdateValue(name, value.ToString(), keyIndex);

            return keyValuePairs;
        }

        public static List<KeyValuePair<string, string>> SetValueExistsIndex(this List<KeyValuePair<string, string>> keyValuePairs, string name, string value, int index = 0, bool condition = true)
        {
            var indexData = value.Index(index).Trim();

            return keyValuePairs.SetValueExists(name, indexData, condition);
        }

        public static List<KeyValuePair<string, string>> SetValueExactIndex(this List<KeyValuePair<string, string>> keyValuePairs, string name, string value, int index = 0, bool condition = true)
        {
            var indexData = value.Index(index).Trim();

            return keyValuePairs.SetValueExact(name, indexData, condition);
        }

        public static List<KeyValuePair<string, string>> SetValueByValue(this List<KeyValuePair<string, string>> keyValuePairs, string value, string newValue)
        {
            for (var index = 0; index < keyValuePairs.Count; index++)
            {
                var formIndex = keyValuePairs[index];

                if (formIndex.Value != value) continue;

                keyValuePairs.UpdateValue(formIndex.Key, newValue, index);

                index--;
            }

            return keyValuePairs;
        }

        public static List<KeyValuePair<string, string>> SetValueNotExists<T>(this List<KeyValuePair<string, string>> keyValuePairs, string name, T value, string emptyValue = "")
        {
            var keyIndex = keyValuePairs.FindIndex(x => x.Key.Contains(name));

            keyValuePairs.UpdateValue(name, value?.ToString(), keyIndex, emptyValue);

            return keyValuePairs;
        }

        public static bool UpdateValue(this List<KeyValuePair<string, string>> keyValuePairs, string name, string value, int keyIndex, string emptyValue = null)
        {
            if (keyIndex > -1)
            {
                if (emptyValue != null &&
                    !string.IsNullOrEmpty(keyValuePairs[keyIndex].Value) &&
                    emptyValue != keyValuePairs[keyIndex].Value)
                    return true;

                keyValuePairs.AddValue(keyValuePairs[keyIndex].Key, value);

                keyValuePairs.RemoveAt(keyIndex);

                return true;
            }

            keyValuePairs.AddValue(name, value);

            return false;
        }

        public static List<KeyValuePair<string, string>> UpdateKeys(this List<KeyValuePair<string, string>> keyValuePairs, string keyName, string newKeyName, bool withReplace = true)
        {
            for (var keyIndex = 0; keyIndex < keyValuePairs.Count; keyIndex++)
            {
                var currentKey = keyValuePairs[keyIndex].Key;

                var newKey = withReplace ? newKeyName : $"{currentKey}{newKeyName}";

                if (!currentKey.Contains(keyName) || currentKey.Contains(newKeyName)) continue;

                keyValuePairs.AddValue(newKey, keyValuePairs[keyIndex].Value);

                keyValuePairs.RemoveAt(keyIndex);

                keyIndex--;
            }

            return keyValuePairs;
        }

        public static string GetValue(this List<KeyValuePair<string, string>> keyValuePairs, string keyName, bool ignoreCase = false)
        {
            var stringComparsion = ignoreCase ? StringComparison.InvariantCultureIgnoreCase : StringComparison.InvariantCulture;

            var keyValue = keyValuePairs.Find(x => x.Key.IndexOf(keyName, stringComparsion) > -1);

            return keyValue.Value;
        }

        public static string GetName(this List<KeyValuePair<string, string>> keyValuePairs, string keyName)
        {
            var keyValue = keyValuePairs.Find(x => x.Key.Contains(keyName));

            return keyValue.Key;
        }

        public static List<KeyValuePair<string, string>> GetValues(this List<KeyValuePair<string, string>> keyValuePairs, string keyName)
        {
            var keyValues = keyValuePairs.Where(x => x.Key.Contains(keyName)).ToList();

            return keyValues;
        }

        public static List<KeyValuePair<string, string>> GetValueAndSet(this List<KeyValuePair<string, string>> keyValuePairs, List<KeyValuePair<string, string>> newKeyValuePairs, string keyName, string newKeyName, bool setCondition = true)
        {
            if (!setCondition) return keyValuePairs;

            var keyValue = keyValuePairs.Find(x => x.Key.Contains(keyName));

            newKeyValuePairs.SetValue(newKeyName, keyValue.Value);

            return keyValuePairs;
        }

        public static List<KeyValuePair<string, string>> GetValues(this List<KeyValuePair<string, string>> keyValuePairs, bool sameName, params string[] keyNames)
        {
            var newKeyValuePair = AddValue();

            foreach (var keyName in keyNames)
            {
                var keyValue = keyValuePairs.Find(x => x.Key.IndexOf(keyName, StringComparison.InvariantCultureIgnoreCase) > -1);

                newKeyValuePair.AddValue(sameName ? keyValue.Key : keyName, keyValue.Value);
            }

            return newKeyValuePair;
        }

        public static T2 GetValueExact<T, T2>(this List<KeyValuePair<T, T2>> keyValuePairs, T keyName)
        {
            var keyValue = keyValuePairs.Find(x => x.Key.Equals(keyName));

            return keyValue.Value;
        }

        public static List<KeyValuePair<string, string>> Values(this List<KeyValuePair<string, string>> keyValuePairs, params string[] keyNames)
        {
            var formData = keyValuePairs.Where(x => x.Key.ContainsAny(keyNames)).ToList();

            keyValuePairs.RemoveAll(x => x.Key.ContainsAny(keyNames));

            return formData;
        }

        public static List<KeyValuePair<T, T2>> Values<T, T2>(this List<KeyValuePair<T, T2>> keyValuePairs, T2 valueName)
        {
            var formData = keyValuePairs.Where(x => x.Value.Equals(valueName)).ToList();

            keyValuePairs.RemoveAll(x => x.Value.Equals(valueName));

            return formData;
        }

        public static List<KeyValuePair<string, string>> ValueFilter(this List<KeyValuePair<string, string>> keyValuePairs, params string[] keyNames)
        {
            var formData = keyValuePairs.Where(x => x.Key.ContainsAny(keyNames)).ToList();

            return formData;
        }

        public static List<KeyValuePair<string, string>> ValueFilterExact(this List<KeyValuePair<string, string>> keyValuePairs, params string[] keyNames)
        {
            var formData = keyValuePairs.Where(x => keyNames.Contains(x.Key)).ToList();

            return formData;
        }

        public static KeyValuePair<string, string> FilterByValue(this List<KeyValuePair<string, string>> keyValuePairs, string valueData)
        {
            return keyValuePairs.FirstOrDefault(x => x.Value == valueData);
        }

        public static List<KeyValuePair<string, string>> Update(this List<KeyValuePair<string, string>> keyValuePairs, List<KeyValuePair<string, string>> keyValuePairsToUpdate)
        {
            keyValuePairsToUpdate.AddRange(keyValuePairs);

            return keyValuePairsToUpdate;
        }

        public static List<KeyValuePair<string, string>> AddOnlyValue(this List<KeyValuePair<string, string>> keyValuePairs, string value, bool condition = true)
        {
            if (!condition) return keyValuePairs;

            keyValuePairs.Add(new KeyValuePair<string, string>(null, value));

            return keyValuePairs;
        }

        public static bool HasKey(this List<KeyValuePair<string, string>> keyValuePairs, string keyName)
        {
            return keyValuePairs.Any(x => x.Key.Contains(keyName));
        }

        public static List<KeyValuePair<string, string>> AddPrefix(this List<KeyValuePair<string, string>> keyValuePairs, string validationPrefix)
        {
            var allKeys = keyValuePairs.Select(x => x.Key).ToList();

            foreach (var currentKey in allKeys)
            {
                var newKey = $"{validationPrefix}.{currentKey}";

                keyValuePairs.UpdateKeys(currentKey, newKey);
            }

            return keyValuePairs;
        }

        public static string Serialize<T, T2>(this List<KeyValuePair<T, T2>> keyValuePairs)
        {
            return JsonConvert.SerializeObject(keyValuePairs);
        }

        public static List<KeyValuePair<T, T2>> Deserialize<T, T2>(this string serializedKeyValue)
        {
            try
            {
                return JsonConvert.DeserializeObject<List<KeyValuePair<T, T2>>>(serializedKeyValue);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static int DeserializeValue(this string serializedKeyValue)
        {
            return serializedKeyValue.Deserialize<int, string>().First(x => x.Key == 0).Value.ToInt();
        }

        public static List<KeyValuePair<TKey, TValue>> ToKeyValuePair<T, TKey, TValue>(this IEnumerable<T> sourceContent, Func<T, TKey> keySelector, Func<T, TValue> valueSelector)
        {
            var sourceList = sourceContent as IList<T> ?? sourceContent.ToList();

            return sourceList.Select(x => new KeyValuePair<TKey, TValue>(keySelector(x), valueSelector(x))).ToList();
        }
    }
}
