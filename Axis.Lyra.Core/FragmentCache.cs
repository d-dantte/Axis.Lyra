using Axis.Luna.Extensions;
using Axis.Luna.Operation;
using Axis.Lyra.Core.Configuration;
using Axis.Lyra.Core.Contracts;
using Axis.Lyra.Core.Models;
using Nito.AsyncEx;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.ExceptionServices;

namespace Axis.Lyra.Core
{
	public class FragmentCache : ICache
	{
		/// <summary>
		/// Set this via config provider injection later on
		/// </summary>
		private readonly FragmentCacheConfig _config;

		private readonly ICache _cache;

		private readonly ConcurrentDictionary<string, string> _invalidatedKeys = 
			new ConcurrentDictionary<string, string>();

		private readonly ConcurrentDictionary<string, AsyncReaderWriterLock> _cacheLocks = 
			new ConcurrentDictionary<string, AsyncReaderWriterLock>();

		public event EventHandler<KeyInvalidationEventArgs> KeyInvalidated;

		public FragmentCache(ICache cacheService, IFragmentCacheConfigProvider configProvider = null)
		{
			_cache = cacheService;
			_config = configProvider?.GetConfig() ?? new FragmentCacheConfig
			{
				DefaultFragmentSize = 512000
			};
		}


		public CacheFeature[] FeatureSet => _cache.FeatureSet;

		public Operation<byte[]> Get(string key) => Operation.Try(async () =>
		{
			var @lock = _cacheLocks.GetOrAdd(key, _ => new AsyncReaderWriterLock());
			Exception ex = null;

			using (await @lock.ReaderLockAsync())
			{
				//load the fragment header. Ps - no need to catch exceptions from here because if the header cannot be retrieved,
				//it's subsequent fragments are thus orphaned and can be invalidated via some other process
				var fragmentHeader = await _cache
					.Get(key)
					.Then(FragmentHeader.Deserialize);

				//request for all fragments
				var fragments = Enumerable
					.Range(0, fragmentHeader.FragmentCount)
					.Select(index =>
					{
						var fragmentKey = GetFragmentKey(fragmentHeader.Id, index);

						return new
						{
							Key = fragmentKey,
							Index = index,
							Value = _cache.Get(fragmentKey)
						};
					});

				try
				{
					//await all operations - execute the requests
					await fragments
						.Select(tuple => tuple.Value)
						.Fold();

					//stitch the bytes back together and return
					return fragments
						.OrderBy(tuple => tuple.Index)
						.Select(tuple => tuple.Value.Resolve()) //<-- safe because we already "folded" above
						.SelectMany()
						.ToArray();
				}
				catch (Exception e)
				{
					ex = e;
				}
			}

			//ex is definitely not null, invalidate all keys
			if (ex is Exceptions.KeyNotFoundException
			|| ex is Exceptions.ExpiredEntryException)
				await Invalidate(key);

			return ExceptionDispatchInfo
				.Capture(ex)
				.Throw<byte[]>();
		});

		public Operation<byte[]> GetOrSet(
			string key,
			Func<string, CacheEntryInfo> generator) => Operation.Try(async () =>
		{
			if (!await HasKey(key))
				return await Get(key);

			else
			{
				var info = generator.Invoke(key);
				await Set(key, info);

				return info.Value;
			}
		});

		public Operation<bool> HasKey(string key) => Operation.Try(async () =>
		{
			var @lock = _cacheLocks.GetOrAdd(key, _ => new AsyncReaderWriterLock());

			using (await @lock.ReaderLockAsync())
			{
				return await HasKeyInternal(key);
			}
		});

