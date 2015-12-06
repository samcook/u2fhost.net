using System;

namespace u2fhost
{
	public class ApduException : Exception
	{
		public ushort? StatusCode { get; }

		public ApduException(ushort? statusCode = null)
		{
			StatusCode = statusCode;
		}

		public ApduException(string message, ushort? statusCode = null)
			: base(message)
		{
			StatusCode = statusCode;
		}
	}
}