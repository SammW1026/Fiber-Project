using System.Text.Json;

namespace FiberBlueprint;

public static class Program
{
    // Exit codes: 0=OK, 1=input invalid, 2=usage/file error.
    public static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("Usage: FiberBlueprint <network.json> [output.pdf]");
            return 2;
        }

        string inputPath = args[0];
        string outputPath = args.Length > 1
            ? args[1]
            : Path.ChangeExtension(Path.GetFileName(inputPath), ".pdf");

        Network? net;
        try
        {
            string json = File.ReadAllText(inputPath);
            net = JsonSerializer.Deserialize<Network>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (FileNotFoundException)
        {
            Console.WriteLine($"Error: could not find '{inputPath}'.");
            return 2;
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Error: '{inputPath}' is not valid JSON ({ex.Message})");
            return 1;
        }

        if (net == null)
        {
            Console.WriteLine("Error: the input file is empty.");
            return 1;
        }

        var errors = Validator.Validate(net);
        if (errors.Count > 0)
        {
            Console.WriteLine($"Input has {errors.Count} problem(s):");
            foreach (var e in errors) Console.WriteLine($"  - {e}");
            return 1;
        }

        int splitters = net.Pedestals.Sum(p => p.Splitters.Count);
        int cables = net.Pedestals.Sum(p => p.Splitters.Sum(s => s.Cables.Count));
        Console.WriteLine($"Loaded '{net.Name}': {net.Pedestals.Count} pedestal(s), {splitters} splitter(s), {cables} cable(s).");

        int pages = BlueprintRenderer.Render(net, outputPath);
        Console.WriteLine($"Wrote {outputPath} ({pages} page(s)).");
        return 0;
    }
}
