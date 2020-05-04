using Axis.Lyra.Redis.Configuration;
using Axis.Lyra.Redis.Contracts;
using StackExchange.Redis;
using System;
using System.Threading;
using Xunit;
using Xunit.Abstractions;

namespace Axis.Lyra.Redis.Tests
{
	public class RedisAdapterTests
	{
		private readonly ITestOutputHelper _output;

		public RedisAdapterTests(ITestOutputHelper helper)
		{
			_output = helper;
		}

		[Fact]
		public void Test1()
		{
			var redis = ConnectionMultiplexer.Connect("localhost");
			var db = redis.GetDatabase();

			var length = 52428800;
			var data = new byte[length];

			var r = new Random();
			for (int cnt = 0; cnt < length; cnt++)
			{
				data[cnt] = (byte)r.Next(0, 128);
			}

			var key = "x-periment-1";
			db.StringSet(key, data);

			Thread.Sleep(2000);
			var timetolive1 = db.KeyIdleTime(key);

			Thread.Sleep(1000);
			db.KeyExists(key);
			var timetolive2 = db.KeyIdleTime(key);
		}

		[Fact]
		public void Test3()
		{
			var redis = ConnectionMultiplexer.Connect("localhost");
			var db = redis.GetDatabase();

			redis.GetSubscriber().Subscribe("Channelx", (c, v) =>
			{
				_output.WriteLine("received: " + v);
			});

			redis.GetSubscriber().Publish("Channelx", "Haq haq");
		}

		[Fact]
		public void Test2()
		{
			var redis = ConnectionMultiplexer.Connect("localhost");
			var db = redis.GetDatabase();

			var timetolive = db.KeyTimeToLive("x-periment-1");

		}

		[Fact]
		public void GeneralTest()
		{
			var config = new RedisConfiguration
			{
				Multiplexer = ConnectionMultiplexer.Connect("localhost")
			};
		}
	}

	public class ConfigProvider : IRedisConfigProvider
	{
		private readonly RedisConfiguration _config;

		public RedisConfiguration GetConfiguration() => _config;

		public ConfigProvider(RedisConfiguration config)
		{
			_config = config;
		}
	}
}
