using RevitMCPSDK.API.Interfaces;
using revit_mcp_plugin.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace revit_mcp_plugin.Core
{
    /// <summary>
    /// Spiegelt de volledige build-map van de MCP-server (server/build, dus index.js, register.js,
    /// utils/ en tools/ samen) vanaf een gedeelde netwerklocatie naar deze machine, zodat een team
    /// de MCP-server centraal kan beheren: een nieuwe build op de netwerkschijf zetten is voldoende,
    /// de volgende keer dat de plugin (dus Revit) opstart wordt de lokale build-map bijgewerkt.
    /// </summary>
    public static class McpServerSyncService
    {
        private const string NetworkBuildPathKey = "NetworkMcpServerBuildPath";
        private const string LocalBuildPathKey = "LocalMcpServerBuildPath";

        public static void Sync(ILogger logger)
        {
            try
            {
                string networkBuildPath = PathManager.GetConfigSetting(NetworkBuildPathKey);
                string localBuildPath = PathManager.GetConfigSetting(LocalBuildPathKey);

                if (string.IsNullOrWhiteSpace(networkBuildPath) || string.IsNullOrWhiteSpace(localBuildPath))
                {
                    logger.Info("MCP-server synchronisatie overgeslagen: {0} en/of {1} is niet ingesteld in RevitMCPPlugin.dll.config.",
                        NetworkBuildPathKey, LocalBuildPathKey);
                    return;
                }

                if (!Directory.Exists(networkBuildPath))
                {
                    logger.Warning("MCP-server synchronisatie overgeslagen: netwerkmap niet bereikbaar: {0}", networkBuildPath);
                    return;
                }

                int copied = 0;
                int removed = 0;
                SyncDirectory(networkBuildPath, localBuildPath, ref copied, ref removed);

                logger.Info("MCP-server build gesynchroniseerd vanaf {0}: {1} bestand(en) gekopieerd, {2} verwijderd.",
                    networkBuildPath, copied, removed);
            }
            catch (Exception ex)
            {
                logger.Error("MCP-server synchronisatie mislukt: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Spiegelt sourceDir naar destDir: kopieert nieuwe/gewijzigde bestanden en submappen, en
        /// verwijdert lokale bestanden/submappen die niet (meer) in sourceDir voorkomen. Wordt
        /// recursief aangeroepen voor elke submap (bv. utils/, tools/).
        /// </summary>
        private static void SyncDirectory(string sourceDir, string destDir, ref int copied, ref int removed)
        {
            Directory.CreateDirectory(destDir);

            string[] sourceFiles = Directory.GetFiles(sourceDir);
            HashSet<string> sourceFileNames = new HashSet<string>(
                sourceFiles.Select(Path.GetFileName), StringComparer.OrdinalIgnoreCase);

            foreach (string sourceFile in sourceFiles)
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(sourceFile));

                bool isNewOrChanged = !File.Exists(destFile) ||
                    File.GetLastWriteTimeUtc(sourceFile) > File.GetLastWriteTimeUtc(destFile);

                if (isNewOrChanged)
                {
                    File.Copy(sourceFile, destFile, overwrite: true);
                    copied++;
                }
            }

            foreach (string existingFile in Directory.GetFiles(destDir))
            {
                if (!sourceFileNames.Contains(Path.GetFileName(existingFile)))
                {
                    File.Delete(existingFile);
                    removed++;
                }
            }

            string[] sourceSubDirs = Directory.GetDirectories(sourceDir);
            HashSet<string> sourceSubDirNames = new HashSet<string>(
                sourceSubDirs.Select(Path.GetFileName), StringComparer.OrdinalIgnoreCase);

            foreach (string sourceSubDir in sourceSubDirs)
            {
                string destSubDir = Path.Combine(destDir, Path.GetFileName(sourceSubDir));
                SyncDirectory(sourceSubDir, destSubDir, ref copied, ref removed);
            }

            foreach (string existingSubDir in Directory.GetDirectories(destDir))
            {
                if (!sourceSubDirNames.Contains(Path.GetFileName(existingSubDir)))
                {
                    Directory.Delete(existingSubDir, recursive: true);
                    removed++;
                }
            }
        }
    }
}
