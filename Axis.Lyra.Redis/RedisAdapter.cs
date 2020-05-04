using Axis.Luna.Operation;
using Axis.Lyra.Core;
using Axis.Lyra.Core.Contracts;
using Axis.Lyra.Core.Exceptions;
using Axis.Lyra.Core.Models;
using Axis.Lyra.Redis.Configuration;
using Axis.Lyra.Redis.Contracts;
using StackExchange.Redis;
using System;
using System.Linq;

namespace Axis.Lyra.Redis
{
	public class RedisAdapter : ICache
	{
		private static readonly string InvalidationNotificationChannel = "Axis.Lyra.Redis.RedisAdapter/8e818e4e-62d2-4f88-864a-2d28bbb33ff9";
		private static readonly CacheFeature[] _FeatureSet = new[] { CacheFeature.Expiration };


		private readonly RedisConfiguration _redisConfig;


		public event EventHandler<KeyInvalidationEventArgs> KeyInvalidated;

		public CacheFeature[] FeatureSet => _FeatureSet.ToArray();

		public RedisAdapter(IRedisConfigProvider configProvider)
		{
			_redisConfig = configProvider.GetConfiguration()?.Clone() ?? throw new ArgumentNullException("Invalid configuration");

			//register invalidation subscription
			_redisConfig.Multiplexer
				.GetSubscriber()
				.Subscribe(
					InvalidationNotificationChannel,
					SourceInvalidated,
					_redisConfig.InvalidationNotificationSubscriptionFlags);
		}


		public Operation<byte[]> Get(string key) => Operation.Try(async () =>
		{
			var db = _redisConfig.Multiplexer.GetDatabase();

			if (_redisConfig.AsyncKeyReadTransformation != null)
				return await _redisConfig.AsyncKeyReadTransformation.Invoke(db, key);

			else
			{
				var readCommandFlags = _redisConfig.KeyReadCommandTransformation?.Invoke(key) ?? CommandFlags.None;
				var value = await db.StringGetAsync(key, readCommandFlags);

				return (byte[])value;
			}
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
				var entry = generator.Invoke(key);
				await Set(key, entry);
				return entry.Value;
			}
		});

		public Operation<bool> HasKey(string key) => Operation.Try(async () =>
		{
			var db = _redisConfig.Multiplexer.GetDatabase();
			var keyReadCommand = _redisConfig.KeyReadCommandTransformation?.Invoke(key) ?? CommandFlags.None;

			return await db.KeyExistsAsync(key, keyReadCommand);
		});

		public Operation Invalidate(string key) => Operation.Try(async () =>
		{
			var db = _redisConfig.Multiplexer.GetDatabase();
			var keyWritecommand = _redisConfig.KeyWriteCommandTransformation?.Invoke(key) ?? CommandFlags.None;

			await db.KeyDeleteAsync(key, keyWritecommand);

			//invalidate
			await _redisConfig.Multiplexer
				.GetSubscriber()
				.PublishAsync(InvalidationNotificationChannel, key);
		});

		public Operation ResetExpiration(string key, DateTimeOffset newExpiration) => Operation.Try(async () =>
		{
			var db = _redisConfig.Multiplexer.GetDatabase();
			var readCommandFlags = _redisConfig.KeyReadCommandTransformation?.Invoke(key) ?? CommandFlags.None;

			if(await db.KeyExistsAsync(key, readCommandFlags)
			  && await db.KeyTimeToLiveAsync(key, readCommandFlags) != null)
			{
				var writeCommandFlags = _redisConfig.KeyWriteCommandTransformation?.Invoke(key) ?? CommandFlags.None;
				await db.KeyExpireAsync(key, newExpiration.DateTime, writeCommandFlags);
			}
		});

		/// <summary>
		/// No-Op
		/// </summary>
		public Operation ResetIdleThreshold(
			string key,
			TimeSpan newThreshold)
			=> Operation.FromVoid();

		public Operation Set(string key, CacheEntryInfo info) => Operation.Try(async () =>
		{
			var db = _redisConfig.Multiplexer.GetDatabase();

			if (_redisConfig.AsyncKeyWriteTransformation != null)
				await _redisConfig.AsyncKeyWriteTransformation.Invoke(db, key, info.Value);

			else
			{
				var writeCommandFlags = _redisConfig.KeyWriteCommandTransformation?.Invoke(key) ?? CommandFlags.None;
				var succeeded = await db.StringSetAsync(
					key,
					info.Value,
					info.ExpiresOn - DateTime.Now,
					When.Always,
					writeCommandFlags);

				if (!succeeded)
					throw new KeyNotAddedException(key);
			}

			//invalidate
			await _redisConfig.Multiplexer
				.GetSubscriber()
				.PublishAsync(InvalidationNotificationChannel, key);
		});

		public bool TryGet(string key, out byte[] value)
		{
			value = null;
			try
			{
				var db = _redisConfig.Multiplexer.GetDatabase();
				var keyReadCommand = _redisConfig.KeyReadCommandTransformation?.Invoke(key) ?? CommandFlags.None;

				if (!db.KeyExists(key, keyReadCommand))
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

		private void SourceInvalidated(RedisChannel channel, RedisValue message)
		{
			try
			{
				var entryKey = (string)message;

				KeyInvalidated?.Invoke(this, new KeyInvalidationEventArgs(entryKey));
			}
			catch
			{ }
		}
	}
}
