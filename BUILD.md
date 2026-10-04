# Building HuntAndPeck from the command line

No Visual Studio IDE, `dotnet` CLI, or .NET Framework targeting pack is required.

## Prerequisites

- .NET Framework 4.8.1 runtime, i.e. **Windows 10 21H2+ or Windows 11 22H2+** (built in on Windows 11 22H2+).
  Targeting 4.8.1 is a deliberate choice; older Windows versions are not supported.
- MSBuild 17 from **Visual Studio 2022 Build Tools** (or any VS 2022 edition).
- Network access to nuget.org for the first restore (configured in `NuGet.config`).

Reference assemblies come from the `Microsoft.NETFramework.ReferenceAssemblies` NuGet package, and
`Interop.UIAutomationClient.dll` is generated at build time from the UIAutomationClient type library
registered in Windows (see `src/HuntAndPeck/UIAutomationInterop.targets`), so the .NET Framework SDK
(TlbImp/AxImp) is not needed.

## Build

From the repository root, in PowerShell:

```powershell
$msbuild = "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
& $msbuild src\HuntAndPeck.sln -restore -p:Configuration=Release
```

Output: `src\HuntAndPeck\bin\Release\` (`hap.exe`, `hap.exe.config`, `HuntAndPeck.NativeMethods.dll`,
`Interop.UIAutomationClient.dll`, `Hardcodet.NotifyIcon.Wpf.dll`).

Runtime configuration (settings file, log location, start with Windows) is described in the
[README](README.md#configuration).

Use `-p:Configuration=Debug` for a debug build.

## Test

After building, run the xUnit tests with either runner:

```powershell
# xUnit console runner (restored into the NuGet global packages folder by the test project;
# that folder is $env:NUGET_PACKAGES if set, otherwise %USERPROFILE%\.nuget\packages)
$nugetPackages = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { "$env:USERPROFILE\.nuget\packages" }
& "$nugetPackages\xunit.runner.console\2.9.3\tools\net481\xunit.console.exe" `
    src\HuntAndPeck.Tests\bin\Release\HuntAndPeck.Tests.dll

# or VSTest (requires the "Testing tools core features - Build Tools" component of VS 2022 Build Tools)
& "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\Common7\IDE\Extensions\TestPlatform\vstest.console.exe" `
    src\HuntAndPeck.Tests\bin\Release\HuntAndPeck.Tests.dll
```

## Notes

- The projects are classic (non-SDK) csproj files using `PackageReference`. SDK-style projects
  would need the .NET SDK, which VS Build Tools does not include by default.
- Legacy / unused: `src\build.ps1` and `src\build.cake` (Cake 0.20, VS2017 MSBuild, packages.config
  restore), `src\.nuget\NuGet.exe`, and any `src\packages\` folder from old packages.config restores.
  They are not part of the build; use the commands above.
