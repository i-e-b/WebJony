using System;
using System.IO;

namespace WrapperRoleListener.Internal.AssemblyLoading
{
    [Serializable]
    public class BuildVersion: PartiallyOrdered
    {
        public BuildVersion(string spec, string location)
        {
            if (!TryFill(spec, this)) throw new InvalidDataException("Build version was in an invalid format");
            Location = location;
        }

        public BuildVersion() { }

        public int Year { get; set; }
        public int Month { get; set; }
        public int Day { get; set; }
        public int Build { get; set; }
        public string Location { get; set; }

        public override string ToString()
        {
            return string.Join(".", Year, Month.ToString("D2"), Day.ToString("D2"), Build);
        }

        public string ToUnderscoreString()
        {
            return string.Join("_", Year, Month.ToString("D2"), Day.ToString("D2"), Build);
        }

        public static implicit operator string(BuildVersion bv) { return bv.ToString(); }

        private static bool TryFill(string str, BuildVersion vers)
        {
            var bits = str.Split('.');
            if (bits.Length != 4) return false;

            if (!int.TryParse(bits[0], out var year)) return false;
            if (!int.TryParse(bits[1], out var month)) return false;
            if (!int.TryParse(bits[2], out var day)) return false;
            if (!int.TryParse(bits[3], out var build)) return false;

            vers.Year = year;
            vers.Month = month;
            vers.Day = day;
            vers.Build = build;

            return true;
        }

        public static bool TryParse(string str, out BuildVersion vers){
            vers = new BuildVersion();
            return TryFill(str, vers);
        }

        public override int CompareTo(object obj)
        {
            if (!(obj is BuildVersion)) return -1;

            var other = (BuildVersion)obj;
            int c = Year.CompareTo(other.Year);
            if (c != 0) return c;
            c = Month.CompareTo(other.Month);
            if (c != 0) return c;
            c = Day.CompareTo(other.Day);
            if (c != 0) return c;
            return Build.CompareTo(other.Build);
        }
        
        public override int GetHashCode()
        {
            // ReSharper disable NonReadonlyMemberInGetHashCode
            return (Year << 20) ^ (Month << 16) ^ (Day << 8) ^ Build;
            // ReSharper restore NonReadonlyMemberInGetHashCode
        }
    }
}