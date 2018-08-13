using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Containers;
using Mono.Cecil;
using WrapperMarkerAttributes;

namespace WrapperRoleListener.Internal.AssemblyLoading
{
    /// <summary>
    /// Keeps a lookup table of versions to root assembly paths.
    /// </summary>
    [Serializable]
    public class VersionTable<T> {
        private readonly Dictionary<int, KeyValuePair<BuildVersion, T>> versionTable;
        private readonly object VersionLock = new object();
        private readonly string markerName = typeof(ApplicationSetupPointAttribute).FullName;
        private List<string> _versionNames;
        private string _versionsAvailable;

        public VersionTable()
        {
            versionTable = new Dictionary<int, KeyValuePair<BuildVersion, T>>();
        }

        /// <summary>
        /// Try to add a new service version. Ignores if it is exactly the same as one that's already been seen, or if it has no setup marker
        /// </summary>
        /// <param name="typePath">Full file path to the candidate DLL</param>
        /// <param name="candidateFunc">A function that will build the handler if a correct new version is found.
        /// Takes (typePath, versionName, majorVersion) and should return the container type T</param>
        public void SubmitVersion(string typePath, Func<string, string, int, T> candidateFunc)
        {
            try
            {
                using (var assmDef = RetryReadAssembly(typePath))
                {
                    if (assmDef == null) return;

                    BuildVersion.TryParse(assmDef.Name.Version.ToString(), out var minorVersion);
                    minorVersion.Location = typePath;
                    var mods = assmDef.Modules;
                    foreach (var mod in mods)
                    {
                        var types = mod.Types;
                        foreach (var type in types)
                        {
                            if (type.CustomAttributes.All(attr => attr.AttributeType.FullName != markerName)) continue;

                            var target = type.CustomAttributes.First(attr => attr.AttributeType.FullName == markerName);
                            var majorVersion = target.ConstructorArguments.FirstOrDefault().Value as int? ?? 0;

                            if (majorVersion < 1) throw new Exception("Major version was invalid");

                            InsertVersion(typePath, candidateFunc, majorVersion, minorVersion);
                            return;
                        }
                    }
                }
            }
            catch (FileNotFoundException fnfex)
            {
                Trace.TraceWarning("Looks like a file became unavailable mid scan: " + fnfex);
                // Scanned file is gone (deleted while being scanned)
                throw new DelayRescanException();
            }
            catch (BadImageFormatException)
            {
                // found something that Cecil can't load. Probably a native .dll file. Ignore it.
                // ReSharper disable once RedundantJumpStatement
                return;
            }
            // nothing found
        }

        /// <summary>
        /// Remove and dispose of any versions whose files have been deleted.
        /// A rescan will be needed afterwards to effect a version roll-back.
        /// </summary>
        public void ClearRemovedVersions()
        {
            lock (VersionLock)
            {
                var keys = versionTable.Keys.ToList();
                foreach (var key in keys)
                {
                    var spec = versionTable[key];
                    var vers = spec.Key;

                    if (File.Exists(vers.Location)) continue;

                    versionTable.Remove(key);
                    if ( spec.Value is IDisposable old) old.Dispose();
                }
            }
        }

        /// <summary>
        /// Get the most recent app for the given major value. Returns null if no matches
        /// </summary>
        public Result<T> GetForVersionHeader(string headerValue)
        {
            // ReSharper disable InconsistentlySynchronizedField
            if (string.IsNullOrWhiteSpace(headerValue)) return Result.Failure<T>("Version header missing");
            if (!int.TryParse(headerValue, out var major)) return Result.Failure<T>("Version header malformed");
            if (!versionTable.ContainsKey(major)) return Result.Failure<T>("No matching version loaded");

            return Result.Success(versionTable[major].Value);
            // ReSharper restore InconsistentlySynchronizedField
        }


        /// <summary>
        /// Get an exact version (if available) from a string as supplied by `VersionNames()`
        /// </summary>
        public Result<T> GetExactVersion(string requestedVersion)
        {
            var bits = requestedVersion.Split('-');

            if (bits.Length != 2) return Result.Failure<T>("Version request malformed");
            if (!int.TryParse(bits[0], out var major)) return Result.Failure<T>("Version request major malformed");
            if (!BuildVersion.TryParse(bits[1], out var minor)) return Result.Failure<T>("Version request minor malformed");

            lock (VersionLock)
            {
                if (!versionTable.ContainsKey(major)) return Result.Failure<T>("No matching version loaded in the API host");

                var found = versionTable[major];
                if ( ! found.Key.Equals(minor))  return Result.Failure<T>("The requested version has been superseded");

                return Result.Success(found.Value);
            }
        }

