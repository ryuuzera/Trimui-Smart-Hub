using ControlzEx.Standard;
using CsQuery;
using CsQuery.ExtensionMethods;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using TrimuiSmartHub.Application.Helpers;
using TrimuiSmartHub.Application.Model;
using TrimuiSmartHub.Application.Repository;
using TrimuiSmartHub.Application.Services.Trimui;


namespace TrimuiSmartHub.Application.Services
{

    class RetrosticService : IDisposable
    {
        private readonly HttpClient httpClient;

        private CookieContainer cookieContainer;
        private Uri baseUri {  get; set; }

        private readonly GameRepository gameRepository;

        public static RetrosticService New()
        {
            return new RetrosticService();
        }

        private RetrosticService()
        {
            cookieContainer = new CookieContainer();

            var handler = new HttpClientHandler
            {
                CookieContainer = cookieContainer
            };

            httpClient = new HttpClient(handler);

            httpClient.Timeout = TimeSpan.FromSeconds(15);

            baseUri = new("https://www.retrostic.com/");

            gameRepository = GameRepository.New();
        }
        public void Dispose()
        {
            httpClient.Dispose();
        }
        public async Task<List<GameInfoRetrostic>> SearchGamesByName(string gameName, string emulator)
        {
            var result = new List<GameInfoRetrostic>();

            var firstLetter = gameName.ToLower()[0];
            var emulatorUri = EmulatorDictionary.EmulatorMapRetrostic.FirstOrDefault(x => x.Key.Equals(emulator)).Value.ToUri();
            var sortingUri = emulatorUri.AddQuery("sorting", firstLetter);

            string currentUri = sortingUri.ToString();

            while (!string.IsNullOrEmpty(currentUri))
            {
                CQ lastDom = httpClient.GetStringAsync(currentUri).Result;

                var tableRows = lastDom["tbody tr[itemtype*='VideoGame']"];

                var gameList = tableRows["td[class*='d-sm-table-cell'] a[href*='/roms/']"]
                                .Where(x => x.HasAttribute("title") && x.GetAttribute("title").ClearSpecial().ToLower().Contains(gameName?.ClearSpecial().ToLower()));

                var gameImgs = gameList.Select(x => x.ChildNodes[0].GetAttribute("data-src")).ToList();

                var gameRegions = tableRows["td"]
                .Where(x =>
                {
                    var parentHtml = CQ.Create(x.ParentNode.InnerHTML);

                    var hasMatchingTitle = parentHtml["td[class*='d-sm-table-cell'] a[href*='/roms/']"]
                        .Any(a => a.HasAttribute("title") && a.GetAttribute("title").ClearSpecial().ToLower().Contains(gameName?.ClearSpecial().ToLower()));

                    return hasMatchingTitle && x.InnerHTML.Contains("/flags") && !x.InnerHTML.Contains("\n");
                })
                .Select(x => x.InnerText)
                .ToList();

                foreach (var item in gameList)
                {
                    var gameInfo = new GameInfoRetrostic
                    {
                        Emulator = emulator,
                        Title = Regex.Replace(item.GetAttribute("title").ToString()
                                     .Replace("Roms", string.Empty)
                                     .Replace("ISO", string.Empty),
                                     @"\(\s*[^)]+\s*\)|\[\s*[^\]]+\s*\]", string.Empty).Trim(),
                        BoxArt = baseUri.Append(gameImgs[gameList.IndexOf(item)] ?? ""),
                        DownloadPage = baseUri.Append(item.GetAttribute("href") ?? ""),
                        Region = gameRegions[gameList.IndexOf(item)] ?? ""
                    };

                    result.Add(gameInfo);
                }

                var nextPageElement = lastDom["a[class*='page-link']"].FirstOrDefault(x => x.InnerText.Equals("&gt;"));

                if (nextPageElement != null)
                {
                    currentUri = baseUri.Append(nextPageElement.GetAttribute("href")).ToString();
                    continue;
                }
                
                currentUri = null; 
            }

            return result;
        }

        public async Task<List<GameInfoRetrostic>> ListGamesByEmulator(string emulator)
        {
            var result = new List<GameInfoRetrostic>();

            var emulatorUri = EmulatorDictionary.EmulatorMapRetrostic.FirstOrDefault(x => x.Key.Equals(emulator)).Value.ToUri();

            CQ lastDom = httpClient.GetStringAsync(emulatorUri).Result;

            var tableRows = lastDom["tbody tr[itemtype*='VideoGame']"];

            var gameList = tableRows["td[class*='d-sm-table-cell'] a[href*='/roms/']"].Where(x => x.HasAttribute("title"));

            var gameImgs = gameList.Select(x => x.ChildNodes[0].GetAttribute("data-src")).ToList();

            var gameRegions = tableRows["td"].Where(x => x.InnerHTML.Contains("/flags") && !x.InnerHTML.Contains("\n")).Select(x => x.InnerText).ToList();

            foreach (var item in gameList)
            {
                var gameInfo = new GameInfoRetrostic
                {
                    Emulator = emulator,
                    Title = Regex.Replace(item.GetAttribute("title").ToString()
                                 .Replace("Roms", string.Empty)
                                 .Replace("ISO", string.Empty),
                                 @"\(\s*[^)]+\s*\)|\[\s*[^\]]+\s*\]", string.Empty).Trim(),
                    BoxArt = baseUri.Append(gameImgs[gameList.IndexOf(item)] ?? ""),
                    DownloadPage = baseUri.Append(item.GetAttribute("href") ?? ""),
                    Region = gameRegions[gameList.IndexOf(item)] ?? ""
                };

                result.Add(gameInfo);
            }

            return result;

        }
        public async Task<(Stream contentStream, FileStream fileStream, long totalBytes)> DownloadGame(GameInfoRetrostic gameInfo)
        {
            CQ downloadGamePage = httpClient.GetStringAsync(gameInfo.DownloadPage).Result;

            var session = downloadGamePage["input[name*='session']"]?.FirstOrDefault()?.GetAttribute("value");
            var gameinfo = gameInfo.DownloadPage?.ToString().Split('/');

            var formContent = KeyValueHelper.AddValue("session", session)
                                            .AddValue("rom_url", gameinfo[5])
                                            .AddValue("console_url", gameinfo[4]);

            var content = new FormUrlEncodedContent(formContent);
            downloadGamePage = httpClient.PostAsync(gameInfo.DownloadPage?.Append("download"), content).Result.Content.ReadAsStringAsync().Result;

            var downloadLink = Regex.Match(downloadGamePage.RenderSelection(), @"window\.location\.href\s*=\s*""(https:\/\/[^\s""]+)""").Groups[1].Value;
            var response = await httpClient.GetAsync(downloadLink, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;

            var contentStream = await response.Content.ReadAsStreamAsync();

            var romsFolder = TrimuiService.New().GetRomsFolder(gameInfo.Emulator!);

            var fileStream = new FileStream(Path.Combine(romsFolder, $"{gameInfo.Title}.{downloadLink.Split('.').Last()}"), FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            return (contentStream, fileStream, totalBytes);
        }


    }
}
