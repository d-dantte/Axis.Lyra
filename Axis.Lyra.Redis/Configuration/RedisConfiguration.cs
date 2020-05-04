using StackExchange.Redis;
using System;
using System.Threading.Tasks;

namespace Axis.Lyra.Redis.Configuration
{
	public class RedisConfiguration
	{
		public IConnectionMultiplexer Multiplexer { get; set; }

		public CommandFlags InvalidationNotificationSubscriptionFlags { get; set; } = CommandFlags.None;

		public Func<RedisKey, CommandFlags> KeyWriteCommandTransformation { get; set; }

		public Func<RedisKey, CommandFlags> KeyReadCommandTransformation { get; set; }

		public Func<IDatabase, RedisKey, byte[], Task> AsyncKeyWriteTransformation { get; set; }

		public Func<IDatabase, RedisKey, Task<byte[]>> AsyncKeyReadTransformation { get; set; }


		internal RedisConfiguration Clone()
		{
			//first, validate
			Validate();

			//then copy values to a new instance and return
			return new RedisConfiguration
			{
				Multiplexer = Multiplexer,
				AsyncKeyReadTransformation = AsyncKeyReadTransformation,
				AsyncKeyWriteTransformation = AsyncKeyWriteTransformation,
				InvalidationNotificationSubscriptionFlags = InvalidationNotificationSubscriptionFlags,
				KeyReadCommandTransformation = KeyReadCommandTransformation,
				KeyWriteCommandTransformation = KeyWriteCommandTransformation
			};
		}

		/// <summary>
		/// Ensures this configuration is correct, throws an exception if not
		/// </summary>
		private void Validate()
		{
			if (Multiplexer == null)
				throw new Exception("Invalid Multiplexer");
		}
	}
}
