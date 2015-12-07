using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HidLibrary;
using u2fhost.Logging;

namespace u2fhost
{
	public class U2FHidDevice : ApduDevice, IDisposable
	{
		//DEVICES = [
		//    (0x1050, 0x0200),  # Gnubby
		//    (0x1050, 0x0113),  # YubiKey NEO U2F
		//    (0x1050, 0x0114),  # YubiKey NEO OTP+U2F
		//    (0x1050, 0x0115),  # YubiKey NEO U2F+CCID
		//    (0x1050, 0x0116),  # YubiKey NEO OTP+U2F+CCID
		//    (0x1050, 0x0120),  # Security Key by Yubico
		//    (0x1050, 0x0410),  # YubiKey Plus
		//    (0x1050, 0x0402),  # YubiKey 4 U2F
		//    (0x1050, 0x0403),  # YubiKey 4 OTP+U2F
		//    (0x1050, 0x0406),  # YubiKey 4 U2F+CCID
		//    (0x1050, 0x0407),  # YubiKey 4 OTP+U2F+CCID
		//]

		private static readonly ILog Log = LogProvider.GetCurrentClassLogger();

		private const byte TYPE_INIT = 0x80;
		private const int HID_RPT_SIZE = 64;
		private const byte STAT_ERR = 0xbf;

		private const byte CMD_PING = 0x01;
		private const byte CMD_INIT = 0x06;
		private const byte CMD_WINK = 0x08;
		private const byte CMD_APDU = 0x03;

		private const uint BROADCAST_CID = 0xffffffff;

		private const int HidTimeoutMs = 1000;

		private readonly Random random = new Random();

		private readonly IHidDevice hidDevice;
		private byte[] cid;

		protected U2FHidDevice(IHidDevice hidDevice)
		{
			this.hidDevice = hidDevice;
			this.cid = BitConverter.GetBytes(BROADCAST_CID);
		}

		public static async Task<U2FHidDevice> OpenAsync(IHidDevice hidDevice)
		{
			var device = new U2FHidDevice(hidDevice);
			await device.InitAsync();
			return device;
		}

		protected async Task InitAsync()
		{
			Log.Debug("Init");

			var nonce = new byte[8];
			random.NextBytes(nonce);
			var response = await CallAsync(CMD_INIT, nonce);

			while (!response.Take(8).SequenceEqual(nonce))
			{
				await Task.Delay(100);
				Log.Debug("Wrong nonce, read again...");
				response = await CallAsync(CMD_INIT, nonce);
			}

			this.cid = response.Skip(8).Take(4).ToArray();

			Log.Debug($"Cid: {BitConverter.ToString(this.cid)}");
		}

		public void SetMode(string mode)
		{
			throw new NotImplementedException();
		}

		protected override async Task<byte[]> DoSendApduAsync(byte[] data)
		{
			return await CallAsync(CMD_APDU, data);
		}

		public async Task Wink()
		{
			await CallAsync(CMD_WINK);
		}

		public async Task<byte[]> PingAsync(byte[] data)
		{
			return await CallAsync(CMD_PING, data);
		}

		private async Task<byte[]> CallAsync(byte command, byte[] data = null)
		{
			await SendRequestAsync(command, data);
			return await ReadResponseAsync(command);
		}

