# BIM AI — Revit & Rhino MCP Automation

An AI-driven BIM automation workspace that lets a large language model (Claude, via
[Claude Code](https://claude.com/claude-code)) drive **Autodesk Revit** and **Rhino/Grasshopper**
through natural language. Instead of clicking through ribbons and dialogs, you describe what you
want ("create A-101 to A-105 and drop the level plans onto them") and the model calls the right
tools to make it happen inside the live application.

The project wraps two CAD/BIM applications as [Model Context Protocol (MCP)](https://modelcontextprotocol.io)
servers so any MCP-capable client can query and modify the model.

---

## Table of Contents

- [Goals](#goals)
- [Architecture](#architecture)
- [Repository Layout](#repository-layout)
- [Request Lifecycle](#request-lifecycle)
- [Current Capabilities](#current-capabilities)
- [Getting Started](#getting-started)
- [Building & Deploying](#building--deploying)
- [Adding a New MCP Tool (Walkthrough)](#adding-a-new-mcp-tool-walkthrough)
- [Roadmap / Possible Extensions](#roadmap--possible-extensions)
- [Troubleshooting](#troubleshooting)

---

## Goals

- **Natural-language BIM.** Replace repetitive, dialog-heavy Revit/Rhino workflows with intent
  expressed in plain language, and let the AI translate that into precise API calls.
- **Fill the gaps that manual add-ins leave.** The Revit tool set is deliberately modeled after
  common productivity plugins (e.g. BIMSpeed): batch parameter editing, sheet/view automation,
  model QA — but driven by an LLM rather than a fixed UI.
- **Two engines, one interface.** Revit (documentation/BIM) and Rhino + Grasshopper
  (free-form/parametric geometry) are both exposed through MCP so the model can move between them.

---

## Architecture

The system is a **three-tier bridge**. The AI never talks to the Revit API directly; it speaks MCP
to a Node process, which speaks JSON-RPC over TCP to a C# add-in running *inside* Revit.

```
┌─────────────────────┐   MCP / stdio    ┌──────────────────────────┐   JSON-RPC / TCP 8080   ┌────────────────────────────┐
│  Claude Code (LLM)  │ ───────────────► │  MCP Server (Node / TS)  │ ──────────────────────► │  Revit Add-in (C# .NET 4.8)│
│  MCP client         │ ◄─────────────── │  revit-mcp/server        │ ◄────────────────────── │  runs inside Revit 2024    │
└─────────────────────┘   tool results   └──────────────────────────┘   JSON result / error   └────────────────────────────┘
                                                                                                        │
                                                                                          ExternalEvent │ (Revit API is single-threaded)
                                                                                                        ▼
                                                                                          ┌────────────────────────────┐
                                                                                          │  CommandSet DLL            │
                                                                                          │  Commands + EventHandlers  │
                                                                                          │  → Autodesk.Revit.DB       │
                                                                                          └────────────────────────────┘
```

A parallel branch exposes **Rhino** through the third-party [`rhinomcp`](https://github.com/jingcheng-chen/rhinomcp)
server (launched via `uvx rhinomcp`), giving the same client access to Rhino geometry and the
Grasshopper canvas (`gh_*` tools).

### Tier 1 — MCP Server (`revit-mcp/server`, TypeScript / Node)

- Built on `@modelcontextprotocol/sdk`, communicates with the client over **stdio**.
- Each capability is a small module in `src/tools/*.ts` that calls `server.tool(name, description, zodSchema, handler)`.
- [`register.ts`](revit-mcp/server/src/tools/register.ts) **auto-discovers** every file in `src/tools/`
  and invokes its `register*` export — so adding a tool never requires editing a central registry.
- [`ConnectionManager.ts`](revit-mcp/server/src/utils/ConnectionManager.ts) opens a fresh TCP socket
  to `localhost:8080` per request and **serializes** all requests through a mutex (the Revit API is
  single-threaded and cannot handle concurrent commands).
- A small SQLite database (`revit-data.db`) backs the `store_*`/`query_*` tools for persisting
  extracted model data between calls.

### Tier 2 — Revit Plugin (`revit-mcp/plugin`, C# / WPF, .NET Framework 4.8)

- An `IExternalApplication` ([`Application.cs`](revit-mcp/plugin/Core/Application.cs)) that adds a
  **"Revit MCP Switch"** toggle button and a **Settings** button to the ribbon.
- [`SocketService.cs`](revit-mcp/plugin/Core/SocketService.cs) hosts the TCP listener on port **8080**,
  parses **JSON-RPC** requests, looks the method up in the command registry, and dispatches it.
- Because Revit API calls must run on Revit's main thread, commands are executed via the
  **ExternalEvent** pattern (`ExternalEventManager`), and the socket thread blocks on a
  `ManualResetEvent` until the handler completes.
- At startup it reads `commandRegistry.json` to know which commands to load from the CommandSet DLL.

### Tier 3 — Command Set (`revit-mcp/commandset`, C# class library)

The actual Revit API work. Each tool is three C# pieces:

| Piece | Location | Responsibility |
|-------|----------|----------------|
| **Model** | `commandset/Models/**` | JSON request/response DTOs (`[JsonProperty]`) |
| **Command** | `commandset/Commands/**` | Validates params, raises the external event, waits for completion |
| **EventHandler** | `commandset/Services/**` | Runs on Revit's thread inside a `Transaction`, does the API work |

The CommandSet is multi-targeted (`Release R20`…`R26`) so the same source compiles against multiple
Revit versions via `#if REVIT2024_OR_GREATER` guards. This workspace deploys the **Release R24**
(Revit 2024, .NET 4.8) build.

---

## Repository Layout

```
BIM_AI_REBIT_RIR/
├── .mcp.json                     ← MCP server config read by Claude Code (revit + rhino)
├── .claude/
│   ├── settings.json             ← { "enableAllProjectMcpServers": true }
│   └── settings.local.json       ← permissions
├── revit-mcp/                    ← Revit MCP monorepo
│   ├── command.json              ← Source-of-truth command manifest
│   ├── mcp-servers-for-revit.sln
│   ├── plugin/                   ← Tier 2: Revit add-in (ribbon, socket, dispatch)
│   ├── commandset/               ← Tier 3: per-command Model/Command/EventHandler
│   │   ├── Models/               ← request/response DTOs by domain
│   │   ├── Commands/             ← command entry points by domain
│   │   └── Services/             ← ExternalEvent handlers (the real API work)
│   └── server/                   ← Tier 1: TypeScript MCP server
│       ├── src/tools/*.ts        ← one file per tool (auto-registered)
│       ├── src/utils/            ← ConnectionManager, SocketClient
│       └── build/index.js        ← compiled entry point (referenced by .mcp.json)
└── rhino-mcp/                    ← Rhino MCP (jingcheng-chen/rhinomcp)
```

---

## Request Lifecycle

Taking `set_element_parameters` as an example:

1. **LLM** decides to call the tool and emits an MCP `tools/call` over stdio.
2. **MCP server** (`set_element_parameters.ts`) validates args with its Zod schema and calls
   `withRevitConnection(...)`, which acquires the mutex and opens a TCP socket to `:8080`.
3. It sends a **JSON-RPC** request: `{ "method": "set_element_parameters", "params": {...}, "id": ... }`.
4. **Plugin** `SocketService.ProcessJsonRPCRequest` finds the registered `SetElementParametersCommand`
   and calls `Execute`.
5. The **Command** stashes the request on its handler and calls `RaiseAndWaitForCompletion(30000)`,
   raising a Revit **ExternalEvent** and blocking the socket thread.
6. The **EventHandler** runs on Revit's main thread, opens a `Transaction`, does the work, fills a
   result object, and signals the `ManualResetEvent`.
7. The result bubbles back up: EventHandler → Command → JSON-RPC response → MCP tool result → LLM.

---

## Current Capabilities

> ~28 Revit tools plus the full Rhino/Grasshopper tool set. Grouped by intent:

### Read / query (Revit)
- `list_levels`, `list_views`, `list_sheets` — enumerate model structure
- `get_element_properties` — all parameters of one element
- `get_selected_elements`, `get_current_view_elements`, `get_current_view_info`
- `get_available_family_types` — discover loadable types (families, title blocks, …)
- `analyze_model_statistics` — element counts by category/type/family/level
- `get_material_quantities` — quantity/material takeoffs
- `export_room_data` — rooms with area/volume/perimeter + parameters
- `ai_element_filter` — flexible element querying by criteria

### Create / modify (Revit)
- `set_element_parameters` — **batch-write** parameters across many elements *(done, verified)*
- `create_sheets` — **batch-create sheets**, auto-place views/schedules in a grid *(new — this workspace)*
- `create_level`, `create_grid`, `create_room`
- `create_point_based_element`, `create_line_based_element`, `create_surface_based_element`
- `create_structural_framing_system`, `create_dimensions`
- `color_elements`, `tag_all_walls`, `tag_all_rooms`
- `operate_element` (select/hide/isolate/…), `delete_element`

### Escape hatch & persistence
- `send_code_to_revit` — execute arbitrary C# against the live `Document` (rapid prototyping)
- `store_project_data` / `store_room_data` / `query_stored_data` — SQLite-backed persistence

### Rhino / Grasshopper (via `rhinomcp`)
- Geometry: `create_object(s)`, `extrude_curve`, `loft`, `sweep1`, `pipe`, boolean ops, `offset_curve`, …
- Document/layers: `get_document_summary`, `create_layer`, `select_objects`, `capture_viewport`
- Code execution: `execute_rhinoscript_python_code`, `execute_rhinocommon_csharp_code`
- **Grasshopper**: `gh_add_component`, `gh_connect_components`, `gh_build_graph`, `gh_run_solution`, … —
  build and drive parametric definitions programmatically

---

## Getting Started

### Prerequisites
- Windows 11, **Autodesk Revit 2024** (targets .NET Framework 4.8)
- **Node.js** ≥ 20 (developed on v24) for the MCP server
- **.NET SDK** with the R24 workloads for building the C# projects
- **Rhino 8** + the `rhinomcp` Rhino plugin (optional, for the Rhino branch)
- An MCP client — Claude Code

### One-time setup
1. Build & deploy the Revit add-in (see below), or install a pre-built copy into
   `%AppData%\Autodesk\Revit\Addins\2024\`.
2. Build the MCP server: `cd revit-mcp/server && npm install && npm run build`.
3. Confirm [`.mcp.json`](.mcp.json) points at `revit-mcp/server/build/index.js`.
4. In `.claude/settings.json`, keep `{ "enableAllProjectMcpServers": true }`.

### Each session
1. Open Revit **and a model**.
2. Click **Add-Ins → Revit MCP Plugin → "Revit MCP Switch"** to start the TCP listener on `:8080`.
3. Start your MCP client in this folder. Ask it to, e.g., *"list the sheets in the model"* to confirm
   the bridge is live.

---

## Building & Deploying

The Revit add-in DLL is **locked while Revit is open** — close Revit before copying.

```bash
# 1. Build the CommandSet (Tier 3) for Revit 2024
cd revit-mcp
dotnet build commandset/RevitMCPCommandSet.csproj -c "Release R24"
#    → commandset/bin/Release R24/RevitMCPCommandSet.dll

# 2. Build the MCP server (Tier 1)
cd server
npm run build
#    → server/build/**  (register.ts auto-picks up new tools)
```

```powershell
# 3. Close Revit, then deploy the DLL
Copy-Item "revit-mcp/commandset/bin/Release R24/RevitMCPCommandSet.dll" `
  "$env:AppData/Autodesk/Revit/Addins/2024/revit_mcp_plugin/Commands/RevitMCPCommandSet/2024/RevitMCPCommandSet.dll" -Force
```

4. Register the command in **two** places:
   - `revit-mcp/command.json` (source manifest), and
   - `%AppData%/Autodesk/Revit/Addins/2024/revit_mcp_plugin/Commands/commandRegistry.json` (the file
     the plugin actually reads at startup — it does **not** auto-scan).
5. Reopen Revit → open a model → click **Revit MCP Switch**, then **restart your MCP client** so the
   Node server reloads and exposes the new tool.

---

## Adding a New MCP Tool (Walkthrough)

Every tool has **four source files + two manifest entries**. Below is the exact recipe, using the
recently added **`create_sheets`** tool as the worked example.

### 1. Model — request/response DTOs
`commandset/Models/Views/CreateSheetsModel.cs`

```csharp
public class SheetSpecInput
{
    [JsonProperty("number")] public string Number { get; set; }
    [JsonProperty("name")]   public string Name { get; set; }
    [JsonProperty("viewIds")] public List<int> ViewIds { get; set; } = new();
}

public class CreateSheetsRequest
{
    [JsonProperty("titleBlockTypeId")] public int? TitleBlockTypeId { get; set; }
    [JsonProperty("sheets")]           public List<SheetSpecInput> Sheets { get; set; } = new();
}
// + result DTOs (CreateSheetsResult, SheetCreateResult, …) with [JsonProperty] names
```

### 2. EventHandler — the actual Revit API work
`commandset/Services/CreateSheetsEventHandler.cs`

```csharp
public class CreateSheetsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
{
    public CreateSheetsRequest Request { get; set; }
    public CreateSheetsResult ResultInfo { get; private set; }
    private readonly ManualResetEvent _resetEvent = new(false);

    public bool WaitForCompletion(int ms = 10000) { _resetEvent.Reset(); return _resetEvent.WaitOne(ms); }

    public void Execute(UIApplication app)
    {
        try
        {
            var doc = app.ActiveUIDocument.Document;
            using var t = new Transaction(doc, "Create Sheets");
            t.Start();
            // … ViewSheet.Create, Viewport.Create, per-item try/catch so one
            //   failure doesn't abort the batch, collect results …
            t.Commit();
            ResultInfo = new CreateSheetsResult { Success = true, /* … */ };
        }
        catch (Exception ex) { ResultInfo = new() { Success = false, Message = ex.Message }; }
        finally { _resetEvent.Set(); }
    }

    public string GetName() => "Create Sheets";
}
```

Key conventions:
- Do everything inside **one `Transaction`**; wrap each item so a single bad input reports an error
  instead of rolling back the whole batch.
- Version-guard ElementId access: `#if REVIT2024_OR_GREATER (int)id.Value #else id.IntegerValue #endif`.
- The document variable is `doc = app.ActiveUIDocument.Document`.

### 3. Command — validation + event plumbing
`commandset/Commands/Views/CreateSheetsCommand.cs`

```csharp
public class CreateSheetsCommand : ExternalEventCommandBase
{
    private CreateSheetsEventHandler _handler => (CreateSheetsEventHandler)Handler;
    public override string CommandName => "create_sheets";

    public CreateSheetsCommand(UIApplication uiApp)
        : base(new CreateSheetsEventHandler(), uiApp) { }

    public override object Execute(JObject parameters, string requestId)
    {
        var request = parameters?.ToObject<CreateSheetsRequest>();
        if (request == null || request.Sheets.Count == 0)
            throw new ArgumentException("sheets is required and must not be empty");

        _handler.Request = request;
        if (RaiseAndWaitForCompletion(60000)) return _handler.ResultInfo;
        throw new TimeoutException("create_sheets timed out");
    }
}
```

### 4. TypeScript tool — the MCP surface
`server/src/tools/create_sheets.ts`

```typescript
export function registerCreateSheetsTool(server: McpServer) {
  server.tool(
    "create_sheets",
    "Batch-create drawing sheets, optionally placing views on each …",  // rich description guides the LLM
    {
      titleBlockTypeId: z.number().optional().describe("Title block type ElementId; omit for the first one"),
      sheets: z.array(z.object({
        number: z.string().describe("Unique sheet number, e.g. 'A-101'"),
        name:   z.string().describe("Sheet title"),
        viewIds: z.array(z.number()).optional().describe("Views to auto-place in a grid"),
      })).describe("Sheets to create, in order"),
    },
    async (args) => {
      const response = await withRevitConnection((client) =>
        client.sendCommand("create_sheets", {
          titleBlockTypeId: args.titleBlockTypeId,
          sheets: args.sheets.map((s) => ({ number: s.number, name: s.name, viewIds: s.viewIds ?? [] })),
        }));
      return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
    });
}
```

> The tool `description` and each `.describe()` are effectively the **prompt** the LLM reads to decide
> when and how to call the tool — invest in them. Note the file only needs a `register*` export; you
> never touch `register.ts`.

### 5. Register in both manifests

`revit-mcp/command.json`:
```json
{ "commandName": "create_sheets", "description": "Batch-create sheets …", "assemblyPath": "RevitMCPCommandSet.dll" }
```

`…/Addins/2024/revit_mcp_plugin/Commands/commandRegistry.json`:
```json
{ "commandName": "create_sheets", "assemblyPath": "RevitMCPCommandSet\\2024\\RevitMCPCommandSet.dll", "enabled": true, "description": "…", "supportedRevitVersions": [] }
```

### 6. Build, deploy, reload
Run the two builds, close Revit, copy the DLL, reopen Revit + model + **MCP Switch**, restart the MCP
client. Then verify by asking the model to exercise the new tool.

> **Prototype faster:** for a one-off or to validate an API path before committing to the four-file
> pattern, use `send_code_to_revit` to run C# directly against the live `Document`. Promote it to a
> proper tool once the shape stabilizes.

---

## Roadmap / Possible Extensions

**Done**
- ✅ Read-only model introspection (levels/views/sheets/properties/statistics/quantities)
- ✅ `set_element_parameters` — batch parameter editing *(verified on a real model)*
- ✅ `create_sheets` — sheet & view/schedule automation *(built & deployed; pending on-model test)*

**Near-term (Revit)**
- **Model QA / Warning Checker** — enumerate & classify Revit warnings (unenclosed rooms, duplicate
  marks, overlaps), with AI-suggested or automated fixes.
- **Schedule/quantity export to Excel** — round-trip takeoffs to spreadsheets.
- **View creation** — batch-generate plans/sections/callouts (feeding `create_sheets`).
- **Sheet numbering & revision management** — bulk renumber, apply revision clouds/tags.

**Cross-application**
- **Rhino ↔ Revit interop** — push Grasshopper-generated geometry (e.g. the Tokyo Tower lattice study)
  into Revit as mass/adaptive components.
- **Grasshopper-authored parametric families** driven from natural language.

**Platform**
- Config-driven port (currently hard-wired to 8080), richer logging/telemetry surfaced to the client,
  and packaging so the add-in installs without manual `commandRegistry.json` edits.

---

## Troubleshooting

| Symptom | Cause / Fix |
|---------|-------------|
| New tool doesn't appear in the client | MCP server not reloaded — **restart the client** after building the server. |
| `<command> timed out` right after reopening Revit | The ExternalEvent/Switch isn't ready yet. Re-run a known-good tool (`list_sheets`); if that also times out it's a session issue, not your code — click **MCP Switch** again / wait a moment. |
| DLL copy fails: *file in use* | **Revit is open** and has the DLL locked. Close Revit, then copy. |
| Command loads but isn't found | Missing/incorrect entry in the deployed `commandRegistry.json` (not `command.json`). `assemblyPath` must be `RevitMCPCommandSet\\2024\\RevitMCPCommandSet.dll`. |
| `connect to revit client failed` | The plugin's TCP listener isn't running — open a model and click **Revit MCP Switch**. |
| Build succeeds but DLL is stale | The correct output is `commandset/bin/Release R24/RevitMCPCommandSet.dll` (not `bin/Release R24/net48/`). |

---

## Credits

- Revit MCP bridge based on the **mcp-servers-for-revit** architecture (CommandSet portions
  © Duong Tran Quang / DTDucas).
- Rhino/Grasshopper integration via [`rhinomcp`](https://github.com/jingcheng-chen/rhinomcp).
- Built to be driven by [Claude Code](https://claude.com/claude-code) and the Model Context Protocol.
