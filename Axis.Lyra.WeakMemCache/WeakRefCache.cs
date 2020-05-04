using Axis.Luna.Operation;
using Axis.Lyra.Core;
using Axis.Lyra.Core.Contracts;
using Axis.Lyra.Core.Models;
using Axis.Lyra.WeakMemCache.Models;
using System;
using System.Collections.Concurrent;
using System.Linq;

namespace Axis.Lyra.WeakMemCache
{
	public class WeakRefCache : ICache
	{
		private static readonly CacheFeature[] _FeatureSet = new[]
		{
			CacheFeature.Expiration,
			CacheFeature.IdleThreshold
		};

		private readonly ConcurrentDictionary<string, string> _invalidatedKeys = new ConcurrentDictionary<string, string>();
		private readonly ConcurrentDictionary<string, WeakReference<CacheEntry>> _cacheEntries = new ConcurrentDictionary<string, WeakReference<CacheEntry>>();

		public event EventHandler<KeyInvalidationEventArgs> KeyInvalidated;

		public CacheFeature[] FeatureSet => _FeatureSet.ToArray();

		public Operation<byte[]> Get(string key) => Operation.Try(() =>
		{
			if (!_cacheEntries.TryGetValue(key, out var weakRef))
				throw new Core.Exceptions.KeyNotFoundException(key);

			if (!weakRef.TryGetTarget(out var entry))
			{
				OnInvalidate(new KeyInvalidationEventArgs(key));

				throw new Core.Exceptions.KeyNotFoundException(key);
			}

			if (entry.IsExpired)
				throw new Core.Exceptions.ExpiredEntryException(key, entry.ExpiredOn.Value);

			entry.AccessTimeStamp = DateTimeOffset.Now;

			return entry.Value;
		});

		public Operation<byte[]> GetOrSet(
			string key,
			Func<string, CacheEntryInfo> generator) 
			=> Operation.Try(async () =>
			{
				if (await HasKey(key))
					return await Get(key);

				else
				{
					var info = generator.Invoke(key);
					await Set(key, info);

					return info.Value;
				}
			});

		public Operation<bool> HasKey(string key) => Operation.Try(() =>
		{
			if (!_cacheEntries.TryGetValue(key, out var weakRef))
				return false;

			else if (!weakRef.TryGetTarget(out _))
			{
				try
				{
					OnInvalidate(new KeyInvalidationEventArgs(key));
				}
				catch { }

				return false;
			}

			return true;
		});

		public Operation Invalidate(string key) => Operation.Try(() =>
		{
			//add our lock (key) and lock on it. subsequent calls with the same key will not enter the if statement
			if (_invalidatedKeys.TryAdd(key, key))
			{
				try
				{
					//remove the key if it exists
					if (_cacheEntries.TryRemove(key, out _))
						OnInvalidate(new KeyInvalidationEventArgs(key));
				}
				finally
				{
					//release the lock
					_invalidatedKeys.TryRemove(key, out _);
				}
			}
		});

		public Operation ResetExpiration(string key, DateTimeOffset newExpiration) => Operation.Try(() =>
		{
			if (!_cacheEntries.TryGetValue(key, out var @ref))
				throw new Core.Exceptions.KeyNotFoundException(key);

			else if (!@ref.TryGetTarget(out var entry))
			{
				OnInvalidate(new KeyInvalidationEventArgs(key));

				throw new Core.Exceptions.KeyNotFoundException(key);
			}

			else if (entry.IsExpired)
				throw new Core.Exceptions.ExpiredEntryException(key, entry.ExpiredOn.Value);

			else
			{
				entry.AccessTimeStamp = DateTimeOffset.Now;

				if (entry.ShouldExpireOn != null)
					entry.ShouldExpireOn = newExpiration;
			}
		});

		public Operation ResetIdleThreshold(string key, TimeSpan newThreshold) => Operation.Try(() =>
		{
			if (!_cacheEntries.TryGetValue(key, out var @ref))
				throw new Core.Exceptions.KeyNotFoundException(key);

			else if (!@ref.TryGetTarget(out var entry))
			{
				OnInvalidate(new KeyInvalidationEventArgs(key));

				throw new Core.Exceptions.KeyNotFoundException(key);
			}

			else if (entry.IsExpired)
				throw new Core.Exceptions.ExpiredEntryException(key, entry.ExpiredOn.Value);

			else
			{
				entry.AccessTimeStamp = DateTimeOffset.Now;

				if (entry.IdleThreshold != null)
					entry.IdleThreshold = newThreshold;
			}
		});

		public Operation Set(
			string key, 
			CacheEntryInfo info) 
			=> Operation.Try(async () =>
			{
				if(_cacheEntries.ContainsKey(key))
					await Invalidate(key);

				var entry = new CacheEntry(
					key,
					info.Value,
					info.IdleThreshold,
					info.ExpiresOn);

				if (!_cacheEntries.TryAdd(key, new WeakReference<CacheEntry>(entry)))
					throw new Core.Exceptions.KeyNotAddedException(key);
			});

		public bool TryGet(string key, out byte[] value)
		{
			value = null;
			try
			{
				if (!HasKey(key).Resolve())
					return false;

				else
				{
					value = Get(key).Resolve();

					return true;
				}
			}
			catch
			{
				return false;
			}
		}

		protected virtual void OnInvalidate(KeyInvalidationEventArgs eventArgs)
		{
			KeyInvalidated?.Invoke(this, eventArgs);
		}
	}
}
