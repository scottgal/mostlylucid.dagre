using System.Diagnostics;

namespace Mostlylucid.Dagre.Indexed;

/// <summary>
///     Orchestrator that runs layout using indexed (int-array) implementations
///     for the three hot-path phases (Rank, Order, Position), while reusing
///     the original DagreLayout methods for setup and finalization.
/// </summary>
public static class IndexedDagreLayout
{
    /// <summary>
    ///     When true, prints per-phase timing to stderr.
    /// </summary>
    public static bool TraceTiming { get; set; }

    /// <summary>
    ///     Run layout using indexed algorithms for hot paths.
    ///     The DagreGraph is mutated in place - same contract as DagreLayout.RunLayout.
    /// </summary>
    public static void RunLayout(DagreGraph g, Action<ExtProgressInfo> progress = null)
    {
        var ext = new ExtProgressInfo();
        progress?.Invoke(ext);
        var sw = TraceTiming ? Stopwatch.StartNew() : null;

        // Phase 1: Setup (reuse original - not hot paths)
        DagreLayout.MakeSpaceForEdgeLabels(g);
        DagreLayout.RemoveSelfEdges(g);
        Acyclic.Run(g);
        NestingGraph.Run(g);
        Trace(sw, "Phase 1 Setup");

        // Phase 2: Ranking — INDEXED
        ext.Caption = "rank (indexed)";
        progress?.Invoke(ext);
        {
            var nonCompound = Util.AsNonCompoundGraph(g);
            var ig = IndexedGraph.FromDagreGraph(nonCompound);
            Trace(sw, "  FromDagreGraph (rank)");
            IndexedNetworkSimplex.Run(ig);
            Trace(sw, "  NetworkSimplex");
            ig.WriteRanksTo(nonCompound);
            // nonCompound shares NodeLabel references with g, so ranks propagate
        }

        // Phase 3: Post-rank setup (structural mutations on DagreGraph)
        DagreLayout.InjectEdgeLabelProxies(g);
        DagreLayout.RemoveEmptyRanks(g);
        NestingGraph.Cleanup(g);
        Util.NormalizeRanks(g);
        DagreLayout.AssignRankMinMax(g);
        DagreLayout.RemoveEdgeLabelProxies(g);
        Trace(sw, "  Phase 3a post-rank setup");

        ext.MainProgress = 0.1f;
        progress?.Invoke(ext);
        ext.Caption = "Normalize.run";
        Normalize.Run(g);
        Trace(sw, "  Normalize.Run");
        ParentDummyChains._parentDummyChains(g);
        Trace(sw, "  ParentDummyChains");
        AddBorderSegments._addBorderSegments(g);
        Trace(sw, "  AddBorderSegments");

        // Check if compound nodes exist (subgraphs with children).
        // IndexedOrder/Position don't handle compound sortSubgraph grouping,
        // so fall back to original Order+Position for compound graphs.
        var isCompound = false;
        foreach (var v in g.NodesRaw())
            if (g.HasChildren(v))
            {
                isCompound = true;
                break;
            }

        if (isCompound)
            // Full original pipeline for compound graphs (preserves subgraph grouping)
            RunCompoundLayout(g, ext, progress, sw);
        else
            // Indexed pipeline for non-compound graphs (maximum performance)
            RunIndexedLayout(g, ext, progress, sw);
    }

    /// <summary>
    ///     Full original pipeline for compound graphs (with subgraphs).
    /// </summary>
    private static void RunCompoundLayout(DagreGraph g, ExtProgressInfo ext,
        Action<ExtProgressInfo> progress, Stopwatch sw)
    {
        // Phase 4: Ordering — Original (handles compound sortSubgraph)
        ext.Caption = "order (compound)";
        ext.MainProgress = 0.3f;
        progress?.Invoke(ext);
        Order._order(g);
        Trace(sw, "  Order._order (compound)");

        ext.MainProgress = 0.5f;
        progress?.Invoke(ext);
        DagreLayout.InsertSelfEdges(g);

        // Phase 5: Position — Original (via shared NodeLabel references)
        ext.Caption = "position (compound)";
        CoordinateSystem.Adjust(g);
        DagreLayout.Position(g);
        Trace(sw, "  DagreLayout.Position (compound)");

        // Phase 6: Finalization
        DagreLayout.PositionSelfEdges(g);
        DagreLayout.RemoveBorderNodes(g);
        Trace(sw, "  SelfEdges+RemoveBorder");

        ext.Caption = "undo";
        Normalize.Undo(g, f =>
        {
            ext.AdditionalProgress = f;
            progress?.Invoke(ext);
        });
        Trace(sw, "  Normalize.Undo");

        FinishLayout(g, ext, progress, sw);
    }

