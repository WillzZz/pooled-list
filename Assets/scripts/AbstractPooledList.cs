using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace pooledList
{
    public abstract class AbstractPooledList<T, U> : MonoBehaviour, IPooledList<T,U>
        where T : Component, IPooledItem<U>
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
        /// Whether or not to prepopulate at awake(start?) time.
        /// This will create an upfront perf cost instead of deferring.
        /// </summary>
        public bool Prepopulate; 
        
        /// <summary>
        /// This represents the number of objects that are retained and used in our pool
        /// This indirectly determines how much "buffer" you have, as well
        /// e.g. A grid with Rows of 5 columns and 4 visible rows
        ///      With a PoolSize of 20, we have *no* buffer
        ///      But with 40, we have a buffer of 20 (10 on each side!)
        /// </summary>
        public int PoolSize;

        /// <summary>
        /// The parent of objects which are in the pool. This is inactive!
        /// </summary>
        public Transform PoolParent;

        public Vector2 CellSize;
        public Vector2 Padding;
        public int BufferedRows;
        public int columns;

        protected readonly List<List<PoolEmptyObject>> Empties = new List<List<PoolEmptyObject>>(); 
        protected readonly Stack<T> Pool = new Stack<T>(); 
        protected readonly List<T> InUse = new List<T>();

        //Track the first and last index of currently active tiles
        private Vector2 ActiveIndices = Vector2.zero;

        //TODO: Allow for horizontal expanding lists!
        
        private int rows { get { return Empties.Count; } }
        private bool doNotUpdate;
        private int itemCt;

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

            if (Prepopulate)
            {
                //TODO
            }


            Scrollbar.onValueChanged.AddListener(OnScrollbarValue);
        }

        protected virtual void Start()
        {
        }
        private void RecalculateSize()
        {
            //TODO Need to adjust this for differing anchor setups
            Content.sizeDelta = new Vector2(width, height);
        }

        private List<PoolEmptyObject> GetRowForNextEmpty()
        {
            bool createNext = itemCt % columns == 0; //mod 0 means last row is full
            List<PoolEmptyObject> row = createNext ? new List<PoolEmptyObject>() : Empties.Last();
            if (createNext)
            {
                Empties.Add(row);
                RecalculateSize();
            }

            return row;
        }

        /// <summary>
        /// Mark tiles as active or inactive
        /// </summary>
        /// <param name="px"></param>
        protected void OnScrollbarValue(float px)
        {
            if (doNotUpdate ||
                VisibleRect == null ||
                Grid == null ||
                ScrollRect == null)
                return;


            Vector2 newActiveIndices = CalculateActiveIndices(px);
            bool performCorrection = false;

            //Special case for first 
            if (ActiveIndices == Vector2.zero)
            {
                for (int i = (int) newActiveIndices.x; i <= newActiveIndices.y; i++)
                {
                    ActivateRow(i,true);
                }
            }
            else if (ActiveIndices != newActiveIndices) //If they're the same, ignore!
            {
                /*
                 * lets say we are moving from 1,5 to 3,7
                 * We need to deactivate [1,2]
                 * We need to activate [6,7]
                 * */
                Vector2 diff = newActiveIndices - ActiveIndices;
//                Debug.Log("diff: " + diff + " active: " + ActiveIndices + " new: " + newActiveIndices + " pool: " + Pool.Count + " inuse: " + InUse.Count);

                //positive is iterating higher through the rows

                //if x > 0, deactivate where Active.x <= row < newActive.x
                //if x < 0, activate where newActive.x <= row < Active.x //equal to because we are enabling everything up to the new active
                //if y > 0, activate where newActive.y >= row > Active.y
                //if y < 0, deactivate where Active.y <= row < newActive.y  //equal to because we are disabling the old active up until the new active

                
                //Deactivate first
                if (diff.x > 0)
                {
                    for (int i = (int)ActiveIndices.x; i < newActiveIndices.x; i++)
                        performCorrection = performCorrection || ActivateRow(i, false);
                }
                if (diff.y < 0)
                {
                    for (int i = (int)ActiveIndices.y; i > newActiveIndices.y; i--)
                        performCorrection = performCorrection || ActivateRow(i, false);
                }
                
                //Then activate
                if (diff.x < 0)
                {
                    for (int i = (int)newActiveIndices.x; i < ActiveIndices.x; i++)
                        performCorrection = performCorrection || ActivateRow(i, true);
                }
                if (diff.y > 0)
                {
                    for (int i = (int)ActiveIndices.y + 1; i <= newActiveIndices.y; i++)
                        performCorrection = performCorrection || ActivateRow(i, true);
                }

            }
            ActiveIndices = newActiveIndices;

                if (performCorrection)
                    CorrectErrors();
        }

        /// <summary>
        /// Perform Error Correction
        /// </summary>
        private void CorrectErrors()
        {
            //We need to iterate each row and enable or disable accordingly. Sadly.

            int incorrect = 0;
            for (int i = 0; i < Empties.Count; i++)
            {
                //I need to know if it was necessary.
                bool correct = ActivateRow(i, RowIsActive(i));
                if (!correct)
                    incorrect++;
            }

            Debug.LogError("Correction was necessary for " + incorrect + " rows");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="row"></param>
        /// <param name="activate"></param>
        /// <returns>Whether to preform a full update upon a failure</returns>
        private bool ActivateRow(int row, bool activate)
        {
            List<PoolEmptyObject> rowToAdjust = Empties[row];
            foreach (var item in rowToAdjust)
            {
                if (activate)
                {
                    if (item.item != null)
                    {
                        return true;
                    }
                    T poolItem = GetObjectFromPool(item.data);
                    item.item = poolItem;
                    poolItem.transform.SetParent(item.transform, false);
                }
                else
                {
                    if (item.item == null)
                    {
                        return true;
                    }
                    ReturnObjectToPool((T) item.item);
                    item.item = null;
                }
            }
            return false;
        }

        //TODO: This assumes vertical scrolling only!
        private Vector2 CalculateActiveIndices(float scrollbarValue)
        {
            if (VisibleRect == null) return Vector2.zero;

            float fullScrollbarValue = height - viewableArea.y; 
            float pos = Mathf.Lerp(fullScrollbarValue, 0f, scrollbarValue); //Lerp from the top to the bottom of scrollbar to determine position

            float lowestPosVisible = pos;
            float highestPosVisible = pos + viewableArea.y;

            int min = Mathf.Max(ViewableRowAtPos(lowestPosVisible) - BufferedRows, 0);
            int max = Mathf.Min(ViewableRowAtPos(highestPosVisible) + BufferedRows, rows-1);


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
            Scrollbar.onValueChanged.Invoke(1);
        }

        public void AddData(U dataItem)
        {
            CreateEmptyObject(dataItem);
            itemCt++;
        }

        private T CreateT(U dataItem)
        {
            if (Tile == null)
            {
                Debug.LogError("Tile is not set");
                return null;
            }
            //Instantiate a T
            GameObject go = (GameObject)Instantiate(Tile);
            T newTile = go.GetComponent<T>();
            newTile.transform.SetParent(Grid.transform, false);
            newTile.SetData(dataItem);
            InUse.Add(newTile);
            return newTile;
        }

        private PoolEmptyObject CreateEmptyObject(U dataItem)
        {
            GameObject go = new GameObject("TileParent", typeof(RectTransform));
            PoolEmptyObject empty = go.AddComponent<PoolEmptyObject>();
            empty.data = dataItem;
            empty.transform.SetParent(Grid.transform, false);

            List<PoolEmptyObject> rowOfEmpties = GetRowForNextEmpty();
            rowOfEmpties.Add(empty);

            return empty;
        }

        private T GetObjectFromPool(IPooledListData dataItem)
        {
            T retv = null;
            if (Pool.Count > 0)
            {
                //Use from the pool
                retv = Pool.Pop();
                InUse.Add(retv);
            }
            else if (Pool.Count + InUse.Count < PoolSize)
            {
                //Create a new one
                retv = CreateT((U) dataItem);
            }
            else if (PoolSize > 0)
            {
                throw new Exception("Pool Limit Exceeded. PoolSize: " + PoolSize);
            }

            retv.SetData((U) dataItem);

            return retv;
        }

        private void ReturnObjectToPool(T pooledObject, bool remove=true)
        {
            if (pooledObject == null) return;

            pooledObject.transform.SetParent(PoolParent, false);
            Pool.Push(pooledObject);
            if (remove)
                InUse.Remove(pooledObject);

        }

        public void Clear()
        {
            foreach (var item in InUse)
            {
                ReturnObjectToPool(item, false); //False so I can iterate the list without craziness. That might work though... test me!!!
            }
            InUse.Clear();
            Empties.Clear();
        }

        private int _maxActiveCells = -1;
        private int MaxActiveCells
        {
            get
            {
                if (_maxActiveCells == -1)
                {

                    _maxActiveCells = MaxVisibleConstrainedAxis*MaxVisiblePrimaryAxis;
                }
                return _maxActiveCells;
            }
        }

        //e.g. max visible rows
        private int MaxVisiblePrimaryAxis
        {
            get
            {
                return (int) (VisibleRect.rect.height/CellSize.y + 2);
            }
        }

        //e.g. max visible columns
        private int MaxVisibleConstrainedAxis
        {
            get
            {
                return columns;
            }
        }

        private bool RowIsActive(int row)
        {
            return row >= ActiveIndices.x && row <= ActiveIndices.y;
        }
    }
}
