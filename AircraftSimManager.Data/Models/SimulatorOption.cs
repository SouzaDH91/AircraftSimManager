using static AircraftSimManager.Utils.Enumerators;

namespace AircraftSimManager.Data.Models
{
	public class SimulatorOption
	{
		public string Name { get; set; } = string.Empty;
		public SimulatorType Type { get; set; }
		public string DefaultAirplanesPath { get; set; } = string.Empty;
		public string Path { get; set; } = string.Empty;
		public bool IsInstalled { get; set; }
	}
}
