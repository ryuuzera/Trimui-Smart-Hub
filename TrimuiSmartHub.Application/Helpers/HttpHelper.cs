using CsQuery.ExtensionMethods.Internal;
using CsQuery.ExtensionMethods;
using CsQuery;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Reflection;

namespace TrimuiSmartHub.Application.Helpers
{
    public static class HttpHelper
    {
        public static Uri AddQuery<T>(this Uri uri, string name, T value)
        {
            var uriBuilder = new UriBuilder(uri);

            var httpValueCollection = HttpUtility.ParseQueryString(uri.Query);

            httpValueCollection.Add(name, value?.ToString());

            uriBuilder.Query = httpValueCollection.ToString();

            return uriBuilder.Uri;
        }

        public static Uri SetQuery(this Uri uri, string name, string value, bool escapeValue = true)
        {
            var uriBuilder = new UriBuilder(uri);

            var httpValueCollection = ParseQueryString(uri);

            var escapedValue = escapeValue ? Uri.EscapeDataString(value) : value;

            var containsKey = httpValueCollection.ContainsKey(name);

            if (containsKey) httpValueCollection.Remove(name);

            httpValueCollection.Add(name, escapedValue);

            var valueList = httpValueCollection.Select(x => $"{x.Key}={x.Value}").ToList();

            var joinedValue = string.Join("&", valueList);

            uriBuilder.Query = joinedValue;

            return uriBuilder.Uri;
        }

        public static Dictionary<string, string> ParseQueryString(this Uri uri)
        {
            var queryIndex = uri.Query.IndexOf('?') + 1;

            var queryString = uri.Query.Substring(queryIndex);

            var splittedQuery = queryString.Split('&');

            return splittedQuery.Select(o => o.Split('='))
                .Where(items => items.Length == 2)
                .ToDictionary(pair => pair[0], pair => pair[1]);
        }

        public static Uri AddQuery(this Uri uri, List<KeyValuePair<string, string>> formContent)
        {
            foreach (var keyValuePair in formContent)
            {
                uri = uri.AddQuery(keyValuePair.Key, keyValuePair.Value);
            }

            return uri;
        }

        public static Uri AddQuery<T>(this Uri uri, T queryString)
        {
            var uriBuilder = new UriBuilder(uri)
            {
                Query = queryString.ToString().Replace("?", string.Empty)
            };

            return uriBuilder.Uri;
        }

        public static Uri Append(this Uri uri, params string[] paths)
        {
            return new Uri(paths.Aggregate(uri.AbsoluteUri, (current, path) => $"{current.TrimEnd('/')}/{path.TrimStart('/')}"));
        }

        public static List<Uri> AddUri(this List<Uri> uriList, Uri currentUri, string uriPath)
        {
            var newUri = new Uri(currentUri, uriPath);

            uriList.Add(newUri);

            return uriList;
        }

        public static Uri ToUri(this string uri)
        {
            return new Uri(uri);
        }

        public static NameValueCollection ParseQueryString(this string queryString)
        {
            return HttpUtility.ParseQueryString(queryString);
        }

        public static HttpRequestMessage CreateRequest(this Uri requestUri, HttpMethod httpMethod, Version httpVersion = null)
        {
            return new HttpRequestMessage
            {
                Version = httpVersion ?? HttpVersion.Version11,
                RequestUri = requestUri,
                Method = httpMethod
            };
        }

        public static FormUrlEncodedContent Encode(this List<KeyValuePair<string, string>> keyValuePairs)
        {
            return new FormUrlEncodedContent(keyValuePairs);
        }

        public static StringContent CustomEncode(this List<KeyValuePair<string, string>> keyValuePairs, Encoding currentEncoding = null)
        {
            var stringPairs = keyValuePairs.Select(kvp =>
            {
                var valueEncode = currentEncoding != null ? HttpUtility.UrlEncode(kvp.Value, currentEncoding) : HttpUtility.UrlEncode(kvp.Value);

                return $"{kvp.Key}={valueEncode}";
            });

            currentEncoding = currentEncoding ?? Encoding.UTF8;

            var encodedContent = string.Join("&", stringPairs);

            var httpContent = new StringContent(encodedContent, currentEncoding, "application/x-www-form-urlencoded");

            return httpContent;
        }

        public static Uri ServicePoint(this Uri uriDetail, int idleTime = 500)
        {
            var servicePoint = ServicePointManager.FindServicePoint(uriDetail);
            servicePoint.Expect100Continue = false;
            servicePoint.MaxIdleTime = idleTime;
            return uriDetail;
        }

