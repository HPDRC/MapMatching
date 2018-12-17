using System;
using System.Collections;
using System.Collections.Generic;

namespace MapMatchingLib.Astar
{
    /// <summary>
    /// The SortableList allows to maintain a list sorted as long as needed.
    /// If no IComparer interface has been provided at construction, then the list expects the Objects to implement IComparer.
    /// If the list is not sorted it behaves like an ordinary list.
    /// When sorted, the list's "Add" method will put new objects at the right place.
    /// As well the "Contains" and "IndexOf" methods will perform a binary search.
    /// </summary>
    [Serializable]
    public class SortableList : IList<Track> // where T:IComparable<T>
    {
        private List<Track> _list;
        private IComparer<Track> _comparer;
        private bool _useObjectsComparison;
        private Dictionary<long, Track> _trackByEndNodeId; 

        /// <summary>
        /// Default constructor.
        /// Since no IComparer is provided here, added objects must implement the IComparer interface.
        /// </summary>
        public SortableList()
        {
            InitProperties(null, 0);
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public void CopyTo(Track[] array, int arrayIndex)
        {
            _list.CopyTo(array, arrayIndex);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        /// <summary>
        /// IList implementation.
        /// Gets - or sets - object's value at a specified index.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Index is less than zero or Index is greater than Count.</exception>
        /// <exception cref="InvalidOperationException">[] operator cannot be used to set a value if KeepSorted property is set to true.</exception>
        public Track this[int index]
        {
            get { return _list[index]; }
            set { _list[index] = value; }
        }

        /// <summary>
        /// IList implementation.
        /// Else it will be added at the end of the list.
        /// </summary>
        /// <param name="o">The object to add.</param>
        /// <returns>The index where the object has been added.</returns>
        /// <exception cref="ArgumentException">The SortableList is set to use object's IComparable interface, and the specifed object does not implement this interface.</exception>
        public void Add(Track o)
        {
            int index = IndexOf(o);
            int newIndex = index >= 0 ? index : -index - 1;
            if (newIndex >= Count)
            {
                _list.Add(o);
            }
            else
            {
                _list.Insert(newIndex, o);
            }
            _trackByEndNodeId[o.EndNode.Id] = o;
        }

        /// <summary>
        /// IList implementation.
        /// Search for a specified object in the list.
        /// </summary>
        /// <param name="o">The object to look for</param>
        /// <returns>true if the object is in the list, otherwise false.</returns>
        public bool Contains(Track o)
        {
            return _list.BinarySearch(o, _comparer) >= 0;
        }

        /// <summary>
        /// IList implementation.
        /// Returns the index of the specified object in the list.
        /// </summary>
        /// <param name="o">The object to locate.</param>
        /// <returns>
        /// If the object has been found, a positive integer corresponding to its position.
        /// If the objects has not been found, a negative integer which is the bitwise complement of the index of the next element.
        /// </returns>
        public int IndexOf(Track o)
        {
            return  _list.BinarySearch(o, _comparer);
            //while (result > 0 && _list[result - 1].Equals(o)) result--; // We want to point at the FIRST occurence
            //return result;
        }

        public void Insert(int index, Track o)
        {
            _list.Insert(index, o);
        }


        /// <summary>
        /// IList implementation.
        /// Idem <see cref="ArrayList">ArrayList</see>
        /// </summary>
        public void Clear()
        {
            _list.Clear();
            _trackByEndNodeId.Clear();
        }

        /// <summary>
        /// IList implementation.
        /// Idem <see cref="ArrayList">ArrayList</see>
        /// </summary>
        /// <param name="value">The object whose value must be removed if found in the list.</param>
        public bool Remove(Track value)
        {
            _trackByEndNodeId.Remove(value.EndNode.Id);
            return _list.Remove(value);
        }

        /// <summary>
        /// IList implementation.
        /// Idem <see cref="ArrayList">ArrayList</see>
        /// </summary>
        /// <param name="index">Index of object to remove.</param>
        public void RemoveAt(int index)
        {
            _trackByEndNodeId.Remove(_list[index].EndNode.Id);
            _list.RemoveAt(index);
        }


        /// <summary>
        /// IList.ICollection implementation.
        /// Idem <see cref="ArrayList">ArrayList</see>
        /// </summary>
        public int Count
        {
            get { return _list.Count; }
        }

        /// <summary>
        /// IList.ICollection implementation.
        /// Idem <see cref="ArrayList">ArrayList</see>
        /// </summary>
        public bool IsSynchronized
        {
            get { return ((ICollection) _list).IsSynchronized; }
        }

        /// <summary>
        /// IList.ICollection implementation.
        /// Idem <see cref="ArrayList">ArrayList</see>
        /// </summary>
        public object SyncRoot
        {
            get { return ((ICollection) _list).SyncRoot; }
        }

        /// <summary>
        /// IList.IEnumerable implementation.
        /// Idem <see cref="ArrayList">ArrayList</see>
        /// </summary>
        /// <returns>Enumerator on the list.</returns>
        public IEnumerator<Track> GetEnumerator()
        {
            return _list.GetEnumerator();
        }
        
        /// <summary>
        /// Idem IndexOf(object), but with a specified equality function
        /// </summary>
        /// <param name="o">The object to locate.</param>
        /// <returns></returns>
        public Track IndexOfSameEndNode(Track o)
        {
            return _trackByEndNodeId.ContainsKey(o.EndNode.Id) ? _trackByEndNodeId[o.EndNode.Id] : null;
        }

        /// <summary>
        /// Object.ToString() override.
        /// Build a string to represent the list.
        /// </summary>
        /// <returns>The string refecting the list.</returns>
        public override string ToString()
        {
            string outString = "{";
            for (int i = 0; i < _list.Count; i++)
                outString += _list[i] + (i != _list.Count - 1 ? "; " : "}");
            return outString;
        }

        /// <summary>
        /// Object.Equals() override.
        /// </summary>
        /// <returns>true if object is equal to this, otherwise false.</returns>
        public override bool Equals(object o)
        {
            SortableList sl = (SortableList) o;
            if (sl.Count != Count) return false;
            for (int i = 0; i < Count; i++)
                if (!sl[i].Equals(this[i])) return false;
            return true;
        }

        /// <summary>
        /// Object.GetHashCode() override.
        /// </summary>
        /// <returns>HashCode value.</returns>
        //public override int GetHashCode()
        //{
        //    return _list.GetHashCode();
        //}

        /// <summary>
        /// Returns the object of the list whose value is minimum
        /// </summary>
        /// <returns>The minimum object in the list</returns>
        public int IndexOfMin()
        {
            return _list.Count > 0 ? 0 : -1;
        }

        private class Comparison : IComparer<Track>
        {
            public int Compare(Track o1, Track o2)
            {
                return o1.Evaluation.CompareTo(o2.Evaluation);
            }
        }

        private void InitProperties(IComparer<Track> comparer, int capacity)
        {
            if (comparer != null)
            {
                _comparer = comparer;
                _useObjectsComparison = false;
            }
            else
            {
                _comparer = new Comparison();
                _useObjectsComparison = true;
            }
            _list = capacity > 0 ? new List<Track>(capacity) : new List<Track>();
            _trackByEndNodeId=new Dictionary<long, Track>();
        }
    }
}