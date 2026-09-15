using AircraftSimManager.Data.Entities;
using AircraftSimManager.Data.Models;
using Microsoft.Data.Sqlite;
using System.Globalization;
using System.IO;
using System.Net.Http;

namespace AircraftSimManager.Data.Services
{
	public class DatabaseService
	{
		private readonly string _dbPath;
		private static readonly HttpClient _httpClient = new HttpClient();
		private const string OurAirportsCsvUrl = "https://davidmegginson.github.io/ourairports-data/airports.csv";

		public DatabaseService()
		{
			string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AircraftSimManager");
			Directory.CreateDirectory(folder);
			_dbPath = Path.Combine(folder, "appdata.db");

			InitializeDatabase();
		}

		private SqliteConnection Connection(bool connectionAsync = false)
		{
			var connection = new SqliteConnection($"Data Source={_dbPath}");
			if (!connectionAsync)
			{
				connection.Open();
			}

			return connection;
		}

		private void InitializeDatabase()
		{
			using var connection = Connection();

			// Tabela de Aeroportos Mundial
			string createAirports = @"
            CREATE TABLE IF NOT EXISTS airports (
                icao TEXT PRIMARY KEY,
                latitude REAL NOT NULL,
                longitude REAL NOT NULL
            );";

			// Tabela de Histórico de Voos (incluindo a coluna aircraft_name)
			string createHistory = @"
            CREATE TABLE IF NOT EXISTS flight_history (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                aircraft_name TEXT,
                origin TEXT NOT NULL,
                destination TEXT NOT NULL,
                alternate TEXT,
                distance_nm REAL,
                passengers REAL,
                cargo_kg REAL,
                total_fuel_kg REAL,
                date_calculated TEXT
            );";

			using var cmd1 = new SqliteCommand(createAirports, connection);
			cmd1.ExecuteNonQuery();

			using var cmd2 = new SqliteCommand(createHistory, connection);
			cmd2.ExecuteNonQuery();

			// Garante que a coluna aircraft_name exista se o banco já tiver sido criado anteriormente
			try
			{
				string alterTable = "ALTER TABLE flight_history ADD COLUMN aircraft_name TEXT;";
				using var cmdAlter = new SqliteCommand(alterTable, connection);
				cmdAlter.ExecuteNonQuery();
			}
			catch
			{
				// Ignora o erro se a coluna já existir
			}

			Task.Run(async () => await SeedAirportsFromCsvAsync());
		}

		private async Task SeedAirportsFromCsvAsync()
		{
			try
			{
				using var connection = Connection(true);
				await connection.OpenAsync();

				string countQuery = "SELECT COUNT(*) FROM airports;";
				using var countCmd = new SqliteCommand(countQuery, connection);
				long count = (long)(await countCmd.ExecuteScalarAsync() ?? 0);

				if (count > 0) return;

				var response = await _httpClient.GetAsync(OurAirportsCsvUrl);
				if (!response.IsSuccessStatusCode) return;

				using var stream = await response.Content.ReadAsStreamAsync();
				using var reader = new StreamReader(stream);

				string? headerLine = await reader.ReadLineAsync();
				if (headerLine == null) return;

				var headers = parseCsvLine(headerLine);
				int identIdx = headers.IndexOf("ident");
				int latIdx = headers.IndexOf("latitude_deg");
				int lonIdx = headers.IndexOf("longitude_deg");

				if (identIdx == -1 || latIdx == -1 || lonIdx == -1) return;

				using var transaction = connection.BeginTransaction();
				string insertQuery = "INSERT OR IGNORE INTO airports (icao, latitude, longitude) VALUES (@icao, @lat, @lon);";

				using var cmd = new SqliteCommand(insertQuery, connection, transaction);
				var pIcao = cmd.Parameters.Add("@icao", SqliteType.Text);
				var pLat = cmd.Parameters.Add("@lat", SqliteType.Real);
				var pLon = cmd.Parameters.Add("@lon", SqliteType.Real);

				string? line;
				while ((line = await reader.ReadLineAsync()) != null)
				{
					var cols = parseCsvLine(line);
					if (cols.Count > Math.Max(identIdx, Math.Max(latIdx, lonIdx)))
					{
						string icao = cols[identIdx].Trim().ToUpper();

						if (icao.Length >= 3 && icao.Length <= 4 &&
							double.TryParse(cols[latIdx], NumberStyles.Any, CultureInfo.InvariantCulture, out double lat) &&
							double.TryParse(cols[lonIdx], NumberStyles.Any, CultureInfo.InvariantCulture, out double lon))
						{
							pIcao.Value = icao;
							pLat.Value = lat;
							pLon.Value = lon;
							await cmd.ExecuteNonQueryAsync();
						}
					}
				}

				await transaction.CommitAsync();
			}
			catch (Exception)
			{
			}
		}

