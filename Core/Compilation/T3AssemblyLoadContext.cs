#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel.Design.Serialization;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Runtime.Versioning;
using System.Threading;
using T3.Core.IO;
using T3.Core.Logging;
using NuGet.Configuration;
using NuGet.Frameworks;
// memory extensions
using static System.MemoryExtensions;

namespace T3.Core.Compilation;

/// <summary>
/// This is the actual place where assemblies are loaded and dependencies are resolved on a per-dll level.
/// Inheriting from <see cref="AssemblyLoadContext"/> allows us to load assemblies in a custom way, which is required
/// as assemblies are loaded from different locations for each package.
///
/// Each package has its own <see cref="T3AssemblyLoadContext"/> that is used to load the assemblies of that package. If a package relies on another package
/// from a CSProj-level, the dependency's load context and dlls are added to the dependent's load context such that the dependent's dlls can be loaded
/// referencing the types provided by the dependency.
///
/// For example, the LibEditor package has a dependency on Lib. When LibEditor is loaded, the Lib package is loaded first via LibEditor's load context. Then
/// the loading procedure continues until LibEditor is fully loaded with all its dependencies.
///
/// Unfortunately this process is very complex, and is not thoroughly tested with large dependency chains.
/// </summary>
internal sealed class T3AssemblyLoadContext : AssemblyLoadContext
{
    public event EventHandler? UnloadBegan;
    internal event EventHandler? UnloadBeganInternal;
    private readonly Lock _dependencyLock = new();

    internal AssemblyTreeNode? Root { get; private set; }

    private readonly List<AssemblyLoadContext> _dependencyContexts = [];
    private static readonly List<AssemblyTreeNode> _coreNodes = [];

    private static readonly Lock _nugetLock = new();
    private static readonly AssemblyLoadContext _nugetContext = new("NuGet", true);
    private static readonly List<AssemblyTreeNode> _loadedNuGetAssemblies = [];

    private static readonly List<T3AssemblyLoadContext> _loadContexts = [];
    private static readonly Lock _loadContextLock = new();
    private static readonly DllImportResolver _dllImportResolver = NativeDllResolver; // todo - this likely violates the encapsulation of assembly load contexts
    private bool _unloaded;
    private static readonly string FrameworkDescription = RuntimeInformation.FrameworkDescription;
    private static readonly string RuntimeIdentifier = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier.Split('-')[0].Trim();

    // todo - per-project directory, since this walks up the directory provided to find nuget config files
    private static readonly string _nugetDirectory = SettingsUtility.GetGlobalPackagesFolder(Settings.LoadDefaultSettings(null, null, null));

    static T3AssemblyLoadContext()
    {
        (AssemblyLoadContext Context, (Assembly Assembly, AssemblyName name)[] assemblies)[]? allAssemblies = All
           .Select(ctx => (
                              ctx: ctx,
                              assemblies: ctx.Assemblies
                                             .Select(x => (asm: x, name: x.GetName()))
                                             .ToArray()))
           .ToArray();

        // create "root" nodes for each assembly context - one per context and one per directory for each context
        foreach (var ctxGroup in allAssemblies)
        {
            List<string> directories = new();
            foreach (var assemblyDef in ctxGroup.assemblies)
            {
                string? directory;

                try
                {
                    directory = Path.GetDirectoryName(assemblyDef.Assembly.Location);
                }
                catch
                {
                    continue;
                }

                if (directory == null || directories.Contains(directory))
                    continue;

                directories.Add(directory);
                var node = new AssemblyTreeNode(assemblyDef.Assembly, ctxGroup.Context, false, true, null); // no native resolver bc they already have one
                _coreNodes.Add(node);
            }
        }

        // add references to each core node where applicable, reusing existing nodes to create the tree
        for (var index = 0; index < _coreNodes.Count; index++)
        {
            var node = _coreNodes[index];
            var dependencies = node.Assembly.GetReferencedAssemblies();
            foreach (var dependencyName in dependencies)
            {
                foreach (var ctxGroup in allAssemblies)
                {
                    foreach (var asmAndName in ctxGroup.assemblies)
                    {
                        if (asmAndName.name != dependencyName)
                            continue;

                        AssemblyTreeNode? depNode = null;
                        var nameStr = dependencyName.GetNameSafe();
                        foreach (var coreNode in _coreNodes)
                        {
                            if (coreNode.TryFindExisting(nameStr, out depNode))
                                break;
                        }

                        depNode ??= new AssemblyTreeNode(asmAndName.Assembly, ctxGroup.Context, false, false, _dllImportResolver);

                        node.AddReferenceTo(depNode);
                    }
                }
            }
        }
    }

