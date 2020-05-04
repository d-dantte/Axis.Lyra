using System;
using System.Linq;

namespace Axis.Lyra.Core.Models
{
	public class FragmentHeader
	{
		public Guid Id { get; private set; }

		public int FragmentCount { get; private set; }

		public int FragmentSize { get; private set; }

		public FragmentHeader(Guid id, int size, int count)
		{
			Id = id == Guid.Empty
				? throw new Exception("Invalid id")
				: id;
			FragmentSize = Math.Abs(size);
			FragmentCount = Math.Abs(count);
		}

		public byte[] Serialize()
		{
			var bytes = Id
				.ToByteArray()
				.AsEnumerable()
				.Concat(BitConverter.GetBytes(FragmentSize))
				.Concat(BitConverter.GetBytes(FragmentCount))
				.ToArray();

			return bytes;
		}

		public static FragmentHeader Deserialize(byte[] stream)
		{
			return new FragmentHeader(
				new Guid(stream.Take(16).ToArray()),
				BitConverter.ToInt32(stream, 16),
				BitConverter.ToInt32(stream, 20));
		}
	}
}
