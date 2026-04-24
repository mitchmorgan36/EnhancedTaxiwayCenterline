# Building The DLL In Visual Studio

This project is already set up for Visual Studio. You do not need to create a new project.

## Before You Start

1. Close AutoCAD if it currently has this plugin loaded.
2. Make sure the project files are in this folder:
   - `EnhancedTaxiwayCenterline.sln`
   - `EnhancedTaxiwayCenterline.csproj`
   - `EnhancedTaxiwayCenterline.cs`
3. Open Visual Studio 2022.

## Open The Project

1. In Visual Studio, click `File` > `Open` > `Project/Solution`.
2. Browse to this folder:
   - `C:\Users\mmorgan\OneDrive - Crawford, Murphy & Tilly Inc\Work\Autocad\CAD File Resources\NET Scripts\EnhancedTaxiwayCenterline`
3. Select `EnhancedTaxiwayCenterline.sln`.
4. Click `Open`.

## What You Should See

- A panel named `Solution Explorer`
- A solution named `EnhancedTaxiwayCenterline`
- Under it, a project also named `EnhancedTaxiwayCenterline`
- The source file `EnhancedTaxiwayCenterline.cs`

If `Solution Explorer` is not visible:
- click `View` > `Solution Explorer`

## Open The Code File

1. In `Solution Explorer`, double-click `EnhancedTaxiwayCenterline.cs`.
2. Make any code changes you want.
3. Save the file with `Ctrl+S`.

## Set The Build Options

Near the top of Visual Studio there should be dropdowns for build settings.

Set them to:
- `Debug`
- `x64`

If you do not see `x64`:
1. Click `Build` > `Configuration Manager`.
2. In the `Active solution platform` dropdown, choose `x64`.
3. If `x64` is not listed, choose `<New...>` and create it from `x64`.
4. Click `Close`.

## Build The DLL

Use either method:

- press `Ctrl+Shift+B`
- or click `Build` > `Build Solution`

## What Success Looks Like

If the build works, you should see a message like:

`Build succeeded`

The DLL will be created here:

- `bin\Debug\EnhancedTaxiwayCenterline.dll`

Full path:

- `C:\Users\mmorgan\OneDrive - Crawford, Murphy & Tilly Inc\Work\Autocad\CAD File Resources\NET Scripts\EnhancedTaxiwayCenterline\bin\Debug\EnhancedTaxiwayCenterline.dll`

## Load It In AutoCAD

1. Open AutoCAD.
2. Type `NETLOAD`.
3. Browse to:
   - `C:\Users\mmorgan\OneDrive - Crawford, Murphy & Tilly Inc\Work\Autocad\CAD File Resources\NET Scripts\EnhancedTaxiwayCenterline\bin\Debug`
4. Select `EnhancedTaxiwayCenterline.dll`.
5. Click `Open`.
6. Run the command:
   - `ENHANCEDTCL`

## If The Build Fails

### If Visual Studio says the DLL is in use

That usually means AutoCAD still has the old DLL loaded.

Fix:
1. Close AutoCAD completely.
2. Check Task Manager and make sure `acad.exe` is gone.
3. Build again.

### If you see warnings about files in `obj` being denied

That usually means a previous build file is still locked by another process.

Try:
1. Close AutoCAD.
2. Close Visual Studio.
3. Reopen the solution.
4. Build again.

### If the wrong file gets loaded in AutoCAD

Always load the DLL from:
- `bin\Debug\EnhancedTaxiwayCenterline.dll`

Do not load files from:
- `obj`

## Release Build

If you later want a release build:

1. Change the top dropdown from `Debug` to `Release`
2. Keep platform as `x64`
3. Build again

The output will be:

- `bin\Release\EnhancedTaxiwayCenterline.dll`

## Notes

- This Visual Studio project is configured to use the AutoCAD 2026 DLL references already on this machine.
- It is also configured to build without needing the separate `.NET SDK` that VS Code asked for.
- The `bin` folder holds the final files you use.
- The `obj` folder holds temporary build files.
