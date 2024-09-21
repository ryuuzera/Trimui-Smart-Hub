using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrimuiSmartHub.Application.Model
{
    public class GameInfoRetrostic
    {
        public string Title { get; set; }
        public Uri? BoxArt { get; set; }
        public string? Region { get; set; }
        public Uri? DownloadPage { get; set; }
    }
}
