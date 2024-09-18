using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrimuiSmartHub.Application.Model
{
    public class GameData
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? System { get; set; }
        public string? RomName { get; set; }
        public string? Developer { get; set; }
        public string? Crc32 { get; set; }
        public string? Serial { get; set; }

        public GameData() { }

        public GameData(int id, string name, string system, string romName, string developer, string crc32, string serial)
        {
            Id = id;
            Name = name;
            System = system;
            RomName = romName;
            Developer = developer;
            Crc32 = crc32;
            Serial = serial;
        }
        public static GameData FromDataReader(SQLiteDataReader reader)
        {
            return new GameData
            {
                Id = reader["Id"] != DBNull.Value ? Convert.ToInt32(reader["Id"]) : 0,
                Name = reader["Name"] != DBNull.Value ? reader["Name"].ToString() : string.Empty,
                System = reader["System"] != DBNull.Value ? reader["System"].ToString() : string.Empty,
                RomName = reader["RomName"] != DBNull.Value ? reader["RomName"].ToString() : string.Empty,
                Developer = reader["Developer"] != DBNull.Value ? reader["Developer"].ToString() : string.Empty,
                Crc32 = reader["Crc32"] != DBNull.Value ? reader["Crc32"].ToString() : string.Empty,
                Serial = reader["Serial"] != DBNull.Value ? reader["Serial"].ToString() : string.Empty
            };
        }
    }

}