    private static List<AssemblyTreeNode> CoreNodes => _coreNodes;
    private readonly string _nativeDllDirectory;

    internal T3AssemblyLoadContext(string assemblyName, string directory) :
        base(assemblyName, true)
    {
        Log.Debug($"{Name}: Creating new assembly load context for {assemblyName}");
        Resolving += (_, name) =>
                     {
                         var result = OnResolving(name);

                         if (result != null)
                         {
                             // check versions of the assembly - if different, log a warning.
                             // todo: actually do something with this information later
                             if (ProjectSettings.Config.LogAssemblyVersionMismatches)
                             {
                                 var assemblyNameOfResult = result.GetName();

                                 if (assemblyNameOfResult.Version != name.Version)
                                 {
                                     Log.Warning($"Assembly {name.Name} loaded with different version: {assemblyNameOfResult.Version} vs {name.Version}");
                                 }
                             }
                         }

                         if (result == null)
                         {
                             Log.Error($"{Name!}: Failed to resolve assembly '{name.Name}'");
                             return Root!.Assembly;
                         }

                         return result;
                     };
        Unloading += (_) => { Log.Debug($"{Name!}: Unloading assembly context"); };

        lock (_loadContextLock)
        {
            _loadContexts.Add(this);
        }

        _nativeDllDirectory = directory;

        var path = Path.Combine(directory, Name!) + ".dll";

        try
        {
            var asm = LoadFromAssemblyPath(path);
            Root = new AssemblyTreeNode(asm, this, true, true, _dllImportResolver);
            Log.Debug($"{Name} : Loaded root assembly {asm.FullName} from '{path}'");
        }
        catch (Exception e)
        {
            Log.Error($"{Name!}: Failed to load root assembly {Name}: {e}");
        }
    }

