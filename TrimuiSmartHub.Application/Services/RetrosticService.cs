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

        public async Task<List<GameInfoRetrostic>> ListGamesByEmulator(string emulator)
        {
            var result = new List<GameInfoRetrostic>();

            var emulatorUri = EmulatorDictionary.EmulatorMapRetrostic.FirstOrDefault(x => x.Key.Equals(emulator)).Value.ToUri();

            CQ lastDom = httpClient.GetStringAsync(emulatorUri).Result;

            var gameList = lastDom["td[class*='d-sm-table-cell'] a[href*='/roms/']"].Where(x => x.HasAttribute("title"));

            var gameImgs = gameList.Select(x => x.ChildNodes[0].GetAttribute("data-src")).ToList();

            foreach (var item in gameList)
            {
                var gameInfo = new GameInfoRetrostic
                {
                    Title = item.GetAttribute("title"),
                    BoxArt = baseUri.Append(gameImgs[gameList.IndexOf(item)] ?? ""),
                    DownloadPage = baseUri.Append(item.GetAttribute("href") ?? "")
                };

                result.Add(gameInfo);
            }

            //var findGame = gameLinks.Select(x => x.GetAttribute("title")).Where(x => x.IsNotNullOrEmpty()).Where(x => x.Contains(romName)).First();

            //var findGameLink = gameLinks.FirstOrDefault(x => x.GetAttribute("title").IsNotNullOrEmpty() && x.GetAttribute("title").Contains(romName)).GetAttribute("href");

            //var downloadGamePageLink = baseUri.Append(findGameLink.ToString());

            return result;

        }
        public async Task<byte[]?> DownloadGame(GameInfoRetrostic gameInfo)
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
            var canReportProgress = totalBytes != -1;

            using (var contentStream = await response.Content.ReadAsStreamAsync())
            using (var fileStream = new FileStream($"{gameInfo.Title}." + downloadLink.Split('.').Last(), FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
            {
                var buffer = new byte[8192];
                long totalBytesRead = 0;
                int bytesRead;
                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalBytesRead += bytesRead;

                    if (canReportProgress)
                    {
                        Debug.WriteLine($"Downloaded {totalBytesRead} of {totalBytes} bytes. {(totalBytesRead * 100 / totalBytes):0.00}% complete.");
                    }
                    else
                    {
                        Debug.WriteLine($"Downloaded {totalBytesRead} bytes.");
                    }
                }
            }

            return [0];
        }


    }
}
