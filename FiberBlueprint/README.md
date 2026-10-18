Fiber Optic System Blueprint Generator

A C# command-line tool that reads a fiber network description (JSON) and draws a
printable blueprint as a PDF: one sheet per splitter, with a title block, numbered
output ports, standard fiber colors, cable labels and endpoints. Unused ports are
drawn as "spare".

Class project for Programming Languages 

Run it

```
dotnet run -- samples/demo_medium.json demo_medium.pdf
```


Input format

```
{
  "name": "Demo Colony Fiber",
  "pedestals": [
    { "name": "Main Pedestal",
      "splitters": [
        { "name": "PON1_1", "ratio": 16, "input": "PON1 Primary port 1",
          "cables": [ { "port": 1, "label": "CBL#2-01", "endpoint": "House A" } ] }
      ] }
  ]
}
```

