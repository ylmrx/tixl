#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.Versioning;
using System.Threading;
using NuGet.Configuration;
using NuGet.Frameworks;
using T3.Core.Logging;

namespace T3.Core.Compilation;

internal sealed partial class T3AssemblyLoadContext
{
    private static readonly Lock _nugetLock = new();
    private static readonly AssemblyLoadContext _nugetContext = new("NuGet", true);
    private static readonly List<AssemblyTreeNode> _loadedNuGetAssemblies = [];
    
    // todo - per-project directory, since this walks up the directory provided to find nuget config files
    private static readonly string _nugetDirectory = SettingsUtility.GetGlobalPackagesFolder(Settings.LoadDefaultSettings(null, null, null));
    

    private readonly record struct NugetFrameworkDirectory(DirectoryInfo Directory, NuGetFramework Framework) : IFrameworkSpecific
    {
        NuGetFramework IFrameworkSpecific.TargetFramework => Framework;
    }

    private static bool TrySearchNugetAssemblies(AssemblyName asmName, NuGetFramework[] myTargets, string name, string? debugName,
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
            foreach (var dir in potentialSubdirs)
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
                // check for any non-matching but compatible frameworks (e.g. netstandard)wmv
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

            if (packageFrameworkDir is null || packageFrameworkDir == default(NugetFrameworkDirectory))
            {
                Log.Warning($"{debugName!}: Could not find compatible framework for assembly {name} in nuget package {packageDirName} version {versionString}. checking .net framework");
                
                // check for .NET Framework directories in an ugly and stupid way
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
            // should we...
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
                                                        for (int i = 0; i < postfix.Length; i++)
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
                              filter: v => v.Major == version.Major && v.Minor == version.Minor,
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
                              filter: v => v.Major == version.Major,
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
                              filter: v => true, // any version
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

            static bool TryGetNearest((DirectoryInfo dir, Version version)[] versionsToSearch, Version version, Func<Version, bool> filter,
                                      Func<Version, int> getNumber, [NotNullWhen(true)] out DirectoryInfo? versionDirectory)
            {
                var selected = versionsToSearch
                              .Where(x => filter(x.version))
                              .ToArray();
                switch (selected.Length)
                {
                    case 0:
                        versionDirectory = null;
                        return false;
                    case 1:
                        versionDirectory = selected[0].dir;
                        return true;
                }

                var versionNumber = getNumber(version);

                // order by preference - higher versions preferred,
                Array.Sort(selected,
                           comparison: (x, y) => (versionNumber - getNumber(x.version)).CompareTo(versionNumber - getNumber(y.version)));
                versionDirectory = selected[0].dir;
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

    private Assembly? SearchNugetForAssemblies(AssemblyName asmName, string name)
    {
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
            }

            foreach (var node in _loadedNuGetAssemblies)
            {
                if (node.TryFindUnreferenced(name, out var asmNode))
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
    }
}