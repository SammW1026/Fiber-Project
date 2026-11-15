#!/usr/bin/env bash
# Runs every sample through FiberBlueprint and checks the exit code and output.
# Usage: ./run_tests.sh
cd "$(dirname "$0")"
dotnet build -nologo -v q >/dev/null || { echo "Build failed"; exit 1; }
APP="dotnet bin/Debug/net8.0/FiberBlueprint.dll"
mkdir -p output
pass=0; fail=0

check() { # name, expected exit code, expected text, command...
  local name="$1" code="$2" text="$3"; shift 3
  out=$("$@" 2>&1); got=$?
  if [ "$got" = "$code" ] && grep -qF -- "$text" <<<"$out"; then
    echo "PASS  $name"; pass=$((pass+1))
  else
    echo "FAIL  $name (exit $got, expected $code; looking for: $text)"; echo "$out" | sed 's/^/      /'; fail=$((fail+1))
  fi
}

check demosmall.json             0 "(3 page(s))"  $APP samples/demosmall.json output/demosmall.pdf
check demomedium.json            0 "(5 page(s))"  $APP samples/demomedium.json output/demomedium.pdf
check demolarge.json             0 "(10 page(s))"  $APP samples/demolarge.json output/demolarge.pdf
check "demomedium --no-overview" 0 "(4 page(s))"  $APP samples/demomedium.json output/nooverview.pdf --no-overview
check "demolarge --check"        0 "Input is valid" $APP --check samples/demolarge.json
check badratio.json              1 "ratio 1:5 is not supported"        $APP samples/badratio.json output/x.pdf
check badportrange.json          1 "port 9 is outside 1-4"             $APP samples/badportrange.json output/x.pdf
check badduplicateport.json      1 "used by more than one cable"       $APP samples/badduplicateport.json output/x.pdf
check badmissinglabel.json       1 "has no label"                      $APP samples/badmissinglabel.json output/x.pdf
check badjson.json               1 "is not valid JSON"                 $APP samples/badjson.json output/x.pdf
check badfeedsmissing.json       1 "which does not exist"              $APP samples/badfeedsmissing.json output/x.pdf
check badfeedsloop.json          1 "form a feed loop"                  $APP samples/badfeedsloop.json output/x.pdf
check badfedtwice.json           1 "fed by more than one cable"        $APP samples/badfedtwice.json output/x.pdf
check badduplicatesplitter.json  1 "must be unique"                    $APP samples/badduplicatesplitter.json output/x.pdf
check "missing file"             2 "could not find"                    $APP samples/nope.json
check "unknown option"           2 "Unknown option"                    $APP samples/demosmall.json --bogus
check "builder (scripted)"       0 "(4 page(s))"  bash -c "$APP --new output/built.json < samples/builder_input.txt"
check "builder output re-loads"  0 "1 splitter link(s)"                $APP --check output/built.json

echo "$pass passed, $fail failed"
[ "$fail" = 0 ]
