using Axis.Luna.Extensions;
using Axis.Luna.Operation;
using Axis.Lyra.Core.Contracts;
using Axis.Lyra.Core.Exceptions;
using Axis.Lyra.Core.Models;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.ExceptionServices;

namespace Axis.Lyra.Core
{
	/// <summary>
	/// Multi-level cache node designed to accept a primary cache and secondary caches.
	/// Each secondary cache is placed in a separate node in the order they appear in the <see cref="CacheLevelCollection"/>, with
	/// elements further in the collection appearing further down in the node levels.
	/// 
	/// This cache utilizes a --- caching policy
	/// </summary>
	public class MultiLevelCacheNode: ICache
	{
		private readonly ConcurrentDictionary<string, string> _eventSurpressionTokens = new ConcurrentDictionary<string, string>();

		private readonly CacheLevel _level;
		private readonly MultiLevelCacheNode _childNode;

		public event EventHandler<KeyInvalidationEventArgs> KeyInvalidated;

		public MultiLevelCacheNode(
			ICacheLevelCollectionProvider levelProvider)
			: this(levelProvider.GetCacheLevels() ?? throw new NullCacheCollectionProviderResult())
		{
		}

		private MultiLevelCacheNode(CacheLevelCollection cacheLevels)
		{
			_level = cacheLevels.First();

			if (cacheLevels.HasRest)
			{
				_childNode = new MultiLevelCacheNode(cacheLevels.Rest());
				_childNode.KeyInvalidated += ChildNodeKeyInvalidated;
			}
			else
			{
				_childNode = null;
				_level.PrimaryCache.KeyInvalidated += PrimaryCacheKeyInvalidated;
			}
		}


		public CacheFeature[] FeatureSet => _level.PrimaryCache.FeatureSet;

		public Operation<byte[]> Get(string key)
		{
			if (_level.ForbiddenKeys.Contains(key))
				return _childNode?.Get(key) ?? throw new KeyNotFoundException(key);

			else return _level.PrimaryCache
				.Get(key)
				.Catch(e =>
				{
					return _childNode?.Get(key) ?? ExceptionDispatchInfo
						.Capture(e)
						.Throw<Operation<byte[]>>();
				});
		}

		public Operation<byte[]> GetOrSet(
			string key,
			Func<string, CacheEntryInfo> generator)
		{
			return HasKey(key)
				.Then(hasKey =>
				{
					if (hasKey)
						return Get(key);

					else
					{
						var info = generator.Invoke(key);
						return Set(key, info).Then(() => info.Value);
					}
				});
		}

		public Operation<bool> HasKey(string key) => Operation.Try(async () =>
		{
			if (_level.ForbiddenKeys.Contains(key))
				return false;
			
			else return await _level.PrimaryCache.HasKey(key)
				|| _childNode != null
				? await _childNode.HasKey(key)
				: false;
		});

		public Operation Invalidate(string key) => Operation.Try(() =>
		{
			return _eventSurpressionTokens.GrantOne(key, _key =>
			{
				return _level.PrimaryCache
					.Invalidate(key)
					.Then(() => _childNode.Invalidate(key));
			});
		});

		public Operation ResetExpiration(
			string key,
			DateTimeOffset newExpiration)
			=> _level.PrimaryCache.ResetExpiration(key, newExpiration);

		public Operation ResetIdleThreshold(
			string key,
			TimeSpan newThreshold)
			=> _level.PrimaryCache.ResetIdleThreshold(key, newThreshold);

		public Operation Set(string key, CacheEntryInfo info) => Operation.Try(() =>
		{
			if(_childNode == null)
			{
				return _eventSurpressionTokens.GrantOne(
					key: key,
					granted: _key => _level.PrimaryCache.Set(key, info),
					denied: _key => throw new Exception("could not set"));
			}
			else
			{
				return _childNode
					.Set(key, info)
					.Then(() => _level.PrimaryCache.Set(key, info));
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
			catch
			{
				return false;
			}
		}

		protected void OnInvalidate(string key)
		{
			if (string.IsNullOrEmpty(key))
				throw new ArgumentNullException(nameof(key));

			else if (KeyInvalidated != null)
			{
				_eventSurpressionTokens.GrantOne(key, _key =>
				{
					KeyInvalidated?.Invoke(
						this,
						new KeyInvalidationEventArgs(key));
				});
			}
		}

		#region Event Handlers
		private void PrimaryCacheKeyInvalidated(
			object sender, 
			KeyInvalidationEventArgs e)
		{
			//tell the parent to invalidate itself
			OnInvalidate(e.Key);
		}

		private void ChildNodeKeyInvalidated(
			object sender, 
			KeyInvalidationEventArgs e)
		{
			//tell the parent to invalidate itself
			OnInvalidate(e.Key);
		}
		#endregion
	}
}
