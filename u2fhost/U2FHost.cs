using System;
using System.Threading;
using System.Threading.Tasks;
using HidLibrary;
using u2flib;
using u2flib.Data;

namespace u2fhost
{
	public static class U2FHost
	{
		public static async Task<DeviceRegistration> RegisterAsync(IHidDevice hidDevice, string appId, string facet, CancellationToken? cancellationToken = null)
		{
			cancellationToken = cancellationToken ?? CancellationToken.None;

			if (hidDevice == null || !hidDevice.IsConnected)
			{
				throw new ArgumentException("Hid device not connected", nameof(hidDevice));
			}

			using (var u2fHidDevice = await U2FHidDevice.OpenAsync(hidDevice))
			{
				var startRegistration = U2F.StartRegistration(appId);

				Console.WriteLine("Touch token to register");
				var registerResponse = await WaitForTokenInputAsync(() => U2Fv2.RegisterAsync(u2fHidDevice, startRegistration, facet), cancellationToken.Value);

				var deviceRegistration = U2F.FinishRegistration(startRegistration, registerResponse);
				Console.WriteLine("Registered");

				return deviceRegistration;
			}
		}

		public static async Task AuthenticateAsync(IHidDevice hidDevice, DeviceRegistration deviceRegistration, string appId, string facet, bool checkOnly = false, CancellationToken? cancellationToken = null)
		{
			cancellationToken = cancellationToken ?? CancellationToken.None;

			if (hidDevice == null || !hidDevice.IsConnected)
			{
				throw new ArgumentException("Hid device not connected", nameof(hidDevice));
			}

			using (var u2fHidDevice = await U2FHidDevice.OpenAsync(hidDevice))
			{
				var startAuthentication = U2F.StartAuthentication(appId, deviceRegistration);

				Console.WriteLine("Touch token to authenticate");
				var authenticateResponse = await WaitForTokenInputAsync(() => U2Fv2.AuthenticateAsync(u2fHidDevice, startAuthentication, facet, checkOnly), cancellationToken.Value).ConfigureAwait(false);

				U2F.FinishAuthentication(startAuthentication, authenticateResponse, deviceRegistration);
				Console.WriteLine("Authenticated");
			}
		}

		public static async Task<T> WaitForTokenInputAsync<T>(Func<Task<T>> func, CancellationToken cancellationToken)
		{
			T response;
			while (true)
			{
				cancellationToken.ThrowIfCancellationRequested();

				try
				{
					response = await func();
				}
				catch (ApduException ex) when (ex.StatusCode == Constants.APDU_USE_NOT_SATISFIED)
				{
					await Task.Delay(250, cancellationToken);
					continue;
				}
				break;
			}

			return response;
		}
	}
}