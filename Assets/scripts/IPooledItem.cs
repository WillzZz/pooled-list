namespace pooledList
{
    public interface IPooledItem<T>
        where T : IPooledListData
    {
        object Key { get; }
        void SetData(T data);
    }
}