		private async Task SendRequestAsync(byte command, byte[] data = null)
		{
			//Log.Debug($"SendRequest: {command:X2}");

			if (data == null)
			{
				data = new byte[0];
			}

			//Log.Debug($"Data: {BitConverter.ToString(data)}");

			//var reportSize = hidDevice.Capabilities.InputReportByteLength;
			var reportSize = HID_RPT_SIZE;

			var size = data.Length;
			var bc_l = (byte)(size & 0xff);
			var bc_h = (byte)(size >> 8 & 0xff);
			var payloadData = data.Take(reportSize - 7).ToArray();

			//Log.Debug($"Payload data: {BitConverter.ToString(payloadData)}");

			var payloadBuilder = new ByteArrayBuilder();
			payloadBuilder.Append(cid);
			payloadBuilder.Append((byte)(TYPE_INIT | command));
			payloadBuilder.Append(bc_h);
			payloadBuilder.Append(bc_l);
			payloadBuilder.Append(payloadData);
			while (payloadBuilder.Length < reportSize)
			{
				payloadBuilder.Append(0x00);
			}

			var payload = payloadBuilder.GetBytes();
			var report = hidDevice.CreateReport();
			report.Data = payload;
			await hidDevice.WriteReportAsync(report, HidTimeoutMs);

			var remainingData = data.Skip(reportSize - 7).ToArray();
			var seq = 0;
			while (remainingData.Length > 0)
			{
				payloadData = remainingData.Take(reportSize - 5).ToArray();
				//Log.Debug($"Payload data: {BitConverter.ToString(payloadData)}");

				payloadBuilder.Clear();
				payloadBuilder.Append(cid);
				payloadBuilder.Append((byte)(0x7f & seq));
				payloadBuilder.Append(payloadData);
				while (payloadBuilder.Length < reportSize)
				{
					payloadBuilder.Append(0x00);
				}

				payload = payloadBuilder.GetBytes();
				report = hidDevice.CreateReport();
				report.Data = payload;
				if (!await hidDevice.WriteReportAsync(report, HidTimeoutMs))
				{
					throw new Exception("Error writing to device");
				}

				remainingData = remainingData.Skip(reportSize - 5).ToArray();
				seq++;
			}
		}

		private async Task<byte[]> ReadResponseAsync(byte command)
		{
			//Log.Debug("ReadResponse");

			//var reportSize = hidDevice.Capabilities.OutputReportByteLength;
			var reportSize = HID_RPT_SIZE;

			var byteArrayBuilder = new ByteArrayBuilder();

			byteArrayBuilder.Append(cid);
			byteArrayBuilder.Append((byte)(TYPE_INIT | command));

			var resp = Encoding.ASCII.GetBytes(".");
			var header = byteArrayBuilder.GetBytes();

			HidReport report = null;

			while (!resp.Take(header.Length).SequenceEqual(header))
			{
				report = await hidDevice.ReadReportAsync(HidTimeoutMs);

				if (report.ReadStatus != HidDeviceData.ReadStatus.Success)
				{
					throw new Exception("Error reading from device");
				}

				resp = report.Data;

				byteArrayBuilder.Clear();
				byteArrayBuilder.Append(cid);
				byteArrayBuilder.Append(STAT_ERR);

				if (resp.Take(header.Length).SequenceEqual(byteArrayBuilder.GetBytes()))
				{
					throw new Exception("Error in response header");
				}
			}

			var dataLength = (report.Data[5] << 8) + report.Data[6];

			var payloadData = report.Data.Skip(7).Take(Math.Min(dataLength, reportSize)).ToArray();
			//Log.Debug($"Payload data: {BitConverter.ToString(payloadData)}");

			byteArrayBuilder.Clear();
			byteArrayBuilder.Append(payloadData);
			dataLength -= (int)byteArrayBuilder.Length;

			var seq = 0;
			while (dataLength > 0)
			{
				report = await hidDevice.ReadReportAsync(HidTimeoutMs);

				if (report.ReadStatus != HidDeviceData.ReadStatus.Success)
				{
					throw new Exception("Error reading from device");
				}

				if (!report.Data.Take(4).SequenceEqual(cid))
				{
					throw new Exception("Wrong CID from device");
				}
				if (report.Data[4] != (byte)(seq & 0x7f))
				{
					throw new Exception("Wrong SEQ from device");
				}
				seq++;
				payloadData = report.Data.Skip(5).Take(Math.Min(dataLength, reportSize)).ToArray();
				//Log.Debug($"Payload data: {BitConverter.ToString(payloadData)}");

				dataLength -= payloadData.Length;
				byteArrayBuilder.Append(payloadData);
			}

			var result = byteArrayBuilder.GetBytes();

			return result;
		}

		public void Dispose()
		{
			hidDevice.CloseDevice();
		}
	}
}
