using System.Collections.Generic;

namespace pooledList
{
    public interface IPooledList<T,U> 
        where T : IPooledItem<U>
        where U : IPooledListData
    {
        void AddData(IEnumerable<U> data);
        void AddData(U dataItem);
        void Clear();
    }
}
