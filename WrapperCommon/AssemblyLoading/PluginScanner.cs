using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WrapperCommon.AssemblyLoading
{
    /// <summary>
    /// Watch for changes to a plugin directory for the setup marker attribute. Supplies paths to DLLs.
    /// <para>Keeps a list of last seen implementing assemblies and exposes a change event</para>
    /// </summary>
    public class PluginScanner
    {
        private readonly string _pluginDirectory;
        private readonly HashSet<string> _knownPlugins;
        private static readonly object scanLock = new object();


        public PluginScanner(string pluginDirectory)
        {
            if (!Directory.Exists(pluginDirectory)) throw new Exception("Can't watch \"" + pluginDirectory + "\", might not exist or not enough permissions");
            _pluginDirectory = pluginDirectory;

            _knownPlugins = new HashSet<string>();

            CurrentlyAvailable = new string[0];
        }

        public event EventHandler<PluginsChangedEventArgs<string>> PluginsChanged;

        /// <summary>
        /// An enumerable of file paths, each to a discovered DLL
        /// </summary>
        public IEnumerable<string> CurrentlyAvailable { get; protected set; }

        protected virtual void OnPluginsChanged()
        {
            PluginsChanged?.Invoke(this, new PluginsChangedEventArgs<string> { AvailablePlugins = CurrentlyAvailable.ToArray() });
        }

        /// <summary>
        /// Try to rescan for available plug-ins
        /// </summary>
        public void RefreshPlugins()
        {
            RefreshTarget(_pluginDirectory);
        }

        /// <summary>
        /// To to scan just a single target for changes.
        /// Returns true if new versions were found.
        /// </summary>
        public void RefreshTarget(string targetDirectory) {
            lock (scanLock)
            {
                var everything = Directory
                    .EnumerateFiles(targetDirectory, "*.dll", SearchOption.AllDirectories)
                    .Where(NotSystemDll);

                var newPlugins = new HashSet<string>(everything);

                _knownPlugins.UnionWith(newPlugins);
                CurrentlyAvailable = _knownPlugins.ToArray();
            }

            OnPluginsChanged();
        }

        /// <summary>
        /// Reject common dependencies included by Web sites and Web APIs
        /// </summary>
        private bool NotSystemDll(string path)
        {
            var name = Path.GetFileName(path) ?? "";
            if (name.StartsWith("Microsoft.")) return false;
            if (name.StartsWith("System.")) return false;
            if (name.StartsWith("Antlr3.")) return false;
            if (name.StartsWith("Newtonsoft.")) return false;
            if (name.StartsWith("WebGrease.")) return false;

            return true;
        }
    }
}