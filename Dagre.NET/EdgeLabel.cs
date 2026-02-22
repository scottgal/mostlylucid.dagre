using System;
using System.Collections;
using System.Collections.Generic;

namespace Dagre
{
    /// <summary>
    /// Strongly-typed edge label replacing Dictionary&lt;string, object&gt; property bags.
    /// Implements IDictionary for backward compatibility with code that uses string indexers.
    /// </summary>
    public class EdgeLabel : IDictionary<string, object>
    {
        // Layout — auto-set flags on write
        private float _x, _y, _width, _height;
        private List<DagrePoint> _points;
        public float X { get => _x; set { _x = value; Set(F_X); } }
        public float Y { get => _y; set { _y = value; Set(F_Y); } }
        public float Width { get => _width; set { _width = value; Set(F_Width); } }
        public float Height { get => _height; set { _height = value; Set(F_Height); } }
        public List<DagrePoint> Points { get => _points; set { _points = value; Set(F_Points); } }

        // Edge properties — auto-set flags
        private int _weight, _minlen;
        private string _labelPos;
        private float _labelOffset;
        public int Weight { get => _weight; set { _weight = value; Set(F_Weight); } }
        public int Minlen { get => _minlen; set { _minlen = value; Set(F_Minlen); } }
        public string LabelPos { get => _labelPos; set { _labelPos = value; Set(F_LabelPos); } }
        public float LabelOffset { get => _labelOffset; set { _labelOffset = value; Set(F_LabelOffset); } }

        // Acyclic module — auto-set flags
        private string _forwardName;
        private bool _reversed;
        public string ForwardName { get => _forwardName; set { _forwardName = value; Set(F_ForwardName); } }
        public bool Reversed { get => _reversed; set { _reversed = value; Set(F_Reversed); } }

        // Network simplex — auto-set flag
        private int _cutvalue;
        public int Cutvalue { get => _cutvalue; set { _cutvalue = value; Set(F_Cutvalue); } }

        // Normalize — auto-set flag
        private int _labelRank;
        public int LabelRank { get => _labelRank; set { _labelRank = value; Set(F_LabelRank); } }

        // Nesting — auto-set flag
        private bool _nestingEdge;
        public bool NestingEdge { get => _nestingEdge; set { _nestingEdge = value; Set(F_NestingEdge); } }

        // Caller-provided — auto-set flag
        private object _source;
        public object Source { get => _source; set { _source = value; Set(F_Source); } }

        // Overflow for any properties not covered
        private Dictionary<string, object> _overflow;

        // Track which properties have been set
        private uint _setFlags;

        private const int F_X = 0, F_Y = 1, F_Width = 2, F_Height = 3, F_Points = 4;
        private const int F_Weight = 5, F_Minlen = 6, F_LabelPos = 7, F_LabelOffset = 8;
        private const int F_ForwardName = 9, F_Reversed = 10, F_Cutvalue = 11;
        private const int F_LabelRank = 12, F_NestingEdge = 13, F_Source = 14;

        private bool IsSet(int bit) => (_setFlags & (1u << bit)) != 0;
        private void Set(int bit) => _setFlags |= (1u << bit);
        private void Unset(int bit) => _setFlags &= ~(1u << bit);

        public object this[string key]
        {
            get => TryGetTyped(key, out var val) ? val : (_overflow != null && _overflow.TryGetValue(key, out val) ? val : throw new KeyNotFoundException(key));
            set => SetValue(key, value);
        }

        private bool TryGetTyped(string key, out object value)
        {
            switch (key)
            {
                case "x": value = (object)X; return IsSet(F_X);
                case "y": value = (object)Y; return IsSet(F_Y);
                case "width": value = (object)Width; return IsSet(F_Width);
                case "height": value = (object)Height; return IsSet(F_Height);
                case "points": value = Points; return IsSet(F_Points);
                case "weight": value = (object)Weight; return IsSet(F_Weight);
                case "minlen": value = (object)Minlen; return IsSet(F_Minlen);
                case "labelpos": value = LabelPos; return IsSet(F_LabelPos);
                case "labeloffset": value = (object)LabelOffset; return IsSet(F_LabelOffset);
                case "forwardName": value = ForwardName; return IsSet(F_ForwardName);
                case "reversed": value = (object)Reversed; return IsSet(F_Reversed);
                case "cutvalue": value = (object)Cutvalue; return IsSet(F_Cutvalue);
                case "labelRank": value = (object)LabelRank; return IsSet(F_LabelRank);
                case "nestingEdge": value = (object)NestingEdge; return IsSet(F_NestingEdge);
                case "source": value = Source; return IsSet(F_Source);
                default: value = null; return false;
            }
        }

