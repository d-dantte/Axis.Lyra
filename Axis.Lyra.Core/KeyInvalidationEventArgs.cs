using System;

namespace Axis.Lyra.Core
{
	public class KeyInvalidationEventArgs: EventArgs
	{
		public string Key { get; }

		public KeyInvalidationEventArgs(string key)
		{
			Key = key ?? throw new ArgumentNullException(nameof(key));
		}
	}
}
