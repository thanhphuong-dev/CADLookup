# CADLookup for ZWCAD 2026

Build from the repository root on Windows:

```powershell
.\build-zwcad.ps1 -ZwcadManagedDir "C:\Users\xemch\Downloads\Telegram Desktop\ThưvienZwCAD"
```

The staged output is `.artifacts\CADLookup.ZWCAD`. In ZWCAD, run `NETLOAD`, load `CADLookup.Host.ZWCAD.dll`, then run `CADLOOKUP` or use the CADLookup Ribbon tab. The **Select** Ribbon button starts interactive selection and opens the WPF inspector after selection completes. Other commands include `CADLOOKUPREFRESH`, `CADLOOKUPSNOOPDB`, and `CADLOOKUPLANG`.

The project targets .NET Framework 4.8. It uses `ZwManaged.dll` from the directory passed to the build script. If that folder does not contain `ZwDatabaseMgd.dll`, `ZcCui.dll`, `ZcWindows.dll`, and `ZdWindows.dll`, the build restores the ZWCAD 2026 `ZWCAD.NetApi` reference package. These host API assemblies are compile references only and are not copied to the staged output; the matching assemblies must be supplied by the installed ZWCAD host. The package is not an installer and does not install ZWCAD.

The local `ZwManaged.dll` reports file version `26.5.0.18164` and assembly version `26.0.26.0`. The companion API references used for the successful build have matching assembly versions. The adapter has compile verification only: a ZWCAD installation is still needed to verify `NETLOAD`, Ribbon registration, interactive selection, WPF window ownership, and shutdown/reload behavior.
