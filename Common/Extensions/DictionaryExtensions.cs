using System;
using System.Collections.Generic;
using System.Linq;
using Common.Configuration;

namespace Common
{
    public static class DictionaryExtensions
    {
        public static bool ContainsKeyIgnoringCase(this IDictionary<string, object> dictionary, string desiredKeyOfAnyCase)
        {
            return GetKeyIgnoringCase(dictionary, desiredKeyOfAnyCase) != null;
        }

        public static string GetKeyIgnoringCase(this IDictionary<string, object> dictionary, string desiredKeyOfAnyCase)
        {
            return dictionary.FirstOrDefault(a => a.Key.Equals(desiredKeyOfAnyCase, StringComparison.OrdinalIgnoreCase)).Key;
        }

        /// <summary>
        /// Gets the value stored in this dictionary.
        /// </summary>
        /// <remarks>
        /// https://stackoverflow.com/questions/14150508/how-to-get-null-instead-of-the-keynotfoundexception-accessing-dictionary-value-b
        /// </remarks>
        /// <typeparam name="TKey">The key type.</typeparam>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <param name="dictionaty">This IDictionary.</param>
        /// <param name="key">The key.</param>
        /// <param name="defaultValue">The optional default value.</param>
        /// <returns>The value stored in this dictionary, or the default value for the type.</returns>
        public static TValue GetValue<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue = default(TValue))
        {
            TValue value;
            return dictionary.TryGetValue(key, out value) ? value : defaultValue;
        }

        public static bool TryGetValueIgnoringCase(this IDictionary<string, object> dictionary, string desiredKeyOfAnyCase, out object value)
        {
            var key = GetKeyIgnoringCase(dictionary, desiredKeyOfAnyCase);
            if (key != null)
            {
                return dictionary.TryGetValue(key, out value);
            }
            else
            {
                value = null;
                return false;
            }
        }

        public static bool TryGetValueOrDefaultIgnoringCase<V>(this IDictionary<string, object> dictionary, string key, out V value)
        {
            if (dictionary.TryGetValueIgnoringCase(key, out object objectValue) && objectValue is V)
            {
                value = (V)objectValue;
                return true;
            }
            else
            {
                value = default;
                return false;
            }
        }

        //public static bool ContainsKeyIgnoringCase(this IDictionary<string, TargetFieldMap> dictionary, string desiredKeyOfAnyCase)
        //{
        //    return GetKeyIgnoringCase(dictionary, desiredKeyOfAnyCase) != null;
        //}

        //public static string GetKeyIgnoringCase(this IDictionary<string, TargetFieldMap> dictionary, string desiredKeyOfAnyCase)
        //{
        //    return dictionary.FirstOrDefault(a => a.Key.Equals(desiredKeyOfAnyCase, StringComparison.OrdinalIgnoreCase)).Key;
        //}

        //public static bool TryGetValueIgnoringCase(this IDictionary<string, TargetFieldMap> dictionary, string desiredKeyOfAnyCase, out TargetFieldMap value)
        //{
        //    var key = GetKeyIgnoringCase(dictionary, desiredKeyOfAnyCase);
        //    if (key != null)
        //    {
        //        return dictionary.TryGetValue(key, out value);
        //    }
        //    else
        //    {
        //        value = null;
        //        return false;
        //    }
        //}
    }
}
