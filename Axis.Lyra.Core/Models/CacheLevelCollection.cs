using Axis.Lyra.Core.Contracts;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Axis.Lyra.Core.Models
{
	public class CacheLevelCollection : IEnumerable<CacheLevel>
	{
		private readonly CacheLevel[] _innerList;


		public bool HasRest => _innerList.Length > 1;

		public CacheLevelCollection(CacheLevel first, params CacheLevel[] rest)
		{
			_innerList = new[] { first ?? throw new ArgumentNullException(nameof(first)) }
				.Concat(rest)
				.ToArray();
		}

		public CacheLevelCollection Rest() 
			=> new CacheLevelCollection(
				_innerList.Skip(1).First(),
				_innerList.Skip(2).ToArray());

		public IEnumerator<CacheLevel> GetEnumerator() => (_innerList as IEnumerable<CacheLevel>).GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => _innerList.GetEnumerator();
	}
}
