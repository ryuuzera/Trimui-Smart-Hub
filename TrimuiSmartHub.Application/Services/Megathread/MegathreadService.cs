using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using TrimuiSmartHub.Application.Repository;
using TrimuiSmartHub.Application.Services.LibRetro;

namespace TrimuiSmartHub.Application.Services.Megathread
{
    class MegathreadService : IDisposable
    {
        private readonly HttpClient httpClient;

        private readonly Uri baseUri = new("https://r-roms.github.io/megathread/nintendo/");

        private readonly GameRepository gameRepository;

        public static MegathreadService New()
        {
            return new MegathreadService();
        }

        private MegathreadService()
        {
            httpClient = new HttpClient();

            httpClient.Timeout = TimeSpan.FromSeconds(15);

            gameRepository = GameRepository.New();
        }
        public void Dispose()
        {
            httpClient.Dispose();
        }

        //public byte[]? DownloadGame(string emulator, string romName)
        //{

        //}

        
    } 
}
