using UnityEngine;

namespace pooledList
{
    public class PoolEmptyObject : MonoBehaviour
    {
        public IPooledListData data { get; set; }
        public Component item { get; set; }
    }
}
