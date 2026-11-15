# Runs every sample through FiberBlueprint and checks the exit code and output.
# Usage (PowerShell, from the project folder):  .\run_tests.ps1
Set-Location $PSScriptRoot
dotnet build -nologo -v q | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Host "Build failed"; exit 1 }
$dll = "bin/Debug/net8.0/FiberBlueprint.dll"
New-Item -ItemType Directory -Force output | Out-Null
$script:pass = 0; $script:fail = 0

function Check($name, $code, $text, [string[]]$appArgs, $stdin = $null) {
    if ($stdin) { $out = (Get-Content $stdin | & dotnet $dll @appArgs) -join "`n" }
    else        { $out = (& dotnet $dll @appArgs) -join "`n" }
    $got = $LASTEXITCODE
    if ($got -eq $code -and $out.Contains($text)) { Write-Host "PASS  $name"; $script:pass++ }
    else {
        Write-Host "FAIL  $name (exit $got, expected $code; looking for: $text)" -ForegroundColor Red
        $out -split "`n" | ForEach-Object { Write-Host "      $_" }
        $script:fail++
    }
}

Check "demosmall.json"            0 "(3 page(s))"    @("samples/demosmall.json", "output/demosmall.pdf")
Check "demomedium.json"           0 "(5 page(s))"    @("samples/demomedium.json", "output/demomedium.pdf")
Check "demolarge.json"            0 "(10 page(s))"    @("samples/demolarge.json", "output/demolarge.pdf")
Check "demomedium --no-overview"  0 "(4 page(s))"    @("samples/demomedium.json", "output/nooverview.pdf", "--no-overview")
Check "demolarge --check"         0 "Input is valid" @("--check", "samples/demolarge.json")
Check "badratio.json"             1 "ratio 1:5 is not supported"  @("samples/badratio.json", "output/x.pdf")
Check "badportrange.json"         1 "port 9 is outside 1-4"       @("samples/badportrange.json", "output/x.pdf")
Check "badduplicateport.json"     1 "used by more than one cable" @("samples/badduplicateport.json", "output/x.pdf")
Check "badmissinglabel.json"      1 "has no label"                @("samples/badmissinglabel.json", "output/x.pdf")
Check "badjson.json"              1 "is not valid JSON"           @("samples/badjson.json", "output/x.pdf")
Check "badfeedsmissing.json"      1 "which does not exist"        @("samples/badfeedsmissing.json", "output/x.pdf")
Check "badfeedsloop.json"         1 "form a feed loop"            @("samples/badfeedsloop.json", "output/x.pdf")
Check "badfedtwice.json"          1 "fed by more than one cable"  @("samples/badfedtwice.json", "output/x.pdf")
Check "badduplicatesplitter.json" 1 "must be unique"              @("samples/badduplicatesplitter.json", "output/x.pdf")
Check "missing file"              2 "could not find"              @("samples/nope.json")
Check "unknown option"            2 "Unknown option"              @("samples/demosmall.json", "--bogus")
Check "builder (scripted)"        0 "(4 page(s))"                 @("--new", "output/built.json") "samples/builder_input.txt"
Check "builder output re-loads"   0 "1 splitter link(s)"          @("--check", "output/built.json")

Write-Host "$($script:pass) passed, $($script:fail) failed"
if ($script:fail -gt 0) { exit 1 }
