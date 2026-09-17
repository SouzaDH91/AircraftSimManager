using System;
using System.Windows.Data;

namespace AircraftSimManager.Shared.Helpers
{
	public class LocExtension : Binding
	{
		public LocExtension(string key) : base($"[{key}]")
		{
			Source = TranslationSource.Instance;
			Mode = BindingMode.OneWay;
		}
	}
}
