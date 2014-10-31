using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace wcorwin.ele.util.view.pooledList
{
    public abstract class AbstractPooledList<T, U> : MonoBehaviour, IPooledList<T,U>
        where T : Component, IPooledScrollItem<U>
        where U : IPooledListData 

    {
        /// <summary>
        /// Used to determine the "visible" area of our list
        /// </summary>
        public RectTransform VisibleRect;

        /// <summary>
        /// When setting Horizontal/Vertical, we need to set the scrollrect as well.
        /// </summary>
        public ScrollRect ScrollRect;

        /// <summary>
        /// The scrollbar for our list
        /// </summary>
        public Scrollbar Scrollbar;

        /// <summary>
        /// Our grid object
        /// </summary>
        public GridLayoutGroup Grid;

        /// <summary>
        /// Sizes our content
        /// </summary>
        public RectTransform Content;

        /// <summary>
        /// Prefab of our Tile object. 
        /// *MUST* have a component of type T
        /// </summary>
        public GameObject Tile;
        
        
        /// <summary>
        /// This represents the number of objects that are retained and used in our pool
        /// This indirectly determines how much "buffer" you have, as well
        /// e.g. A grid with Rows of 5 columns and 4 visible rows
        ///      With a PoolSize of 20, we have *no* buffer
        ///      But with 40, we have a buffer of 20 (10 on each side!)
        /// </summary>
        public int PoolSize;

        protected List<List<T>> Items = new List<List<T>>();

        //TODO: Allow for horizontal expanding lists!
        public int columns;
        public int rows { get { return Items.Count; } }
        private int itemCt = 0;
        public bool doNotUpdate = false;

        public Vector2 CellSize;
        public Vector2 Padding;

        //Track the first and last index of currently active tiles
        public Vector2 ActiveIndices = Vector2.up;

        private float _width = -1f;
        private float width
        {
            get
            {
                if (_width == -1f)
                    _width = columns * (CellSize.x) + totalPaddingX;

                return _width;
            }
        }

        private float height
        {
            get
            {
                return rows * CellSize.y + totalPaddingY;
            }
        }
        private float totalPaddingX
        {
            get
            {
                return (columns - 1) * Padding.x;
            }
        }
        private float totalPaddingY
        {
            get
            {
                return (rows - 1) * Padding.y;
            }
        }

        private Vector2 _viewableArea = Vector2.zero;

        private Vector2 viewableArea
        {
            get
            {
                if (VisibleRect == null) return Vector2.zero;

                if (_viewableArea == Vector2.zero)
                    _viewableArea = new Vector2(VisibleRect.rect.width, VisibleRect.rect.height);

                return _viewableArea;
            }
        }
        protected virtual void Awake()
        {
            //TODO: Calculate rows/columns?

            //add a listener to scrollbar on value changed
            //when that updates, we can check what is visible!
            //we need a list of active items (or rather, a start and end index for what is active)
            //In the vertical only, we have a start row and end row to know what is active
            //  for horizontal, we have start col, end col

            //when an update happens, we need to know what should be active, and if that has changed, we should make some inactive (and return to pool)
            //      and then activate and add an item from pool to anything that is becoming active

            //viewable rows: 
            ActiveIndices = CalculateActiveIndices(1);
            Scrollbar.onValueChanged.AddListener(OnScrollbarValue);
        }

        protected virtual void Start()
        {
//            Scrollbar.onValueChanged.Invoke(1);
        }
        private void RecalculateSize()
        {
            //TODO Need to adjust this for differing anchor setups
            Content.sizeDelta = new Vector2(width, height);
        }
        private void AddItem(T item)
        {

            List<T> lastRow = GetRowForNextItem();
            lastRow.Add(item);
            itemCt++;

            item.Activate(RowIsActive(Items.Count - 1));
        }
        private List<T> GetRowForNextItem()
        {
            bool createNext = itemCt % columns == 0; //mod 0 means last row is full
            List<T> row = createNext ? new List<T>() : Items.Last();
            if (createNext)
            {
                Items.Add(row);
                RecalculateSize();
            }

            return row;
        }

        private bool RowIsActive(int row)
        {
            return row >= ActiveIndices.x && row <= ActiveIndices.y;
        }

        /// <summary>
        /// Mark tiles as active or inactive
        /// </summary>
        /// <param name="px"></param>
        private void OnScrollbarValue(float px)
        {
            if (doNotUpdate ||
                VisibleRect == null ||
                Grid == null ||
                ScrollRect == null)
                return;

            Vector2 newActiveIndices = CalculateActiveIndices(px);

            if (ActiveIndices != newActiveIndices) //If they're the same, ignore!
            {
                /*
                 * lets say we are moving from 1,5 to 3,7
                 * We need to deactivate [1,2]
                 * We need to activate [6,7]
                 * */
                Vector2 diff = newActiveIndices - ActiveIndices;
                Debug.Log(diff);


                //positive is iterating higher through the rows

                //if x > 0, deactivate where Active.x <= row < newActive.x
                //if x < 0, activate where newActive.x <= row < Active.x //equal to because we are enabling everything up to the new active
                //if y > 0, activate where newActive.y >= row > Active.y
                //if y < 0, deactivate where Active.y <= row < newActive.y  //equal to because we are disabling the old active up until the new active

                if (diff.x > 0)
                {
                    for (int i = (int)ActiveIndices.x; i < newActiveIndices.x; i++)
                        ActivateRow(i, false);
                }
                else if (diff.x < 0)
                {
                    for (int i = (int)ActiveIndices.x; i >= newActiveIndices.x; i--)
                        ActivateRow(i, true);
                }

                if (diff.y > 0)
                {
                    for (int i = (int)ActiveIndices.y + 1; i <= newActiveIndices.y; i++)
                        ActivateRow(i, true);
                }
                else if (diff.y < 0)
                {
                    for (int i = (int)ActiveIndices.y; i > newActiveIndices.y; i--)
                        ActivateRow(i, false);
                }

                ActiveIndices = newActiveIndices;
            }
        }

        private void ActivateRow(int row, bool activate)
        {
            List<T> rowToAdjust = Items[row];
            foreach (var item in rowToAdjust)
            {
                item.Activate(activate);
            }
        }

        //TODO: This assumes vertical scrolling only!
        private Vector2 CalculateActiveIndices(float scrollbarValue)
        {
            if (VisibleRect == null) return Vector2.zero;

            float fullScrollbarValue = height - viewableArea.y; 
            float pos = Mathf.Lerp(fullScrollbarValue, 0f, scrollbarValue); //Lerp from the top to the bottom of scrollbar to determine position

            float lowestPosVisible = pos;
            float highestPosVisible = pos + viewableArea.y;

            int min = ViewableRowAtPos(lowestPosVisible);
            int max = ViewableRowAtPos(highestPosVisible);

            Debug.Log("Calculated active indices as: " + min + " , " + max);
            return new Vector2(min, max);
        }

        private int ViewableRowAtPos(float pos)
        {
            return (int)(pos / (CellSize.y + Padding.y));
        }

        public void AddData(IEnumerable<U> data)
        {
            doNotUpdate = true;
            foreach (var item in data)
                AddData(item);
            doNotUpdate = false;
            ActiveIndices = CalculateActiveIndices(Scrollbar.value);
            Scrollbar.onValueChanged.Invoke(1);
        }

        public void AddData(U dataItem)
        {
            if (Tile == null)
            {
                Debug.LogError("No Tile set for PooledList");
                return;
            }
            //Instantiate a T
            GameObject go = (GameObject) Instantiate(Tile);
            T newTile = go.GetComponent<T>();
            newTile.transform.SetParent(Grid.transform, false);
            newTile.SetData(dataItem);
            AddItem(newTile);
        }

        public void Clear()
        {
            
        }
    }
}
