using System;
using System.Linq;
using System.Threading.Tasks;
using HidLibrary;
using log4net;

namespace u2fhost.console
{
	public static class Sample
	{
		private static readonly ILog Log = LogManager.GetLogger(typeof (Sample));

		public static async Task Run(int vendorId, int productId)
		{
			Log.Info("Insert U2F device");

			IHidDevice hidDevice = null;

			while (hidDevice == null)
			{
				using (hidDevice = HidDevices.Enumerate(vendorId, productId).FirstOrDefault())
				{
					if (hidDevice == null)
					{
						await Task.Delay(250);
						continue;
					}

					var appId = "http://localhost";
					var facet = "http://localhost";

					var registration = await U2FHost.RegisterAsync(hidDevice, appId, facet);

					await U2FHost.AuthenticateAsync(hidDevice, registration, appId, facet);
				}
			}
		}
	}
}