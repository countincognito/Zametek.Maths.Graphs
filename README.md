# Zametek.Maths.Graphs

[![NuGet Version](https://img.shields.io/nuget/v/Zametek.Maths.Graphs.Primitives.svg)](https://www.nuget.org/packages/Zametek.Maths.Graphs.Primitives "NuGet Version") **Zametek.Maths.Graphs.Primitives**

[![NuGet Version](https://img.shields.io/nuget/v/Zametek.Maths.Graphs.Compilers.svg)](https://www.nuget.org/packages/Zametek.Maths.Graphs.Compilers "NuGet Version") **Zametek.Maths.Graphs.Compilers**

A headless library for building and compiling vertex (Activity-on-Vertex) and arrow (Activity-on-Arrow) directed graphs for project scheduling. It provides dynamic dependency resolution, transitive reduction, edge redirection, critical path calculation, activity priority calculation, and mapped/unmapped resource scheduling.

- **Zametek.Maths.Graphs.Primitives** — the domain model: activities, events, edges, nodes, graphs, resources, resource schedules and work streams, plus their interfaces and enums.
- **Zametek.Maths.Graphs.Compilers** — the builders, compilers and engines that turn a set of dependent activities into a compiled schedule.

## Extensibility

Every engine the builders rely on sits behind a public interface and can be supplied through the builders' (and compilers') engine-injecting constructors:

- `IIdGenerator<T>` — ID generation (`NextIdGenerator`, `PreviousIdGenerator`).
- `IEventGenerator<T>` — event creation (`EventGenerator`, `RemovableEventGenerator`).
- `IActivityGenerator<…>` — dummy activity creation (`DummyActivityGenerator`).
- `IArrowCriticalPathEngine<…>` / `IVertexCriticalPathEngine<…>` — critical path calculation.
- `IArrowStronglyConnectedComponentsFinder<…>` / `IVertexStronglyConnectedComponentsFinder<…>` — cycle detection.
- `IResourceSchedulingEngine<…>` — resource scheduling and its surrounding pipeline.

Custom engines read graph state through the read-only `IArrowGraphState<…>` / `IVertexGraphState<…>` contracts; the concrete, mutable state stays internal to the library.

## Breaking changes

### 3.0.0

- `ICanBeRemoved` now declares `SetAsReadOnly()` and `SetAsRemovable()`. Previously these mutators lived only on `IActivity<…>` and the concrete `Event` / `Activity` types, leaving `IEvent<T>` asymmetric. Any external type implementing `IEvent<T>` — or `ICanBeRemoved` directly — must now provide both methods. This makes events symmetric with activities and allows `RemovableEventGenerator<T>` to decorate any `IEventGenerator<T>` (defaulting to `EventGenerator<T>`) instead of constructing events directly.
