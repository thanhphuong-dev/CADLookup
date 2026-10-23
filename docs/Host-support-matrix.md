# CAD host verification matrix

This matrix records facts verified for the current build and what still needs a real host SDK. A public product feature list is not treated as evidence that a CADLookup adapter is loadable or distributable.

| Host/version | Runtime/API | Command, selection, database access | Modeless UI/deployment | Status |
|---|---|---|---|---|
| AutoCAD 2023 (R24.2) | Build uses local `AcMgd.dll`, `AcDbMgd.dll`, `AcCoreMgd.dll`, and `AdWindows.dll`; .NET Framework 4.8. The SDK is present at `C:\Program Files\Autodesk\AutoCAD 2023`. | `CADLOOKUP` autoloaded from the bundle. An in-process smoke harness created and verified Line, Circle, Polyline, Text, Block Attribute, Layer, and erased objects; it also checked 257 listed properties for the selected Line, geometry summary, a visible owned WPF window, and CADLookup Ribbon controls. Transactions use `StartOpenCloseTransaction`; modeless UI actions enqueue host commands. | `.bundle` manifest targets R24.2 and deploys CADLookup and HandyControl assemblies. AutoCAD SDK assemblies, including `AdWindows.dll`, stay host-provided. | Build, unit/UI tests, and live smoke verified on 2026-10-23. |
| AutoCAD 2024 | Autodesk identifies .NET Framework 4.8 and compatible managed SDK releases. | Not built against an installed 2024 SDK here. | Separate manifest/build validation required. | Not packaged. |
| AutoCAD 2025–2026 | Autodesk identifies .NET 8 and version-specific managed SDKs. | Not built against those SDKs. The current net48 binary is not the target for these releases. | Requires separate runtime target and bundle output. | Not packaged. |
| AutoCAD 2027 | Autodesk identifies .NET 10 and a version-specific SDK. | Not built against that SDK. | Requires separate runtime target and bundle output. | Not packaged. |
| ZWCAD 2024–2026 | Official developer portal lists version-specific .NET/ZRX development guides and reference manuals. Exact CLR, assembly filenames, load compatibility, and license/redistribution terms remain unverified. | No installed SDK or ZWCAD session was found on this machine. | Bundle and modeless UI behavior remain untested. | Research only; no adapter shipped. |
| enjiCAD | Official product information advertises .NET and GRX development. Exact managed API package/assemblies, runtime per release, command/transaction behavior, bundle installation, and SDK redistribution terms remain unverified. | No installed SDK or enjiCAD session was found on this machine. | Bundle and modeless UI behavior remain untested. | Research only; no adapter shipped. |

## Evidence

- AutoCAD 2023 registry installation: version 24.2.53.0; managed assemblies are resolved from the installed 2023 folder during build. `PackageContents.xml` declares R24.2.
- Live AutoCAD 2023 smoke report (`%TEMP%\CADLookup-AutoCAD-smoke.txt`): `PASS`, `RibbonTab=CADLookup`, `RibbonButtons=Inspect,Select,Refresh`, visible owned WPF window, one selected object, Line properties including `Length`, `Start point`, and `End point`, one geometry summary, and contract checks for Circle, Polyline, Text, Block Attribute, Layer, and an erased object. The report is produced by `tests/CADLookup.Host.AutoCAD.Smoke`.
- Autodesk's [managed .NET compatibility table](https://help.autodesk.com/cloudhelp/2027/ENU/AutoCAD-Customization/files/GUID-A6C680F2-DE2E-418A-A182-E4884073338A.htm) maps AutoCAD 2024 to .NET 4.8, 2025–2026 to .NET 8, and 2027 to .NET 10.
- ZWSOFT's [ZWCAD developer portal](https://www.zwsoft.com/support/zwcad-devdoc) lists .NET/ZRX development resources by version. It does not, on the landing page, prove the details needed to compile and package CADLookup.
- enjiCAD's [product site](https://enjicad.vn/) advertises .NET/GRX support. The specific SDK/runtime and redistribution details still need vendor documentation or an installed licensed SDK.

## Gate before adding a host adapter

Record the exact product and version, CLR/.NET target, managed assembly names, command/selection API, transaction/document lock rules, handle and property coverage, WPF owner/modeless support, plugin loader/bundle format, SDK redistribution terms, and smoke-test results. Compile and run contract tests against that host's SDK before making any compatibility claim.
