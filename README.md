# Module Creator

Second agent for the Multi-Agent Builder. Open `LD-agent-gap.sln` in Visual Studio 2022 and press F5.

For each module name it checks in this order:

1. The local module library in `modules.json`.
2. Your GitHub repositories, only when `GITHUB_TOKEN` is filled in.
3. If both miss, it writes the module.

Every new module is saved twice: once in the project folder and once in the library folder. Unknown keys are written as `process.env.DATABASE_URL || "PLACEHOLDER"` so the file still runs.

```text
dotnet run --project src/AgentGap -- "Scope Notes" "Invoice export"
```
