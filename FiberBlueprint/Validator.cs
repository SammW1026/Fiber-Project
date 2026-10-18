namespace FiberBlueprint;

// Validate a Network. Returns a list of error messages (empty = valid).
public static class Validator
{
    static readonly int[] AllowedRatios = { 2, 4, 8, 16, 32 };

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
                    if (string.IsNullOrWhiteSpace(cable.Endpoint))
                        errors.Add($"{where}: the cable on port {cable.Port} has no endpoint.");
                }
            }
        }
        return errors;
    }
}
