using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace RoR2BepInExPack.Utilities;

/// <summary>
/// Alternative implementation for ConditionalWeakTable that actually works
/// </summary>
/// <typeparam name="TKey"></typeparam>
/// <typeparam name="TValue"></typeparam>
public class FixedConditionalWeakTable<TKey, TValue> : IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>, FixedConditionalWeakTableManager.IShrinkable
    where TKey : class
    where TValue : class
{
    private ConstructorInfo cachedConstructor = null;
    private readonly ConcurrentDictionary<WeakReferenceWrapper<TKey>, TValue> valueByKey = new(new WeakReferenceWrapperComparer<TKey>());

    /// <inheritdoc/>
    public TValue this[TKey key]
    {
        get
        {
            return valueByKey[new WeakReferenceWrapper<TKey>(key, true)];
        }
        set
        {
            valueByKey[new WeakReferenceWrapper<TKey>(key, false)] = value;
        }
    }

    /// <inheritdoc/>
    public ICollection<TKey> Keys
    {
        get
        {
            List<TKey> keys = new List<TKey>(valueByKey.Count);
            foreach (WeakReferenceWrapper<TKey> keyReference in valueByKey.Keys)
            {
                if (keyReference.weakReference.TryGetTarget(out TKey key))
                {
                    keys.Add(key);
                }
            }

            return keys.AsReadOnly();
        }
    }

    /// <inheritdoc/>
    public ICollection<TValue> Values
    {
        get
        {
            List<TValue> values = new List<TValue>(valueByKey.Count);
            foreach ((WeakReferenceWrapper<TKey> keyReference, TValue value) in valueByKey)
            {
                if (keyReference.weakReference.TryGetTarget(out _))
                {
                    values.Add(value);
                }
            }

            return values.AsReadOnly();
        }
    }

    /// <summary>
    /// Gets the number of living key/value pairs contained in the <see cref="FixedConditionalWeakTable{TKey, TValue}"/>
    /// </summary>
    /// <remarks>
    /// In order to return an accurate value, getting this value requires a re-count of the collection to determine which values are still alive in memory, use <see cref="SpeculativeCount"/> in order to avoid this re-count
    /// </remarks>
    public int Count
    {
        get
        {
            ForceShrink();
            return valueByKey.Count;
        }
    }

    /// <summary>
    /// Gets the approximate number of key/value pairs contained in the <see cref="FixedConditionalWeakTable{TKey, TValue}"/>
    /// </summary>
    /// <remarks>
    /// This value is always greater than or equal to the number of living elements in the table, depending on when the table was last re-counted.
    /// </remarks>
    public int SpeculativeCount => valueByKey.Count;

    bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;

    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => Keys;

    IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => Values;

    public FixedConditionalWeakTable()
    {
        FixedConditionalWeakTableManager.Add(this);
    }

    /// <summary>
    /// Add a value for the specified key
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public void Add(TKey key, TValue value)
    {
        if (key is null)
        {
            throw new ArgumentNullException(nameof(key));
        }
        if (!valueByKey.TryAdd(new WeakReferenceWrapper<TKey>(key, false), value))
        {
            throw new ArgumentException($"The key already exists");
        }
    }

    /// <summary>
    /// Removes a key and its value from the table.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public bool Remove(TKey key)
    {
        return valueByKey.TryRemove(new WeakReferenceWrapper<TKey>(key, true), out _);
    }

    /// <summary>
    /// Tries to get the value of the specified key.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public bool TryGetValue(TKey key, out TValue value)
    {
        return valueByKey.TryGetValue(new WeakReferenceWrapper<TKey>(key, true), out value);
    }

    /// <summary>
    /// Gets the value of the specified key, or creates a new one with defaultFunc and adds it to the table
    /// </summary>
    /// <param name="key"></param>
    /// <param name="defaultFunc"></param>
    /// <returns></returns>
    public TValue GetValue(TKey key, Func<TKey, TValue> defaultFunc)
    {
        if (TryGetValue(key, out var value))
        {
            return value;
        }

        value = defaultFunc(key);
        Add(key, value);
        return value;
    }

    /// <summary>
    /// Gets the value of the specified key, or creates a new one with default constructor and adds it to the table
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    /// <exception cref="MissingMethodException"></exception>
    public TValue GetOrCreateValue(TKey key)
    {
        if (TryGetValue(key, out var value))
        {
            return value;
        }

        if (cachedConstructor is null)
        {
            var type = typeof(TValue);
            cachedConstructor = type.GetConstructor(Array.Empty<Type>());
            if (cachedConstructor is null)
            {
                throw new MissingMethodException($"{type.FullName} doesn't have public parameterless constructor");
            }
        }

        value = (TValue)cachedConstructor.Invoke(Array.Empty<object>());
        Add(key, value);
        return value;
    }

    void ForceShrink()
    {
        ((FixedConditionalWeakTableManager.IShrinkable)this).Shrink();
    }

    void FixedConditionalWeakTableManager.IShrinkable.Shrink()
    {
        foreach (var item in valueByKey)
        {
            if (!item.Key.weakReference.TryGetTarget(out _))
            {
                valueByKey.TryRemove(new WeakReferenceWrapper<TKey>(item.Key.targetHashCode), out _);
            }
        }
    }

    /// <inheritdoc/>
    public bool ContainsKey(TKey key)
    {
        return valueByKey.ContainsKey(new WeakReferenceWrapper<TKey>(key, true));
    }

    /// <inheritdoc/>
    public void Clear()
    {
        valueByKey.Clear();
    }

    void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> kvp)
    {
        Add(kvp.Key, kvp.Value);
    }

    bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> kvp)
    {
        return TryGetValue(kvp.Key, out TValue value) && EqualityComparer<TValue>.Default.Equals(value, kvp.Value);
    }

    void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        if (array is null)
            throw new ArgumentNullException(nameof(array));

        if (arrayIndex < 0 || arrayIndex >= array.Length)
            throw new ArgumentOutOfRangeException(nameof(arrayIndex), $"{nameof(arrayIndex)} is not a valid index in {nameof(array)}");

        int count = Count;
        if (arrayIndex + count > array.Length)
            throw new ArgumentOutOfRangeException(nameof(array), "Destination array is not long enough to copy all the items in the collection.");

        foreach ((WeakReferenceWrapper<TKey> keyReference, TValue value) in valueByKey)
        {
            if (keyReference.weakReference.TryGetTarget(out TKey key))
            {
                array[arrayIndex++] = new KeyValuePair<TKey, TValue>(key, value);
            }
        }
    }

    bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> kvp)
    {
        return TryGetValue(kvp.Key, out TValue value) && EqualityComparer<TValue>.Default.Equals(value, kvp.Value) && Remove(kvp.Key);
    }

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        foreach ((WeakReferenceWrapper<TKey> keyWrapper, TValue value) in valueByKey)
        {
            if (keyWrapper.weakReference.TryGetTarget(out TKey key))
            {
                yield return new KeyValuePair<TKey, TValue>(key, value);
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private readonly struct WeakReferenceWrapper<T> where T : class
    {
        public readonly int targetHashCode;
        public readonly WeakReference<T> weakReference;
        public readonly T target;

        public WeakReferenceWrapper(T target, bool strongReference)
        {
            targetHashCode = target.GetHashCode();
            if (strongReference)
            {
                this.target = target;
                weakReference = null;
            }
            else
            {
                this.target = null;
                weakReference = new WeakReference<T>(target);
            }
        }

        public WeakReferenceWrapper(int targetHashCode)
        {
            this.targetHashCode = targetHashCode;
            target = null;
            weakReference = null;
        }
    }

    private readonly struct WeakReferenceWrapperComparer<T> : IEqualityComparer<WeakReferenceWrapper<T>> where T : class
    {
        public bool Equals(WeakReferenceWrapper<T> first, WeakReferenceWrapper<T> second)
        {
            var firstTarget = first.target;
            var secondTarget = second.target;

            //No target and reference means we are looking for dead items to delete
            if (firstTarget is null && first.weakReference is null)
            {
                return !second.weakReference.TryGetTarget(out _);
            }
            if (secondTarget is null && second.weakReference is null)
            {
                return !first.weakReference.TryGetTarget(out _);
            }

            if (firstTarget is null && !first.weakReference.TryGetTarget(out firstTarget))
            {
                return false;
            }

            if (secondTarget is null && !second.weakReference.TryGetTarget(out secondTarget))
            {
                return false;
            }

            return firstTarget == secondTarget;
        }

        public int GetHashCode(WeakReferenceWrapper<T> obj)
        {
            return obj.targetHashCode;
        }
    }
}

internal static class FixedConditionalWeakTableManager
{
    private const int shrinkAttemptDelay = 2000;

    private static readonly object lockObject = new();
    private static readonly List<WeakReference<IShrinkable>> instances = new();
    private static int lastCollectionCount = 0;

    public static void Add(IShrinkable weakTable)
    {
        lock (lockObject)
        {
            if (instances.Count == 0)
            {
                new Thread(ShrinkThreadLoop).Start();
            }
            instances.Add(new WeakReference<IShrinkable>(weakTable));
        }
    }

    private static void ShrinkThreadLoop()
    {
        while (true)
        {
            //Once in a while if there was garbage collection clean up dead references
            Thread.Sleep(shrinkAttemptDelay);
            var newCollectionCount = GC.CollectionCount(2);
            if (lastCollectionCount == newCollectionCount)
            {
                continue;
            }
            lastCollectionCount = newCollectionCount;

            lock (lockObject)
            {
                for (var i = instances.Count - 1; i >= 0; i--)
                {
                    if (!instances[i].TryGetTarget(out var weakTable))
                    {
                        instances.RemoveAt(i);
                        continue;
                    }

                    weakTable.Shrink();
                }
                if (instances.Count == 0)
                {
                    return;
                }
            }
        }
    }

    internal interface IShrinkable
    {
        void Shrink();
    }
}
