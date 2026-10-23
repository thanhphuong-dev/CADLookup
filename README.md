# CADLookup

CADLookup is an in-process WPF inspector for the active CAD drawing. Shared contracts and inspection models do not reference a CAD SDK; all native database access is performed by a host adapter from a valid host command context.

## Current implementation

- **AutoCAD 2023** adapter targeting the installed AutoCAD 2023 managed SDK and .NET Framework 4.8.
- AutoCAD Ribbon tab **CADLookup** with **Inspect**, **Select**, and **Refresh** buttons, alongside the `CADLOOKUP` command.
- `CADLookup.Core` contains SDK-free contracts, domain snapshots, and the inspection application service; `CADLookup.Infrastructure` owns local settings and file logging.
- Modeless WPF `CADLOOKUP` window owned by the AutoCAD main window, with HandyControl theme resources, Vietnamese/English labels, saved language preference, object list, handle lookup, property search/source filter, geometry tab, object-reference details, copy value/path, JSON/CSV export, and navigation history.
- Fixed entity information includes object identity, layer/state, visual properties, line/circle/polyline/arc/ellipse/spline/hatch measurements, dimension values, text, block attributes, layer and block-table records, bounding extents, owner/block links, and XData.
- Public CLR properties are listed as deferred members. Double-click a member to evaluate it in the host command context; complex values and collections can be opened again. Public method signatures and event types are catalogued but never invoked or subscribed to.
- Inspection applies depth, property, and collection limits; cycles and failing getters are reported as property states instead of terminating the session.
- `CADLookup.Playground` uses a fake object graph to exercise nested values, lists, methods, events, and a cyclic reference without starting CAD.
- `CADLookup.Core.Tests` covers limits, deferred member paths, cycle detection, per-member failures, and method/event cataloguing. `CADLookup.UI.Tests` covers filtering, navigation history, and handle-lookup routing.

This is not yet full RevitLookup feature parity. CADLookup does not invoke methods, browse arbitrary CAD database tables, edit drawing objects, provide live database-wide search, or implement geometry visualizers. ZWCAD and enjiCAD adapters are not shipped: each requires its own SDK/runtime build and real-host verification.

## Build, test, install

Use Windows with the .NET SDK, .NET Framework 4.8 targeting pack, and AutoCAD 2023 installed. The AutoCAD SDK assemblies are referenced from the installation and are not copied into the bundle.

```powershell
dotnet test .\CADLookup.sln -c Release -p:AutoCADManagedDir="C:\Program Files\Autodesk\AutoCAD 2023"
.\build-bundle.ps1 -AutoCADManagedDir "C:\Program Files\Autodesk\AutoCAD 2023" -Install
```

The build script stages `.artifacts\CADLookup.bundle`; `-Install` copies it into the current user's AutoCAD `ApplicationPlugins` directory. Restart AutoCAD, select one or more drawing objects, then click **CADLookup > Inspect** or run `CADLOOKUP`. **Select** starts an AutoCAD selection prompt and **Refresh** reloads the active inspection. Double-click a row with a navigation reference to inspect it; double-click a deferred reflection property to evaluate it. Right-click a property for copy value, copy path, and open-related-object actions. Enter a hexadecimal handle in the top bar to find an object in the active drawing.

To build without installing, omit `-Install`. To load manually, copy the staged bundle to `ApplicationPlugins`, or `NETLOAD` `CADLookup.Host.AutoCAD.dll` from the Release output; keep `CADLookup.Core.dll`, `CADLookup.UI.dll`, and `HandyControl.dll` beside it.

## AutoCAD 2023 smoke test

`tests/CADLookup.Host.AutoCAD.Smoke` is an in-process integration harness and is built with the solution. In a disposable, unsaved AutoCAD drawing, copy `tests/CADLookup.Host.AutoCAD.Smoke/bin/Release/net48/CADLookup.Host.AutoCAD.Smoke.dll` into the installed bundle's `Contents/Windows` folder, run `CADLOOKUP`, then `NETLOAD` that copied harness and run `CADLOOKUPSMOKE`. The harness creates and verifies Line, Circle, Polyline, Text, Block with Attribute, Layer, and erased objects; it also verifies the Ribbon controls and owned WPF window. It writes a PASS/FAIL report to `%TEMP%\CADLookup-AutoCAD-smoke.txt`.

## Host support status

| Host | Status | Notes |
|---|---|---|
| AutoCAD 2023 | Verified MVP target | `R24.2`, .NET Framework 4.8; build and live in-host smoke test passed with a preselected Line. |
| AutoCAD 2024–2027 | Not packaged | Runtime/API changes require separate build targets and bundle manifests. |
| ZWCAD | Research only | Vendor publishes version-specific .NET/ZRX development kits; adapter API and runtime need a dedicated SDK spike. |
| enjiCAD | Research only | Product information advertises .NET/GRX development; exact managed API assemblies, runtime, loading model, and redistribution terms have not been verified. |

Do not ship another host adapter until its selection, transaction, document-context, modeless UI, object identity, and packaging behavior have been tested in that CAD product.

See [the detailed host verification matrix](docs/Host-support-matrix.md) for evidence and unresolved SDK/runtime questions.
