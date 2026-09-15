namespace AircraftSimManager.Data.Models
{
	public class AircraftModel
	{
		public string Name { get; set; } = string.Empty;
		public int MaxPassengers { get; set; }
		public double MaxCargoKg { get; set; }
		public double CruiseBurnPerHourKg { get; set; }

		// Capacidade combinada dos tanques das asas (Asa Esquerda + Asa Direita)
		public double WingTanksCapacityKg { get; set; }

		// Capacidade do tanque central (f fuselagem)
		public double CenterTankCapacityKg { get; set; }

		// Propriedade calculada para a capacidade total de combustível
		public double TotalFuelCapacityKg => WingTanksCapacityKg + CenterTankCapacityKg;
	}
}
