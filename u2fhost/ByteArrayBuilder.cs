using System.IO;

namespace u2fhost
{
	internal class ByteArrayBuilder
	{
		private MemoryStream stream;

		public ByteArrayBuilder()
		{
			stream = new MemoryStream();
		}

		public void Append(byte value)
		{
			stream.WriteByte(value);
		}

		public void Append(byte[] values)
		{
			stream.Write(values, 0, values.Length);
		}

		public byte[] GetBytes()
		{
			return stream.ToArray();
		}

		public long Length => stream.Length;

		public void Clear()
		{
			stream = new MemoryStream();
		}
	}
}