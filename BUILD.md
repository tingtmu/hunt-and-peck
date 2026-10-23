# Building HuntAndPeck from the command line

No Visual Studio IDE or .NET Framework targeting pack is required.

## Prerequisites

- .NET Framework 4.8.1 runtime, i.e. **Windows 10 21H2+ or Windows 11 22H2+** (built in on Windows 11 22H2+).
  Targeting 4.8.1 is a deliberate choice; older Windows versions are not supported.
- MSBuild 17 from **Visual Studio 2022 Build Tools** (or any VS 2022 edition), with the **.NET SDK**
  component (Individual components → ".NET SDK"), which the SDK-style project files need. `global.json` pins
  it to a 9.0 SDK (9.0.300 or a later 9.0 release), the version that VS 2022 Build Tools (MSBuild 17.14) installs. From the command line:
  `& "C:\Program Files (x86)\Microsoft Visual Studio\Installer\setup.exe" modify --installPath "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools" --add Microsoft.NetCore.Component.SDK --passive`
- Network access to nuget.org for the first restore (configured in `NuGet.config`).

Reference assemblies come from the `Microsoft.NETFramework.ReferenceAssemblies` NuGet package (referenced
automatically by the .NET SDK), and `Interop.UIAutomationClient.dll` is generated at build time from the
UIAutomationClient type library registered in Windows (see `src/HuntAndPeck/UIAutomationInterop.targets`), so
the .NET Framework SDK (TlbImp/AxImp) is not needed. That generation runs only in Visual Studio's
`MSBuild.exe`, so build with it rather than `dotnet build`.

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

The integration tests (`[Trait("Category", "Integration")]`) launch a small WinForms fixture window (`src\HuntAndPeck.UiaFixture`) and scan it through UI Automation, so they need an interactive desktop.
Skip them (e.g. on a headless machine) by adding `-notrait "Category=Integration"` to the `xunit.console.exe` command.

## CI / Release

GitHub Actions (`.github/workflows/ci.yml`) builds and tests every push and pull request on
`windows-latest`, using the same commands as above. It also checks that the build left no files outside
`bin\` and `obj\`, and builds the release zip on every push. Pushing a `v*` tag (e.g. `v1.8.2`) additionally
checks that the tag matches `<Version>` in `src\Directory.Build.props`, then publishes a
GitHub Release with `HuntAndPeck-<version>.zip` and `SHA256SUMS.txt`.

To release a new version, set `<Version>` in `src\Directory.Build.props` and the `assemblyIdentity`
version in `src\HuntAndPeck\app.manifest` (a test checks they match), then push the `v<version>` tag.

## Notes

- The projects are SDK-style csproj files targeting `net481`. Settings shared by all of them (target
  framework, output paths, version, product, copyright) are in `src\Directory.Build.props`. Source files
  are picked up automatically, so new `.cs` and `.xaml` files don't need to be added to the project.
- Legacy / unused: `src\build.ps1` and `src\build.cake` (Cake 0.20, VS2017 MSBuild, packages.config
  restore), `src\.nuget\NuGet.exe`, and any `src\packages\` folder from old packages.config restores.
  They are not part of the build; use the commands above.