        private void SetValue(string key, object value)
        {
            switch (key)
            {
                case "x": X = Convert.ToSingle(value); break;
                case "y": Y = Convert.ToSingle(value); break;
                case "width": Width = Convert.ToSingle(value); break;
                case "height": Height = Convert.ToSingle(value); break;
                case "points": Points = (List<DagrePoint>)value; break;
                case "weight": Weight = Convert.ToInt32(value); break;
                case "minlen": Minlen = Convert.ToInt32(value); break;
                case "labelpos": LabelPos = value as string ?? value?.ToString(); break;
                case "labeloffset": LabelOffset = Convert.ToSingle(value); break;
                case "forwardName": ForwardName = value as string ?? value?.ToString(); break;
                case "reversed": Reversed = Convert.ToBoolean(value); break;
                case "cutvalue": Cutvalue = Convert.ToInt32(value); break;
                case "labelRank": LabelRank = Convert.ToInt32(value); break;
                case "nestingEdge": NestingEdge = Convert.ToBoolean(value); break;
                case "source": Source = value; break;
                default:
                    if (_overflow == null) _overflow = new Dictionary<string, object>();
                    _overflow[key] = value;
                    break;
            }
        }

        public bool ContainsKey(string key)
        {
            if (TryGetTyped(key, out _)) return true;
            return _overflow != null && _overflow.ContainsKey(key);
        }

        public bool TryGetValue(string key, out object value)
        {
            if (TryGetTyped(key, out value)) return true;
            if (_overflow != null) return _overflow.TryGetValue(key, out value);
            value = null;
            return false;
        }

        public void Add(string key, object value) => SetValue(key, value);

        public bool Remove(string key)
        {
            switch (key)
            {
                case "x": Unset(F_X); return true;
                case "y": Unset(F_Y); return true;
                case "width": Unset(F_Width); return true;
                case "height": Unset(F_Height); return true;
                case "points": Unset(F_Points); return true;
                case "weight": Unset(F_Weight); return true;
                case "minlen": Unset(F_Minlen); return true;
                case "labelpos": Unset(F_LabelPos); return true;
                case "labeloffset": Unset(F_LabelOffset); return true;
                case "forwardName": Unset(F_ForwardName); return true;
                case "reversed": Unset(F_Reversed); return true;
                case "cutvalue": Unset(F_Cutvalue); return true;
                case "labelRank": Unset(F_LabelRank); return true;
                case "nestingEdge": Unset(F_NestingEdge); return true;
                case "source": Unset(F_Source); return true;
                default:
                    return _overflow != null && _overflow.Remove(key);
            }
        }

        public ICollection<string> Keys
        {
            get
            {
                var keys = new List<string>();
                if (IsSet(F_X)) keys.Add("x");
                if (IsSet(F_Y)) keys.Add("y");
                if (IsSet(F_Width)) keys.Add("width");
                if (IsSet(F_Height)) keys.Add("height");
                if (IsSet(F_Points)) keys.Add("points");
                if (IsSet(F_Weight)) keys.Add("weight");
                if (IsSet(F_Minlen)) keys.Add("minlen");
                if (IsSet(F_LabelPos)) keys.Add("labelpos");
                if (IsSet(F_LabelOffset)) keys.Add("labeloffset");
                if (IsSet(F_ForwardName)) keys.Add("forwardName");
                if (IsSet(F_Reversed)) keys.Add("reversed");
                if (IsSet(F_Cutvalue)) keys.Add("cutvalue");
                if (IsSet(F_LabelRank)) keys.Add("labelRank");
                if (IsSet(F_NestingEdge)) keys.Add("nestingEdge");
                if (IsSet(F_Source)) keys.Add("source");
                if (_overflow != null) keys.AddRange(_overflow.Keys);
                return keys;
            }
        }

        public ICollection<object> Values
        {
            get
            {
                var vals = new List<object>();
                foreach (var key in Keys) vals.Add(this[key]);
                return vals;
            }
        }

        public int Count
        {
            get
            {
                int c = 0;
                var flags = _setFlags;
                while (flags != 0) { c += (int)(flags & 1); flags >>= 1; }
                if (_overflow != null) c += _overflow.Count;
                return c;
            }
        }

        public bool IsReadOnly => false;

        public void Add(KeyValuePair<string, object> item) => SetValue(item.Key, item.Value);
        public void Clear() { _setFlags = 0; _overflow?.Clear(); }
        public bool Contains(KeyValuePair<string, object> item) => ContainsKey(item.Key);
        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            foreach (var kvp in this) array[arrayIndex++] = kvp;
        }
        public bool Remove(KeyValuePair<string, object> item) => Remove(item.Key);

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            foreach (var key in Keys)
                yield return new KeyValuePair<string, object>(key, this[key]);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
