using AircraftSimManager.Commands;
using AircraftSimManager.Data.Entities;
using AircraftSimManager.Data.Models;
using AircraftSimManager.Data.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace AircraftSimManager.ViewModels
{
	public class FuelCalculatorViewModel : ViewModelBase
	{
		private readonly DatabaseService _dbService;
		private readonly AirportService _airportService;
		private readonly ConfigService _configService;
		private AircraftModel _selectedAircraft;

		private const double KgToLbs = 2.20462;

		private string _originIcao;
		private string _destinationIcao;
		private string _alternateIcao;

		private double _flightDistanceNM;
		private double _passengerCount = 132;
		private double _cargoWeightKg = 1200;
		private double _cruiseBurnPerHourKg = 2400;

		private bool _isLbsSelected;
		private double _totalBlockFuelKg;
		private double _leftTankFuel;
		private double _centerTankFuel;
		private double _rightTankFuel;

		public double MaxPassengers => SelectedAircraft?.MaxPassengers ?? 180;
		public double MaxCargoKg => SelectedAircraft?.MaxCargoKg ?? 4500;

		public double PaxOccupancyPercentage => MaxPassengers > 0 ? Math.Min(100, (PassengerCount / MaxPassengers) * 100) : 0;
		public double CargoOccupancyPercentage => MaxCargoKg > 0 ? Math.Min(100, (CargoWeightKg / MaxCargoKg) * 100) : 0;

		public ObservableCollection<FlightRecord> FlightHistory { get; set; } = new();
		public ObservableCollection<AircraftModel> AvailableAircraft { get; set; }

		public ICommand SaveFlightCommand { get; }

		#region Properties (Getters/Setters)
		public string OriginIcao
		{
			get => _originIcao;
			set { _originIcao = value?.ToUpper(); OnPropertyChanged(); AutoCalculateDistance(); RecalculateAll(); }
		}

		public string DestinationIcao
		{
			get => _destinationIcao;
			set { _destinationIcao = value?.ToUpper(); OnPropertyChanged(); AutoCalculateDistance(); RecalculateAll(); }
		}

		public string AlternateIcao
		{
			get => _alternateIcao;
			set { _alternateIcao = value?.ToUpper(); OnPropertyChanged(); RecalculateAll(); }
		}

		public double FlightDistanceNM
		{
			get => _flightDistanceNM;
			set { _flightDistanceNM = value; OnPropertyChanged(); RecalculateAll(); }
		}

		public double PassengerCount
		{
			get => _passengerCount;
			set
			{
				_passengerCount = Math.Min(value, MaxPassengers); // Trava no limite
				OnPropertyChanged();
				OnPropertyChanged(nameof(PaxOccupancyPercentage));
				RecalculateAll();
			}
		}

		public double CargoWeightKg
		{
			get => _cargoWeightKg;
			set
			{
				_cargoWeightKg = Math.Min(value, MaxCargoKg); // Trava no limite
				OnPropertyChanged();
				OnPropertyChanged(nameof(CargoOccupancyPercentage));
				RecalculateAll();
			}
		}

		public bool IsLbsSelected
		{
			get => _isLbsSelected;
			set
			{
				if (_isLbsSelected != value)
				{
					_isLbsSelected = value;
					_configService.WeightUnit = value ? "LBS" : "KG";
					_configService.SaveSettings();
					OnPropertyChanged();
					OnPropertyChanged(nameof(UnitLabel));
					RecalculateAll();
				}
			}
		}

		public string UnitLabel => IsLbsSelected ? "lbs" : "kg";

		public double TotalBlockFuelDisplay => IsLbsSelected ? _totalBlockFuelKg * KgToLbs : _totalBlockFuelKg;
		public double LeftTankFuelDisplay => IsLbsSelected ? _leftTankFuel * KgToLbs : _leftTankFuel;
		public double CenterTankFuelDisplay => IsLbsSelected ? _centerTankFuel * KgToLbs : _centerTankFuel;
		public double RightTankFuelDisplay => IsLbsSelected ? _rightTankFuel * KgToLbs : _rightTankFuel;
		#endregion

		public FuelCalculatorViewModel()
		{
			_dbService = new DatabaseService();
			_airportService = new AirportService(_dbService);
			_configService = new ConfigService();

			// Popula lista de aeronaves disponíveis
			AvailableAircraft = new ObservableCollection<AircraftModel>
			{
				new AircraftModel
				{
					Name = "Boeing 737-700",
					MaxPassengers = 149,
					MaxCargoKg = 4000,
					CruiseBurnPerHourKg = 2200,
					WingTanksCapacityKg = 7800,   // ~3.900 kg por asa
					CenterTankCapacityKg = 13000
				},
				new AircraftModel
				{
					Name = "Boeing 737-800",
					MaxPassengers = 180,
					MaxCargoKg = 4500,
					CruiseBurnPerHourKg = 2400,
					WingTanksCapacityKg = 7800,   // ~3.900 kg por asa
					CenterTankCapacityKg = 13000
				},
				new AircraftModel
				{
					Name = "Airbus A320neo",
					MaxPassengers = 174,
					MaxCargoKg = 4000,
					CruiseBurnPerHourKg = 2000,
					WingTanksCapacityKg = 12500,  // Tanques das asas + outer cells
					CenterTankCapacityKg = 6500
				},
				new AircraftModel
				{
					Name = "ATR 72-600",
					MaxPassengers = 72,
					MaxCargoKg = 1500,
					CruiseBurnPerHourKg = 650,
					WingTanksCapacityKg = 5000,   // Todo o combustível fica nas asas
					CenterTankCapacityKg = 0
				},
				new AircraftModel
				{
					Name = "Boeing 777-300ER",
					MaxPassengers = 396,
					MaxCargoKg = 20000,
					CruiseBurnPerHourKg = 7500,
					WingTanksCapacityKg = 58000,  // ~29.000 kg por asa
					CenterTankCapacityKg = 87500
				}
			};

			_selectedAircraft = AvailableAircraft[0]; // Padrão B737-800

			SaveFlightCommand = new RelayCommand(ExecuteSaveFlight);

			AutoCalculateDistance();
			RecalculateAll();
			LoadHistory();
		}

		public AircraftModel SelectedAircraft
		{
			get => _selectedAircraft;
			set
			{
				if (_selectedAircraft != value && value != null)
				{
					_selectedAircraft = value;
					OnPropertyChanged();

					// Ajusta passageiros e carga se excederem o novo limite
					if (PassengerCount > MaxPassengers) PassengerCount = MaxPassengers;
					if (CargoWeightKg > MaxCargoKg) CargoWeightKg = MaxCargoKg;

					// Notifica mudanças nos limites e porcentagens
					OnPropertyChanged(nameof(MaxPassengers));
					OnPropertyChanged(nameof(MaxCargoKg));
					OnPropertyChanged(nameof(PaxOccupancyPercentage));
					OnPropertyChanged(nameof(CargoOccupancyPercentage));

					RecalculateAll();
				}
			}
		}

		private void AutoCalculateDistance()
		{
			if (!string.IsNullOrEmpty(OriginIcao) && !string.IsNullOrEmpty(DestinationIcao))
			{
				var origin = _airportService.GetAirport(OriginIcao);
				var dest = _airportService.GetAirport(DestinationIcao);

				if (origin != null && dest != null)
				{
					_flightDistanceNM = _airportService.CalculateHaversineDistance(origin.Latitude, origin.Longitude, dest.Latitude, dest.Longitude);
					OnPropertyChanged(nameof(FlightDistanceNM));
				}
			}
		}

		private void RecalculateAll()
		{
			if (SelectedAircraft == null) return;

			double averageSpeedKnots = 430;
			double flightTimeHours = FlightDistanceNM > 0 ? FlightDistanceNM / averageSpeedKnots : 0;

			// Utiliza o CruiseBurnPerHourKg da aeronave selecionada
			double tripFuel = (flightTimeHours * SelectedAircraft.CruiseBurnPerHourKg) + 350;
			double contingencyFuel = tripFuel * 0.05;
			double alternateFuel = (80.0 / averageSpeedKnots) * SelectedAircraft.CruiseBurnPerHourKg;
			double holdingFuel = SelectedAircraft.CruiseBurnPerHourKg * 0.5;

			_totalBlockFuelKg = Math.Ceiling(tripFuel + contingencyFuel + alternateFuel + holdingFuel);

			// Distribuição de combustível usando o limite das asas da aeronave atual
			double maxWing = SelectedAircraft.TotalFuelCapacityKg;
			double halfFuel = _totalBlockFuelKg / 2;

			if (halfFuel <= maxWing)
			{
				_leftTankFuel = halfFuel;
				_rightTankFuel = halfFuel;
				_centerTankFuel = 0;
			}
			else
			{
				_leftTankFuel = maxWing;
				_rightTankFuel = maxWing;
				_centerTankFuel = _totalBlockFuelKg - (maxWing * 2);
			}

			OnPropertyChanged(nameof(TotalBlockFuelDisplay));
			OnPropertyChanged(nameof(LeftTankFuelDisplay));
			OnPropertyChanged(nameof(CenterTankFuelDisplay));
			OnPropertyChanged(nameof(RightTankFuelDisplay));
		}

		private void ExecuteSaveFlight(object obj)
		{
			var record = new FlightRecord
			{
				Origin = OriginIcao,
				Destination = DestinationIcao,
				Alternate = AlternateIcao,
				DistanceNM = FlightDistanceNM,
				Passengers = PassengerCount,
				CargoKg = CargoWeightKg,
				TotalFuelKg = _totalBlockFuelKg
			};

			_dbService.SaveFlight(record);
			LoadHistory();
		}

		private void LoadHistory()
		{
			FlightHistory.Clear();
			var items = _dbService.GetFlightHistory();
			foreach (var item in items)
			{
				FlightHistory.Add(item);
			}
		}
	}
}
