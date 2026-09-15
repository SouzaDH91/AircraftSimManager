namespace AircraftSimManager.Data.Entities
{
	public class FlightRecord
	{
		public int Id { get; set; }
		public string Origin { get; set; } = string.Empty;
		public string Destination { get; set; } = string.Empty;
		public string Alternate { get; set; } = string.Empty;
		public double DistanceNM { get; set; }
		public double Passengers { get; set; }
		public double CargoKg { get; set; }
		public double TotalFuelKg { get; set; }
		public string DateCalculated { get; set; } = string.Empty;
	}
}
