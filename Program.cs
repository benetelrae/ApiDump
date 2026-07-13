using System.Reflection;

// Metadata-only explorer for the Civil 3D managed API (loads all Aecc*Mgd.dll).
// Usage:
//   dotnet run -- members <FullTypeName>     list a type's public instance properties
//   dotnet run -- methods <FullTypeName>     list a type's public methods (incl. static)
//   dotnet run -- search  <substring>        list all type names containing <substring>

string acad = @"C:\Program Files\Autodesk\AutoCAD 2026";
string c3d = Path.Combine(acad, "C3D");
string appPlugins = @"C:\Program Files\Autodesk\ApplicationPlugins";
string runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location);

// Every Civil managed assembly we can find: the C3D folder plus any Aecc*Mgd.dll
// shipped inside ApplicationPlugins bundles (e.g. Drainage). Add more roots as needed.
var civilDlls = Directory.GetFiles(c3d, "Aecc*Mgd.dll").ToList();
if (Directory.Exists(appPlugins))
    civilDlls.AddRange(Directory.GetFiles(appPlugins, "Aecc*Mgd.dll", SearchOption.AllDirectories));
civilDlls = civilDlls.GroupBy(Path.GetFileName).Select(g => g.First()).ToList();  // de-dupe by name

// Resolver path = AutoCAD/C3D/ACA + each folder holding a Civil assembly + .NET runtime.
var resolveDirs = new List<string> { acad, c3d, Path.Combine(acad, "ACA"), runtimeDir };
resolveDirs.AddRange(civilDlls.Select(Path.GetDirectoryName));

var paths = resolveDirs.Distinct().Where(Directory.Exists)
                .SelectMany(d => Directory.GetFiles(d, "*.dll"))
                .GroupBy(Path.GetFileName).Select(g => g.First())   // de-dupe by file name
                .ToList();

var resolver = new PathAssemblyResolver(paths);
using var mlc = new MetadataLoadContext(resolver);

var assemblies = new List<Assembly>();
foreach (var dll in civilDlls)
{
    try { assemblies.Add(mlc.LoadFromAssemblyPath(dll)); }
    catch { /* skip assemblies whose metadata won't load */ }
}
Console.Error.WriteLine($"(loaded {assemblies.Count} Civil assemblies)");

if (args.Length < 2) { Console.WriteLine("usage: members <FullTypeName> | search <substring>"); return; }
string cmd = args[0], arg = args[1];

static Type[] SafeTypes(Assembly a)
{
    try { return a.GetTypes(); }
    catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null).ToArray(); }
}

if (cmd == "search")
{
    foreach (var t in assemblies.SelectMany(SafeTypes)
                                .Where(t => t.FullName != null &&
                                            t.FullName.Contains(arg, StringComparison.OrdinalIgnoreCase))
                                .OrderBy(t => t.FullName))
        Console.WriteLine(t.FullName);
}
else if (cmd == "members")
{
    Type t = assemblies.Select(a => a.GetType(arg)).FirstOrDefault(x => x != null);
    if (t == null) { Console.WriteLine("TYPE NOT FOUND: " + arg); return; }
    Console.WriteLine($"=== {t.FullName} ===");
    foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(p => p.Name))
    {
        string rt;
        try { rt = p.PropertyType.Name; } catch { rt = "?"; }
        Console.WriteLine($"  {p.Name} -> {rt}");
    }
}
else if (cmd == "methods")
{
    Type t = assemblies.Select(a => a.GetType(arg)).FirstOrDefault(x => x != null);
    if (t == null) { Console.WriteLine("TYPE NOT FOUND: " + arg); return; }
    Console.WriteLine($"=== {t.FullName} methods ===");
    foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                       .Where(m => !m.IsSpecialName).OrderBy(m => m.Name))
    {
        string ps = string.Join(", ", m.GetParameters().Select(p =>
        {
            string pt; try { pt = p.ParameterType.Name; } catch { pt = "?"; }
            return pt + " " + p.Name;
        }));
        string ret; try { ret = m.ReturnType.Name; } catch { ret = "?"; }
        string kind = m.IsStatic ? "static " : "";
        Console.WriteLine($"  {kind}{ret} {m.Name}({ps})");
    }
}
