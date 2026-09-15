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

		private void InitializeDatabase()
		{
			using var connection = new SqliteConnection($"Data Source={_dbPath}");
			connection.Open();

			// Tabela de Aeroportos Mundial (INDEX no ICAO otimiza buscas instantâneas)
			string createAirports = @"
                CREATE TABLE IF NOT EXISTS airports (
                    icao TEXT PRIMARY KEY,
                    latitude REAL NOT NULL,
                    longitude REAL NOT NULL
                );";

			// Tabela de Histórico de Voos
			string createHistory = @"
                CREATE TABLE IF NOT EXISTS flight_history (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
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

			// Importa o CSV do OurAirports em background se a base estiver vazia
			Task.Run(async () => await SeedAirportsFromCsvAsync());
		}

		private async Task SeedAirportsFromCsvAsync()
		{
			try
			{
				using var connection = new SqliteConnection($"Data Source={_dbPath}");
				await connection.OpenAsync();

				// Verifica se já existem aeroportos cadastrados
				string countQuery = "SELECT COUNT(*) FROM airports;";
				using var countCmd = new SqliteCommand(countQuery, connection);
				long count = (long)(await countCmd.ExecuteScalarAsync() ?? 0);

				if (count > 0) return; // Se já foi populado, ignora o download

				// Download do arquivo CSV
				var response = await _httpClient.GetAsync(OurAirportsCsvUrl);
				if (!response.IsSuccessStatusCode) return;

				using var stream = await response.Content.ReadAsStreamAsync();
				using var reader = new StreamReader(stream);

				string? headerLine = await reader.ReadLineAsync();
				if (headerLine == null) return;

				// Identifica o índice das colunas necessárias no cabeçalho do CSV
				var headers = parseCsvLine(headerLine);
				int identIdx = headers.IndexOf("ident");
				int latIdx = headers.IndexOf("latitude_deg");
				int lonIdx = headers.IndexOf("longitude_deg");

				if (identIdx == -1 || latIdx == -1 || lonIdx == -1) return;

				// Inicia transação SQLite em lote (essencial para altíssima performance)
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

						// Garante que só salvaremos ICAOs válidos de 3 a 4 caracteres
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
				// Trata falha no download/importação sem travar a aplicação
			}
		}

		// Parser simples de linha CSV respeitando aspas
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

			using var connection = new SqliteConnection($"Data Source={_dbPath}");
			connection.Open();

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
			using var connection = new SqliteConnection($"Data Source={_dbPath}");
			connection.Open();

			string insertQuery = @"
                INSERT INTO flight_history 
                (origin, destination, alternate, distance_nm, passengers, cargo_kg, total_fuel_kg, date_calculated)
                VALUES (@origin, @destination, @alternate, @distance, @pax, @cargo, @fuel, @date);";

			using var cmd = new SqliteCommand(insertQuery, connection);
			cmd.Parameters.AddWithValue("@origin", flight.Origin);
			cmd.Parameters.AddWithValue("@destination", flight.Destination);
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
			using var connection = new SqliteConnection($"Data Source={_dbPath}");
			connection.Open();

			string query = "SELECT id, origin, destination, alternate, distance_nm, passengers, cargo_kg, total_fuel_kg, date_calculated FROM flight_history ORDER BY id DESC LIMIT 20;";
			using var cmd = new SqliteCommand(query, connection);
			using var reader = cmd.ExecuteReader();

			while (reader.Read())
			{
				history.Add(new FlightRecord
				{
					Id = reader.GetInt32(0),
					Origin = reader.GetString(1),
					Destination = reader.GetString(2),
					Alternate = reader.IsDBNull(3) ? "" : reader.GetString(3),
					DistanceNM = reader.GetDouble(4),
					Passengers = reader.GetDouble(5),
					CargoKg = reader.GetDouble(6),
					TotalFuelKg = reader.GetDouble(7),
					DateCalculated = reader.GetString(8)
				});
			}

			return history;
		}
	}
}
