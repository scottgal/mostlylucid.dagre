using System;
using System.Collections;
using System.Collections.Generic;

namespace Dagre
{
    /// <summary>
    /// Strongly-typed node label replacing Dictionary&lt;string, object&gt; property bags.
    /// Implements IDictionary for backward compatibility with code that uses string indexers.
    /// </summary>
    public class NodeLabel : IDictionary<string, object>
    {
        // Layout coordinates — auto-set flags on write so ContainsKey works
        private float _x, _y, _width, _height;
        public float X { get => _x; set { _x = value; Set(F_X); } }
        public float Y { get => _y; set { _y = value; Set(F_Y); } }
        public float Width { get => _width; set { _width = value; Set(F_Width); } }
        public float Height { get => _height; set { _height = value; Set(F_Height); } }

        // Ranking — auto-set flags on write so ContainsKey works
        private int _rank, _order, _minRank, _maxRank;
        public int Rank { get => _rank; set { _rank = value; Set(F_Rank); } }
        public int Order { get => _order; set { _order = value; Set(F_Order); } }
        public int MinRank { get => _minRank; set { _minRank = value; Set(F_MinRank); } }
        public int MaxRank { get => _maxRank; set { _maxRank = value; Set(F_MaxRank); } }

        // Tree structure (network simplex) — auto-set flags
        private int _low, _lim, _cutvalue;
        private string _parent;
        public int Low { get => _low; set { _low = value; Set(F_Low); } }
        public int Lim { get => _lim; set { _lim = value; Set(F_Lim); } }
        public string Parent { get => _parent; set { _parent = value; Set(F_Parent); } }
        public int Cutvalue { get => _cutvalue; set { _cutvalue = value; Set(F_Cutvalue); } }

        // Dummy/border info — auto-set flags
        private string _dummy, _borderType, _borderTop, _borderBottom;
        public string Dummy { get => _dummy; set { _dummy = value; Set(F_Dummy); } }
        public string BorderType { get => _borderType; set { _borderType = value; Set(F_BorderType); } }
        public string BorderTop { get => _borderTop; set { _borderTop = value; Set(F_BorderTop); } }
        public string BorderBottom { get => _borderBottom; set { _borderBottom = value; Set(F_BorderBottom); } }
        private Dictionary<string, string> _borderLeft, _borderRight;
        public Dictionary<string, string> BorderLeft { get => _borderLeft; set { _borderLeft = value; Set(F_BorderLeft); } }
        public Dictionary<string, string> BorderRight { get => _borderRight; set { _borderRight = value; Set(F_BorderRight); } }

        // Subgraph/nesting — auto-set flags
        private string _root, _nestingRoot;
        private bool _isGroup;
        public string Root { get => _root; set { _root = value; Set(F_Root); } }
        public bool IsGroup { get => _isGroup; set { _isGroup = value; Set(F_IsGroup); } }
        public string NestingRoot { get => _nestingRoot; set { _nestingRoot = value; Set(F_NestingRoot); } }

        // Edge-related (for dummy nodes representing edges) — auto-set flags
        private object _edgeObj, _edgeLabel, _e, _label;
        private int _labelRank;
        private string _labelPos;
        public object EdgeObj { get => _edgeObj; set { _edgeObj = value; Set(F_EdgeObj); } }
        public object EdgeLabel { get => _edgeLabel; set { _edgeLabel = value; Set(F_EdgeLabel); } }
        public object E { get => _e; set { _e = value; Set(F_E); } }
        public object Label { get => _label; set { _label = value; Set(F_Label); } }
        public int LabelRank { get => _labelRank; set { _labelRank = value; Set(F_LabelRank); } }
        public string LabelPos { get => _labelPos; set { _labelPos = value; Set(F_LabelPos); } }

        // Self-edges — auto-set flag
        private List<SelfEdgeInfo> _selfEdges;
        public List<SelfEdgeInfo> SelfEdges { get => _selfEdges; set { _selfEdges = value; Set(F_SelfEdges); } }

        // Misc — auto-set flags
        private object _source;
        private bool _nestingEdge;
        public object Source { get => _source; set { _source = value; Set(F_Source); } }
        public bool NestingEdge { get => _nestingEdge; set { _nestingEdge = value; Set(F_NestingEdge); } }
        public RankTag RankTag;      // cached rank info (no flag needed — not dictionary-backed)

