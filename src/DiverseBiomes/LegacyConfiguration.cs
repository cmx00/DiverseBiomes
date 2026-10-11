using System.IO;
using BepInEx;
using BepInEx.Configuration;

namespace DiverseBiomes
{
    internal static class LegacyConfiguration
    {
        private static ConfigFile legacy;
        private static bool checkedFile;
        internal static ConfigEntry<T> Bind<T>(ConfigFile config, string section, string key, T fallback, ConfigDescription description)
        {
            if (!checkedFile)
            {
                checkedFile = true;
                string path = Path.Combine(Paths.ConfigPath, "Zincon.DSPVegetationScale.cfg");
                if (File.Exists(path)) legacy = new ConfigFile(path, false) { SaveOnConfigSet = false };
            }
            return config.Bind(section, key, legacy == null ? fallback : legacy.Bind(section, key, fallback).Value, description);
        }
        internal static ConfigEntry<T> Bind<T>(ConfigFile config, string section, string key, T fallback, string description)
            => Bind(config, section, key, fallback, new ConfigDescription(description));
    }
}
