using System;

namespace MapMatchingLib.RTree
{
    public class ElementDistance<T> : IComparable<ElementDistance<T>>
    {
        public int Id;
        public T Element;
        public double Distance;

        public ElementDistance(int id, double distance)
        {
            Id = id;
            Distance = distance;
        }

        public ElementDistance(int id, T element, double distance)
        {
            Id = id;
            Element = element;
            Distance = distance;
        }
        
        public int CompareTo(ElementDistance<T> obj)
        {
            return Distance.CompareTo(obj.Distance);
        }
    }
}
