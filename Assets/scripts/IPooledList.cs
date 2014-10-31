using System.Collections.Generic;

namespace wcorwin.ele.util.view.pooledList
{
    public interface IPooledList<T,U> 
        where T : IPooledScrollItem<U>
        where U : IPooledListData
    {
        void AddData(IEnumerable<U> data);
        void AddData(U dataItem);
        void Clear();
    }
}
