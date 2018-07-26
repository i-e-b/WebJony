using System.Configuration;
using System.IO;
using System.Reflection;
using SkinnyJson;

namespace WrapperCommon.Security
{
    /// <summary>
    /// Security setting and keys loaded from JSON file.
    /// If the file is not available, settings will be blank.
    /// </summary>
    public static class SecuritySettings {
        public static readonly SecurityConfig Config;

        static SecuritySettings()
        {
            try
            {
                // Try to load settings:
                var path = ConfigurationManager.AppSettings["SecurityConfigFile"];
                if (!File.Exists(path)) {
                    // try to find next to assembly:
                    path = Path.Combine(Assembly.GetExecutingAssembly().CodeBase.Replace("file:///", ""), "../security.json");
                    if (!File.Exists(path)) return;
                }

                Config = Json.Defrost<SecurityConfig>(File.ReadAllText(path));
            }
            catch
            {
                Config = new SecurityConfig();
            }
        }
    }
}