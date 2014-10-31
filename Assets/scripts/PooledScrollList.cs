using System.Collections.Generic;
using UnityEngine;
using wcorwin.ele.util.view.pooledList;

namespace wcorwin.ele.util.view
{
    public class PooledScrollList : AbstractPooledList<PooledScrollItem, TestScrollData>
    {

        protected override void Start()
        {
            base.Start();
            List<TestScrollData> datas = new List<TestScrollData>();
            for (int i = 0; i < 60; i++)
            {
                datas.Add(new TestScrollData(i));
            }
            AddData(datas);
        }
    }

    public class TestScrollData : IPooledListData
    {
        public TestScrollData(int key)
        {
            Key = key;
        }
        public object Key { get; set; }
    }
}
