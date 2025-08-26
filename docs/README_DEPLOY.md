# Progesi – Build & Auto-Deploy to Grasshopper

This solution has been prepared to **auto-deploy** all class library outputs to your Grasshopper Libraries folder on **Windows**.

## How it works
- A root `Directory.Build.props` defines a `DeployToGrasshopper` MSBuild **Target** that runs **AfterBuild**.
- It copies:
  - The project's main output (the `.dll` or `.gha`)
  - All **copy-local dependencies** (`ReferenceCopyLocalPaths`)

to: `%APPDATA%\Grasshopper\Libraries\Progesi`

## What you need to do
- Build the solution in **Debug** or **Release** from Visual Studio.
- Ensure that any Grasshopper component project is a **Class Library** and targets the correct framework (e.g. .NET Framework 4.8 for Grasshopper 7 / Rhino 7).
- If you produce a `.dll` and need a `.gha`, set the assembly name accordingly or keep using your existing post-build renaming (optional). The deploy target copies whatever your `$(TargetPath)` emits.

## Notes
- No more manual copying of `.dll` / `.gha` files.
- All dependencies are copied too, avoiding missing assemblies at runtime.
- The deploy folder is: `%APPDATA%\Grasshopper\Libraries\Progesi`
- If you need to disable deployment for a specific project, add to that `.csproj`:
  ```xml
  <PropertyGroup>
    <DisableGrasshopperDeploy>true</DisableGrasshopperDeploy>
  </PropertyGroup>
  ```
  and then update the target's Condition accordingly if you want selective deploys.