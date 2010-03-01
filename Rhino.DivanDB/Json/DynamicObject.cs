using System;
using Lucene.Net.Documents;

namespace Rhino.DivanDB.Json
{
    public abstract class DynamicObject
    {
        public abstract DynamicObject this[string key] { get; }

        protected abstract object Value { get; }

        public static bool operator==(DynamicObject dyn, string val)
        {
            return Equals(dyn.Value, val);
        }

        public static bool operator !=(DynamicObject dyn, string val)
        {
            return Equals(dyn.Value, val) == false;
        }

        public static bool operator ==(DynamicObject dyn, bool val)
        {
            return Equals(dyn.Value, val);
        }

        public static bool operator !=(DynamicObject dyn, bool val)
        {
            return Equals(dyn.Value, val) == false;
        }

        public static bool operator ==(DynamicObject dyn, int val)
        {
            return Equals(dyn.Value, val);
        }

        public static bool operator !=(DynamicObject dyn, int val)
        {
            return Equals(dyn.Value, val) == false;
        }

        public static bool operator >(DynamicObject dyn, int val)
        {
            return Convert.ToInt32(dyn.Value) > val;
        }

        public static bool operator <(DynamicObject dyn, int val)
        {
            return Convert.ToInt32(dyn.Value) < val;
        }

        public static bool operator >=(DynamicObject dyn, int val)
        {
            return Convert.ToInt32(dyn.Value) >= val;
        }

        public static bool operator <=(DynamicObject dyn, int val)
        {
            return Convert.ToInt32(dyn.Value) <= val;
        }

        public bool Equals(DynamicObject other)
        {
            return !ReferenceEquals(null, other);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (DynamicObject)) return false;
            return Equals((DynamicObject) obj);
        }

        public override int GetHashCode()
        {
            if (Value == null)
                return 0;
            return Value.GetHashCode();
        }


        public string ToIndexableString()
        {
            var val = Value;
            if (val is DateTime)
                return DateTools.DateToString((DateTime)val, DateTools.Resolution.DAY);

            if (val is int)
                return NumberTools.LongToString((int)val);

            return val.ToString();
        }
    }
}