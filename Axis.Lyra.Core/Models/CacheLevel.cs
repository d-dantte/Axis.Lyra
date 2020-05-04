using Axis.Luna.Extensions;
using Axis.Lyra.Core.Contracts;
using System.Collections.Generic;

namespace Axis.Lyra.Core.Models
{
	public class CacheLevel
	{
		/// <summary>
		/// The Cache implementation this level refers to
		/// </summary>
		public ICache PrimaryCache { get; set; }

		/// <summary>
		/// Keys that are forbidden from being stored on this level
		/// </summary>
		public HashSet<string> ForbiddenKeys { get; } = new HashSet<string>();


		internal CacheLevel Clone()
		{
			var clone = new CacheLevel { PrimaryCache = PrimaryCache };
			ForbiddenKeys.ForAll(key => clone.ForbiddenKeys.Add(key));

			return clone;
		}

	}
}
