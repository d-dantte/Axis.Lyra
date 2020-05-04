using Axis.Luna.Operation;
using Axis.Lyra.Core.Models;
using System;
using System.Collections.Generic;

namespace Axis.Lyra.Core.Contracts
{
	/// <summary>
	/// Interface representing the contract for Caches
	/// </summary>
	public interface ICache
	{
		/// <summary>
		/// A set of features implemented by this cache
		/// </summary>
		CacheFeature[] FeatureSet { get; }

		/// <summary>
		/// Sets or overwrites a key-value pair
		/// </summary>
		/// <param name="key"></param>
		/// <param name="info"></param>
		/// <returns>An operation encapsulating the result of the request</returns>
		Operation Set(string key, CacheEntryInfo info);

		/// <summary>
		/// Gets the value associated with the given key; fails otherwise.
		/// </summary>
		/// <param name="key">The key whose value is requested</param>
		/// <returns>An operation encapsulating the result of the request</returns>
		/// <exception cref="Exceptions.KeyNotFoundException">If the key was not found</exception>
		Operation<byte[]> Get(string key);
		
		/// <summary>
		/// Gets the key associated with the given key, or sets it and returns the value set.
		/// </summary>
		/// <param name="key">The key whose value is requested (or written to)</param>
		/// <param name="generator">A function that generates the value if a write is required</param>
		/// <returns>An operation encapsulating the result of the request</returns>
		Operation<byte[]> GetOrSet(string key, Func<string, CacheEntryInfo> generator);

		/// <summary>
		/// Queries for the presence of a key in the cache. This method does not affect any sliding expiration times of the key in question
		/// </summary>
		/// <param name="key"></param>
		/// <returns>The encapsulating operation</returns>
		Operation<bool> HasKey(string key);

		/// <summary>
		/// Using the "TryGet*" pattern, returns as an out parameter, the value associated to a key, or null, as well as a boolean indicating
		/// success of the operation
		/// </summary>
		/// <param name="key">The key</param>
		/// <param name="value">The value associated to the key</param>
		/// <returns>A boolean indicating success of the operation</returns>
		bool TryGet(string key, out byte[] value);

		/// <summary>
		/// Reset the expiration time ONLY IF one was set before.
		/// </summary>
		/// <param name="key">The key whose expiration time is to be reset</param>
		/// <param name="newExpirationTime">The new expiration time</param>
		/// <returns>Operation encapsulating the result of the call</returns>
		Operation ResetExpiration(string key, DateTimeOffset newExpirationTime);

		/// <summary>
		/// Reset the idle-threshold time ONLY IF one was set before.
		/// </summary>
		/// <param name="key">The key whose idle-threshold time is to be reset</param>
		/// <param name="idleThreshold">The new idle-threshold time</param>
		/// <returns>Operation encapsulating the result of the call</returns>
		Operation ResetIdleThreshold(string key, TimeSpan idleThreshold);

		/// <summary>
		/// Remove the key if it exists and notify anyone subscribed that the key is no longer valid
		/// </summary>
		/// <param name="key">the key to invalidate</param>
		/// <returns>The encapsulating operation</returns>
		Operation Invalidate(string key);

		/// <summary>
		/// Event representing invaidation of keys
		/// </summary>
		event EventHandler<KeyInvalidationEventArgs> KeyInvalidated;
	}
}
