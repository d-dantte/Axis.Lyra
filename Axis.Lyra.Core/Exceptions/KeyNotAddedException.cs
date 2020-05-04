using System;

namespace Axis.Lyra.Core.Exceptions
{
	/// <summary>
	/// An exception thrown when a key-addition attempt failed, and they key was not set in the cache
	/// </summary>
	public class KeyNotAddedException: Exception
	{
		public string Key { get; }

		public KeyNotAddedException(string key, Exception inner = null)
			:base($"The key '{key}' could not be added", inner)
		{
			Key = key;
		}
	}
}
