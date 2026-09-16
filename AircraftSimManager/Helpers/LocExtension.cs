using System;
using System.Windows.Data;

namespace AircraftSimManager.Helpers
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