    // called if Load method returns null - searches other contexts and nuget packages
    private Assembly? OnResolving(AssemblyName asmName)
    {
        var name = asmName.GetNameSafe();

        #if DEBUG
        if (_unloaded)
        {
            Log.Error($"{Name!}: Attempted to resolve assembly {name} after unload");
            return null;
        }

        #endif

        // try other assembly contexts
        lock (_loadContextLock)
        {
            // try to find existing in others
            foreach (var ctx in _loadContexts)
            {
                if (ctx == this)
                    continue;

                var root = ctx.Root;

                if (root == null)
                    continue;

                if (root.TryFindExisting(name, out var asmNode))
                {
                    // add the dependency to our context
                    AddDependency(asmNode);
                    return asmNode.Assembly;
                }
            }

            // try to find unreferenced in others
            foreach (var ctx in _loadContexts)
            {
                if (ctx == this)
                    continue;

                var root = ctx.Root;

                if (root == null)
                    continue;

                if (root.TryFindUnreferenced(name, out var asmNode))
                {
                    // add the dependency to our context
                    AddDependency(asmNode);
                    return asmNode.Assembly;
                }
            }
        }

        // check nuget packages
        lock (_nugetLock)
        {
            foreach (var node in _loadedNuGetAssemblies)
            {
                if (node.TryFindExisting(name, out var asmNode))
                {
                    // add the dependency to our context
                    AddDependency(asmNode);
                    return asmNode.Assembly;
                }

                if (node.TryFindUnreferenced(name, out asmNode))
                {
                    // add the dependency to our context
                    AddDependency(asmNode);
                    return asmNode.Assembly;
                }
            }

            var rootFrameworks = Root!.Assembly.GetCustomAttributes<TargetFrameworkAttribute>()
                                      .Select(x => AsNuGetFramework(x.FrameworkName))
                                      .ToArray();
            if (TrySearchNugetAssemblies(asmName, rootFrameworks, name, Name, out var nugetNode))
            {
                AddDependency(nugetNode);
                _loadedNuGetAssemblies.Add(nugetNode);
                return nugetNode.Assembly;
            }

            return null;
        }

        static bool TrySearchNugetAssemblies(AssemblyName asmName, NuGetFramework[] myTargets, string name, string? debugName,
                                             [NotNullWhen(true)] out AssemblyTreeNode? node)
        {
            var basePath = _nugetDirectory;
            var packageDirName = (asmName.Name ?? asmName.FullName.Split(',')[0]).ToLower();
            var version = asmName.Version;
            var hasVersion = version != null;
            var versionString = hasVersion ? $"{version!.Major}.{version.Minor}.{version.Build}" : "0.0.0";
            var baseDirectory = new DirectoryInfo(Path.Combine(basePath, packageDirName));
            if (!baseDirectory.Exists)
            {
                // ffs.... we will enter here if an assembly reference is named differently from its package name
                // which is very annoying. currently this is only an issue with unsplasharp, so I will handle this in the stupidest way possible.
                // literally just by grabbing the first folder whose name starts with the requested dll name
                // if none are found, itll be an issue
                var potentialSubdirs = Directory.GetDirectories(basePath, packageDirName + "*", SearchOption.TopDirectoryOnly);
                bool success = false;
                foreach(var dir in potentialSubdirs)
                {
                    var dirInfo = new DirectoryInfo(dir);
                    if (dirInfo.Exists)
                    {
                        baseDirectory = dirInfo;
                        success = true;
                        break;
                    }
                }

                if (!success)
                {
                    Log.Error($"{debugName!}: NuGet package directory {baseDirectory} does not exist - is the package installed? " +
                              $"is its dll named according to the package it comes from?");
                    node = null;
                    return false;
                }
            }

            DirectoryInfo? versionDirectory = null;
            if (hasVersion)
            {
                versionDirectory = new DirectoryInfo(Path.Combine(baseDirectory.FullName, versionString));
                if (!versionDirectory.Exists)
                {
                    Log.Warning($"{debugName!}: NuGet package version directory {versionDirectory} does not exist");
                    if (!TryGetNearestVersionDirectory(baseDirectory, version!, out versionDirectory, out var reason))
                    {
                        Log.Error($"{debugName!}: Could not find nearest version directory for {versionString} in {baseDirectory.FullName}: {reason}");
                        node = null;
                        return false;
                    }
                }
            }
            else
            {
                // get the latest version directory
                var versionDirectories = baseDirectory.GetDirectories();
                Array.Sort(versionDirectories, (a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
                versionDirectory = versionDirectories.LastOrDefault();
            }

            if (versionDirectory is not { Exists: true })
            {
                Log.Error($"{debugName!}: NuGet package version directory {versionString} could not be resolved");
                node = null;
                return false;
            }

            NugetFrameworkDirectory? packageFrameworkDir = null;

            // now we have the version directory, search for the assembly in the lib directory
            var libDirectory = new DirectoryInfo(Path.Combine(versionDirectory.FullName, "lib"));
            if (libDirectory.Exists)
            {
                // search the lib directory
                // collect framework subdirectories and order by preference
                var rawSubDirs = libDirectory.GetDirectories();
                var subDirectories = rawSubDirs
                                    .Select(x => new NugetFrameworkDirectory(Directory: x, Framework: NuGetFramework.ParseFolder(x.Name)))
                                    .Where(x => x.Framework != NuGetFramework.UnsupportedFramework)
                                    .ToArray();

                foreach (var f in myTargets)
                {
                    // find the subdirectory that matches the target framework
                    packageFrameworkDir = NuGetFrameworkUtility.GetNearest(subDirectories, f);

                    if (packageFrameworkDir is not null && packageFrameworkDir != default(NugetFrameworkDirectory))
                    {
                        break;
                    }
                }

                if (packageFrameworkDir is null || packageFrameworkDir == default(NugetFrameworkDirectory))
                {
                    // check for any non-matching but compatible frameworks (e.g. netstandard)
                    NugetFrameworkDirectory? highestStandard = null;
                    foreach (var subDir in subDirectories)
                    {
                        if (!subDir.Framework.Framework.StartsWith(".NETStandard")) continue;

                        if (highestStandard == null)
                        {
                            highestStandard = subDir;
                            continue;
                        }

                        var netStandardVersion = subDir.Framework.Version;
                        var currentVersion = highestStandard.Value.Framework.Version;
                        if (netStandardVersion > currentVersion)
                        {
                            highestStandard = subDir;
                        }

                        Log.Warning($"{debugName!}: Using compatible framework {subDir.Framework} for assembly {name} from nuget package {packageDirName} version {versionString}");
                        break;
                    }

                    packageFrameworkDir = highestStandard;
                }
                
                if(packageFrameworkDir is null || packageFrameworkDir == default(NugetFrameworkDirectory))
                {
                    Log.Warning($"{debugName!}: Could not find compatible framework for assembly {name} in nuget package {packageDirName} version {versionString}. checking .net framework");
                    // check for .NET Framework directories
                    var netFrameworkDirs = rawSubDirs
                                          .Where(x => x.Name.StartsWith("net4", StringComparison.OrdinalIgnoreCase) || x.Name.StartsWith("net3"))
                                          .OrderBy(x => x.Name)
                                          .Select(x => new NugetFrameworkDirectory(Directory: x, Framework: NuGetFramework.ParseFolder(x.Name)))
                                          .ToArray();
                    packageFrameworkDir = netFrameworkDirs.LastOrDefault();
                    if (packageFrameworkDir is not null && packageFrameworkDir != default(NugetFrameworkDirectory))
                    {
                        Log.Warning($"{debugName!}: Using .NET Framework directory {packageFrameworkDir.Value.Framework} for assembly {name} from" +
                                    $" nuget package {packageDirName} version {versionString}");
                    }
                }
            }

            if (packageFrameworkDir is null || packageFrameworkDir == default(NugetFrameworkDirectory))
            {
                node = null;
                return false;
                // todo: re: below - should we...
                // check "runtime" folder
                // switch to runtimes folder, where we need to filter by RID
                // next we need to enter into the correct framework directory

                // now we need to filter by platform RID if such folders exist
                // var rid = RuntimeIdentifier;
            }

            // now we have the package framework directory, search for the assembly in it
            var directory = packageFrameworkDir.Value.Directory;
            var assemblyFile = Path.Combine(directory.FullName, $"{name}.dll");
            if (!File.Exists(assemblyFile))
            {
                Log.Error($"{debugName!}: NuGet package assembly {assemblyFile} does not exist");
                node = null;
                return false;
            }

            try
            {
                var assembly = _nugetContext.LoadFromAssemblyPath(assemblyFile);
                node = new AssemblyTreeNode(assembly, _nugetContext, true, true, _dllImportResolver);
                Log.Debug($"{debugName!}: Loaded assembly {asmName.FullName} from nuget package {packageDirName} version {versionString}");
                return true;
            }
            catch (Exception e)
            {
                Log.Error($"{debugName!}: Failed to load assembly {asmName.FullName} from nuget package: {e}");
                node = null;
                return false;
            }

            static bool TryGetNearestVersionDirectory(DirectoryInfo baseDir, Version version, [NotNullWhen(true)] out DirectoryInfo? versionDirectory,
                                                      [NotNullWhen(false)] out string? reason)
            {
                // get nearest version
                // if the version directory does not exist, we need to find the nearest version, with a preference towards newer
                (DirectoryInfo dir, Version version)[] versionDirectories;
                
                try
                {
                    versionDirectories = baseDir.EnumerateDirectories()
                                                .Where(x => !char.IsLetter(x.Name[0]))
                                                .Select(x =>
                                                        {
                                                            // construct version from the directories
                                                            var versionSplit = x.Name.Split('-'); // ignore postfixes like -alpha000147
                                                            var basicVersion = versionSplit[0];
                                                            if (versionSplit.Length <= 1) 
                                                                return (dir: x, version: new Version(basicVersion));
                                                            
                                                            // grab the numeric part of the postfix as a revision number
                                                            int startNumberIndex = -1;
                                                            var postfix = versionSplit[1];
                                                            for(int i = 0; i < postfix.Length; i++)
                                                            {
                                                                if (char.IsDigit(postfix[i]))
                                                                {
                                                                    startNumberIndex = i;
                                                                    break;
                                                                }
                                                            }

                                                            if (startNumberIndex == -1) 
                                                                return (dir: x, version: new Version(basicVersion));
                                                                
                                                            int endNumberIndexExclusive = startNumberIndex + 1;
                                                            for (; endNumberIndexExclusive < postfix.Length; endNumberIndexExclusive++)
                                                            {
                                                                if (!char.IsDigit(postfix[endNumberIndexExclusive]))
                                                                {
                                                                    break;
                                                                }
                                                            }

                                                            var numberSpan = postfix.AsSpan(startNumberIndex, endNumberIndexExclusive - startNumberIndex);
                                                            if (int.TryParse(numberSpan, out var revision))
                                                            {
                                                                versionSplit[0] += $".{revision}";
                                                            }
                                                            return (dir: x, version: new Version(basicVersion));
                                                        })
                                                .OrderBy(x => x.version)
                                                .ToArray();
                }
                catch (Exception e) // error parsing a version - dont really wanna deal with this right now lol
                {
                    reason = $"Error parsing version directories in {baseDir.FullName}: {e.Message}";
                    versionDirectory = null;
                    return false;
                }

                if (versionDirectories.Length == 0)
                {
                    reason = "No nuget version directories found";
                    versionDirectory = null;
                    return false;
                }

                if (TryGetNearest(versionsToSearch: versionDirectories,
                                  version: version,
                                  selector: v => v.Major == version.Major && v.Minor == version.Minor,
                                  getNumber: x => x.Build == -1 ? x.Revision : x.Build,
                                  versionDirectory: out var dir))
                {
                    versionDirectory = dir;
                    reason = null;
                    return true;
                }

                // if we have no non-breaking versions, we need to find the nearest breaking version
                if (TryGetNearest(versionsToSearch: versionDirectories,
                                  version: version,
                                  selector: v => v.Major == version.Major,
                                  getNumber: x => x.Minor,
                                  versionDirectory: out dir))
                {
                    versionDirectory = dir;
                    reason = null;
                    return true;
                }

                // if we have none with the same major version, time to go nuclear
                if (TryGetNearest(versionsToSearch: versionDirectories,
                                  version: version,
                                  selector: v => true, // any version
                                  getNumber: x => x.Major,
                                  versionDirectory: out dir))
                {
                    versionDirectory = dir;
                    reason = null;
                    return true;
                }

                versionDirectory = versionDirectories[0].dir; // fallback to the first version directory
                reason = $"No suitable version directory found for {version} in {baseDir.FullName}, using {versionDirectory.Name} instead";
                return true;

                static bool TryGetNearest((DirectoryInfo dir, Version version)[] versionsToSearch, Version version, Func<Version, bool> selector,
                                          Func<Version, int> getNumber, [NotNullWhen(true)] out DirectoryInfo? versionDirectory)
                {
                    var selected = versionsToSearch
                                   .Where(x => selector(x.version))
                                   .ToArray();
                    switch (versionsToSearch.Length)
                    {
                        case 0:
                            versionDirectory = null;
                            return false;
                        case 1:
                            versionDirectory = versionsToSearch[0].dir;
                            return true;
                    }

                    var versionNumber = getNumber(version);

                    // order by preference - higher versions preferred,
                    Array.Sort(versionsToSearch,
                               comparison: (x, y) => (versionNumber - getNumber(x.version)).CompareTo(versionNumber - getNumber(y.version)));
                    versionDirectory = versionsToSearch[0].dir;
                    return true;
                }
            }
        }

        static NuGetFramework AsNuGetFramework(string rawName)
        {
            var frameworkParts = rawName.Split(',');
            if (frameworkParts.Length == 1)
                return new NuGetFramework(frameworkParts[0]);

            try
            {
                var versionPart = frameworkParts[1];
                const string versionPrefix = "Version=v";
                if (versionPart.StartsWith(versionPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    versionPart = versionPart[versionPrefix.Length..];
                }

                var version = new Version(versionPart);
                return new NuGetFramework(frameworkParts[0], version);
            }
            catch (Exception e)
            {
                Log.Error($"Failed to parse version from framework name '{rawName}': {e}");
                return new NuGetFramework(frameworkParts[0]);
            }
        }
    }

    private readonly record struct NugetFrameworkDirectory(DirectoryInfo Directory, NuGetFramework Framework) : IFrameworkSpecific
    {
        NuGetFramework IFrameworkSpecific.TargetFramework => Framework;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        #if DEBUG
        if (_unloaded)
        {
            Log.Error($"{Name!}: Attempted to load assembly {assemblyName} after unload");
            return null;
        }
        #endif

        var name = assemblyName.GetNameSafe();

        foreach (var coreRef in CoreNodes)
        {
            if (coreRef.TryFindExisting(name, out var coreAssembly))
            {
                AddDependency(coreAssembly);
                return coreAssembly.Assembly;
            }

            if (coreRef.TryFindUnreferenced(name, out coreAssembly))
            {
                AddDependency(coreAssembly);
                return coreAssembly.Assembly;
            }
        }

        if (Root is null)
        {
            Log.Error($"{Name!}: Root is null, cannot resolve assembly {name}");
            return null;
        }

        if (Root!.TryFindExisting(name, out var node))
        {
            AddDependency(node);
            return node.Assembly;
        }

        if (Root!.TryFindUnreferenced(name, out node))
        {
            AddDependency(node);
            return node.Assembly;
        }

        return OnResolving(assemblyName);
    }

    private static IntPtr NativeDllResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        //Log.Debug($"{assembly.FullName}: Resolving native dll {libraryName} for assembly {assembly.FullName}");
        const DllImportSearchPath defaultSearchPath = DllImportSearchPath.AssemblyDirectory
                                                      | DllImportSearchPath.UseDllDirectoryForDependencies
                                                      | DllImportSearchPath.ApplicationDirectory;

        if (NativeLibrary.TryLoad(libraryName, assembly, searchPath ?? defaultSearchPath, out var handle))
        {
            //Log.Debug($"{assembly.FullName!}: Successfully resolved native dll {libraryName}");
            return handle;
        }

        Log.Error($"{assembly.FullName!}: Failed to resolve native dll {libraryName} relative to assembly '{assembly.Location}'");
        return IntPtr.Zero;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        // manual dll resolution with the potential of having nothing but the name of the dll sans the extension
        string fullPath;
        bool pathFullyQualified = false;
        if (Path.IsPathFullyQualified(unmanagedDllName))
        {
            fullPath = unmanagedDllName;
            pathFullyQualified = true;
        }
        else if (!unmanagedDllName.EndsWith(".dll"))
        {
            fullPath = Path.Combine(_nativeDllDirectory, unmanagedDllName + ".dll");
        }
        else
        {
            fullPath = Path.Combine(_nativeDllDirectory, unmanagedDllName);
        }

        if (File.Exists(fullPath))
        {
            try
            {
                return LoadUnmanagedDllFromPath(fullPath);
            }
            catch (Exception e)
            {
                Log.Error($"{Name!}: Failed to load unmanaged dll {unmanagedDllName} from path {fullPath}: {e}");
                return IntPtr.Zero;
            }
        }

        // check for the .so extension on linux/mac
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
        {
            var unixPath = pathFullyQualified
                               ? Path.ChangeExtension(fullPath, ".so")
                               : Path.Combine(_nativeDllDirectory, unmanagedDllName + ".so");

            if (File.Exists(unixPath))
            {
                try
                {
                    return LoadUnmanagedDllFromPath(unixPath);
                }
                catch (Exception e)
                {
                    Log.Error($"{Name!}: Failed to load unmanaged dll {unmanagedDllName} from unix path {unixPath}: {e}");
                    return IntPtr.Zero;
                }
            }
        }

        return IntPtr.Zero;
    }

    private void AddDependency(AssemblyTreeNode node)
    {
        _ = Root!.AddReferenceTo(node);

        var ctx = node.LoadContext;
        if (ctx == this)
            return;

        lock (_dependencyLock)
        {
            if (!_dependencyContexts.Contains(ctx))
            {
                // subscribe to the unload event of the dependency context
                if (ctx is T3AssemblyLoadContext tixlCtx)
                {
                    tixlCtx.UnloadBeganInternal += OnDependencyUnloaded;
                }
                else
                {
                    ctx.Unloading += OnNonTixlDependencyUnloaded;
                }

                _dependencyContexts.Add(ctx);
                Log.Debug($"{Name!}: Added dependency {node.Name} from {ctx.Name}");
            }
        }
    }

    private void OnNonTixlDependencyUnloaded(AssemblyLoadContext ctx)
    {
        ctx.Unloading -= OnNonTixlDependencyUnloaded;
        RemoveDependency(ctx);
    }

    private void OnDependencyUnloaded(object? sender, EventArgs e)
    {
        var ctx = (T3AssemblyLoadContext)sender!;
        ctx.UnloadBeganInternal -= OnDependencyUnloaded;
        RemoveDependency(ctx);
    }

    private void RemoveDependency(AssemblyLoadContext ctx)
    {
        lock (_dependencyLock)
        {
            _dependencyContexts.Remove(ctx);
            BeginUnload(); // begin unloading ourselves too
        }
    }

    internal void BeginUnload()
    {
        if (_unloaded)
            throw new InvalidOperationException($"Assembly context {Name} already unloaded");
        _unloaded = true;

        lock (_dependencyLock)
        {
            // unsubscribe from all our dependencies
            for (int i = _dependencyContexts.Count - 1; i >= 0; i--)
            {
                var ctx = _dependencyContexts[i];
                if (ctx is T3AssemblyLoadContext tixlCtx)
                {
                    tixlCtx.UnloadBeganInternal -= OnDependencyUnloaded;
                }
                else
                {
                    ctx.Unloading -= OnNonTixlDependencyUnloaded;
                }

                _dependencyContexts.RemoveAt(i);
            }
        }

        lock (_loadContextLock)
        {
            _loadContexts.Remove(this);
        }

        Root = null; // dereference our assembly as we will need to reload it 

        try
        {
            UnloadBeganInternal?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception e)
        {
            Log.Error($"{Name!}: Exception thrown on assembly unload (internal): {e}");
        }

        try
        {
            UnloadBegan?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception e)
        {
            Log.Error($"{Name!}: Exception thrown on assembly unload: {e}");
        }

        Unload();
    }

    private sealed class AssemblyNameAndPath(string path, string fileBasedName)
    {
        private AssemblyName? _assemblyName;
        private bool _triedToLoad;
        public string Path { get; } = path;
        public string FileBasedName { get; } = fileBasedName;
        public bool Claimed;

        public AssemblyName? AssemblyName
        {
            get
            {
                if (_triedToLoad)
                    return _assemblyName;

                try
                {
                    _assemblyName = AssemblyName.GetAssemblyName(Path);
                }
                catch
                {
                    _assemblyName = null;
                }

                _triedToLoad = true;
                return _assemblyName;
            }
        }
    }
}

internal static class AssemblyNameExtensions
{
    public static string GetNameSafe(this AssemblyName asmName) => asmName.Name ?? asmName.FullName;

    // extension method
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ContainsAll(this ReadOnlySpan<char> span, ReadOnlySpan<char> other)
    {
        return span.IndexOf(other) != -1;
    }
}