# CADLookup for VinaCAD 2026

This build targets the VinaCAD 2026 .NET 8 managed API installed on the build machine. It contains a separate VinaCAD adapter and reuses the shared CADLookup WPF/MVVM UI and HandyControl theme.

## Load and use

1. Build with `build-vinacad.ps1`, or use the prebuilt files in this folder.
2. In VinaCAD 2026, load `CADLookup.Host.VinaCAD.dll` using the plugin loading command/workflow provided by VinaCAD.
3. Run `CADLOOKUP` to open the inspector. The Ribbon tab is named `CADLookup`; its **Select objects** button runs `CADLOOKUPSELECT` and opens the WPF inspector after a successful selection.
4. `CADLOOKUPSNOOPDB` lists database objects. The inspector also supports refresh, handle lookup, language selection, search, navigation, and JSON export.

The plugin uses the VinaCAD 2026 managed API from the local installation. Do not copy VinaCAD SDK assemblies into the plugin output or distribute them with CADLookup.

## Verification status

The VinaCAD 2026 target compiles against the installed SDK. It has not yet been loaded in a live VinaCAD session, so Ribbon rendering, modeless window ownership, selection behavior, and shutdown/reload still need an in-host smoke test.
