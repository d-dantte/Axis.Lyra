using Axis.Lyra.Redis.Configuration;

namespace Axis.Lyra.Redis.Contracts
{
	public interface IRedisConfigProvider
	{
		RedisConfiguration GetConfiguration();
	}
}
