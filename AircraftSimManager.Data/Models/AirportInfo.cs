using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AircraftSimManager.Data.Models
{
	public class AirportInfo
	{
		public string Icao { get; set; }
		public double Latitude { get; set; }
		public double Longitude { get; set; }

		public AirportInfo(string icao, double lat, double lon)
		{
			Icao = icao;
			Latitude = lat;
			Longitude = lon;
		}
	}
}
