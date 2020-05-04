using Axis.Lyra.Core.Models;

namespace Axis.Lyra.Core.Contracts
{
	public interface ICacheLevelCollectionProvider
	{
		CacheLevelCollection GetCacheLevels();
	}
}