    /// <summary>
    ///     Indexed pipeline for non-compound graphs (best performance).
    /// </summary>
    private static void RunIndexedLayout(DagreGraph g, ExtProgressInfo ext,
        Action<ExtProgressInfo> progress, Stopwatch sw)
    {
        // Phase 4: Ordering — INDEXED
        ext.Caption = "order (indexed)";
        ext.MainProgress = 0.3f;
        progress?.Invoke(ext);

        var nodeCountBeforeSelfEdges = g.NodeCount();

        var ig2 = IndexedGraph.FromDagreGraph(g);
        Trace(sw, "  FromDagreGraph (order)");
        IndexedOrder.Run(ig2);
        Trace(sw, "  IndexedOrder");
        ig2.WriteOrdersTo(g);

        ext.MainProgress = 0.5f;
        progress?.Invoke(ext);
        DagreLayout.InsertSelfEdges(g);

        // Phase 5: Position — INDEXED
        ext.Caption = "position (indexed)";
        var nodeCountAfterSelfEdges = g.NodeCount();
        var rankDir = g.Graph().RankDir?.ToLower() ?? "tb";
        var isLrRl = rankDir == "lr" || rankDir == "rl";

        CoordinateSystem.Adjust(g);

        IndexedGraph igPos;
        if (nodeCountAfterSelfEdges == nodeCountBeforeSelfEdges)
        {
            if (isLrRl)
                ig2.SwapWidthHeight();
            IndexedPosition.Run(ig2);
            Trace(sw, "  IndexedPosition (reused)");
            ig2.WritePositionsTo(g);
            igPos = ig2;
        }
        else
        {
            var ig3 = IndexedGraph.FromDagreGraphNonCompound(g);
            Trace(sw, "  FromDagreGraphNonCompound (position)");
            IndexedPosition.Run(ig3);
            Trace(sw, "  IndexedPosition");
            ig3.WritePositionsTo(g);
            igPos = ig3;
        }

        // Phase 6: Finalization
        DagreLayout.PositionSelfEdges(g);
        DagreLayout.RemoveBorderNodes(g);
        Trace(sw, "  SelfEdges+RemoveBorder");

        ext.Caption = "undo";
        IndexedNormalize.Undo(igPos, g, f =>
        {
            ext.AdditionalProgress = f;
            progress?.Invoke(ext);
        });
        Trace(sw, "  IndexedNormalize.Undo");

        FinishLayout(g, ext, progress, sw);
    }

    private static void FinishLayout(DagreGraph g, ExtProgressInfo ext,
        Action<ExtProgressInfo> progress, Stopwatch sw)
    {
        DagreLayout.FixupEdgeLabelCoords(g);
        CoordinateSystem.Undo(g);
        DagreLayout.TranslateGraph(g);
        Trace(sw, "  Fixup+CoordUndo+Translate");
        DagreLayout.AssignNodeIntersects(g);
        Trace(sw, "  AssignNodeIntersects");
        DagreLayout.ReversePointsForReversedEdges(g);
        Acyclic.Undo(g);
        Trace(sw, "  ReversePoints+Acyclic.Undo");

        ext.AdditionalProgress = 1;
        ext.MainProgress = 1;
        progress?.Invoke(ext);
    }

    private static void Trace(Stopwatch sw, string label)
    {
        if (sw == null) return;
        Console.Error.WriteLine($"  [{sw.ElapsedMilliseconds,6}ms] {label}");
    }
}
