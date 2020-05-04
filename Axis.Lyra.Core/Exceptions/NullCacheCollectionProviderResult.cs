using System;

namespace Axis.Lyra.Core.Exceptions
{
	public class NullCacheCollectionProviderResult: Exception
	{
		public NullCacheCollectionProviderResult(string message = null)
			: base(message ?? "Null CacheLevelCollection provided")
		{
		}
	}
}
