# Zametek.Maths.Graphs

[![CI](https://github.com/countincognito/Zametek.Maths.Graphs/actions/workflows/ci.yml/badge.svg)](https://github.com/countincognito/Zametek.Maths.Graphs/actions/workflows/ci.yml)

A headless library implementing the [Critical Path Method](https://en.wikipedia.org/wiki/Critical_path_method) for project-planning graphs: activity networks, critical-path analysis, transitive reduction, cycle detection and resource-constrained scheduling. It is the scheduling engine behind [Zametek.ProjectPlan](https://github.com/countincognito/Zametek.ProjectPlan), a free, open-source, cross-platform desktop alternative to Microsoft Project.

This repository hosts two implementations:

| Folder | Implementation | Notes |
| --- | --- | --- |
| [`dotnet/`](dotnet/README.md) | C# / .NET — **the reference implementation** | Published to NuGet as [Zametek.Maths.Graphs.Primitives](https://www.nuget.org/packages/Zametek.Maths.Graphs.Primitives) and [Zametek.Maths.Graphs.Compilers](https://www.nuget.org/packages/Zametek.Maths.Graphs.Compilers) |
| [`rust/`](rust/README.md) | Rust port | Cargo workspace with two crates mirroring the NuGet packages |

## Keeping the implementations in sync

The C# implementation is the reference; the Rust port tracks it at **behavioural parity** — the same inputs must produce the same outputs.

That contract is enforced by the test suites: the Rust golden tests carry expected values copied verbatim from the C# tests, and a runnable example reproduces a published Zametek.ProjectPlan schedule exactly. Both suites run in CI, so any change to C# behaviour must land together with the matching update to the Rust suite — a failing Rust build after a C# change is the drift alarm working as intended, and the fix is a deliberate mirror-update of the expected values, not a patch to make it green.

The Rust workspace version tracks the NuGet package version: matching versions mean verified parity.

## Documentation

- [C# library guide](dotnet/README.md) — concepts (CPM, slack, the two graph flavours, resource scheduling) and a full API walkthrough.
- [Rust port guide](rust/README.md) — crate layout, design notes and how the C# patterns map onto Rust.
- [Performance notes](dotnet/docs/PERFORMANCE.md) — the .NET performance and deep-graph-hardening plan, with the applied phases recorded.

## Licence

[BSD-2-Clause](LICENSE), covering both implementations.
