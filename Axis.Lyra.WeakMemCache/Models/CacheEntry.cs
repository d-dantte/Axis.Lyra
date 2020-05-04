using System;

namespace Axis.Lyra.WeakMemCache.Models
{
	public class CacheEntry
	{
		public bool IsExpired => ExpiredOn.HasValue;

		public DateTimeOffset? ExpiredOn
		{
			get
			{
				var now = DateTimeOffset.Now;
				if (now > ShouldExpireOn)
					return ShouldExpireOn.Value;

				else if ((now - AccessTimeStamp) > IdleThreshold)
					return AccessTimeStamp + IdleThreshold.Value;

				else
					return null;
			}
		}

		public DateTimeOffset AccessTimeStamp { get; set; }

		public TimeSpan? IdleThreshold { get; set; }

		public DateTimeOffset? ShouldExpireOn { get; internal set; }

		public byte[] Value { get; }

		public string Key { get; }


		public CacheEntry(
			string key,
			byte[] value,
			TimeSpan? idleThreshold = null, 
			DateTimeOffset? shouldExpireOn = null)
		{
			AccessTimeStamp = DateTimeOffset.Now;
			Key = key;
			Value = value;
			IdleThreshold = idleThreshold;
			ShouldExpireOn = shouldExpireOn;
		}
	}
}
