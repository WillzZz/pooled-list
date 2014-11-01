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
        /// When setting Horizontal/Vertical, we need to set the scrollrect as well.
        /// </summary>
        public ScrollRect ScrollRect;

        /// <summary>
        /// Our grid object
        /// </summary>
        public GridLayoutGroup Grid;

        /// <summary>
        /// Prefab of our Tile object. 
        /// *MUST* have a component of type T
        /// </summary>
        public GameObject Tile;

        /// <summary>
        /// This represents the number of objects that are retained and used in our pool
        /// </summary>
        public int PoolSize;

        /// <summary>
        /// The buffer is how many extra items in each direction
        /// to include as an added precaching mechanism.
        /// This is measured in rows or columns
        /// </summary>
        public int Buffer;


        private GridLayoutGroup.Constraint _constraint;
        private GridLayoutGroup.Constraint constraint
        {
            get
            {
                if (Grid.constraint == GridLayoutGroup.Constraint.Flexible)
                    throw new PooledListException("PooledList does not support the Flexible GridConstraint");
                return Grid.constraint;
            }
        }
        /// <summary>
        /// The scrollbar for our list
        /// </summary>
        protected Scrollbar Scrollbar
        {
            get
            {
                return constraint == GridLayoutGroup.Constraint.FixedRowCount ?
                    ScrollRect.horizontalScrollbar :
                    ScrollRect.verticalScrollbar;
            }
        }
        /// <summary>
        /// The parent of objects which are in the pool. This is inactive!
        /// </summary>
        private Transform _poolParent;
        protected Transform PoolParent
        {
            get
            {
                if (_poolParent == null)
                {
                    GameObject go = new GameObject("PoolParent", typeof(RectTransform));
                    go.SetActive(false);
                    _poolParent = go.transform;
                    _poolParent.SetParent(transform);
                }

                return _poolParent;
            }
        }

        /// <summary>
        /// Used to determine the "visible" area of our list
        /// </summary>
        private RectTransform _visibleRect;
        protected RectTransform VisibleRect
        {
            get
            {
                if (ScrollRect != null && _visibleRect == null)
                {
                    _visibleRect = ScrollRect.GetComponent<RectTransform>();
                }
                return _visibleRect;
            }
        }

        protected int columns
        {
            get
            {
                return constraint == GridLayoutGroup.Constraint.FixedColumnCount
                    ? Grid.constraintCount
                    : Empties.Count;
            }
        }

        protected int rows
        {
            get
            {
                return constraint == GridLayoutGroup.Constraint.FixedRowCount
                    ? Grid.constraintCount
                    : Empties.Count;
            } 
        }

        protected int constrainedAxisCount
        {
            get
            {
                return Grid.constraintCount;
            }
        }

        protected int secondaryAxisCount
        {
            get
            {
                return Empties.Count;
            }
        }

        //These can both offer Null Refs if Grid isn't added. That's to be expected.
        protected Vector2 CellSize { get { return Grid.cellSize; } }
        protected Vector2 Spacing { get { return Grid.spacing; } }

        protected readonly List<List<PoolEmptyObject>> Empties = new List<List<PoolEmptyObject>>(); 
        protected readonly Stack<T> Pool = new Stack<T>(); 
        protected readonly List<T> InUse = new List<T>();

        //Track the first and last index of currently active tiles
        protected Vector2 ActiveIndices = Vector2.zero;

        //TODO: Allow for horizontal expanding lists!

        protected virtual void Awake()
        {
            if (Grid == null)
            {
                Debug.LogError("PooledList has no Grid component. Please set one via Inspector.");
            }
            else if (Grid.constraint == GridLayoutGroup.Constraint.Flexible)
            {
                Debug.LogError("PooledList does not yet support Flexible Grid constraint");
            }

            if (ScrollRect == null)
            {
                Debug.LogError("PooledList has no ScrollRect component. Please set one via Inspector.");
            }

            if (Scrollbar == null)
            {
                Debug.LogError("PooledList cannot find a scrollbar. Please ensure ScrollRect has one set.");
            }

            if (Tile == null)
            {
                Debug.LogError("PooledList does not have a Tile Prefab set. ");
            }
        }
        protected virtual void Start()
        {
            Scrollbar.onValueChanged.AddListener(OnScrollbarValue);
        }

        private bool doNotUpdate;
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
                return (columns - 1) * Spacing.x;
            }
        }
        private float totalPaddingY
        {
            get
            {
                return (rows - 1) * Spacing.y;
            }
        }


        //Cache to optimize.
        //Could support dynamically sizing lists if we did not cache
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

        private void RecalculateSize()
        {
            //TODO Need to adjust this for differing anchor setups
            ScrollRect.content.sizeDelta = new Vector2(width, height);
        }

        private List<PoolEmptyObject> GetRowForNextEmpty()
        {

            if (Empties.Count > 0)
            {
                List<PoolEmptyObject> lastRow = Empties.Last();
                if (lastRow.Count % columns != 0)
                    return lastRow;
            }
            
            List<PoolEmptyObject> row = new List<PoolEmptyObject>();
            Empties.Add(row);
            RecalculateSize();

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

                
                //TODO Is there a cleaner way to write this logic?
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
        /// It's possible to be seeking through your list too quickly
        /// This can result in orphaned tiles, so we clean them up.
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

        private bool RowIsActive(int row)
        {
            return row >= ActiveIndices.x && row <= ActiveIndices.y;
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

            int min = Mathf.Max(ViewableRowAtPos(lowestPosVisible) - Buffer, 0);
            int max = Mathf.Min(ViewableRowAtPos(highestPosVisible) + Buffer, rows-1);


            return new Vector2(min, max);
        }

        private int ViewableRowAtPos(float pos)
        {
            return (int)(pos / (CellSize.y + Spacing.y));
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
            InUse.Add(retv);

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
        /*
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

        */
        
    }

    internal class PooledListException : Exception
    {
        
        public PooledListException(string message) : base(message)
        {
        }
    }
}
