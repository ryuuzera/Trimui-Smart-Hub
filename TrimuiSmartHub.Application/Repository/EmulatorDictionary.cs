using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrimuiSmartHub.Application.Repository
{
    internal static class EmulatorDictionary
    {
        public static Dictionary<string, string> EmulatorMap = new()
        {
            { "ARCADE", "MAME" },
            { "ATARI2600", "Atari - 2600" },
            { "ATARI7800", "Atari - 7800" },
            { "CPS1", "MAME" },
            { "CPS2", "MAME" },
            { "CPS3", "MAME" },
            { "DC", "Sega - Dreamcast" },
            { "FBNEO", "FBNeo - Arcade Games" },
            { "FC", "Nintendo - Nintendo Entertainment System" },
            { "GB", "Nintendo - Game Boy" },
            { "GBA", "Nintendo - Game Boy Advance" },
            { "GBC", "Nintendo - Game Boy Color" },
            { "MAME2003PLUS", "MAME" }, 
            { "MD", "Sega - Mega Drive - Genesis" },
            { "MS", "Sega - Master System - Mark III" },
            { "N64", "Nintendo - Nintendo 64" },
            { "NDS", "Nintendo - Nintendo DS" },
            { "NEOGEO", "SNK - Neo Geo" },
            { "NGP", "SNK - Neo Geo Pocket" },
            { "PCE", "NEC - PC Engine - TurboGrafx 16" },
            { "PGM", "IGS - PolyGameMaster" }, 
            { "PPSSPP", "Sony - PlayStation Portable" }, 
            { "PS", "Sony - PlayStation" },
            { "SFC", "Nintendo - Super Nintendo Entertainment System" },
            { "WSC", "Bandai - WonderSwan Color" },
            { "GG", "Sega - Game Gear" },
            { "SS", "Sega - Saturn" },
            { "MAME", "MAME" }
        };
    }
}