        public static HttpResponseMessage AddCookie(this HttpResponseMessage httpResponseMessage, CookieContainer cookieContainer, bool onlyValue = false, bool removeOld = false)
        {
            var pageUri = new UriBuilder(httpResponseMessage.RequestMessage.RequestUri.Host).Uri;

            var hasValue = httpResponseMessage.Headers.TryGetValues("Set-Cookie", out var cookieList);

            if (!hasValue) return httpResponseMessage;

            if (onlyValue)
            {
                AddCookieValue(cookieContainer, pageUri, cookieList, removeOld);
            }
            else
            {
                AddCookieHeader(cookieContainer, pageUri, cookieList);
            }

            return httpResponseMessage;
        }

        public static void AddCookieHeader(CookieContainer cookieContainer, Uri pageUri, IEnumerable<string> cookieList)
        {
            foreach (var cookieHeader in cookieList)
            {
                var cookiePath = Regex.Match(cookieHeader, @"(?<=Path=)(.*?)(?=;)").Value;

                if (!string.IsNullOrEmpty(cookiePath) && cookiePath != "/")
                {
                    pageUri = new Uri(pageUri, cookiePath);
                }

                cookieContainer.SetCookies(pageUri, cookieHeader);
            }
        }

        public static HttpResponseMessage AddCookie(this HttpResponseMessage httpResponseMessage, List<Cookie> cookieContainer)
        {
            var pageUri = new UriBuilder(httpResponseMessage.RequestMessage.RequestUri.Host).Uri;

            var hasValue = httpResponseMessage.Headers.TryGetValues("Set-Cookie", out var cookieList);

            if (!hasValue) return httpResponseMessage;

            var pageDomain = PageDomain(pageUri);

            foreach (var cookieHeader in cookieList)
            {
                var cookieValue = Regex.Match(cookieHeader, "(.+?)=(.+?);");

                if (cookieValue.Captures.Count == 0) continue;

                var cookiePath = Regex.Match(cookieHeader, @"(?<=Path=)(.*?)(?=;)").Value;

                cookiePath = cookiePath == "/" ? string.Empty : cookiePath;

                cookieContainer.RemoveAll(x => x.Name == cookieValue.Groups[1].Value && x.Path == cookiePath);

                var newCookie = new Cookie(cookieValue.Groups[1].ToString(), cookieValue.Groups[2].ToString(), cookiePath, pageDomain);

                cookieContainer.Add(newCookie);
            }

            return httpResponseMessage;
        }

        private static void AddCookieValue(CookieContainer cookieContainer, Uri pageUri, IEnumerable<string> cookieList, bool removeOld)
        {
            var pageDomain = PageDomain(pageUri);

            foreach (var cookieHeader in cookieList)
            {
                var currentCookies = cookieContainer.GetAllCookies(pageUri).ToList();

                var cookieValue = Regex.Match(cookieHeader, "(.+?)=(.+?);");

                if (cookieValue.Captures.Count == 0) continue;

                var cookieExist = currentCookies.Where(x => x.Value == cookieValue.Groups[2].Value).ToList();

                if (removeOld)
                {
                    cookieExist = currentCookies.Where(x => x.Name == cookieValue.Groups[1].Value).ToList();
                }

                if (cookieExist.Any())
                {
                    foreach (var cookie in cookieExist)
                    {
                        cookie.Expired = true;
                    }
                }

                var cookiePath = Regex.Match(cookieHeader, @"(?<=Path=)(.*?)(?=;|$)").Value;

                var newCookie = new Cookie(cookieValue.Groups[1].ToString(), cookieValue.Groups[2].ToString(), cookiePath, pageDomain);

                cookieContainer.Add(newCookie);
            }
        }

        public static string PageDomain(this Uri pageUri, bool withDot = false, int partMax = 3)
        {
            var pageDomain = pageUri.Host;

            var pageUriSplitted = pageDomain.Split('.').ToList();

            if (pageUriSplitted.Count > partMax)
            {
                pageUriSplitted.RemoveAt(0);

                pageDomain = string.Join(".", pageUriSplitted);
            }

            return withDot ? $".{pageDomain}" : pageDomain;
        }

        public static void RemoveCookie(this List<Cookie> cookieExist)
        {
            if (!cookieExist.Any()) return;

            foreach (var cookie in cookieExist)
            {
                cookie.Expired = true;
            }
        }

