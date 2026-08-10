using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace revit_mcp_plugin.Utils
{
    public static class PathManager
    {
        private const string ConfigFileName = "RevitMCPPlugin.dll.config";
        private const string NetworkRootPathKey = "NetworkRootPath";

        private static readonly Dictionary<string, string> _configSettingsCache = new Dictionary<string, string>();
        private static XElement _appSettingsElement;
        private static bool _configDocumentLoaded;

        /// <summary>
        /// Gets the root application data directory.
        /// Wordt overschreven door de "NetworkRootPath"-instelling in RevitMCPPlugin.dll.config,
        /// zodat Commands en Logs vanaf een netwerkschijf gebruikt kunnen worden.
        /// </summary>
        public static string GetAppDataDirectoryPath()
        {
            return GetConfigSetting(NetworkRootPathKey) ?? GetPluginDirectoryPath();
        }

        /// <summary>
        /// Gets the directory that contains the running plugin assembly itself.
        /// </summary>
        private static string GetPluginDirectoryPath()
        {
            string applicationPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            return Path.GetDirectoryName(applicationPath);
        }

        /// <summary>
        /// Reads a setting from the appSettings section of RevitMCPPlugin.dll.config, if present.
        /// Returns null when the config file or the setting is missing.
        /// Gedeeld door PathManager en McpToolsSyncService, zodat het configbestand maar één keer wordt ingelezen.
        /// </summary>
        public static string GetConfigSetting(string key)
        {
            EnsureConfigDocumentLoaded();

            if (_configSettingsCache.TryGetValue(key, out string cachedValue))
                return cachedValue;

            string value = _appSettingsElement?
                .Elements("add")
                .FirstOrDefault(e => (string)e.Attribute("key") == key)?
                .Attribute("value")?.Value;

            if (string.IsNullOrWhiteSpace(value))
                value = null;

            _configSettingsCache[key] = value;
            return value;
        }

        /// <summary>
        /// Laadt RevitMCPPlugin.dll.config eenmalig in het geheugen.
        /// </summary>
        private static void EnsureConfigDocumentLoaded()
        {
            if (_configDocumentLoaded)
                return;

            _configDocumentLoaded = true;

            try
            {
                string configPath = Path.Combine(GetPluginDirectoryPath(), ConfigFileName);
                if (File.Exists(configPath))
                {
                    var document = XDocument.Load(configPath);
                    _appSettingsElement = document.Root?.Element("appSettings");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading {ConfigFileName}: {ex.Message}");
            }
        }
        /// <summary>
        /// Gets the path to the Commands directory
        /// </summary>
        public static string GetCommandsDirectoryPath()
        {
            string appDataDirectory = GetAppDataDirectoryPath();
            string commandsDirectory = Path.Combine(appDataDirectory, "Commands");

            EnsureDirectoryExists(commandsDirectory);

            return commandsDirectory;
        }
        /// <summary>
        /// Gets the path to the Logs directory
        /// </summary>
        public static string GetLogsDirectoryPath()
        {
            string appDataDirectory = GetAppDataDirectoryPath();
            string logsDirectory = Path.Combine(appDataDirectory, "Logs");

            EnsureDirectoryExists(logsDirectory);

            return logsDirectory;
        }
        /// <summary>
        /// Gets the path to the command registry file.
        /// If the file doesn't exist, creates it with default content.
        /// </summary>
        /// <param name="createIfNotExists">Whether to create a default file if it doesn't exist (default: true)</param>
        /// <returns>Path to the command registry file</returns>
        public static string GetCommandRegistryFilePath(bool createIfNotExists = true)
        {
            string commandsDirectory = GetCommandsDirectoryPath();
            string registryFilePath = Path.Combine(commandsDirectory, "commandRegistry.json");

            if (createIfNotExists && !File.Exists(registryFilePath))
            {
                CreateDefaultCommandRegistryFile(registryFilePath);
            }

            return registryFilePath;
        }
        /// <summary>
        /// Creates a default command registry file with empty commands array
        /// </summary>
        /// <param name="filePath">Path where to create the file</param>
        private static void CreateDefaultCommandRegistryFile(string filePath)
        {
            try
            {
                var defaultRegistry = new { commands = new object[] { } };
                string jsonContent = JsonConvert.SerializeObject(defaultRegistry, Formatting.Indented);

                File.WriteAllText(filePath, jsonContent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating default command registry file: {ex.Message}");
            }
        }
        /// <summary>
        /// Ensures that the specified directory exists
        /// </summary>
        /// <param name="directoryPath">The path to check and create if needed</param>
        private static void EnsureDirectoryExists(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
        }
    }
}
