using System.Collections.Generic;

namespace pooledList
{
    public class PooledList : AbstractPooledList<PooledItem, PooledData>
    {

        protected override void Start()
        {
            base.Start();
            List<PooledData> datas = new List<PooledData>();
            for (int i = 0; i < 120; i++)
            {
                datas.Add(new PooledData(i));
            }
            AddData(datas);
        }
    }
}
