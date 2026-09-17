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
		private double _passengerCount;
		private double _cargoWeightKg;

		private bool _isLbsSelected;
		private double _totalBlockFuelKg;
		private double _leftTankFuel;
		private double _centerTankFuel;
		private double _rightTankFuel;

		private FlightRecord _selectedFlightRecord;

		public ObservableCollection<FlightRecord> FlightHistory { get; set; } = new();
		public ObservableCollection<AircraftModel> AvailableAircraft { get; set; }

		public ICommand SaveFlightCommand { get; }
		public ICommand DeleteFlightCommand { get; }

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
				_passengerCount = Math.Min(value, MaxPassengers);
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
				_cargoWeightKg = Math.Min(value, MaxCargoKg);
				OnPropertyChanged();
				OnPropertyChanged(nameof(CargoWeightDisplay));
				OnPropertyChanged(nameof(CargoOccupancyPercentage));
				RecalculateAll();
			}
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

					if (PassengerCount > MaxPassengers) PassengerCount = MaxPassengers;
					if (CargoWeightKg > MaxCargoKg) CargoWeightKg = MaxCargoKg;

					OnPropertyChanged(nameof(MaxPassengers));
					OnPropertyChanged(nameof(MaxCargoKg));
					OnPropertyChanged(nameof(MaxCargoDisplay));
					OnPropertyChanged(nameof(PaxOccupancyPercentage));
					OnPropertyChanged(nameof(CargoOccupancyPercentage));

					RecalculateAll();
				}
			}
		}

		// Carrega os dados do voo selecionado na UI
		public FlightRecord SelectedFlightRecord
		{
			get => _selectedFlightRecord;
			set
			{
				_selectedFlightRecord = value;
				OnPropertyChanged();
				if (_selectedFlightRecord != null)
				{
					LoadSelectedFlightRecord(_selectedFlightRecord);
				}
			}
		}

		public double LeftTankFuel
		{
			get => _leftTankFuel;
			set { _leftTankFuel = value; OnPropertyChanged(nameof(LeftTankFuel)); OnPropertyChanged(nameof(LeftTankFuelDisplay)); }
		}

		public double CenterTankFuel
		{
			get => _centerTankFuel;
			set { _centerTankFuel = value; OnPropertyChanged(nameof(CenterTankFuel)); OnPropertyChanged(nameof(CenterTankFuelDisplay)); }
		}

		public double RightTankFuel
		{
			get => _rightTankFuel;
			set { _rightTankFuel = value; OnPropertyChanged(nameof(RightTankFuel)); OnPropertyChanged(nameof(RightTankFuelDisplay)); }
		}

		public double TotalBlockFuel
		{
			get => _totalBlockFuelKg;
			set
			{
				_totalBlockFuelKg = value;
				OnPropertyChanged(nameof(TotalBlockFuel));
				OnPropertyChanged(nameof(TotalBlockFuelDisplay));
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
					OnPropertyChanged(nameof(TotalBlockFuelDisplay));
					OnPropertyChanged(nameof(LeftTankFuelDisplay));
					OnPropertyChanged(nameof(CenterTankFuelDisplay));
					OnPropertyChanged(nameof(RightTankFuelDisplay));
					OnPropertyChanged(nameof(CargoWeightDisplay));
					OnPropertyChanged(nameof(MaxCargoDisplay));
				}
			}
		}

		// Conversões e rótulos dinâmicos (KG / LBS)
		public string UnitLabel => IsLbsSelected ? "lbs" : "kg";

		public double CargoWeightDisplay => IsLbsSelected ? _cargoWeightKg * KgToLbs : _cargoWeightKg;
		public double MaxCargoDisplay => IsLbsSelected ? MaxCargoKg * KgToLbs : MaxCargoKg;

		public double TotalBlockFuelDisplay => IsLbsSelected ? _totalBlockFuelKg * KgToLbs : _totalBlockFuelKg;
		public double LeftTankFuelDisplay => IsLbsSelected ? _leftTankFuel * KgToLbs : _leftTankFuel;
		public double CenterTankFuelDisplay => IsLbsSelected ? _centerTankFuel * KgToLbs : _centerTankFuel;
		public double RightTankFuelDisplay => IsLbsSelected ? _rightTankFuel * KgToLbs : _rightTankFuel;

		public double MaxPassengers => SelectedAircraft?.MaxPassengers ?? 180;
		public double MaxCargoKg => SelectedAircraft?.MaxCargoKg ?? 4500;

		public double PaxOccupancyPercentage => MaxPassengers > 0 ? Math.Min(100, (PassengerCount / MaxPassengers) * 100) : 0;
		public double CargoOccupancyPercentage => MaxCargoKg > 0 ? Math.Min(100, (CargoWeightKg / MaxCargoKg) * 100) : 0;
		#endregion

		public FuelCalculatorViewModel()
		{
			_dbService = new DatabaseService();
			_airportService = new AirportService(_dbService);
			_configService = new ConfigService();

			AvailableAircraft = new ObservableCollection<AircraftModel>
		{
			new AircraftModel { Name = "Boeing 737-700", MaxPassengers = 149, MaxCargoKg = 4000, CruiseBurnPerHourKg = 2200, WingTanksCapacityKg = 7800, CenterTankCapacityKg = 13000 },
			new AircraftModel { Name = "Boeing 737-800", MaxPassengers = 180, MaxCargoKg = 4500, CruiseBurnPerHourKg = 2400, WingTanksCapacityKg = 7800, CenterTankCapacityKg = 13000 },
			new AircraftModel { Name = "Airbus A320neo", MaxPassengers = 174, MaxCargoKg = 4000, CruiseBurnPerHourKg = 2000, WingTanksCapacityKg = 12500, CenterTankCapacityKg = 6500 },
			new AircraftModel { Name = "ATR 72-600", MaxPassengers = 72, MaxCargoKg = 1500, CruiseBurnPerHourKg = 650, WingTanksCapacityKg = 5000, CenterTankCapacityKg = 0 },
			new AircraftModel { Name = "Boeing 777-300ER", MaxPassengers = 396, MaxCargoKg = 20000, CruiseBurnPerHourKg = 7500, WingTanksCapacityKg = 58000, CenterTankCapacityKg = 87500 }
		};

			_selectedAircraft = AvailableAircraft[0];

			SaveFlightCommand = new RelayCommand(ExecuteSaveFlight);
			DeleteFlightCommand = new RelayCommand(ExecuteDeleteFlight);

			AutoCalculateDistance();
			RecalculateAll();
			LoadHistory();
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

		private void CalculateFuelDistribution(double requiredFuelKg)
		{
			if (SelectedAircraft == null) return;

			double maxWingCapacity = SelectedAircraft.WingTanksCapacityKg;
			double maxSingleWingCapacity = maxWingCapacity / 2;

			if (requiredFuelKg <= maxWingCapacity)
			{
				LeftTankFuel = requiredFuelKg / 2;
				RightTankFuel = requiredFuelKg / 2;
				CenterTankFuel = 0;
			}
			else
			{
				LeftTankFuel = maxSingleWingCapacity;
				RightTankFuel = maxSingleWingCapacity;
				CenterTankFuel = requiredFuelKg - maxWingCapacity;
			}
		}

		private void RecalculateAll()
		{
			if (SelectedAircraft == null || FlightDistanceNM <= 0) return;

			double estimatedHours = FlightDistanceNM / 400.0;
			double tripFuel = estimatedHours * SelectedAircraft.CruiseBurnPerHourKg;
			double reserveFuel = SelectedAircraft.CruiseBurnPerHourKg * 0.75;
			double extraWeightFuel = (PassengerCount * 84 + CargoWeightKg) * 0.03;

			double totalRequiredKg = tripFuel + reserveFuel + extraWeightFuel;

			if (totalRequiredKg > SelectedAircraft.TotalFuelCapacityKg)
			{
				totalRequiredKg = SelectedAircraft.TotalFuelCapacityKg;
			}

			TotalBlockFuel = totalRequiredKg;
			CalculateFuelDistribution(totalRequiredKg);
		}

		private void ExecuteSaveFlight(object obj)
		{
			var record = new FlightRecord
			{
				AircraftName = SelectedAircraft?.Name,
				Origin = OriginIcao,
				Destination = DestinationIcao,
				Alternate = AlternateIcao,
				DistanceNM = FlightDistanceNM,
				Passengers = PassengerCount,
				CargoKg = CargoWeightKg,
				TotalFuelKg = TotalBlockFuel
			};

			_dbService.SaveFlight(record);
			LoadHistory();
		}

		private void ExecuteDeleteFlight(object obj)
		{
			if (obj is FlightRecord record)
			{
				_dbService.DeleteFlight(record.Id);
				LoadHistory();
			}
		}

		private void LoadSelectedFlightRecord(FlightRecord record)
		{
			var aircraft = AvailableAircraft.FirstOrDefault(a => a.Name == record.AircraftName);
			if (aircraft != null)
			{
				SelectedAircraft = aircraft;
			}

			OriginIcao = record.Origin;
			DestinationIcao = record.Destination;
			AlternateIcao = record.Alternate;
			PassengerCount = record.Passengers;
			CargoWeightKg = record.CargoKg;

			RecalculateAll();
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
