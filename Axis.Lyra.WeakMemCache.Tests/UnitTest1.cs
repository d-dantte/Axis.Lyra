using Axis.Luna.Operation;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Axis.Lyra.WeakMemCache.Tests
{
	public class UnitTest1
	{
		[Fact]
		public async Task Test1()
		{
			var memcache = new WeakRefCache();
			bool invalidated = false;
			memcache.KeyInvalidated += (sender, arg) => invalidated = true;
			var key = "FirstKey";
			await Set(memcache, key);
			var testValue = new[] { (byte)1, (byte)2, (byte)3, (byte)4, (byte)5 };

			var hasKey = await memcache.HasKey(key);
			Assert.True(hasKey);

			var value = await memcache.Get(key);
			Assert.True(testValue.SequenceEqual(value));

			var gotten = memcache.TryGet(key, out value);
			Assert.True(gotten);
			Assert.True(testValue.SequenceEqual(value));

			//test expiration
			await Task.Delay(6000);
			await Assert.ThrowsAsync<Core.Exceptions.ExpiredEntryException>(async () => await memcache.Get(key));

			//invalidate
			GC.Collect();
			hasKey = await memcache.HasKey(key); //should trigger the invalidated event
			Assert.False(hasKey);
			Assert.True(invalidated);
		}

		public Operation Set(WeakRefCache memcache, string key) 
			=> memcache.Set(key, new Core.Models.CacheEntryInfo
			{
				IdleThreshold = TimeSpan.FromMilliseconds(2000),
				Value = new[] { (byte)1, (byte)2, (byte)3, (byte)4, (byte)5 }
			});
	}
}
