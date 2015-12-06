using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace u2fhost
{
	public abstract class ApduDevice
	{
		public async Task<string[]> GetSupportedVersionsAsync()
		{
			try
			{
				var versionBytes = await SendApduAsync(Constants.INS_GET_VERSION);

				return new[] {Encoding.ASCII.GetString(versionBytes)};
			}
			catch (ApduException ex)
			{
				// v0 didn't support the instruction
				return ex.StatusCode == 0x6d00 ? new[] {"v0"} : new string[0];
			}
		}

		protected abstract Task<byte[]> DoSendApduAsync(byte[] data);

		/// <summary>
		/// Sends an APDU to the device, and waits for a response
		/// </summary>
		public async Task<byte[]> SendApduAsync(byte ins, byte p1 = 0x00, byte p2 = 0x00, byte[] data = null)
		{
			if (data == null)
			{
				data = new byte[0];
			}
			var size = data.Length;
			var l0 = (byte) (size >> 16 & 0xff);
			var l1 = (byte) (size >> 8 & 0xff);
			var l2 = (byte) (size & 0xff);

			var byteArrayBuilder = new ByteArrayBuilder();
			byteArrayBuilder.Append(new byte[] {0x00, ins, p1, p2, l0, l1, l2});
			byteArrayBuilder.Append(data);
			byteArrayBuilder.Append(new byte[] {0x04, 0x00});

			var apduData = byteArrayBuilder.GetBytes();

			var response = await DoSendApduAsync(apduData);

			var responseData = response.Take(response.Length - 2).ToArray();
			var status = response.Skip(response.Length - 2).Take(2).Reverse().ToArray();

			var statusCode = BitConverter.ToUInt16(status, 0);

			if (statusCode != Constants.APDU_OK)
			{
				throw new ApduException(statusCode);
			}

			return responseData;
		}
	}
}