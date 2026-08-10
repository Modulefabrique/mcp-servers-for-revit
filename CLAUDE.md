# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

This repo (a fork of `mcp-servers-for-revit/mcp-servers-for-revit`) has three independently-built components that together let an MCP client (Claude, etc.) drive Autodesk Revit:

```
MCP Client <--stdio--> server/ (TypeScript)  <--TCP/JSON-RPC--> plugin/ (C#, runs inside Revit) --loads--> commandset/ (C#, actual Revit API calls)
```

- `server/` — MCP server (Node/TypeScript). Exposes one MCP tool per Revit operation.
- `plugin/` — the Revit add-in (`revit_mcp_plugin`). Runs inside Revit, hosts a raw TCP socket server, dispatches incoming JSON-RPC calls to registered commands, and provides the WPF Settings UI for enabling/disabling commands.
- `commandset/` — the actual `IRevitCommand` implementations (`RevitMCPCommandSet`) that call the Revit API. Built and loaded as a separate assembly, dynamically discovered by the plugin at runtime (not statically referenced).
- `tests/commandset/` — integration tests (TUnit) that run against a live Revit process.

`origin` points at the user's own fork (`Modulefabrique/mcp-servers-for-revit`), not the upstream repo — a normal `git push` only reaches the fork.

## Commands

### MCP server (`server/`)
```bash
cd server
npm install
npm run build        # tsc -> server/build/
npx tsx server/src/index.ts   # run directly during development, no build needed
```

### Plugin + command set (C#)
Open `mcp-servers-for-revit.sln` (contains plugin, commandset, and tests projects). Build via a specific configuration, e.g. `Release R26` / `Debug R25` — there is no plain `Debug`/`Release` config, the Revit year is part of the configuration name and selects both the target framework and `RevitVersion` MSBuild property (see "Multi-version targeting" below). Building `commandset` automatically copies its DLLs + `command.json` into the plugin's `Commands/RevitMCPCommandSet/<year>/` output (and into the local Revit Addins folder for Debug configs) via MSBuild targets in the `.csproj` files — no manual copy step needed.

### Tests (`tests/commandset/`)
Requires a running, fully-loaded Revit 2025 or 2026 instance (tests inject into it — no separate addin install needed) and the .NET 10 SDK.
```bash
dotnet test -c Debug.R26 -r win-x64 tests/commandset   # Revit 2026
dotnet test -c Debug.R25 -r win-x64 tests/commandset   # Revit 2025
```
`-r win-x64` is required on ARM64 machines (Revit API assemblies are x64-only). Test classes extend `RevitApiTest`; setup/teardown use `[Before(HookType.Class)]` / `[After(HookType.Class)]` with `[HookExecutor<RevitThreadExecutor>]` to run on Revit's API thread.

### Releasing
A single `v*` tag drives everything via `.github/workflows/release.yml` (builds plugin+commandset for Revit 2020-2026, publishes GitHub release zips, publishes `server/` to npm as `mcp-server-for-revit` via OIDC trusted publishing). To cut a release: `./scripts/release.ps1 -Version X.Y.Z` (bumps `server/package.json`/`package-lock.json` and `plugin/Properties/AssemblyInfo.cs`, commits, tags), then `git push origin main --tags`.

## Architecture

### Transport is raw TCP, not WebSocket
Despite the README diagram saying "WebSocket", the actual transport is a plain TCP socket: `plugin/Core/SocketService.cs` runs a `TcpListener` on port 8080, and `server/src/utils/SocketClient.ts` (`RevitClientConnection`) connects with Node's `net.Socket`. Messages are JSON-RPC 2.0 objects serialized with no delimiter; both sides attempt `JSON.parse`/`JsonConvert.DeserializeObject` on the accumulated buffer and treat a parse failure as "wait for more data" (see `processBuffer` in `SocketClient.ts`).

