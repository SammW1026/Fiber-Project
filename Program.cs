using System.Text.Json;

namespace FiberBlueprint;

public static class Program
{
    // Exit codes: 0=OK, 1=input invalid, 2=usage/file error.
    public static int Main(string[] args)
    {
        var flags = args.Where(a => a.StartsWith("--")).Select(a => a.ToLowerInvariant()).ToHashSet();
        var files = args.Where(a => !a.StartsWith("--")).ToList();

        if (flags.Contains("--help") || (files.Count == 0 && !flags.Contains("--new")))
        {
            PrintUsage();
            return files.Count == 0 && !flags.Contains("--help") ? 2 : 0;
        }

        var unknown = flags.Except(new[] { "--new", "--check", "--no-overview", "--no-schedule", "--help" }).ToList();
        if (unknown.Count > 0)
        {
            Console.WriteLine($"Unknown option(s): {string.Join(", ", unknown)}");
            PrintUsage();
            return 2;
        }

        if (flags.Contains("--new"))
            return RunBuilder(files.Count > 0 ? files[0] : "network.json");

        string inputPath = files[0];
        var (net, code) = LoadAndValidate(inputPath);
        if (net == null) return code;

        if (flags.Contains("--check"))
        {
            Console.WriteLine("Input is valid.");
            return 0;
        }

        string outputPath = files.Count > 1
            ? files[1]
            : Path.ChangeExtension(Path.GetFileName(inputPath), ".pdf");
        var opts = new RenderOptions
        {
            Overview = !flags.Contains("--no-overview"),
            Schedule = !flags.Contains("--no-schedule"),
        };
        return Render(net, outputPath, opts);
    }

    static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  FiberBlueprint <network.json> [output.pdf] [--no-overview] [--no-schedule]");
        Console.WriteLine("  FiberBlueprint --check <network.json>     validate only, no PDF");
        Console.WriteLine("  FiberBlueprint --new [network.json]       build a network by answering questions");
    }

    static (Network? net, int code) LoadAndValidate(string inputPath)
    {
        Network? net;
        try
        {
            net = NetworkFile.Load(inputPath);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            Console.WriteLine($"Error: could not find '{inputPath}'.");
            return (null, 2);
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Error: '{inputPath}' is not valid JSON ({ex.Message})");
            return (null, 1);
        }

        if (net == null)
        {
            Console.WriteLine("Error: the input file is empty.");
            return (null, 1);
        }

        var errors = Validator.Validate(net);
        if (errors.Count > 0)
        {
            Console.WriteLine($"Input has {errors.Count} problem(s):");
            foreach (var e in errors) Console.WriteLine($"  - {e}");
            return (null, 1);
        }

        var topo = new Topology(net);
        int cables = topo.All.Sum(r => r.Splitter.Cables.Count);
        Console.WriteLine($"Loaded '{net.Name}': {net.Pedestals.Count} pedestal(s), {topo.All.Count} splitter(s), " +
                          $"{cables} cable(s), {topo.Links.Count} splitter link(s).");
        return (net, 0);
    }

    static int Render(Network net, string outputPath, RenderOptions opts)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
            int pages = BlueprintRenderer.Render(net, outputPath, opts);
            Console.WriteLine($"Wrote {outputPath} ({pages} page(s)).");
            return 0;
        }
        catch (IOException ex)
        {
            Console.WriteLine($"Error: could not write '{outputPath}' ({ex.Message}). Is it open in a PDF viewer?");
            return 2;
        }
    }

    static int RunBuilder(string jsonPath)
    {
        var net = new Builder(Console.In, Console.Out).Run();
        Console.WriteLine();

        var errors = Validator.Validate(net);
        if (errors.Count > 0)
        {
            Console.WriteLine($"The network has {errors.Count} problem(s):");
            foreach (var e in errors) Console.WriteLine($"  - {e}");
            Console.WriteLine("Saving anyway so you can fix it by hand or re-run with --check.");
        }

        NetworkFile.Save(net, jsonPath);
        Console.WriteLine($"Saved {jsonPath}.");
        if (errors.Count > 0) return 1;

        return Render(net, Path.ChangeExtension(jsonPath, ".pdf"), new RenderOptions());
    }
}
