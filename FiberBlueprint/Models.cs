namespace FiberBlueprint;

// Model classes for JSON input.

public class Network
{
    public string Name { get; set; } = "";
    public List<Pedestal> Pedestals { get; set; } = new();
}

public class Pedestal
{
    public string Name { get; set; } = "";
    public List<Splitter> Splitters { get; set; } = new();
}

public class Splitter
{
    public string Name { get; set; } = "";   // e.g. PON1_1
    public int Ratio { get; set; }           // 1:Ratio
    public string Input { get; set; } = "";  // feed source
    public List<Cable> Cables { get; set; } = new();
}

public class Cable
{
    public int Port { get; set; }                  // 1..Ratio
    public string Label { get; set; } = "";        // e.g. CBL#2-01
    public string Endpoint { get; set; } = "";     // destination
    public string Connector { get; set; } = "SC-APC";
}