        /// <summary>
        /// Gets the most recent version available
        /// </summary>
        public T GetLatest()
        {
            lock (VersionLock)
            {
                var topVers = versionTable.Keys.Max();

                return versionTable[topVers].Value;
            }
        }

        /// <summary>
        /// Returns true if there are no versions loaded
        /// </summary>
        public bool IsEmpty()
        {
            // ReSharper disable InconsistentlySynchronizedField
            return versionTable.Count == 0;
            // ReSharper restore InconsistentlySynchronizedField
        }

        public string VersionsAvailable()
        {
            return _versionsAvailable;
        }

        /// <summary>
        /// Return a list of version names. Each is formatted as "{major}-{minor.x.y.z}"
        /// </summary>
        public IEnumerable<string> VersionNames()
        {
            return _versionNames;
        }

        /// <summary>
        /// Return all currently loaded values
        /// </summary>
        public IEnumerable<T> AllVersions()
        {
            lock (VersionLock)
            {
                var list = new List<T>();
                foreach (var pair in versionTable)
                {
                    list.Add(pair.Value.Value);
                }
                return list;
            }
        }

        
        public static Type GetSetupContainer(Assembly assm)
        {
            try
            {
                var allTypes = assm.GetTypes();
                return allTypes.FirstOrDefault(IsSetupContainer);
            }
            catch (ReflectionTypeLoadException rex)
            {
                var chain = string.Join("\r\n", rex.LoaderExceptions.Select(ex => ex.ToString()));
                throw new Exception(chain);
            }
        }

        private static AssemblyDefinition RetryReadAssembly(string typePath)
        {
            for (int i = 0; i < 5; i++)
            {
                try { return AssemblyDefinition.ReadAssembly(typePath); }
                catch (IOException) { Thread.Sleep(250); }
                catch (BadImageFormatException) {
                    return null;
                }
            }
            return null;
        }

        /// <summary>
        /// Try replacing existing version with incoming version
        /// </summary>
        private void InsertVersion(string typePath, Func<string, string, int, T> candidateFunc, int major, BuildVersion minorVersion)
        {
            var versionName = major + "_" + minorVersion.ToUnderscoreString();

            // TODO: close and dispose any old or rejected candidates
            var candidate = candidateFunc(typePath, versionName, major);
            lock (VersionLock)
            {
                if (versionTable.ContainsKey(major))
                {
                    var kvp = versionTable[major];
                    if (kvp.Key == minorVersion) return;
                    if (kvp.Key < minorVersion)
                    {
                        // found a newer minor version
                        if (kvp.Value is IDisposable old) old.Dispose();
                        versionTable[major] = new KeyValuePair<BuildVersion, T>(minorVersion, candidate);
                    }
                }
                else
                {
                    // first copy of this major version
                    versionTable.Add(major, new KeyValuePair<BuildVersion, T>(minorVersion, candidate));
                }
                _versionNames = versionTable.Select(kvp => kvp.Key + "-" + kvp.Value.Key).ToList();
                _versionsAvailable = string.Join(", ", VersionNames());
            }
        }

        protected Assembly Domain_AssemblyResolve(ResolveEventArgs args, DirectoryInfo directory)
        {
            var tokens = args.Name.Split(",".ToCharArray());

            var child = TryWithBaseDirectory(tokens, directory.FullName);
            if (child != null) return child;

            var host = TryWithBaseDirectory(tokens, AppDomain.CurrentDomain.BaseDirectory);
            if (host != null) return host;

            return null;
        }
        
        private Assembly TryWithBaseDirectory(string[] tokens, string baseDir)
        {
            string assemblyCulture;
            string assemblyName = tokens[0];
            string assemblyFileName = assemblyName.Replace(".resources","") + ".dll";
            string assemblyPath;
            if (tokens.Length < 2) assemblyCulture = null;
            else assemblyCulture = tokens[2].Substring(tokens[2].IndexOf('=') + 1);

            if (assemblyName.EndsWith(".resources"))
            {
                // Specific resources are located in app subdirectories
                string resourceDirectory = Path.Combine(baseDir, assemblyCulture ?? "en");

                assemblyPath = Path.Combine(resourceDirectory, assemblyName + ".dll");
                if (File.Exists(assemblyPath)) return Assembly.ReflectionOnlyLoadFrom(assemblyPath);
            }

            assemblyPath = Path.Combine(baseDir, assemblyFileName);

            try
            {
                return Assembly.ReflectionOnlyLoadFrom(assemblyPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return null;
            }
        }

        private static bool IsSetupContainer(Type arg)
        {
            var myType = typeof(ApplicationSetupPointAttribute);
            return arg.CustomAttributes.Any(a => a.AttributeType.AssemblyQualifiedName == myType.AssemblyQualifiedName);
        }
    }
}