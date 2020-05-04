using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Axis.Lyra.Core;

namespace Axis.Lyra.Tests
{
	public class ExtensionsTests
	{
		[Fact]
		public void ResultGrantOne_ShouldGrantOnlyOneThreadAccess()
		{
			var dict = new ConcurrentDictionary<string, string>();
			int grantCount = 0;
			int deniedCount = 0;

			void function()
			{
				dict.GrantOne(
					"key",
					_k => grantCount++,
					_k => deniedCount++);

				Thread.Sleep(10);
			};

			var tasks = new[]
			{
				Task.Run(function),
				Task.Run(function),
				Task.Run(function)
			};

			Task.WaitAll(tasks);

			Assert.Equal(1, grantCount);
			Assert.Equal(2, deniedCount);
		}

		[Fact]
		public void VoidGrantOne_ShouldGrantOnlyOneThreadAccess()
		{
			var dict = new ConcurrentDictionary<string, string>();
			int grantCount = 0;
			int deniedCount = 0;

			void function()
			{
				dict.GrantOne(
					"key",
					_k => { grantCount++; },
					_k => { deniedCount++; });

				Thread.Sleep(10);
			};

			var tasks = new[]
			{
				Task.Run(function),
				Task.Run(function),
				Task.Run(function)
			};

			Task.WaitAll(tasks);

			Assert.Equal(1, grantCount);
			Assert.Equal(2, deniedCount);
		}
	}
}
