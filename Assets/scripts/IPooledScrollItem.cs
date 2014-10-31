using wcorwin.ele.util.view.pooledList;

namespace wcorwin.ele.util.view
{
    public interface IPooledScrollItem<T>
        where T : IPooledListData
    {
        bool visible { get; }
        object Key { get; }

        void Activate(bool toActivate);
        void SetData(T data);
    }
}
