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

        public static Dictionary<string, string> EmulatorMapRetrostic = new()
        {
            { "ARCADE", "https://www.retrostic.com/roms/mame" },
            { "ATARI2600", "https://www.retrostic.com/roms/atari-2600" },
            { "ATARI7800", "https://www.retrostic.com/roms/atari-7800" },
            { "CPS1", "https://www.retrostic.com/roms/cps-1" },
            { "CPS2", "https://www.retrostic.com/roms/cps-2" },
            { "CPS3", "https://www.retrostic.com/roms/cps-3" },
            { "DC", "https://www.retrostic.com/roms/dreamcast" },
            //{ "FBNEO", "https://archive.org/download/fbnarcade-fullnonmerged/arcade/" },
            { "FC", "https://www.retrostic.com/roms/nes" },
            { "GB", "https://www.retrostic.com/roms/gb" },
            { "GBA", "https://www.retrostic.com/roms/gba" },
            { "GBC", "https://www.retrostic.com/roms/gbc" },
            { "MAME2003PLUS", "https://www.retrostic.com/roms/mame" },
            { "MD", "https://www.retrostic.com/roms/megadrive" },
            { "MS", "https://www.retrostic.com/roms/master-system" },
            { "N64", "https://www.retrostic.com/roms/n64" },
            { "NDS", "https://www.retrostic.com/roms/nds" },
            { "NEOGEO", "https://www.retrostic.com/roms/neo-geo" },
            { "NGP", "https://www.retrostic.com/roms/neo-geo-pocket" },
            { "PCE", "https://www.retrostic.com/roms/turbografx16" },
            //{ "PGM", "IGS - PolyGameMaster" },
            { "PPSSPP", "https://www.retrostic.com/roms/psp" },
            { "PS", "https://www.retrostic.com/roms/ps-1" },
            { "SFC", "https://www.retrostic.com/roms/snes" },
            { "WSC", "https://www.retrostic.com/roms/wonderswan-color" },
            { "GG", "https://www.retrostic.com/roms/game-gear" },
            { "SS", "https://www.retrostic.com/roms/saturn" },
            { "MAME", "https://www.retrostic.com/roms/mame" }
        };

        public static Dictionary<string, string> EmulatorMapSystem = new()
        {
            { "ARCADE", "Multiple Arcade Machine Emulator" },
            { "ATARI2600", "Atari 2600" },
            { "ATARI7800", "Atari 7800" },
            { "CPS1", "Capcom Play System 1" },
            { "CPS2", "Capcom Play System 2" },
            { "CPS3", "Capcom Play System 3" },
            { "DC", "Sega Dreamcast" },
            //{ "FBNEO", "https://archive.org/download/fbnarcade-fullnonmerged/arcade/" },
            { "FC", "Nintendo Entertainment System" },
            { "GB", "Nintendo Game Boy" },
            { "GBA", "Nintendo Game Boy Advance" },
            { "GBC", "Nintendo Game Boy Color" },
            { "MAME2003PLUS", "Multiple Arcade Machine Emulator" },
            { "MD", "Sega Mega Drive" },
            { "MS", "Sega Master System" },
            { "N64", "Nintendo 64" },
            { "NDS", "Nintendo DS" },
            { "NEOGEO", "Neo Geo" },
            { "NGP", "Neo Geo Pocket" },
            { "PCE", "PC Engine - TurboGrafx16" },
            //{ "PGM", "IGS - PolyGameMaster" },
            { "PPSSPP", "Playstation Portable" },
            { "PS", "Sony PSX/PlayStation 1" },
            { "SFC", "Super Nintendo Entertainment System" },
            { "WSC", "Bandai Wonderswan Color" },
            { "GG", "Sega Game Gear" },
            { "SS", "Sega Saturn" },
            { "MAME", "Multiple Arcade Machine Emulator" }
        };
    }
}
