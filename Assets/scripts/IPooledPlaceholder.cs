using UnityEngine;

namespace pooledList
{
    /// <summary>
    /// Placeholder for a pooled item
    /// We add and remove loading and pooleditems to this
    /// </summary>
    public interface IPooledPlaceholder<T, U>
        where T : IPooledItem<U>
        where U : IPooledListData
    {
        RectTransform RectTransform { get; }
        void AddFromPool(T item);
        T ReturnToPool(RectTransform poolTransform);

    }
}
