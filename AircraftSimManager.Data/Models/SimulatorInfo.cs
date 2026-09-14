using static AircraftSimManager.Utils.Enumerators;

namespace AircraftSimManager.Data.Models
{
	public class SimulatorInfo
	{
		public SimulatorType Type { get; set; }
		public string Name { get; set; } = string.Empty;
		public string RegistryPath { get; set; } = string.Empty;
		public string SubPath { get; set; } = string.Empty;
	}
}
