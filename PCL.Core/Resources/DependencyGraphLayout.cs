namespace PCL.Core.Resources;

/// <summary>依赖图中的一个节点。</summary>
public sealed class DependencyGraphNode {

    public string Id { get; init; } = "";
    public string Label { get; init; } = "";

    /// <summary>距离根资源的层数，根为 0。</summary>
    public int Depth { get; init; }

    public bool IsRoot { get; init; }

    /// <summary>画布坐标（节点中心）。</summary>
    public double X { get; init; }
    public double Y { get; init; }

}

/// <summary>依赖图：节点、边与画布尺寸。</summary>
public sealed class DependencyGraph {

    public IReadOnlyList<DependencyGraphNode> Nodes { get; init; } = [];
    public IReadOnlyList<DependencyEdge> Edges { get; init; } = [];
    public double Width { get; init; }
    public double Height { get; init; }

    public int MaxDepth { get; init; }

    /// <summary>按 ID 查找节点坐标，便于绘制连线。</summary>
    public DependencyGraphNode? Find(string? id) =>
        string.IsNullOrWhiteSpace(id)
            ? null
            : Nodes.FirstOrDefault(node => string.Equals(node.Id, id, StringComparison.OrdinalIgnoreCase));

}

/// <summary>
/// 把依赖解析结果排成分层图：根在最左，依赖按层级向右展开，同层内纵向均分。
/// 只做几何计算，不涉及任何界面类型，便于测试。
/// </summary>
public static class DependencyGraphLayout {

    /// <summary>节点默认尺寸，用于预留边距。</summary>
    public const double NodeWidth = 150;
    public const double NodeHeight = 34;
    private const double MarginX = 24;
    private const double MarginY = 20;

    /// <summary>构建并布局依赖图。</summary>
    public static DependencyGraph Build(ResourceDependencyPlan? plan, double width = 900, double height = 420) {
        if (plan is null || plan.Files.Count == 0) {
            return new DependencyGraph { Width = width, Height = height };
        }
        // 以文件顺序作为节点顺序，父资源必然先于其依赖出现
        var nodes = plan.Files
            .Select(file => new {
                Id = string.IsNullOrWhiteSpace(file.Id) ? file.FileName : file.Id,
                Label = string.IsNullOrWhiteSpace(file.FileName) ? file.Id : file.FileName
            })
            .ToList();
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < nodes.Count; i++) {
            if (!index.ContainsKey(nodes[i].Id)) index[nodes[i].Id] = i;
        }

        // BFS 计算层级
        var depths = new int[nodes.Count];
        Array.Fill(depths, -1);
        var adjacency = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var edge in plan.Edges) {
            if (!adjacency.TryGetValue(edge.FromId, out var list)) {
                list = [];
                adjacency[edge.FromId] = list;
            }
            list.Add(edge.ToId);
        }
        var rootId = nodes[0].Id;
        if (index.ContainsKey(rootId)) depths[index[rootId]] = 0;
        var queue = new Queue<int>();
        queue.Enqueue(index.ContainsKey(rootId) ? index[rootId] : 0);
        while (queue.Count > 0) {
            var current = queue.Dequeue();
            var currentId = nodes[current].Id;
            if (!adjacency.TryGetValue(currentId, out var children)) continue;
            foreach (var childId in children) {
                if (!index.TryGetValue(childId, out var childIndex)) continue;
                if (depths[childIndex] >= 0) continue;
                depths[childIndex] = depths[current] + 1;
                queue.Enqueue(childIndex);
            }
        }
        // 没有被边连到的节点（例如解析失败的同级项）兜底为第 1 层
        for (var i = 0; i < depths.Length; i++) {
            if (depths[i] < 0) depths[i] = i == 0 ? 0 : 1;
        }

        var maxDepth = depths.Length == 0 ? 0 : depths.Max();
        var layers = new Dictionary<int, List<int>>();
        for (var i = 0; i < depths.Length; i++) {
            if (!layers.TryGetValue(depths[i], out var list)) {
                list = [];
                layers[depths[i]] = list;
            }
            list.Add(i);
        }

        var usableWidth = Math.Max(width - NodeWidth, NodeWidth);
        var usableHeight = Math.Max(height - NodeHeight, NodeHeight);
        var stepX = maxDepth == 0 ? 0 : usableWidth / maxDepth;
        var placed = new List<DependencyGraphNode>(nodes.Count);
        foreach (var (depth, members) in layers.OrderBy(pair => pair.Key)) {
            var stepY = members.Count <= 1 ? 0 : usableHeight / (members.Count - 1);
            for (var slot = 0; slot < members.Count; slot++) {
                var nodeIndex = members[slot];
                placed.Add(new DependencyGraphNode {
                    Id = nodes[nodeIndex].Id,
                    Label = nodes[nodeIndex].Label,
                    Depth = depth,
                    IsRoot = depth == 0,
                    X = NodeWidth / 2 + MarginX * 0 + depth * stepX,
                    Y = NodeHeight / 2 + (members.Count <= 1 ? usableHeight / 2 : slot * stepY)
                });
            }
        }

        return new DependencyGraph {
            Nodes = placed.OrderBy(node => node.Depth).ToList(),
            Edges = plan.Edges,
            Width = width,
            Height = height,
            MaxDepth = maxDepth
        };
    }

}
