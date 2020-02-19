namespace VstsPipelineSync
{
    using System.Collections.Generic;

    public static class DictionaryExtension
    {
        public static TValue GetIfExists<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key)
        {
            TValue value;
            if (dict.TryGetValue(key, out value))
            {
                return value;
            }

            return default(TValue);
        }

        public static bool Upsert<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, TValue value)
        {
            if (dict.ContainsKey(key))
            {
                dict[key] = value;
                return false;
            }
            
            dict.Add(key, value);
            return true;
        }
    }
}
