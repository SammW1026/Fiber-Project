namespace FiberBlueprint;

// A splitter together with the pedestal it sits in.
public record SplitterRef(Pedestal Pedestal, Splitter Splitter);

// A cable from an output port of one splitter into the input of another.
public record FeedLink(SplitterRef From, Cable Cable, SplitterRef To)
{
    public bool CrossesPedestal => !ReferenceEquals(From.Pedestal, To.Pedestal);
}

// Works out how splitters connect to each other through "feeds" cables.
public class Topology
{
    public List<SplitterRef> All { get; } = new();
    public Dictionary<string, SplitterRef> ByName { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<FeedLink> Links { get; } = new();
    public Dictionary<Splitter, FeedLink> FedBy { get; } = new();

    public Topology(Network net)
    {
        foreach (var ped in net.Pedestals)
            foreach (var sp in ped.Splitters)
            {
                var r = new SplitterRef(ped, sp);
                All.Add(r);
                ByName.TryAdd(sp.Name ?? "", r);
            }

        foreach (var r in All)
            foreach (var cable in r.Splitter.Cables.OrderBy(c => c.Port))
            {
                if (string.IsNullOrWhiteSpace(cable.Feeds)) continue;
                if (!ByName.TryGetValue(cable.Feeds, out var to)) continue;
                var link = new FeedLink(r, cable, to);
                Links.Add(link);
                FedBy.TryAdd(to.Splitter, link);
            }
    }

    // How many splitters sit between this one and the start of the network.
    public int DepthOf(Splitter s)
    {
        int depth = 0;
        var seen = new HashSet<Splitter> { s };
        while (FedBy.TryGetValue(s, out var link) && seen.Add(link.From.Splitter))
        {
            depth++;
            s = link.From.Splitter;
        }
        return depth;
    }

    // Returns the splitters in a feed loop that contains s, or null if s is not in a loop.
    public List<Splitter>? LoopThrough(Splitter s)
    {
        var path = new List<Splitter> { s };
        var cur = s;
        while (FedBy.TryGetValue(cur, out var link))
        {
            cur = link.From.Splitter;
            if (cur == s) return path;
            if (path.Contains(cur)) return null; // loop exists further up, not through s
            path.Add(cur);
        }
        return null;
    }

    public bool IsDrop(Cable c) => string.IsNullOrWhiteSpace(c.Feeds);
}