### MCP server tool auto-discovery
`server/src/tools/register.ts` scans its own directory at startup and dynamically imports every `.ts`/`.js` file except `index`/`register` themselves, calling whichever exported function name starts with `register` (e.g. `registerGetCurrentViewInfoTool`). **Adding a new tool means only dropping a new file in `server/src/tools/` that exports a `registerXTool(server)` function** — no manual wiring in `register.ts` or `index.ts`. Each tool handler goes through `withRevitConnection` (`server/src/utils/ConnectionManager.ts`), which serializes all Revit connections behind a single mutex (`connectionMutex`) so concurrent tool calls from the MCP client don't race on the one TCP connection, then calls `revitClient.sendCommand(method, params)`.

### Plugin-side command dispatch
`SocketService` hands each incoming JSON-RPC request to `CommandExecutor.ExecuteCommand`, which looks the method name up in an `ICommandRegistry` and calls `command.Execute(params, requestId)`. The registry is populated at plugin startup by `CommandManager.LoadCommands()`, which:
1. Reads enabled commands + their assembly path from a config produced by `ConfigurationManager` (backed by the WPF Settings window in `plugin/UI/`, which reads `command.json` — the manifest of *available* commands shipped with each `commandset` build — and writes a separate registry file recording which ones the user *enabled*).
2. Loads each configured assembly with `Assembly.LoadFrom` and reflects over its types for anything implementing `RevitMCPSDK.API.Interfaces.IRevitCommand`, instantiating it (optionally via `IRevitCommandInitializable.Initialize(uiApplication)` or a `UIApplication`-accepting constructor).

This is why `commandset` is never project-referenced by `plugin` — it's a plugin-in, loaded purely by convention (command name match) at runtime.

### ExternalEvent pattern (Revit API thread-affinity)
Revit API calls must happen on Revit's UI thread, but the socket listener runs on its own thread. Every command in `commandset/Commands/` extends `RevitMCPSDK`'s `ExternalEventCommandBase`, pairing itself with a handler in `commandset/Services/` that implements `IExternalEventHandler` + `IWaitableExternalEventHandler`. The flow: `Command.Execute()` calls `RaiseAndWaitForCompletion(timeoutMs)`, which raises the paired `ExternalEvent` (created/cached per-key by the singleton `plugin/Core/ExternalEventManager.cs`) and blocks on a `ManualResetEvent`; Revit invokes the handler's `Execute(UIApplication)` on its own thread once free, which does the actual Revit API work, stores the result on the handler instance, and signals the event. The command then reads the result back off the handler. When adding a new Revit operation, this Command+EventHandler pair (in `Commands/` and `Services/` respectively) is the pattern to follow — see `GetCurrentViewInfoCommand.cs` / `GetCurrentViewInfoEventHandler.cs` for the minimal example.

### Multi-version Revit targeting
Both `plugin` and `commandset` `.csproj`s define per-Revit-year configurations (`Debug R20`...`Debug R26`, `Release R20`...`Release R26`), each setting `RevitVersion` and `TargetFramework`: **net48** for Revit 2020-2024, **net8.0-windows10.0.19041.0** for 2025-2026. Conditional compilation symbols `REVIT2022_OR_GREATER`, `REVIT2023_OR_GREATER`, `REVIT2024_OR_GREATER` gate API differences (e.g. `ElementId.Value` vs `.IntegerValue`). `RevitMCPSDK` (the base classes referenced everywhere — `IRevitCommand`, `ExternalEventCommandBase`, `ICommandRegistry`, `IWaitableExternalEventHandler`, `RevitVersionAdapter`, JSON-RPC models) is an external NuGet package version-pinned to `$(RevitVersion).*` — its source is not part of this repo.

## Conventions

- This fork's code comments, XML doc comments, and user-facing error/log strings are maintained in **Dutch** (the upstream project's leftover Chinese comments were translated to Dutch, not English) — match this when writing new comments, and prefer existing Dutch technical terms already used in the codebase (e.g. `element`, `aanzicht`/`weergave`, `wand`, `peil`, `familietype`, `parameter`) over inventing new ones.
