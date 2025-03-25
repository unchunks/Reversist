using System;
using System.Collections;
using System.Collections.Generic;

namespace App.Reversi
{
    public class AvailableStoneCount : IEnumerable<KeyValuePair<StoneType, int>>
    {
        public Dictionary<StoneType, int> AvailableCount { get; private set; }
        
        public AvailableStoneCount(Dictionary<StoneType, int> initialCounts)
        {
            AvailableCount = new Dictionary<StoneType, int>();

            foreach (StoneType type in Enum.GetValues(typeof(StoneType)))
            {
                if (initialCounts.TryGetValue(type, out int count))
                {
                    AvailableCount.Add(type, count);
                }
            }
        }

        public bool Decrease(StoneType type)
        {
            if (AvailableCount[type] > 0)
            {
                AvailableCount[type]--;
                return true;
            }
            return false;
        }

        public IEnumerator<KeyValuePair<StoneType, int>> GetEnumerator()
        {
            return AvailableCount.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}