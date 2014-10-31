using UnityEngine;
using UnityEngine.UI;

namespace wcorwin.ele.util.view
{
    public class PooledScrollItem : MonoBehaviour, IPooledScrollItem<TestScrollData>
    {
        public Image Image;
        public Text Text;
        public Color InactiveColor;
        public Color ActiveColor;

        public void RemoveFromPool()
        {
            Debug.Log("Remove");
        }

        public void AddToPool()
        {
            Debug.Log("Add");
        }

        public bool visible 
        {
            get
            {
                return true;
            }
        }

        public object Key { get; private set; }

        public void Activate(bool toActivate)
        {
            Image.color = toActivate ? ActiveColor : InactiveColor;
        }

        public void SetData(TestScrollData data)
        {
            Key = data.Key;
            Text.text = data.Key.ToString();
        }
    }
}
