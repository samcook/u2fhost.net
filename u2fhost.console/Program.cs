using System;
using System.Linq;

namespace u2fhost.console
{
	public class Program
	{
		private const int VendorId = 0x1050;
		private const int ProductId = 0x0120;

		public static void Main(string[] args)
		{
			Sample.Run(VendorId, ProductId).Wait();
		}

		private static void Ping(U2FHidDevice u2F)
		{
			var r = new Random();
			var pingData = new byte[1024];
			r.NextBytes(pingData);

			var pingResponse = u2F.PingAsync(pingData).Result;

			Console.WriteLine($"Ping response data matches request: {pingData.SequenceEqual(pingResponse)}");
		}

		private static void PrintVersions(ApduDevice apdu)
		{
			Console.WriteLine("Supported versions:");
			foreach (var version in apdu.GetSupportedVersionsAsync().Result)
			{
				Console.WriteLine(version);
			}
		}
	}
}
