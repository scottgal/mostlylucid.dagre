namespace Mostlylucid.Dagre;

public class GraphLabel
{
    // Configuration (set by consumer before layout)
    public int RankSep { get; set; }
    public int EdgeSep { get; set; }
    public int NodeSep { get; set; }
    public string RankDir { get; set; }
    public string Ranker { get; set; }
    public string Acyclicer { get; set; }
    public string Align { get; set; }
    public float MarginX { get; set; }
    public float MarginY { get; set; }

    /// <summary>
    /// Controls how aggressively dummy-node X-coordinates are biased toward the ideal
    /// straight line between their original edge endpoints during BK balance.
    /// Range: 0.0 (pure BK) to 1.0 (force onto straight line). Default 0.0 (off).
    /// </summary>
    public float EdgeStraighteningStrength { get; set; }

    // Output (set by layout)
    public double Width { get; set; }
    public double Height { get; set; }

    // Internal temporaries (used during layout, cleared after)
    public string NestingRoot { get; set; }
    public int NodeRankFactor { get; set; }
    public List<string> DummyChains { get; set; }
    public int MaxRank { get; set; }

    // Used by layer graphs in ordering phase
    public string Root { get; set; }
}