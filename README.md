# MostlyLucid.Dagre

[![NuGet](https://img.shields.io/nuget/v/MostlyLucid.Dagre.svg)](https://www.nuget.org/packages/MostlyLucid.Dagre/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

A modern, high-performance C# graph layout engine implementing the Sugiyama (layered) algorithm. Modernized fork of [Dagre.NET](https://github.com/nicknash/dagre.NET) (itself a port of [dagre.js](https://github.com/dagrejs/dagre)).

## What Changed

This fork replaces the original's JavaScript idioms with idiomatic .NET:

- **Zero `dynamic`** — all property bags replaced with strongly-typed `NodeLabel`, `EdgeLabel`, `GraphLabel` classes
- **Zero boxing** — no `Dictionary<string, object>`, no `object[]` parameter arrays
- **AOT-compatible** — no `Microsoft.CSharp` dependency, works with NativeAOT and WASM
- **Multi-target** — `net6.0`, `net8.0`, `net10.0`

## Install

```
dotnet add package MostlyLucid.Dagre
```

## Usage

### High-Level API

```csharp
using Dagre;

var graph = new DagreInputGraph();

var a = graph.AddNode(tag: "Start", width: 100, height: 40);
var b = graph.AddNode(tag: "Process", width: 120, height: 40);
var c = graph.AddNode(tag: "End", width: 100, height: 40);

graph.AddEdge(a, b);
graph.AddEdge(b, c);

graph.Layout();

// Nodes now have X, Y coordinates
Console.WriteLine($"{a.Tag}: ({a.X}, {a.Y})");
Console.WriteLine($"{b.Tag}: ({b.X}, {b.Y})");
Console.WriteLine($"{c.Tag}: ({c.X}, {c.Y})");
```

### Low-Level API

For full control over the layout graph:

```csharp
using Dagre;

var g = new DagreGraph { IsDirected = true, IsCompound = false, IsMultigraph = false };

g.SetGraphLabel(new GraphLabel
{
    RankDir = "TB",
    NodeSep = 50,
    RankSep = 50,
    EdgeSep = 10,
    Ranker = "network-simplex"
});

g.SetNode("a", new NodeLabel { Width = 100, Height = 40 });
g.SetNode("b", new NodeLabel { Width = 120, Height = 40 });
g.SetEdge("a", "b", new EdgeLabel { Weight = 1, Minlen = 1 });

DagreLayout.runLayout(g);

var aLabel = g.Node("a");
Console.WriteLine($"a: ({aLabel.X}, {aLabel.Y})");
```

### Layout Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `RankDir` | `string` | `"TB"` | Layout direction: `TB`, `BT`, `LR`, `RL` |
| `RankSep` | `double` | `50` | Pixels between ranks (layers) |
| `NodeSep` | `double` | `50` | Pixels between nodes in same rank |
| `EdgeSep` | `double` | `10` | Pixels between edges |
| `Align` | `string` | `null` | Node alignment: `UL`, `UR`, `DL`, `DR` |
| `Ranker` | `string` | `"network-simplex"` | Ranking algorithm: `network-simplex`, `tight-tree`, `longest-path` |
| `Acyclicer` | `string` | `null` | Set to `"greedy"` to break cycles |

### Compound Graphs

```csharp
var g = new DagreGraph { IsDirected = true, IsCompound = true };
g.SetGraphLabel(new GraphLabel { RankDir = "TB" });

g.SetNode("group", new NodeLabel());
g.SetNode("a", new NodeLabel { Width = 80, Height = 40 });
g.SetNode("b", new NodeLabel { Width = 80, Height = 40 });

g.SetParent("a", "group");
g.SetParent("b", "group");

g.SetEdge("a", "b", new EdgeLabel());
DagreLayout.runLayout(g);
```

## Performance

Compared to the original Dagre.NET 1.0.0.6 NuGet package:

| Graph Size | Original | This Fork | Speedup | Memory Reduction |
|------------|----------|-----------|---------|-----------------|
| 5 nodes, 8 edges | 1.2 ms / 3.2 MB | 0.3 ms / 0.7 MB | **4x** | **4.6x** |
| 20 nodes, 30 edges | 14.8 ms / 24 MB | 1.8 ms / 4.0 MB | **8x** | **6x** |
| 50 nodes, 80 edges | 140 ms / 136 MB | 17 ms / 24 MB | **8x** | **5.6x** |
| 200 nodes, 350 edges | 4,127 ms / 3.5 GB | 564 ms / 321 MB | **7x** | **11x** |

Benchmarks run with BenchmarkDotNet on .NET 10, using identical graph construction.

## Architecture

The layout pipeline follows the Sugiyama method:

1. **Acyclic** — reverse edges to eliminate cycles
2. **Rank** — assign layers via network simplex
3. **Order** — minimize edge crossings within layers
4. **Coordinate Assignment** — Brandes-Kopf algorithm for X coordinates
5. **Normalize/Denormalize** — insert and remove dummy nodes for long edges
6. **Edge Routing** — compute control points for spline edges

## Credits

- [dagre](https://github.com/dagrejs/dagre) — original JavaScript implementation by Chris Pettitt
- [Dagre.NET](https://github.com/nicknash/dagre.NET) — original C# port by fel88
- [Naiad](https://github.com/SimonCropp/Naiad) — Mermaid-to-SVG library that uses this layout engine

## License

[MIT](LICENSE) — original license from fel88's Dagre.NET port.