        public static List<Cookie> GetCookie(this CookieContainer cookieContainer, string cookieName)
        {
            var currentCookies = cookieContainer.GetAllCookies().ToList();

            var cookieExist = currentCookies.Where(x => x.Name.Contains(cookieName)).ToList();

            return cookieExist;
        }

        public static string GetCookie(this CookieContainer cookieContainer, string cookieName, string domainUrl)
        {
            var currentCookies = cookieContainer.GetAllCookies().ToList();

            var cookieExist = currentCookies.FirstOrDefault(x => x.Name.Contains(cookieName) && x.Domain == domainUrl);

            return cookieExist?.Value;
        }

        public static List<Cookie> GetCookieByPath(this CookieContainer cookieContainer, string cookiePath)
        {
            var currentCookies = cookieContainer.GetAllCookies().ToList();

            var cookieExist = currentCookies.Where(x => x.Path.Contains(cookiePath)).ToList();

            return cookieExist;
        }

        public static void Clear(this CookieContainer cookieContainer)
        {
            var currentCookies = cookieContainer.GetAllCookies().ToList();

            foreach (var cookie in currentCookies)
            {
                cookie.Expired = true;
            }
        }

        public static string GetCookie(this HttpResponseMessage response, string cookieName)
        {
            return !response.Headers.TryGetValues("Set-Cookie", out var cookies)
                ? string.Empty
                : cookies.FirstOrDefault(x => x.Split('=')[0] == cookieName)?.Split(';')[0];
        }

        public static CookieContainer Clone(this CookieContainer cookieTransfer)
        {
            var cookieList = cookieTransfer.GetAllCookies().ToList();

            var cookieContainer = new CookieContainer();

            foreach (var currentCookie in cookieList)
            {
                cookieContainer.Add(currentCookie);
            }

            return cookieContainer;
        }

        public static void AddCookie(this List<Cookie> cookieContainer, string cookieName, string cookieValue = "")
        {
            var newCookie = new Cookie(cookieName, cookieValue);

            cookieContainer.Add(newCookie);
        }

        public static void TransferCookies(this CookieContainer container, IEnumerable<Cookie> cookieList)
        {
            var currentCookies = container.GetAllCookies().ToList();

            foreach (var currentCookie in cookieList)
            {
                var cookieExist = currentCookies.Where(x => x.Name == currentCookie.Name).ToList();

                if (cookieExist.Any())
                {
                    foreach (var cookie in cookieExist)
                    {
                        cookie.Expired = true;
                    }
                }

                var cookieValueSplitted = currentCookie.Value.Split(',');

                foreach (var cookieValue in cookieValueSplitted)
                {
                    var newCookie = new Cookie(currentCookie.Name, cookieValue, currentCookie.Path, currentCookie.Domain);

                    container.Add(newCookie);
                }
            }
        }

        public static IEnumerable<Cookie> GetAllCookies(this CookieContainer container, Uri currentUri = null)
        {
            var domainTableField = container.GetType().GetRuntimeFields().FirstOrDefault(x => x.Name == "m_domainTable");

            if (domainTableField == null) yield break;

            var domainList = (IDictionary)domainTableField.GetValue(container);

            foreach (var domainValue in domainList.Values)
            {
                var firstType = domainValue.GetType().GetRuntimeFields().First(x => x.Name == "m_list");
                var dictionaryValues = (IDictionary)firstType.GetValue(domainValue);

                foreach (CookieCollection cookieCollection in dictionaryValues.Values)
                {
                    foreach (Cookie allCookies in cookieCollection)
                    {
                        if (allCookies.Expired == false && (currentUri == null || currentUri.OriginalString.Contains(allCookies.Domain)))
                        {
                            yield return allCookies;
                        }
                    }
                }
            }
        }

        public static FormUrlEncodedContent CreateEncodedContent(this IEnumerable<IDomObject> redirectData)
        {
            return CreateContent(redirectData).Encode();
        }

        public static List<KeyValuePair<string, string>> CreateContent(this IEnumerable<IDomObject> redirectData, bool repeatedValues = false)
        {
            var keyValuePairs = KeyValueHelper.AddValue();

            CreateContent(keyValuePairs, redirectData, repeatedValues);

            return keyValuePairs;
        }

