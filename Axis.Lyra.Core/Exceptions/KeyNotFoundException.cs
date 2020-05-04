using System;

namespace Axis.Lyra.Core.Exceptions
{
	public class KeyNotFoundException: Exception
	{
		public string Key { get; }

		public KeyNotFoundException(string key, string message = null)
		: base(message)
		{
			Key = key;
		}
	}
}