        // Overflow dictionary for any properties not covered by typed fields
        private Dictionary<string, object> _overflow;

        // Track which properties have been explicitly set (for ContainsKey)
        private ulong _setFlags;

        // Bit positions for each property
        private const int F_X = 0, F_Y = 1, F_Width = 2, F_Height = 3;
        private const int F_Rank = 4, F_Order = 5, F_MinRank = 6, F_MaxRank = 7;
        private const int F_Low = 8, F_Lim = 9, F_Parent = 10, F_Cutvalue = 11;
        private const int F_Dummy = 12, F_BorderType = 13, F_BorderTop = 14, F_BorderBottom = 15;
        private const int F_BorderLeft = 16, F_BorderRight = 17;
        private const int F_Root = 18, F_IsGroup = 19, F_NestingRoot = 20;
        private const int F_EdgeObj = 21, F_EdgeLabel = 22, F_E = 23, F_Label = 24;
        private const int F_LabelRank = 25, F_LabelPos = 26;
        private const int F_SelfEdges = 27, F_Source = 28, F_NestingEdge = 29;

        private bool IsSet(int bit) => (_setFlags & (1UL << bit)) != 0;
        private void Set(int bit) => _setFlags |= (1UL << bit);
        private void Unset(int bit) => _setFlags &= ~(1UL << bit);

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
                case "rank": value = (object)Rank; return IsSet(F_Rank);
                case "order": value = (object)Order; return IsSet(F_Order);
                case "minRank": value = (object)MinRank; return IsSet(F_MinRank);
                case "maxRank": value = (object)MaxRank; return IsSet(F_MaxRank);
                case "low": value = (object)Low; return IsSet(F_Low);
                case "lim": value = (object)Lim; return IsSet(F_Lim);
                case "parent": value = Parent; return IsSet(F_Parent);
                case "cutvalue": value = (object)Cutvalue; return IsSet(F_Cutvalue);
                case "dummy": value = Dummy; return IsSet(F_Dummy);
                case "borderType": value = BorderType; return IsSet(F_BorderType);
                case "borderTop": value = BorderTop; return IsSet(F_BorderTop);
                case "borderBottom": value = BorderBottom; return IsSet(F_BorderBottom);
                case "borderLeft": value = BorderLeft; return IsSet(F_BorderLeft);
                case "borderRight": value = BorderRight; return IsSet(F_BorderRight);
                case "root": value = Root; return IsSet(F_Root);
                case "isGroup": value = (object)IsGroup; return IsSet(F_IsGroup);
                case "nestingRoot": value = NestingRoot; return IsSet(F_NestingRoot);
                case "edgeObj": value = EdgeObj; return IsSet(F_EdgeObj);
                case "edgeLabel": value = EdgeLabel; return IsSet(F_EdgeLabel);
                case "e": value = E; return IsSet(F_E);
                case "label": value = Label; return IsSet(F_Label);
                case "labelRank": value = (object)LabelRank; return IsSet(F_LabelRank);
                case "labelpos": value = LabelPos; return IsSet(F_LabelPos);
                case "selfEdges": value = SelfEdges; return IsSet(F_SelfEdges);
                case "source": value = Source; return IsSet(F_Source);
                case "nestingEdge": value = (object)NestingEdge; return IsSet(F_NestingEdge);
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
                case "rank": Rank = Convert.ToInt32(value); break;
                case "order": Order = Convert.ToInt32(value); break;
                case "minRank": MinRank = Convert.ToInt32(value); break;
                case "maxRank": MaxRank = Convert.ToInt32(value); break;
                case "low": Low = Convert.ToInt32(value); break;
                case "lim": Lim = Convert.ToInt32(value); break;
                case "parent": Parent = value as string ?? value?.ToString(); break;
                case "cutvalue": Cutvalue = Convert.ToInt32(value); break;
                case "dummy": Dummy = value as string ?? value?.ToString(); break;
                case "borderType": BorderType = value as string ?? value?.ToString(); break;
                case "borderTop": BorderTop = value as string ?? value?.ToString(); break;
                case "borderBottom": BorderBottom = value as string ?? value?.ToString(); break;
                case "borderLeft": BorderLeft = (Dictionary<string, string>)value; break;
                case "borderRight": BorderRight = (Dictionary<string, string>)value; break;
                case "root": Root = value as string ?? value?.ToString(); break;
                case "isGroup": IsGroup = Convert.ToBoolean(value); break;
                case "nestingRoot": NestingRoot = value as string ?? value?.ToString(); break;
                case "edgeObj": EdgeObj = value; break;
                case "edgeLabel": EdgeLabel = value; break;
                case "e": E = value; break;
                case "label": Label = value; break;
                case "labelRank": LabelRank = Convert.ToInt32(value); break;
                case "labelpos": LabelPos = value as string ?? value?.ToString(); break;
                case "selfEdges": SelfEdges = (List<SelfEdgeInfo>)value; break;
                case "source": Source = value; break;
                case "nestingEdge": NestingEdge = Convert.ToBoolean(value); break;
                default:
                    if (_overflow == null)
                        _overflow = new Dictionary<string, object>();
                    _overflow[key] = value;
                    break;
            }
        }

        // IDictionary implementation
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
                case "rank": Unset(F_Rank); return true;
                case "order": Unset(F_Order); return true;
                case "minRank": Unset(F_MinRank); return true;
                case "maxRank": Unset(F_MaxRank); return true;
                case "low": Unset(F_Low); return true;
                case "lim": Unset(F_Lim); return true;
                case "parent": Unset(F_Parent); return true;
                case "cutvalue": Unset(F_Cutvalue); return true;
                case "dummy": Unset(F_Dummy); return true;
                case "borderType": Unset(F_BorderType); return true;
                case "borderTop": Unset(F_BorderTop); return true;
                case "borderBottom": Unset(F_BorderBottom); return true;
                case "borderLeft": Unset(F_BorderLeft); return true;
                case "borderRight": Unset(F_BorderRight); return true;
                case "root": Unset(F_Root); return true;
                case "isGroup": Unset(F_IsGroup); return true;
                case "nestingRoot": Unset(F_NestingRoot); return true;
                case "edgeObj": Unset(F_EdgeObj); return true;
                case "edgeLabel": Unset(F_EdgeLabel); return true;
                case "e": Unset(F_E); return true;
                case "label": Unset(F_Label); return true;
                case "labelRank": Unset(F_LabelRank); return true;
                case "labelpos": Unset(F_LabelPos); return true;
                case "selfEdges": Unset(F_SelfEdges); return true;
                case "source": Unset(F_Source); return true;
                case "nestingEdge": Unset(F_NestingEdge); return true;
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
                if (IsSet(F_Rank)) keys.Add("rank");
                if (IsSet(F_Order)) keys.Add("order");
                if (IsSet(F_MinRank)) keys.Add("minRank");
                if (IsSet(F_MaxRank)) keys.Add("maxRank");
                if (IsSet(F_Low)) keys.Add("low");
                if (IsSet(F_Lim)) keys.Add("lim");
                if (IsSet(F_Parent)) keys.Add("parent");
                if (IsSet(F_Cutvalue)) keys.Add("cutvalue");
                if (IsSet(F_Dummy)) keys.Add("dummy");
                if (IsSet(F_BorderType)) keys.Add("borderType");
                if (IsSet(F_BorderTop)) keys.Add("borderTop");
                if (IsSet(F_BorderBottom)) keys.Add("borderBottom");
                if (IsSet(F_BorderLeft)) keys.Add("borderLeft");
                if (IsSet(F_BorderRight)) keys.Add("borderRight");
                if (IsSet(F_Root)) keys.Add("root");
                if (IsSet(F_IsGroup)) keys.Add("isGroup");
                if (IsSet(F_NestingRoot)) keys.Add("nestingRoot");
                if (IsSet(F_EdgeObj)) keys.Add("edgeObj");
                if (IsSet(F_EdgeLabel)) keys.Add("edgeLabel");
                if (IsSet(F_E)) keys.Add("e");
                if (IsSet(F_Label)) keys.Add("label");
                if (IsSet(F_LabelRank)) keys.Add("labelRank");
                if (IsSet(F_LabelPos)) keys.Add("labelpos");
                if (IsSet(F_SelfEdges)) keys.Add("selfEdges");
                if (IsSet(F_Source)) keys.Add("source");
                if (IsSet(F_NestingEdge)) keys.Add("nestingEdge");
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
