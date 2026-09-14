namespace AircraftSimManager.Data.Models
{
	public class AircraftGroup
	{
		public string ModelName { get; set; } = string.Empty;
		public List<LiveryItem> Liveries { get; set; } = new();
	}
}
