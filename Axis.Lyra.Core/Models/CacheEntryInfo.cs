using System;

namespace Axis.Lyra.Core.Models
{
	public class CacheEntryInfo
	{
		public TimeSpan? IdleThreshold { get; set; }

		public DateTimeOffset? ExpiresOn { get; set; }

		public byte[] Value { get; set; }
	}
}
