#!/usr/bin/env python3
"""Usage: python3 -I coverage-summary.py [--gate] <assembly>=<cobertura xml>...
Prints a Markdown table of line and branch coverage for each named assembly, read from the report of the test run that
targets it. Other assemblies in the report (what the run pulls in, such as Core in the UI run) are left out.
--gate adds the minimum (MIN_LINE_COVERAGE below, the one place it is set) to the table and exits 1 when an assembly's line
coverage, rounded to 0.1, is under it.
Exits 1 on a line or branch rate that is missing, unparseable, NaN or infinite."""
import math
import sys
import xml.etree.ElementTree as ET

# The fixed minimum line coverage, in percent, that each assembly must reach (docs/dev-setup-build.md). The workflows pass --gate.
MIN_LINE_COVERAGE = 95.0


def package_rates(assembly, path):
    package = next((p for p in ET.parse(path).getroot().iter("package") if p.get("name") == assembly), None)
    if package is None:
        sys.exit(f"{assembly} is not in {path}")
    return rate(assembly, path, package, "line-rate"), rate(assembly, path, package, "branch-rate")


def rate(assembly, path, package, name):
    try:
        value = float(package.get(name)) * 100
    except (TypeError, ValueError):
        sys.exit(f"{assembly} has no readable {name} in {path}")
    if not math.isfinite(value):
        sys.exit(f"{assembly} has a {name} of {package.get(name)} in {path}, not a number")
    return value


args = sys.argv[1:]
gate = bool(args) and args[0] == "--gate"
if gate:
    args = args[1:]

measured = {}
for argument in args:
    assembly, path = argument.split("=", 1)
    measured[assembly] = package_rates(assembly, path)

print("| Assembly | Lines | Branches |" + (" Minimum lines |" if gate else ""))
print("| --- | ---: | ---: |" + (" ---: |" if gate else ""))
failures = []
for assembly, (lines, branches) in measured.items():
    row = f"| {assembly} | {lines:.1f}% | {branches:.1f}% |"
    if gate:
        row += f" {MIN_LINE_COVERAGE:.1f}% |"
        if round(lines, 1) < MIN_LINE_COVERAGE:
            failures.append(f"{assembly} line coverage {lines:.1f}% is below the minimum {MIN_LINE_COVERAGE:.1f}%")
    print(row)
for failure in failures:
    print(f"\n**{failure}.**")
    print(f"::error::{failure}. Add tests, or lower MIN_LINE_COVERAGE in .github/scripts/coverage-summary.py if the drop is intended.", file=sys.stderr)
sys.exit(1 if failures else 0)
