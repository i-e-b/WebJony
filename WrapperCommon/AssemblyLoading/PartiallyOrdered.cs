using System;

namespace WrapperCommon.AssemblyLoading
{
    /// <summary>
    /// A rough patch for C#'s terrible equality cruft
    /// </summary>
    public abstract class PartiallyOrdered : IComparable {
        public abstract int CompareTo(object obj);
        public abstract override int GetHashCode();

        public static int CompareTo(PartiallyOrdered x, object y) { return x.CompareTo(y); }
        public static bool operator  < (PartiallyOrdered x, PartiallyOrdered y) { return CompareTo(x, y)  < 0; }
        public static bool operator  > (PartiallyOrdered x, PartiallyOrdered y) { return CompareTo(x, y)  > 0; }
        public static bool operator <= (PartiallyOrdered x, PartiallyOrdered y) { return CompareTo(x, y) <= 0; }
        public static bool operator >= (PartiallyOrdered x, PartiallyOrdered y) { return CompareTo(x, y) >= 0; }
        public static bool operator == (PartiallyOrdered x, PartiallyOrdered y) { return CompareTo(x, y) == 0; }
        public static bool operator != (PartiallyOrdered x, PartiallyOrdered y) { return CompareTo(x, y) != 0; }
        public bool Equals(PartiallyOrdered x)    { return CompareTo(this, x) == 0; }
        public override bool Equals(object obj)
        {
            return (obj is PartiallyOrdered ordered) && (CompareTo(this, ordered) == 0);
        }
    }
}