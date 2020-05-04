namespace Axis.Lyra.Core.Contracts
{
	public enum CacheFeature
	{
		/// <summary>
		/// Used to implement sliding window expiration. Once the key has remained idle for the specified threshold time, it is deleted.
		/// Each time the key is accessed, the idle time is reset and the count can begin again.
		/// </summary>
		IdleThreshold,

		/// <summary>
		/// Keys can have a definit expiration time, once reached, the key is deleted, irrespective of any other condition.
		/// </summary>
		Expiration
	}
}
