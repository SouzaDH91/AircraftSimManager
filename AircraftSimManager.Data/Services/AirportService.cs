using AircraftSimManager.Data.Models;
using System.Net.Http;
using System.Text.Json;

namespace AircraftSimManager.Data.Services
{
	public class AirportService
	{
		private readonly DatabaseService _dbService;

		public AirportService(DatabaseService dbService)
		{
			_dbService = dbService;
		}

		public AirportInfo? GetAirport(string icao)
		{
			return _dbService.GetAirport(icao);
		}

		public double CalculateHaversineDistance(double lat1, double lon1, double lat2, double lon2)
		{
			const double EarthRadiusNM = 3440.065;
			double dLat = ToRadians(lat2 - lat1);
			double dLon = ToRadians(lon2 - lon1);

			double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
					   Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
					   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

			double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
			return Math.Round(EarthRadiusNM * c);
		}

		private double ToRadians(double val) => (Math.PI / 180) * val;
	}
}