		private List<string> parseCsvLine(string line)
		{
			var result = new List<string>();
			bool inQuotes = false;
			string current = "";

			foreach (char c in line)
			{
				if (c == '"')
				{
					inQuotes = !inQuotes;
				}
				else if (c == ',' && !inQuotes)
				{
					result.Add(current);
					current = "";
				}
				else
				{
					current += c;
				}
			}
			result.Add(current);
			return result;
		}

		public AirportInfo? GetAirport(string icao)
		{
			if (string.IsNullOrWhiteSpace(icao)) return null;

			using var connection = Connection();

			string query = "SELECT icao, latitude, longitude FROM airports WHERE icao = @icao LIMIT 1;";
			using var cmd = new SqliteCommand(query, connection);
			cmd.Parameters.AddWithValue("@icao", icao.ToUpper().Trim());

			using var reader = cmd.ExecuteReader();
			if (reader.Read())
			{
				return new AirportInfo(
					reader.GetString(0),
					reader.GetDouble(1),
					reader.GetDouble(2)
				);
			}

			return null;
		}

		public void SaveFlight(FlightRecord flight)
		{
			using var connection = Connection();

			string insertQuery = @"
            INSERT INTO flight_history 
            (aircraft_name, origin, destination, alternate, distance_nm, passengers, cargo_kg, total_fuel_kg, date_calculated)
            VALUES (@aircraft, @origin, @destination, @alternate, @distance, @pax, @cargo, @fuel, @date);";

			using var cmd = new SqliteCommand(insertQuery, connection);
			cmd.Parameters.AddWithValue("@aircraft", flight.AircraftName ?? string.Empty);
			cmd.Parameters.AddWithValue("@origin", flight.Origin ?? string.Empty);
			cmd.Parameters.AddWithValue("@destination", flight.Destination ?? string.Empty);
			cmd.Parameters.AddWithValue("@alternate", flight.Alternate ?? string.Empty);
			cmd.Parameters.AddWithValue("@distance", flight.DistanceNM);
			cmd.Parameters.AddWithValue("@pax", flight.Passengers);
			cmd.Parameters.AddWithValue("@cargo", flight.CargoKg);
			cmd.Parameters.AddWithValue("@fuel", flight.TotalFuelKg);
			cmd.Parameters.AddWithValue("@date", DateTime.Now.ToString("dd/MM/yyyy HH:mm"));

			cmd.ExecuteNonQuery();
		}

		public List<FlightRecord> GetFlightHistory()
		{
			var history = new List<FlightRecord>();
			using var connection = Connection();

			string query = "SELECT id, aircraft_name, origin, destination, alternate, distance_nm, passengers, cargo_kg, total_fuel_kg, date_calculated FROM flight_history ORDER BY id DESC LIMIT 20;";
			using var cmd = new SqliteCommand(query, connection);
			using var reader = cmd.ExecuteReader();

			while (reader.Read())
			{
				history.Add(new FlightRecord
				{
					Id = reader.GetInt32(0),
					AircraftName = reader.IsDBNull(1) ? "" : reader.GetString(1),
					Origin = reader.IsDBNull(2) ? "" : reader.GetString(2),
					Destination = reader.IsDBNull(3) ? "" : reader.GetString(3),
					Alternate = reader.IsDBNull(4) ? "" : reader.GetString(4),
					DistanceNM = reader.GetDouble(5),
					Passengers = reader.GetDouble(6),
					CargoKg = reader.GetDouble(7),
					TotalFuelKg = reader.GetDouble(8),
					DateCalculated = reader.IsDBNull(9) ? "" : reader.GetString(9)
				});
			}

			return history;
		}

		// Método de exclusão ajustado para usar SqliteCommand nativo
		public void DeleteFlight(int id)
		{
			using var connection = Connection();
			string deleteQuery = "DELETE FROM flight_history WHERE id = @id;";

			using var cmd = new SqliteCommand(deleteQuery, connection);
			cmd.Parameters.AddWithValue("@id", id);
			cmd.ExecuteNonQuery();
		}
	}
}
