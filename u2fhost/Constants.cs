namespace u2fhost
{
	public static class Constants
	{
		// APDU Instructions
		public const byte INS_ENROLL = 0x01;
		public const byte INS_SIGN = 0x02;
		public const byte INS_GET_VERSION = 0x03;

		// APDU Response Codes
		public const ushort APDU_OK = 0x9000;
		public const ushort APDU_USE_NOT_SATISFIED = 0x6985;
	}
}