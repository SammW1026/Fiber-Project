namespace FiberBlueprint;

// Interactive console builder: asks questions and builds a Network without
// writing JSON by hand. First step toward a full graphical editor.
// Reads from any TextReader so it can be driven by a script in tests.
public class Builder
{
    readonly TextReader input;
    readonly TextWriter output;
    bool endOfInput;

    public Builder(TextReader input, TextWriter output)
    {
        this.input = input;
        this.output = output;
    }

    // Prompt with an optional default; blank answer (or end of input) returns the default.
    string Ask(string question, string def = "")
    {
        output.Write(def.Length > 0 ? $"{question} [{def}]: " : $"{question}: ");
        if (endOfInput) { output.WriteLine(); return def; }
        string? line = input.ReadLine();
        if (line == null) { endOfInput = true; output.WriteLine(); return def; }
        line = line.Replace("﻿", "").Trim(); // PowerShell pipes can add a byte-order mark
        return line.Length == 0 ? def : line;
    }

    int AskInt(string question, int def, Func<int, bool> ok, string hint)
    {
        while (true)
        {
            string a = Ask(question, def.ToString());
            if (int.TryParse(a, out int v) && ok(v)) return v;
            output.WriteLine($"  Please enter {hint}.");
            if (endOfInput) return def;
        }
    }

    bool AskYesNo(string question, bool def)
    {
        string a = Ask(question + " (y/n)", def ? "y" : "n").ToLowerInvariant();
        return a.StartsWith('y');
    }

    public Network Run()
    {
        output.WriteLine("Fiber Blueprint - network builder");
        output.WriteLine("Press Enter to accept the [default]. Leave a name blank to finish that list.");
        output.WriteLine();

        var net = new Network
        {
            Name = Ask("Project name", "My Fiber Network"),
            DrawnBy = Ask("Drawn by"),
            Revision = Ask("Revision", "A"),
        };

        int splitterCount = 0;
        while (!endOfInput)
        {
            output.WriteLine();
            string pedName = Ask($"Pedestal {net.Pedestals.Count + 1} name (blank to finish)");
            if (pedName.Length == 0) break;
            var ped = new Pedestal { Name = pedName };
            net.Pedestals.Add(ped);

            while (!endOfInput)
            {
                string spName = Ask($"  Splitter {ped.Splitters.Count + 1} in {pedName} (blank to finish pedestal)");
                if (spName.Length == 0) break;
                splitterCount++;
                var sp = new Splitter { Name = spName };
                sp.Ratio = AskInt("    Ratio 1:N", 16, v => Validator.AllowedRatios.Contains(v), "2, 4, 8, 16 or 32");
                sp.Input = Ask("    Input / feed source (blank if another splitter feeds it)");
                ped.Splitters.Add(sp);
                AddCables(sp, splitterCount);
            }
        }
        return net;
    }

    void AddCables(Splitter sp, int splitterNo)
    {
        while (!endOfInput)
        {
            int next = Enumerable.Range(1, sp.Ratio).FirstOrDefault(p => sp.Cables.All(c => c.Port != p));
            if (next == 0)
            {
                output.WriteLine($"    All {sp.Ratio} ports on {sp.Name} are used.");
                return;
            }
            string portText = Ask($"    Cable on port (1-{sp.Ratio}, blank to finish splitter)");
            if (portText.Length == 0) return;
            if (portText.Equals("next", StringComparison.OrdinalIgnoreCase)) portText = next.ToString();
            if (!int.TryParse(portText, out int port) || port < 1 || port > sp.Ratio)
            {
                output.WriteLine($"      Port must be 1-{sp.Ratio} (or 'next' for port {next}).");
                continue;
            }
            if (sp.Cables.Any(c => c.Port == port))
            {
                output.WriteLine($"      Port {port} already has a cable.");
                continue;
            }

            var cable = new Cable { Port = port };
            cable.Label = Ask("      Label", $"CBL#{splitterNo}-{port:00}");
            string feeds = Ask("      Feeds splitter (name, or blank for a drop cable)");
            if (feeds.Length > 0)
            {
                cable.Feeds = feeds;
                cable.Endpoint = Ask("      Endpoint", $"{feeds} splitter");
            }
            else
            {
                cable.Endpoint = Ask("      Endpoint (house, building...)");
            }
            cable.Connector = Ask("      Connector", "SC-APC");
            sp.Cables.Add(cable);
        }
    }
}
