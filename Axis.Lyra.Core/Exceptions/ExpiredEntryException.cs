using System;

namespace Axis.Lyra.Core.Exceptions
{
	public class ExpiredEntryException: TimeoutException
	{
		public string Key { get; }
		public DateTimeOffset ExpiredOn { get; }

		public ExpiredEntryException(string key, DateTimeOffset expiredOn)
			:base($"Cach entry for \"{key}\" expired on: {expiredOn}")
		{
			Key = key;
			ExpiredOn = expiredOn;
		}
	}
}
