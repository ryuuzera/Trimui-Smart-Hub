using CsQuery;
using CsQuery.ExtensionMethods.Internal;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TrimuiSmartHub.Application.Repository;
using TrimuiSmartHub.Application.Helpers;

namespace TrimuiSmartHub.Application.Services.LibRetro
{
    internal class LibretroService: IDisposable
    {
        private readonly HttpClient httpClient;

        private readonly Uri baseUri = new("https://thumbnails.libretro.com");

        public static LibretroService New()
        {
            return new LibretroService();
        }

        private LibretroService()
        {
            httpClient = new HttpClient();

            httpClient.Timeout = TimeSpan.FromSeconds(15);
        }

        public byte[]? SearchThumbnail(string console, string title)
        {
            try
            {
                var CurrentUri = baseUri;

                CQ lastDom = httpClient.GetStringAsync(CurrentUri).Result;

                var consoleList = lastDom["a"].Select(x => x.InnerText).ToList();

                var findItem = LevenshteinAlgorithm.FindMostSimilar(console, consoleList).Replace("/", string.Empty);

                CurrentUri = CurrentUri.Append(findItem).Append("Named_Boxarts");

                lastDom = httpClient.GetStringAsync(CurrentUri).Result;

                var gameList = lastDom["a"].Select(x => x.InnerText).ToList();

                var findGame = LevenshteinAlgorithm.FindMostSimilar(title, gameList);

                CurrentUri = CurrentUri.Append(Uri.EscapeDataString(findGame));

                var image = httpClient.GetByteArrayAsync(CurrentUri).Result;

                return image;
            }
            catch (Exception)
            {
                return null;
            }
            
        }

        public void Dispose()
        {
            httpClient.Dispose();
        }
    }
}
