namespace pooledList
{
    public interface IPooledItem<T>
        where T : IPooledListData
    {
        bool visible { get; }
        object Key { get; }

        void Activate(bool toActivate);
        void SetData(T data);
    }
}
