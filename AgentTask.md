# Backend Feature Components Refactoring Task

## Purpose

Evolve the backend toward feature-oriented components that are easy to extend,
test, and eventually make independently replaceable. This is a gradual
architectural goal, not a request for an immediate repository-wide rewrite.

Any developer or AI agent implementing backend functionality must consider this
goal and avoid introducing new coupling or patterns that make it harder to
achieve.

## Target

- Keep `Contracts.ILauncherService` as the sole business entry point for Views.
- Define each UI operation explicitly at the service boundary, including its
  operation name, request parameters, and result type or state/event behavior.
- Group one or more cohesive UI operations into a feature component. A developer
  should be able to implement the feature behavior primarily in one class,
  rather than creating a separate implementation class for every operation.
- Let the intermediate application layer route declared operations to their
  feature component. Routing and registration should be centralized or
  automatically discovered, not copied into multiple dispatch switches.
- Keep shared request/response DTOs in `App/Contracts`; they describe semantic
  data and must not depend on UI controls, Avalonia, `AppConfig`, or internal
  command protocols.
- Keep feature components independent of other feature implementations.
  Shared capabilities should be reused through `Core` or narrow, explicit
  infrastructure contracts rather than direct feature-to-feature calls.
- Give each component only the dependencies it needs. Avoid passing the broad
  `LauncherContext` to new components when a narrower dependency surface is
  practical.
- Preserve the existing distinction between one-shot typed results, ongoing
  state/events, and long-running queued operations. Do not model background
  work as a synchronous-looking one-shot response.

## Guidance for Ongoing Backend Work

Until the target architecture is implemented:

1. Prefer adding functionality to an existing cohesive feature component over
   adding a new one-method class solely to satisfy the current handler shape.
2. Keep `ILauncherService` and its shared DTOs as the public UI boundary. Do not
   expose concrete feature classes or internal command strings to Views.
3. Use the existing typed operation/handler mechanism where appropriate, but
   structure new code so related operations can later be routed to one feature
   component without changing their public semantics.
4. Do not add direct dependencies between feature implementations. Reuse Core
   or introduce a narrow shared contract when cross-feature collaboration is
   necessary.
5. Follow `BACKEND_DEVELOPMENT.md` for component dependency declarations,
   component registration, Core placement, and UI-boundary rules. This task
   supplements that document; it does not waive its current requirements.
6. Do not perform broad architectural migrations as incidental cleanup. Keep
   changes scoped to the requested feature and preserve existing behavior.

## Implementation Expectations

When the refactoring itself is authorized:

- Establish a feature-component contract that declares operations and their
  request/response types, while allowing one implementation class to handle
  multiple cohesive operations.
- Update the intermediate dispatcher to discover or register feature
  components and route each declared operation exactly once. Fail explicitly
  on missing, duplicate, or incompatible operation registrations.
- Keep assembly, desktop adaptation, and UI-thread dispatch in the integration
  layer; keep business orchestration in feature components and reusable
  domain behavior in `Core`.
- Migrate incrementally by feature area. Keep existing callers working during
  each step, and avoid changing Views unless the user explicitly authorizes
  related UI work.
- Add tests for operation-to-component routing, parameter/result contract
  consistency, representative real calls, failure behavior, and relevant
  state transitions. Retain focused Core/service tests for internal rules that
  are not adequately verified through the UI boundary.
- Update `BACKEND_DEVELOPMENT.md`, the component dependency registry, and
  directly related documentation when the architecture or its rules change.

## Non-Goals

- This task does not itself implement runtime loading of third-party plugins.
- It does not require one class per UI method, nor one class for the entire
  backend.
- It does not justify merging unrelated responsibilities or removing useful
  Core, service-adapter, or integration boundaries.
- It does not authorize UI changes, a full rewrite, or behavior changes.
