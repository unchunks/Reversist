using System;
using System.Collections.Generic;

public class ObservableDictionary<TKey, TValue> : Dictionary<TKey, TValue>
{
    public event Action<TKey, TValue> OnValueChanged;

    public new TValue this[TKey key]
    {
        get => base[key];
        set
        {
            if (base.ContainsKey(key) && EqualityComparer<TValue>.Default.Equals(base[key], value))
            {
                return; // 変化がなければ何もしない
            }
            base[key] = value;
            OnValueChanged?.Invoke(key, value); // 値が変わったらイベント発火
        }
    }
}
