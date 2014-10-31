namespace pooledList
{
    public class PooledData : IPooledListData
    {
        public PooledData(int key)
        {
            Key = key;
        }
        public object Key { get; set; }
    }
}
