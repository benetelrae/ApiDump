# ApiDump — Civil 3D managed API explorer

A tiny .NET 8 console tool that reads the Civil 3D managed assemblies' **metadata**
(via `System.Reflection.MetadataLoadContext`) **without executing them**. That's why
it works where PowerShell / normal reflection can't: those actually *load* the
assembly, which drags in native AutoCAD dependencies and fails outside `acad.exe`.
Metadata-only means no Civil 3D needs to be running.

Use it to answer "where does X live in the API?" — far faster than guessing member
names against the compiler.

## Usage
```
dotnet run -c Release -- search  <substring>     # type full-names containing <substring>
dotnet run -c Release -- members <FullTypeName>  # public instance properties (name -> type)
dotnet run -c Release -- methods <FullTypeName>  # public methods incl. static (extension classes)
```

## Examples
```
dotnet run -c Release -- members Autodesk.Civil.DatabaseServices.Styles.StylesRoot
dotnet run -c Release -- search  Pressure
dotnet run -c Release -- methods Autodesk.Civil.DatabaseServices.Styles.StylesRootPressurePipesExtension
```

## How it found the hidden APIs
- **Pressure networks** are extension methods (`styles.GetPressurePipeStyles()`), not
  properties — `methods …StylesRootPressurePipesExtension` revealed them.
- **Bands / projection / label sub-roots** are nested containers — `members <Root>`
  lists their leaf `…Collection` properties.

## Notes
- Paths are hardcoded to AutoCAD/Civil 3D **2026** (`C:\Program Files\Autodesk\AutoCAD 2026`
  + `\C3D` + `\ACA`) plus the .NET 8 runtime dir. Change them in `Program.cs` for other versions.
- Loads `AeccDbMgd.dll` and `AeccPressurePipesMgd.dll`; add more in `Program.cs` as needed.
