using System;
using System.Collections.Concurrent;

namespace Axis.Lyra.Core
{
	public static class Extensions
	{
		internal static TResult GrantOne<TLock, TResult>(this
			ConcurrentDictionary<TLock, TLock> dictionary,
			TLock key,
			Func<TLock, TResult> granted,
			Func<TLock, TResult> denied = null)
		{
			if (dictionary.TryAdd(key, key))
			{
				try
				{
					return granted.Invoke(key);
				}
				finally
				{
					dictionary.TryRemove(key, out _);
				}
			}

			else if (denied != null)
				return denied.Invoke(key);

			else
				return default;
		}

		internal static void GrantOne<TLock>(this
			ConcurrentDictionary<TLock, TLock> dictionary,
			TLock key,
			Action<TLock> granted,
			Action<TLock> denied = null)
		{
			if (dictionary.TryAdd(key, key))
			{
				try
				{
					granted.Invoke(key);
				}
				finally
				{
					dictionary.TryRemove(key, out _);
				}
			}

			else
				denied?.Invoke(key);
		}
	}
}
