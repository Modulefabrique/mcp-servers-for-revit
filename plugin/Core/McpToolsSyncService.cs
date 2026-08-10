using RevitMCPSDK.API.Interfaces;
using revit_mcp_plugin.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace revit_mcp_plugin.Core
{
    /// <summary>
    /// Spiegelt de tools-map van de MCP-server (server/build/tools) vanaf een gedeelde
    /// netwerklocatie naar deze machine, zodat een team de MCP-tools centraal kan beheren:
    /// een tool-bestand toevoegen/wijzigen/verwijderen op de netwerkschijf is voldoende,
    /// de volgende keer dat de plugin (dus Revit) opstart wordt de lokale map bijgewerkt.
    /// </summary>
    public static class McpToolsSyncService
    {
        private const string NetworkToolsPathKey = "NetworkMcpToolsPath";
        private const string LocalToolsPathKey = "LocalMcpServerToolsPath";

        // register.js is de tool-loader zelf (zie server/src/tools/register.ts) en wordt nooit
        // verwijderd, ook niet als de netwerkmap hem niet bevat - anders laadt de MCP-server
        // straks helemaal geen tools meer.
        private const string ProtectedFileName = "register.js";

        public static void Sync(ILogger logger)
        {
            try
            {
                string networkToolsPath = PathManager.GetConfigSetting(NetworkToolsPathKey);
                string localToolsPath = PathManager.GetConfigSetting(LocalToolsPathKey);

                if (string.IsNullOrWhiteSpace(networkToolsPath) || string.IsNullOrWhiteSpace(localToolsPath))
                {
                    logger.Info("MCP-tools synchronisatie overgeslagen: {0} en/of {1} is niet ingesteld in RevitMCPPlugin.dll.config.",
                        NetworkToolsPathKey, LocalToolsPathKey);
                    return;
                }

                if (!Directory.Exists(networkToolsPath))
                {
                    logger.Warning("MCP-tools synchronisatie overgeslagen: netwerkmap niet bereikbaar: {0}", networkToolsPath);
                    return;
                }

                Directory.CreateDirectory(localToolsPath);

                string[] networkFiles = Directory.GetFiles(networkToolsPath);
                // Geen Enumerable.ToHashSet() gebruiken: die extensie bestaat niet op .NET Framework 4.8 (Revit 2020-2024).
                HashSet<string> networkFileNames = new HashSet<string>(
                    networkFiles.Select(Path.GetFileName), StringComparer.OrdinalIgnoreCase);

                int copied = CopyNewOrChangedFiles(networkFiles, localToolsPath);
                int removed = RemoveFilesNotOnNetwork(localToolsPath, networkFileNames);

                logger.Info("MCP-tools gesynchroniseerd vanaf {0}: {1} bestand(en) gekopieerd, {2} verwijderd.",
                    networkToolsPath, copied, removed);
            }
            catch (Exception ex)
            {
                logger.Error("MCP-tools synchronisatie mislukt: {0}", ex.Message);
            }
        }

        private static int CopyNewOrChangedFiles(string[] networkFiles, string localToolsPath)
        {
            int copied = 0;

            foreach (string sourceFile in networkFiles)
            {
                string destFile = Path.Combine(localToolsPath, Path.GetFileName(sourceFile));

                bool isNewOrChanged = !File.Exists(destFile) ||
                    File.GetLastWriteTimeUtc(sourceFile) > File.GetLastWriteTimeUtc(destFile);

                if (isNewOrChanged)
                {
                    File.Copy(sourceFile, destFile, overwrite: true);
                    copied++;
                }
            }

            return copied;
        }

        private static int RemoveFilesNotOnNetwork(string localToolsPath, HashSet<string> networkFileNames)
        {
            int removed = 0;

            foreach (string existingFile in Directory.GetFiles(localToolsPath))
            {
                string fileName = Path.GetFileName(existingFile);

                if (string.Equals(fileName, ProtectedFileName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!networkFileNames.Contains(fileName))
                {
                    File.Delete(existingFile);
                    removed++;
                }
            }

            return removed;
        }
    }
}
