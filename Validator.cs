namespace FiberBlueprint;

// Validate a Network. Returns a list of error messages (empty = valid).
public static class Validator
{
    public static readonly int[] AllowedRatios = { 2, 4, 8, 16, 32 };

    public static List<string> Validate(Network net)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(net.Name))
            errors.Add("Network is missing a name.");
        if (net.Pedestals.Count == 0)
            errors.Add("Network has no pedestals.");

        foreach (var ped in net.Pedestals)
        {
            string pedName = string.IsNullOrWhiteSpace(ped.Name) ? "(unnamed pedestal)" : ped.Name;
            if (string.IsNullOrWhiteSpace(ped.Name))
                errors.Add("A pedestal is missing a name.");
            if (ped.Splitters.Count == 0)
                errors.Add($"Pedestal '{pedName}' has no splitters.");

            foreach (var sp in ped.Splitters)
            {
                string where = $"Pedestal '{pedName}' > Splitter '{sp.Name}'";
                if (string.IsNullOrWhiteSpace(sp.Name))
                    errors.Add($"Pedestal '{pedName}' has a splitter with no name.");

                if (!AllowedRatios.Contains(sp.Ratio))
                {
                    errors.Add($"{where}: ratio 1:{sp.Ratio} is not supported (use 1:2, 1:4, 1:8, 1:16 or 1:32).");
                    continue; // port checks need a valid ratio
                }

                var usedPorts = new HashSet<int>();
                foreach (var cable in sp.Cables)
                {
                    if (cable.Port < 1 || cable.Port > sp.Ratio)
                        errors.Add($"{where}: port {cable.Port} is outside 1-{sp.Ratio}.");
                    else if (!usedPorts.Add(cable.Port))
                        errors.Add($"{where}: port {cable.Port} is used by more than one cable.");

                    if (string.IsNullOrWhiteSpace(cable.Label))
                        errors.Add($"{where}: the cable on port {cable.Port} has no label.");
                    // A cable that feeds another splitter gets its endpoint from that splitter.
                    if (string.IsNullOrWhiteSpace(cable.Endpoint) && string.IsNullOrWhiteSpace(cable.Feeds))
                        errors.Add($"{where}: the cable on port {cable.Port} has no endpoint.");
                }
            }
        }

        CheckLinks(net, errors);
        return errors;
    }

    // Checks that involve more than one splitter: unique names and the "feeds" links.
    static void CheckLinks(Network net, List<string> errors)
    {
        var names = net.Pedestals.SelectMany(p => p.Splitters)
            .Select(s => s.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n));
        foreach (var dup in names.GroupBy(n => n, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            errors.Add($"Splitter name '{dup.Key}' is used {dup.Count()} times; splitter names must be unique.");

        var topo = new Topology(net);

        foreach (var r in topo.All)
            foreach (var cable in r.Splitter.Cables.Where(c => !string.IsNullOrWhiteSpace(c.Feeds)))
            {
                string where = $"Pedestal '{r.Pedestal.Name}' > Splitter '{r.Splitter.Name}'";
                if (!topo.ByName.TryGetValue(cable.Feeds!, out var target))
                    errors.Add($"{where}: port {cable.Port} feeds splitter '{cable.Feeds}', which does not exist.");
                else if (target.Splitter == r.Splitter)
                    errors.Add($"{where}: port {cable.Port} feeds the same splitter it comes from.");
            }

        foreach (var g in topo.Links.GroupBy(l => l.To.Splitter).Where(g => g.Count() > 1))
        {
            string sources = string.Join(", ", g.Select(l => $"{l.From.Splitter.Name} port {l.Cable.Port}"));
            errors.Add($"Splitter '{g.Key.Name}' is fed by more than one cable ({sources}).");
        }

        var reported = new HashSet<string>();
        foreach (var r in topo.All)
        {
            var loop = topo.LoopThrough(r.Splitter);
            if (loop == null || loop.Count < 2) continue; // self-feed is reported above
            string key = string.Join(" -> ", loop.Select(s => s.Name).OrderBy(n => n));
            if (!reported.Add(key)) continue;
            // loop is [s, s's feeder, ...]; reversed it reads in feed order.
            var order = loop.Select(s => s.Name).Reverse().ToList();
            errors.Add($"Splitters form a feed loop: {string.Join(" -> ", order)} -> {order[0]}.");
        }
    }
}
