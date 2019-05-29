using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ResourceMoudle
{
    public class DataUtil
    {
        public static Pair<T1, T2> MakePair<T1, T2>(T1 a, T2 b)
        {
            return new Pair<T1, T2>(a, b);
        }

        public static Triple<T1, T2, T3> MakeTriple<T1, T2, T3>(T1 a, T2 b, T3 c)
        {
            return new Triple<T1, T2, T3>(a, b, c);
        }

        public static T[] SubArray<T>(T[] array, int index, int length)
        {
            T[] result = new T[length];
            Array.Copy(array, index, result, 0, length);
            return result;
        }

        public static V GetValueOrDefault<K, V>(Dictionary<K, V> d, K key)
        {
            V value;
            d.TryGetValue(key, out value);
            return value;
        }

        public static V GetValueOrDefault<K, V>(Dictionary<K, V> d, K key, V defaultValue)
        {
            V value;

            if (d.TryGetValue(key, out value))
            {
                return value;
            }
            else
            {
                return defaultValue;
            }
        }

        public static V GetValueOrDefault<K, V>(Dictionary<K, V> d, K key, Func<V> defaultValueProvider)
        {
            V value;

            if (d.TryGetValue(key, out value))
            {
                return value;
            }
            else
            {
                return defaultValueProvider();
            }
        }

    }
}