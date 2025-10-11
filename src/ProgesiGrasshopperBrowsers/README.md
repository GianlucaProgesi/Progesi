# ProgesiGrasshopperBrowsers (S1-C)
Read-only Grasshopper components to inspect SQLite DB (metadata/variables).

## Project layout
- ProgesiGrasshopperBrowsers.csproj (net48)
- Components/
  - ProgesiMetadataBrowserComponent.cs
  - ProgesiVariableBrowserComponent.cs
- Internal/
  - SqliteBrowser.cs

## Build
- Requires Rhino 7 installed. Override `RhinoInstallDir` MSBuild property if Rhino is installed elsewhere.
- Produces both `.dll` and `.gha` (renamed in PostBuild).

## Use in Grasshopper
- Drop the built `.gha` into `%AppData%\Grasshopper\Libraries` OR reference from your main Progesi plugin.
- Inputs: Run, DbPath (default `data\progesi.db`), Filter*, Limit
- Outputs: Headers, Rows (flat), Info, Count, RowsTree (structured)

## Notes
- Columns that do not exist in your tables are ignored gracefully.
- Filters apply only if the relevant column exists (Name, By/CreatedBy/Author, Ref, LastModifiedUtc).
- Limit is clamped to 1..1000.
