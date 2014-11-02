using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
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

        private bool IsVertical
        {
            get
            {
                return constraint == GridLayoutGroup.Constraint.FixedColumnCount;
            }
        }
        /// <summary>
        /// The scrollbar for our list
        /// </summary>
        protected Scrollbar Scrollbar
        {
            get
            {
                return IsVertical
                    ? ScrollRect.verticalScrollbar
                    : ScrollRect.horizontalScrollbar;
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

        protected int columns { get{ return IsVertical ? Grid.constraintCount : Empties.Count; }}
        protected int rows { get { return IsVertical ? Empties.Count : Grid.constraintCount; } }
        protected int constrainedAxisCount { get { return Grid.constraintCount; }}
        protected int secondaryAxisCount { get { return Empties.Count; }}

        //These can both offer Null Refs if Grid isn't added. That's to be expected.
        protected Vector2 CellSize { get { return Grid.cellSize; } }
        protected Vector2 Spacing { get { return Grid.spacing; } }

        protected readonly List<List<PoolEmptyObject>> Empties = new List<List<PoolEmptyObject>>(); 
        protected readonly Stack<T> Pool = new Stack<T>(); 
        protected readonly List<T> InUse = new List<T>();

        //Track the first and last index of currently active tiles
        protected Vector2 ActiveIndices = Vector2.zero;

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
            else
            {
                if (IsVertical && ScrollRect.horizontalScrollbar != null)
                    ScrollRect.horizontalScrollbar.gameObject.SetActive(false);
                else if (ScrollRect.verticalScrollbar != null)
                    ScrollRect.verticalScrollbar.gameObject.SetActive(false);
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

        private float width
        {
            get
            {
                return columns * (CellSize.x) + totalPaddingX;
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
            ScrollRect.content.sizeDelta = new Vector2(width, height);
        }

        private List<PoolEmptyObject> GetListForNextEmpty()
        {
            if (Empties.Count > 0)
            {
                List<PoolEmptyObject> last = Empties.Last();
                if (last.Count % constrainedAxisCount != 0)
                    return last;
            }
            
            List<PoolEmptyObject> newList = new List<PoolEmptyObject>();
            Empties.Add(newList);
            RecalculateSize();

            return newList;
        }

        /// <summary>
        /// Mark tiles as active or inactive
        /// </summary>
        /// <param name="px"></param>
        protected void OnScrollbarValue(float px)
        {
            if (Grid == null ||
                ScrollRect == null)
                return;


            Vector2 newActiveIndices = CalculateActiveIndices(px);
            bool performCorrection = false;

            //Special case for first 
            if (ActiveIndices == Vector2.zero)
            {
                for (int i = (int) newActiveIndices.x; i <= newActiveIndices.y; i++)
                {
                    ActivateList(i,true);
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
                Debug.Log("diff: " + diff + " active: " + ActiveIndices + " new: " + newActiveIndices + " pool: " + Pool.Count + " inuse: " + InUse.Count);

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
                        performCorrection = performCorrection || ActivateList(i, false);
                }
                if (diff.y < 0)
                {
                    for (int i = (int)ActiveIndices.y; i > newActiveIndices.y; i--)
                        performCorrection = performCorrection || ActivateList(i, false);
                }
                
                //Then activate
                if (diff.x < 0)
                {
                    for (int i = (int)newActiveIndices.x; i < ActiveIndices.x; i++)
                        performCorrection = performCorrection || ActivateList(i, true);
                }
                if (diff.y > 0)
                {
                    for (int i = (int)ActiveIndices.y + 1; i <= newActiveIndices.y; i++)
                        performCorrection = performCorrection || ActivateList(i, true);
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
        /// This is relatively slow, as it requires iterating everything.
        /// </summary>
        private void CorrectErrors()
        {
            //We need to iterate each row and enable or disable accordingly. Sadly.
            for (int i = 0; i < Empties.Count; i++)
            {
                ActivateList(i, ListIsActive(i));
            }
        }

        private bool ListIsActive(int index)
        {
            return index >= ActiveIndices.x && index <= ActiveIndices.y;
        }

        /// <summary>
        /// Activate or deactivate a list of empties
        /// </summary>
        /// <param name="index">column or row index</param>
        /// <param name="activate"></param>
        /// <returns>Whether to preform a full update upon a failure</returns>
        private bool ActivateList(int index, bool activate)
        {
            List<PoolEmptyObject> listToAdjust = Empties[index];
            foreach (var item in listToAdjust)
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

        private Vector2 CalculateActiveIndices(float scrollbarValue)
        {
            if (VisibleRect == null) return Vector2.zero;

            float pos = ScrollBarPos;

            float viewableSecondaryAxis = IsVertical ? viewableArea.y : viewableArea.x;
            float lowestPosVisible = pos;
            float highestPosVisible = pos + viewableSecondaryAxis;

            int min = Mathf.Max(ViewableIndexAtPos(lowestPosVisible) - Buffer, 0);
            int max = Mathf.Min(ViewableIndexAtPos(highestPosVisible) + Buffer, secondaryAxisCount - 1);

            return new Vector2(min, max);
        }

        private float FullScrollbarValue
        {
            get
            {
                return IsVertical
                    ? height - viewableArea.y 
                    : width  - viewableArea.x;
            }
        }

        private float ScrollBarPos
        {
            get
            {
                float start = IsVertical ? FullScrollbarValue : 0f;
                float end = IsVertical ? 0f : FullScrollbarValue;
                return Mathf.Lerp(start, end, Scrollbar.value); //Lerp from the top to the bottom of scrollbar to determine position
            }
        }

        private float DefaultScrollValue
        {
            get
            {
                return IsVertical ? 1f : 0f;
            }
        }

        private int ViewableIndexAtPos(float pos)
        {
            return IsVertical
                ? (int)(pos / (CellSize.y + Spacing.y))
                : (int)(pos / (CellSize.x + Spacing.x));

        }

        public void AddData(IEnumerable<U> data)
        {
            foreach (var item in data)
                AddData(item);
            Scrollbar.onValueChanged.Invoke(DefaultScrollValue);
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

            List<PoolEmptyObject> listForNextEmpty = GetListForNextEmpty();
            listForNextEmpty.Add(empty);

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
                throw new PooledListException("Pool Limit Exceeded. PoolSize: " + PoolSize);
                //TODO A more graceful error case. Increase pool size automatically?
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
            ActiveIndices = Vector2.zero;

            Transform gridTransform = Grid.transform;
            int childCount = gridTransform.childCount;
            for (int i = childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(gridTransform.GetChild(0).gameObject);
            }
        }
    }

    internal class PooledListException : Exception
    {
        
        public PooledListException(string message) : base(message)
        {
        }
    }
}
