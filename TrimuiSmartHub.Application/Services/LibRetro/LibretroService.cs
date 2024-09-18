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
using TrimuiSmartHub.Application.Model;

namespace TrimuiSmartHub.Application.Services.LibRetro
{
    internal class LibretroService: IDisposable
    {
        private readonly HttpClient httpClient;

        private readonly Uri baseUri = new("https://thumbnails.libretro.com");

        private readonly GameRepository gameRepository;

        public static LibretroService New()
        {
            return new LibretroService();
        }

        private LibretroService()
        {
            httpClient = new HttpClient();

            httpClient.Timeout = TimeSpan.FromSeconds(15);

            gameRepository = GameRepository.New();
        }

        private GameData? FindGameFullName(string title)
        {
            return gameRepository.FindGameByRomName(title);
        }

        public async Task<byte[]?> SearchThumbnail(string console, string title)
        {
            try
            {
                var CurrentUri = baseUri;
                CQ lastDom = httpClient.GetStringAsync(CurrentUri).Result;
                if (lastDom == null) return null;

                var consoleList = lastDom["a"].Select(x => x.InnerText).Where(x => !x.Equals("../")).ToList();
                var matchedConsole = LevenshteinAlgorithm.FindMostSimilar(console, consoleList)?.Replace("/", string.Empty);
                if (matchedConsole.IsNullOrEmpty()) return null;

                CurrentUri = CurrentUri.Append(matchedConsole).Append("Named_Boxarts");
                lastDom = httpClient.GetStringAsync(CurrentUri).Result;
                if (lastDom == null) return null;

                var gameList = lastDom["a"].Select(x => x.InnerText).Where(x => !x.Equals("../")).ToList();
                var findGame = LevenshteinAlgorithm.FindMostSimilar(title.Split('.').First(), gameList) ?? LevenshteinAlgorithm.FindMostSimilar(FindGameFullName(title)?.Name?.Split('/').FirstOrDefault().Trim() ?? string.Empty, gameList);
                if (findGame.IsNullOrEmpty()) return null;

                var hrefImage = lastDom["a"].Where(x => x.InnerText.Contains(findGame, StringComparison.OrdinalIgnoreCase))
                                            .FirstOrDefault()?.GetAttribute("href");

                if (hrefImage.IsNullOrEmpty()) return null;

                CurrentUri = CurrentUri.Append(hrefImage);

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
