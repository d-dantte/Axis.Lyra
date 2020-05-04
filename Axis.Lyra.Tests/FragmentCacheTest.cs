using Axis.Luna.Operation;
using Axis.Lyra.Core.Contracts;
using Axis.Lyra.Core.Models;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Axis.Lyra.Core.Tests
{
	public class FragmentCacheTest
	{
		private static readonly int FragmentSize = 100;

		private FragmentCache _sut;
		private Mock<ICache> _primary;
		private ITestOutputHelper _output;

		public FragmentCacheTest(ITestOutputHelper testOutputHelper)
		{
			_output = testOutputHelper;
			_primary = new Mock<ICache>();
			var configProvider = new Mock<IFragmentCacheConfigProvider>();

			//setup
			configProvider
				.Setup(p => p.GetConfig())
				.Returns(new Configuration.FragmentCacheConfig
				{
					DefaultFragmentSize = FragmentSize
				});

			_sut = new FragmentCache(_primary.Object, configProvider.Object);
		}

		[Fact]
		public void HasKey_ShouldCallPrimaryHasKeyOnce()
		{
			//setup
			_primary
				.Setup(p => p.HasKey(It.IsAny<string>()))
				.Returns(Operation.FromResult(true));

			//action
			var result = _sut.HasKey("key");

			//assert
			_primary.Verify(
				times: Times.Once,
				expression: c => c.HasKey("key"));

			Assert.True(result.Resolve());
		}

		#region Set
		[Fact]
		public void Set_WithLessThanFragmentSize_ShouldSetHeaderAndFragment()
		{
			//setup
			var cache = new Dictionary<string, CacheEntryInfo>();
			_primary
				.Setup(p => p.HasKey(It.IsAny<string>()))
				.Returns((string key) => Operation.Try(() => cache.ContainsKey(key)));

			_primary
				.Setup(p => p.Set(
					It.IsAny<string>(),
					It.IsAny<CacheEntryInfo>()))
				.Returns((string key, CacheEntryInfo info) => Operation.Try(() =>
				{
					cache[key] = info;
				}));

			//action
			var bytecount = 99;
			var bytes = RandomBytes(bytecount);
			var info = new CacheEntryInfo { Value = bytes };
			var key = "key";
			var result = _sut.Set(key, info);
			result.ResolveSafely(); //resolve the operation
			var persistedBytes = cache
				.Where(kvp => kvp.Key.Contains("::"))
				.OrderBy(kvp => kvp.Key)
				.SelectMany(kvp => kvp.Value.Value)
				.ToArray();

			//assert
			Assert.True(result.Succeeded);
			Assert.Equal(FragmentCount(bytecount) + 1, cache.Count);
			Assert.True(bytes.SequenceEqual(persistedBytes));
		}

		[Fact]
		public void Set_WithMoreThanFragmentSize_ShouldSetHeaderAndMultipleFragment()
		{
			//setup
			var cache = new Dictionary<string, CacheEntryInfo>();
			_primary
				.Setup(p => p.HasKey(It.IsAny<string>()))
				.Returns((string key) => Operation.Try(() => cache.ContainsKey(key)));

			_primary
				.Setup(p => p.Set(
					It.IsAny<string>(),
					It.IsAny<CacheEntryInfo>()))
				.Returns((string key, CacheEntryInfo info) => Operation.Try(() =>
				{
					cache[key] = info;
				}));

			//action
			var bytecount = 999;
			var bytes = RandomBytes(bytecount);
			var info = new CacheEntryInfo { Value = bytes };
			var key = "key";
			var result = _sut.Set(key, info);
			result.ResolveSafely(); //resolve the operation
			var persistedBytes = cache
				.Where(kvp => kvp.Key.Contains("::"))
				.OrderBy(kvp => kvp.Key)
				.SelectMany(kvp => kvp.Value.Value)
				.ToArray();

			//assert
			Assert.True(result.Succeeded);
			Assert.Equal(FragmentCount(bytecount) + 1, cache.Count);
			Assert.True(bytes.SequenceEqual(persistedBytes));
		}

		[Fact]
		public void Set_WithExistingKey_ShouldCallInvalidateKey()
		{
			//setup
			var cache = new Dictionary<string, CacheEntryInfo>();
			var invalidationCount = 0;
			_primary
				.Setup(p => p.HasKey(It.IsAny<string>()))
				.Returns((string key) => Operation.Try(() => cache.ContainsKey(key)));

			_primary
				.Setup(p => p.Invalidate(It.IsAny<string>()))
				.Returns((string key) => Operation.Try(() =>
				{
					invalidationCount++;
				}));

			_primary
				.Setup(p => p.Set(
					It.IsAny<string>(),
					It.IsAny<CacheEntryInfo>()))
				.Returns((string key, CacheEntryInfo info) => Operation.Try(() =>
				{
					cache[key] = info;
				}));

			_primary
				.Setup(p => p.Get(It.IsAny<string>()))
				.Returns((string key) => Operation.Try(() =>
				{
					return cache[key].Value;
				}));

			//action
			var bytecount = 999;
			var info = new CacheEntryInfo { Value = RandomBytes(bytecount) };
			var key = "key";
			var result = _sut.Set(key, info);
			result.ResolveSafely(); //resolve the operation

			info = new CacheEntryInfo { Value = RandomBytes(bytecount) };
			var result2 = _sut.Set(key, info);
			result2.ResolveSafely(); //resolve the operation

			//assert
			Assert.True(result2.Succeeded);
			Assert.Equal(FragmentCount(bytecount) + 1, invalidationCount);
		}
		#endregion

		#region Get
		[Fact]
		public async void Get_WithValidKey_ShouldReturnTheFoundValue()
		{
			//setup
			var cache = new Dictionary<string, CacheEntryInfo>();
			_primary
				.Setup(p => p.HasKey(It.IsAny<string>()))
				.Returns((string key) => Operation.Try(() => cache.ContainsKey(key)));

			_primary
				.Setup(p => p.Set(
					It.IsAny<string>(),
					It.IsAny<CacheEntryInfo>()))
				.Returns((string key, CacheEntryInfo info) => Operation.Try(() =>
				{
					cache[key] = info;
				}));

			_primary
				.Setup(p => p.Get(It.IsAny<string>()))
				.Returns((string key) => Operation.Try(() => cache[key].Value));

			//action
			var bytecount = 999;
			var bytes = RandomBytes(bytecount);
			var info = new CacheEntryInfo { Value = bytes };
			var key = "key";
			await _sut.Set(key, info);

			var result = await _sut.Get(key);

			//assert
			Assert.True(bytes.SequenceEqual(result));
		}

		[Fact]
		public void Get_WithNonExistingKey_ShouldThrowException()
		{
			//setup
			_primary
				.Setup(p => p.Get(It.IsAny<string>()))
				.Throws(new Exceptions.KeyNotFoundException(""));

			Assert.ThrowsAsync<KeyNotFoundException>(async () => await _sut.Get("key"));
		}

		[Fact]
		public async void Get_WithMissingFragment_ShouldThrowProperException()
		{
			//setup
			var cache = new Dictionary<string, CacheEntryInfo>();
			_primary
				.Setup(p => p.HasKey(It.IsAny<string>()))
				.Returns((string key) => Operation.Try(() => cache.ContainsKey(key)));

			_primary
				.Setup(p => p.Invalidate(It.IsAny<string>()))
				.Returns((string key) => Operation.Try(() => { cache.Remove(key); }));

			_primary
				.Setup(p => p.Set(
					It.IsAny<string>(),
					It.IsAny<CacheEntryInfo>()))
				.Returns((string key, CacheEntryInfo info) => Operation.Try(() =>
				{
					cache[key] = info;
				}));

			_primary
				.Setup(p => p.Get(It.IsAny<string>()))
				.Returns((string key) => Operation.Try(() =>
				{
					if (key.Contains("::1"))
						throw new Exceptions.KeyNotFoundException(key);

					else
						return cache[key].Value;
				}));

			//action
			var bytecount = 999;
			var bytes = RandomBytes(bytecount);
			var info = new CacheEntryInfo { Value = bytes };
			var key = "key";
			await _sut.Set(key, info);
			var fragment = cache.Keys.FirstOrDefault(k => k.Contains("::"));

			//assert
			await Assert.ThrowsAsync<Exceptions.KeyNotFoundException>(async() => await _sut.Get(key));
		}

		#endregion


		private int FragmentCount(int bytecount)
		{
			var count = Math.DivRem(bytecount, FragmentSize, out var rem);
			return count + (rem > 0 ? 1 : 0);
		}

		private byte[] RandomBytes(int bytecount)
		{
			var array = new byte[Math.Abs(bytecount)];
			var random = new Random(Guid.NewGuid().GetHashCode());

			random.NextBytes(array);

			return array;
		}
	}
}
