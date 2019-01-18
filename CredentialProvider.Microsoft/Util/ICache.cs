// Copyright (c) Microsoft. All rights reserved.
//
// Licensed under the MIT license.

namespace NuGetCredentialProvider.Util
{
    public interface ICache<TKey, TValue>
    {
        TValue this[TKey key] { get; set; }

        bool ContainsKey(TKey key);

        bool TryGetValue(TKey key, out TValue value);

        void Remove(TKey key);
    }

    public class NoOpCache<TKey, TValue> : ICache<TKey, TValue>
    {
        public TValue this[TKey key]
        {
            get => default;
            set
            {
            }
        }

        public bool ContainsKey(TKey key)
        {
            return false;
        }

        public void Remove(TKey key)
        {
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            value = default;

            return false;
        }
    }
}
