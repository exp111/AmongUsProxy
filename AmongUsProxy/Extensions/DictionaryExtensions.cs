using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmongUsProxy.Extensions
{
	public static class DictionaryExtensions
	{
        public static TValue EnsureKey<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key)
    where TValue : new()
        {
            TValue val;

            if (!dict.TryGetValue(key, out val))
            {
                val = new TValue();
                dict.Add(key, val);
            }

            return val;
        }
    }
}
