Fiber Optic System Blueprint Generator

A C# command-line tool that reads a fiber network description (JSON) and draws a
printable blueprint as a PDF:

- Sheet 1: system overview of every splitter, grouped by pedestal, with the cables
  that link splitters (dashed when the cable runs between pedestals).
- One detail sheet per splitter, with a title block, numbered output ports, standard
  fiber colors, cable labels, endpoints, spare ports and cross-references to other sheets.
- Cable schedule: a table of every cable (from splitter/port, fiber color, connector, destination).

Run it

    dotnet run -- samples/demomedium.json output/demomedium.pdf
    dotnet run -- samples/demolarge.json output/demolarge.pdf --no-schedule
    dotnet run -- --check samples/badfeedsloop.json      (validate only)
    dotnet run -- --new output/mynetwork.json            (build a network by answering questions)

In Visual Studio, pick a profile from the Run button drop-down (Properties/launchSettings.json):
"Demo (medium network)", "Demo (large network)", "Validate only (bad sample)" or "Interactive builder".

Tests

    ./run_tests.sh        (bash)
    .\run_tests.ps1       (PowerShell)

Exit codes: 0 = OK, 1 = input invalid, 2 = usage or file error.

Input format

```json
{
  "name": "Demo Colony Fiber",
  "revision": "B",
  "drawnBy": "Your name",
  "pedestals": [
    { "name": "Main Pedestal",
      "splitters": [
        { "name": "PON1 Primary", "ratio": 4, "input": "OLT port 1",
          "cables": [ { "port": 1, "label": "CBL#1-01", "feeds": "PON1_1" } ] },
        { "name": "PON1_1", "ratio": 16,
          "cables": [ { "port": 1, "label": "CBL#2-01", "endpoint": "House A" } ] }
      ] }
  ]
}
```

`feeds` (optional) names the downstream splitter a cable goes to. The validator rejects
unknown targets, splitters fed by two cables, feed loops and duplicate splitter names.
`connector` defaults to SC-APC.