		public Operation Set(string key, CacheEntryInfo info) => Operation.Try(async () =>
		{
			var @lock = _cacheLocks.GetOrAdd(key, _ => new AsyncReaderWriterLock());

			using (await @lock.WriterLockAsync())
			{
				if (await HasKeyInternal(key))
					await Invalidate(key);

				//create header
				var fragmentSize = _config.DefaultFragmentSize;
				var header = new FragmentHeader(
					Guid.NewGuid(),
					fragmentSize,
					FragmentCount(info.Value.Length, fragmentSize));

				//get fragments
				var fragments = Enumerable
					.Range(0, header.FragmentCount)
					.Select(index => info.Value
						.Skip(index * header.FragmentSize)
						.Take(header.FragmentSize));

				//store values
				await _cache.Set(
					key,
					new CacheEntryInfo
					{
						Value = header.Serialize(),
						ExpiresOn = info.ExpiresOn,
						IdleThreshold = info.IdleThreshold
					});

				await fragments
					.Select((frag, index) =>
					{
						return _cache.Set(
							GetFragmentKey(header.Id, index),
							new CacheEntryInfo
							{
								Value = frag.ToArray(),
								IdleThreshold = info.IdleThreshold,
								ExpiresOn = info.ExpiresOn
							});
					})
					.Fold();
			}
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
			catch { return false; }
		}

		public Operation ResetExpiration(string key, DateTimeOffset newExpiration) => Operation.Try(async () =>
		{
			var @lock = _cacheLocks.GetOrAdd(key, _ => new AsyncReaderWriterLock());

			using (await @lock.WriterLockAsync())
			{
				//refresh the header
				await _cache.ResetExpiration(key, newExpiration);

				//get the header
				var fragmentHeader = await _cache
					.Get(key)
					.Then(FragmentHeader.Deserialize);

				await Enumerable
					.Range(0, fragmentHeader.FragmentCount)
					.Select(index => GetFragmentKey(fragmentHeader.Id, index))
					.Select(_key => _cache.ResetExpiration(_key, newExpiration))
					.Fold();
			}
		});

		public Operation ResetIdleThreshold(string key, TimeSpan newthreshold) => Operation.Try(async () =>
		{
			var @lock = _cacheLocks.GetOrAdd(key, _ => new AsyncReaderWriterLock());

			using (await @lock.WriterLockAsync())
			{
				//refresh the header
				await _cache.ResetIdleThreshold(key, newthreshold);

				//get the header
				var fragmentHeader = await _cache
					.Get(key)
					.Then(FragmentHeader.Deserialize);

				await Enumerable
					.Range(0, fragmentHeader.FragmentCount)
					.Select(index => GetFragmentKey(fragmentHeader.Id, index))
					.Select(_key => _cache.ResetIdleThreshold(_key, newthreshold))
					.Fold();
			}
		});

		public Operation Invalidate(string key) => Operation.Try(async () =>
		{
			var @lock = _cacheLocks.GetOrAdd(key, _ => new AsyncReaderWriterLock());

			using (await @lock.WriterLockAsync())
			{
				//add our key and lock on it. subsequent calls with the same key will not enter the if statement
				await _invalidatedKeys.GrantOne(key, async _ =>
				{
					try
					{
						if (await HasKeyInternal(key))
						{
							//get the header
							var fragmentHeader = await _cache
								.Get(key)
								.Then(FragmentHeader.Deserialize);

							//delete the header
							await _cache.Invalidate(key);

							await Enumerable
								.Range(0, fragmentHeader.FragmentCount)
								.Select(index => GetFragmentKey(fragmentHeader.Id, index))
								.Select(_cache.Invalidate)
								.Fold();

							KeyInvalidated?.Invoke(this, new KeyInvalidationEventArgs(key));
						}
					}
					finally
					{
						//release the lock
						_invalidatedKeys.TryRemove(key, out _);
					}
				});
			}
		});

		private string GetFragmentKey(Guid fragmentId, int fragmentIndex)
		{
			return $"{fragmentId}::{fragmentIndex}";
		}

		private int FragmentCount(int byteCount, int fragmentSize)
		{
			if (byteCount == 0)
				return 0;

			else return ((byteCount - 1) / fragmentSize) + 1;
		}

		private Operation<bool> HasKeyInternal(string key) => _cache.HasKey(key);

	}
}
