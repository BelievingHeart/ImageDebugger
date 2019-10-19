﻿using System;
using System.Collections;
using System.Collections.Generic;
using UI.Helpers;

namespace UI.ViewModels
{
    /// <summary>
    /// A list that is able to loop backward and forward infinitely
    /// </summary>
    public class RecyclableMegaList<T> : ViewModelBase, ICollection<List<T>>
    {
        private int _currentIndex;

        /// <summary>
        /// Current index of the internal list
        /// </summary>
        public int CurrentIndex
        {
            get { return _currentIndex; }
            private set
            {
                _currentIndex = value;
            }
        }

        /// <summary>
        /// Internal list that stores the data
        /// </summary>
        private List<List<T>> MegaList { get; set; }

        public event Action<int> IndexChanged;

        /// <summary>
        /// Set current index to dstIndex and return that element
        /// </summary>
        /// <param name="dstIndex"></param>
        /// <returns></returns>
        public List<T> JumpTo(int dstIndex)
        {
            if (dstIndex >= Count || dstIndex < 0)
                throw new IndexOutOfRangeException("The index to jump to is invalid");
            CurrentIndex = dstIndex;

            OnIndexChanged(CurrentIndex);
            return this[dstIndex];
        }

        /// <summary>
        /// Return next element in the internal list
        /// If the new index exceeds Count - 1, it will go back to 0
        /// and continue to provide data
        /// </summary>
        /// <returns></returns>
        public List<T> NextUnbounded()
        {
            CurrentIndex++;
            if (CurrentIndex >= MegaList[0].Count) CurrentIndex = 0;

            OnIndexChanged(CurrentIndex);
            return this[CurrentIndex];
        }

        /// <summary>
        /// Return next element in the internal list
        /// If the new index exceeds Count - 1, it will go back to 0
        /// and provide null
        /// </summary>
        /// <returns></returns>
        public List<T> NextBounded()
        {
            CurrentIndex++;
            if (CurrentIndex >= MegaList[0].Count)
            {
                CurrentIndex = -1;
                return null;
            }

            OnIndexChanged(CurrentIndex);
            return this[CurrentIndex];
        }
        
        /// <summary>
        /// Return previous element in the internal list
        /// If the new index is less than 0 , it will go to the end of the list
        /// and continue to provide data
        /// </summary>
        /// <returns></returns>
        public List<T> PreviousUnbounded()
        {
            CurrentIndex--;
            if (CurrentIndex < 0) CurrentIndex = MegaList[0].Count - 1;

            OnIndexChanged(CurrentIndex);
            return this[CurrentIndex];
        }

        /// <summary>
        /// Reconstruct the mega list from another mega list
        /// </summary>
        /// <param name="newValue">The new mega list to assign</param>
        public void Reconstruct(List<List<T>> newValue)
        {
            MegaList = newValue;
            CurrentIndex = -1;
            OnIndexChanged(CurrentIndex);
        }

  

        /// <summary>
        /// Number of list within the mega list
        /// </summary>
        public int NumLists
        {
            get { return MegaList.Count; }
            set
            {
                if (value == MegaList.Count) return;

                MegaList.Clear();
                for (int i = 0; i < value; i++)
                {
                    MegaList.Add(new List<T>());
                }
            }
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="numList">Number of lists within the mega list</param>
        public RecyclableMegaList(int numList = 1)
        {
            CurrentIndex = -1;
            MegaList = new List<List<T>>();
            NumLists = numList;
        }

        public List<T> this[int index]
        {
            get
            {
                var output = new List<T>();
                foreach (var list in MegaList)
                {
                    output.Add(list[index]);
                }

                return output;
            }
            set
            {
                if (index >= Count || index < 0) throw new IndexOutOfRangeException();
                if (value.Count != NumLists)
                    throw new ArgumentException("The count of the added item should equal to NumLists");

                int count = value.Count;
                for (int i = 0; i < count; i++)
                {
                    MegaList[i].Insert(index, value[i]);
                }
            }
        }

        public IEnumerator<List<T>> GetEnumerator()
        {
          
            return MegaList.AsRows().GetEnumerator();
        }


        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(List<T> item)
        {
            if (item.Count != MegaList.Count)
                throw new ArgumentException("The count of the added item should equal to NumLists");

            for (int i = 0; i < item.Count; i++)
            {
                MegaList[i].Add(item[i]);
            }
        }

        public void Clear()
        {
            MegaList.Clear();
            CurrentIndex = -1;
            OnIndexChanged(CurrentIndex);
        }

        public bool Contains(List<T> item)
        {
            throw new System.NotImplementedException();
        }

        public void CopyTo(List<T>[] array, int arrayIndex)
        {
            throw new System.NotImplementedException();
        }

        public bool Remove(List<T> item)
        {
            throw new System.NotImplementedException();
        }

        /// <summary>
        /// Count of elements in individual list
        /// </summary>
        public int Count => MegaList.Count == 0 ? 0 : MegaList[0].Count;

        public bool IsReadOnly
        {
            get { return false; }
        }

        protected virtual void OnIndexChanged(int newIndex)
        {
            IndexChanged?.Invoke(newIndex);
        }
    }
}