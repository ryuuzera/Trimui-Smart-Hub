using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;
using System.IO;
using TrimuiSmartHub.Application.Model;

namespace TrimuiSmartHub.Application.Repository
{
    internal class GameRepository: IDisposable
    {
        private string connectionString { get; set; }
        private SQLiteConnection connection;

        private GameRepository()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string dataDirectory = Path.Combine(baseDirectory, "Data");
            string databasePath = Path.Combine(dataDirectory, "data.tsh");
            
            connectionString = $"Data Source={databasePath};Version=3;";

            connection = new SQLiteConnection(connectionString);

            connection.Open();
        }
        static public GameRepository New()
        {
            return new GameRepository();


        }

        public void Dispose()
        {
            if (connection != null)
            {
                connection.Close();
                connection.Dispose();
            }
        }

        public GameData? FindGameByRomName(string romName)
        {
            try
            {
                string query = "SELECT * FROM Games WHERE romName = @romName;";

                using var command = new SQLiteCommand(query, connection);

                command.Parameters.AddWithValue("@romName", romName);

                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read()) return GameData.FromDataReader(reader);

                    return null;
                }
            }
            catch (Exception ex)
            {
                return null;
            }
        }
    }
}