        public static List<KeyValuePair<string, string>> CreateContent(this List<KeyValuePair<string, string>> keyValuePairs, IEnumerable<IDomObject> redirectData, bool repeatedValues = false)
        {
            foreach (var domElement in redirectData)
            {
                var elementName = domElement.GetAttribute("name") ?? domElement.GetAttribute("id");
                var elementType = domElement.GetAttribute("type") ?? string.Empty;
                var elementValue = string.IsNullOrEmpty(domElement.DefaultValue)
                    ? domElement.Value
                    : domElement.DefaultValue;

                elementValue = UpdateCheckbox(elementType, domElement, elementValue);

                var keyIndex = keyValuePairs.FindIndex(x => x.Key == elementName);

                if (keyIndex == -1 || repeatedValues)
                {
                    keyValuePairs.AddValue(elementName, elementValue);
                }
            }

            return keyValuePairs;
        }

        public static List<KeyValuePair<string, string>> CreateContent(this List<KeyValuePair<string, string>> keyValuePairs, JObject jsonContent)
        {
            foreach (JProperty property in jsonContent.Properties())
            {
                keyValuePairs.SetValue(property.Name, (string)property.Value);
            }

            return keyValuePairs;
        }

        private static string UpdateCheckbox(string elementType, IDomObject domElement, string elementValue)
        {
            if (elementType.ToUpper() == "CHECKBOX" && domElement.Checked && elementValue.IsNullOrEmpty())
            {
                elementValue = "on";
            }

            return elementValue;
        }

        public static void UpdateContent(this IEnumerable<IDomObject> redirectData, List<KeyValuePair<string, string>> keyValuePairs)
        {
            foreach (var domElement in redirectData)
            {
                var elementName = domElement.GetAttribute("name");
                var elementType = domElement.GetAttribute("type") ?? string.Empty;
                var defaultValue = string.IsNullOrEmpty(domElement.DefaultValue)
                    ? domElement.Value
                    : domElement.DefaultValue;

                defaultValue = UpdateCheckbox(elementType, domElement, defaultValue);

                keyValuePairs.SetValueExact(elementName, defaultValue);
            }
        }

        public static StringContent ToEncodedContent(this IEnumerable<KeyValuePair<string, string>> keyValuePairs)
        {
            var httpContent = keyValuePairs.ToStringContent();

            httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

            return httpContent;
        }

        public static MultipartFormDataContent ToMultiPartContent(this IEnumerable<KeyValuePair<string, string>> keyValuePairs, string boundary)
        {
            var httpContent = new MultipartFormDataContent(boundary);

            keyValuePairs.ForEach(x =>
            {
                httpContent.Add(new StringContent(x.Value), x.Key);
            });

            return httpContent;
        }

        public static StringContent ToTextContent(this IEnumerable<KeyValuePair<string, string>> keyValuePairs)
        {
            var httpContent = keyValuePairs.ToStringContent(Environment.NewLine);

            httpContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

            return httpContent;
        }

        public static StringContent ToStringContent(this IEnumerable<KeyValuePair<string, string>> keyValuePairs, string defaultSeparator = "&")
        {
            if (keyValuePairs == null) return new StringContent(string.Empty);

            var stringPairs = keyValuePairs.Select(kvp => $"{kvp.Key}={kvp.Value}");

            var encodedContent = string.Join(defaultSeparator, stringPairs);

            return new StringContent(encodedContent);
        }

        public static string Unescape(this string valueToUnescape)
        {
            return Regex.Unescape(valueToUnescape);
        }

        public static string UrlEncode(this string valueToEncode)
        {
            return HttpUtility.UrlEncode(valueToEncode);
        }

        public static string HtmlEncode(this Uri uriToEncode)
        {
            return HttpUtility.HtmlEncode(uriToEncode);
        }

        public static string HtmlDecode(this string plainText)
        {
            return HttpUtility.HtmlDecode(plainText);
        }

        public static string HtmlEncode(this string plainText)
        {
            return HttpUtility.HtmlAttributeEncode(plainText);
        }

        public static string UrlDecode(this string plainText)
        {
            return HttpUtility.UrlDecode(plainText);
        }

        public static string UrlDecode(this string plainText, Encoding encoding)
        {
            return HttpUtility.UrlDecode(plainText, encoding);
        }
        public static byte[] GzipDecompress(this byte[] gzip)
        {
            using (GZipStream stream = new GZipStream(new MemoryStream(gzip), CompressionMode.Decompress))
            {
                const int size = 4096;
                byte[] buffer = new byte[size];
                using (MemoryStream memory = new MemoryStream())
                {
                    int count = 0;
                    do
                    {
                        count = stream.Read(buffer, 0, size);
                        if (count > 0)
                        {
                            memory.Write(buffer, 0, count);
                        }
                    }
                    while (count > 0);
                    return memory.ToArray();
                }
            }
        }

    }
}
