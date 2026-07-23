# AGENTS.md

## Code architecture

- `ReleaseManagement` is the single bounded context and compiles from one project: `src/ReleaseManagement/ReleaseManagement.csproj`.
- Do not create separate Domain, Application, or Infrastructure projects for this bounded context.
- Put business modules directly under `src/ReleaseManagement`; do not introduce a `Modules`, `Contexts`, or other parent directory between the bounded context and its modules.
- Organize module-first, then by architectural role inside the module. For example, Promotion code belongs under `Promotion/Domain`, `Promotion/Application`, or `Promotion/Infrastructure`; User code belongs under `User/Domain` or `User/Application`.
- Create an architectural-role directory only when that module has code for it. Do not leave placeholder or empty directories.
- Keep one top-level class, record, enum, interface, or struct per file. The filename must match the type.
- Put API code under the owning topic in `apps/ReleasePilot/Api`. Each public route is a separate action method on an attribute-routed controller. Group related routes in one topic controller.
- `Program.cs` is only for service registration and middleware composition. Do not define application routes with `MapGet`, `MapPost`, or other minimal-API route calls.
- Organize tests by the same business topic as the production code.

Canonical layout:

```text
src/ReleaseManagement/
├── ReleaseManagement.csproj
├── Application/
│   └── Domain/
├── ApplicationVersion/
│   └── Domain/
├── Environment/
│   └── Domain/
├── Promotion/
│   ├── Application/
│   ├── Domain/
│   └── Infrastructure/
└── User/
    ├── Application/
    └── Domain/

apps/ReleasePilot/Api/
├── Promotion/
├── Health/
├── Home/
└── Shared/
```

## Task Completion Requirements
- Keep local verification focused on the files and packages changed. Run the smallest relevant test set; do not run the full workspace test suite as a routine completion step.
- Backend changes must include and run focused tests for the changed behavior.
