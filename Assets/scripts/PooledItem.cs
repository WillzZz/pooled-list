﻿using UnityEngine;
using UnityEngine.UI;

namespace pooledList
{
    public class PooledItem : MonoBehaviour, IPooledItem<PooledData>
    {
        public Image Image;
        public Text Text;
        public Color InactiveColor;
        public Color ActiveColor;

        public void RemoveFromPool()
        {
            Debug.Log("Remove");
        }

        public void AddToPool()
        {
            Debug.Log("Add");
        }

        public object Key { get; private set; }

        public void SetData(PooledData data)
        {
            Key = data.Key;
            Text.text = data.Key.ToString();
        }
    }
}
