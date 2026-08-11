using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;
using RevitMCPSDK.API.Utils;
using revit_mcp_plugin.Configuration;
using revit_mcp_plugin.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace revit_mcp_plugin.Core
{
    /// <summary>
    /// <para>Command Manager</para>
    /// </summary>
    public class CommandManager
    {
        private readonly ICommandRegistry _commandRegistry;
        private readonly ILogger _logger;
        private readonly ConfigurationManager _configManager;
        private readonly UIApplication _uiApplication;
        private readonly RevitVersionAdapter _versionAdapter;

        // Mappen waarin een command-assembly is gevonden. Assembly.Load(byte[]) laadt (in
        // tegenstelling tot Assembly.LoadFrom) een assembly zonder bijbehorend pad, waardoor het
        // CLR niet meer automatisch in dezelfde map naar de dependencies van die assembly zoekt
        // (bv. Microsoft.CodeAnalysis(.CSharp).dll, Nice3point.Revit.Toolkit/Extensions.dll).
        // _assemblyResolveHandler haalt die mappen hier op om dependencies alsnog te vinden.
        private static readonly HashSet<string> _knownAssemblyDirectories =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _knownAssemblyDirectoriesLock = new object();
        private static bool _resolveHandlerRegistered;

        /// <summary>
        /// Manager in charge of loading and managing commands.
        /// </summary>
        /// <param name="commandRegistry"></param>
        /// <param name="logger"></param>
        /// <param name="configManager"></param>
        /// <param name="uiApplication"></param>
        public CommandManager(
            ICommandRegistry commandRegistry,
            ILogger logger,
            ConfigurationManager configManager,
            UIApplication uiApplication)
        {
            _commandRegistry = commandRegistry;
            _logger = logger;
            _configManager = configManager;
            _uiApplication = uiApplication;
            _versionAdapter = new RevitVersionAdapter(_uiApplication.Application);

            EnsureAssemblyResolveHandlerRegistered();
        }

        /// <summary>
        /// Registreert eenmalig een AppDomain.AssemblyResolve-handler die dependencies van
        /// command-assembly's alsnog vindt in de map waaruit die command-assembly geladen is
        /// (lokaal of op de netwerkschijf) - het gedrag dat Assembly.LoadFrom gratis gaf, maar
        /// Assembly.Load(byte[]) niet.
        /// </summary>
        private void EnsureAssemblyResolveHandlerRegistered()
        {
            lock (_knownAssemblyDirectoriesLock)
            {
                if (_resolveHandlerRegistered)
                    return;

                _resolveHandlerRegistered = true;
                AppDomain.CurrentDomain.AssemblyResolve += ResolveCommandDependency;
            }
        }

        private static Assembly ResolveCommandDependency(object sender, ResolveEventArgs args)
        {
            string dependencyFileName = new AssemblyName(args.Name).Name + ".dll";

            string[] directoriesToSearch;
            lock (_knownAssemblyDirectoriesLock)
            {
                directoriesToSearch = _knownAssemblyDirectories.ToArray();
            }

            foreach (string directory in directoriesToSearch)
            {
                string candidatePath = Path.Combine(directory, dependencyFileName);
                if (File.Exists(candidatePath))
                {
                    try
                    {
                        return Assembly.Load(File.ReadAllBytes(candidatePath));
                    }
                    catch
                    {
                        // Probeer de volgende map als deze kandidaat niet geladen kan worden.
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// <para>Load all commands specified in the configuration file.</para>
        /// </summary>
        public void LoadCommands()
        {
            _logger.Info("Start loading command.");
            string currentVersion = _versionAdapter.GetRevitVersion();
            _logger.Info("Current Revit version: {0}", currentVersion);

            // Load external commands from the configuration file.
            foreach (var commandConfig in _configManager.Config.Commands)
            {
                try
                {
                    if (!commandConfig.Enabled)
                    {
                        _logger.Info("Skipping disabled command: {0}", commandConfig.CommandName);
                        continue;
                    }

                    // Check Revit version compatibility.
                    if (commandConfig.SupportedRevitVersions != null &&
                        commandConfig.SupportedRevitVersions.Length > 0 &&
                        !_versionAdapter.IsVersionSupported(commandConfig.SupportedRevitVersions))
                    {
                        _logger.Warning("The command {0} is not supported by the current Revit version ({1}} and it has been skipped.",
                            commandConfig.CommandName, currentVersion);
                        continue;
                    }

                    // Replace version placeholder strings in paths.
                    commandConfig.AssemblyPath = commandConfig.AssemblyPath.Contains("{VERSION}")
                        ? commandConfig.AssemblyPath.Replace("{VERSION}", currentVersion)
                        : commandConfig.AssemblyPath;

                    // Load external command assembly.
                    LoadCommandFromAssembly(commandConfig);
                }
                catch (Exception ex)
                {
                    _logger.Error("Failed to load command {0}: {1}", commandConfig.CommandName, ex.Message);
                }
            }

            _logger.Info("Command loading complete.");
        }

        /// <summary>
        /// Loads specific commands in specific assemblies.
        /// </summary>
        /// <param name="config">Configuration class describing the command.</param>
        private void LoadCommandFromAssembly(CommandConfig config)
        {
            try
            {
                // Determine the assembly path.
                string assemblyPath = config.AssemblyPath;
                if (!Path.IsPathRooted(assemblyPath))
                {
                    // If it is not an absolute path, then it is relative to the Command's directory.
                    string baseDir = PathManager.GetCommandsDirectoryPath();
                    assemblyPath = Path.Combine(baseDir, assemblyPath);
                }

                if (!File.Exists(assemblyPath))
                {
                    _logger.Error("Command assembly does not exist: {0}", assemblyPath);
                    return;
                }

                // Zorg dat ResolveCommandDependency in deze map kan zoeken naar dependencies
                // van deze command-assembly (zie EnsureAssemblyResolveHandlerRegistered hierboven).
                lock (_knownAssemblyDirectoriesLock)
                {
                    _knownAssemblyDirectories.Add(Path.GetDirectoryName(assemblyPath));
                }

                // Load assembly from bytes rather than Assembly.LoadFrom: this sidesteps .NET Framework's
                // loadFromRemoteSources restriction when assemblyPath resolves to a network share.
                byte[] rawAssembly = File.ReadAllBytes(assemblyPath);
                Assembly assembly = Assembly.Load(rawAssembly);

                // Find types that implement the IRevitCommand interface.
                Type[] typesInAssembly;
                try
                {
                    typesInAssembly = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException typeLoadEx)
                {
                    // GetTypes() gooit deze exception als één of meer dependencies niet gevonden
                    // konden worden (ook na ResolveCommandDependency). ex.Message alleen ("Unable to
                    // load one or more of the requested types...") verklapt niet welke dependency
                    // het probleem is - LoaderExceptions wel.
                    string details = string.Join("; ",
                        typeLoadEx.LoaderExceptions.Where(e => e != null).Select(e => e.Message));
                    _logger.Error("Kon niet alle types laden uit {0}: {1}", Path.GetFileName(assemblyPath), details);

                    // Types die wél geladen konden worden, staan alsnog in typeLoadEx.Types (met null
                    // op de plek van elk type dat mislukte) - daarmee proberen we nog steeds door te gaan.
                    typesInAssembly = typeLoadEx.Types.Where(t => t != null).ToArray();
                }

                foreach (Type type in typesInAssembly)
                {
                    if (typeof(RevitMCPSDK.API.Interfaces.IRevitCommand).IsAssignableFrom(type) &&
                        !type.IsInterface &&
                        !type.IsAbstract)
                    {
                        try
                        {
                            // Create a command instance.
                            RevitMCPSDK.API.Interfaces.IRevitCommand command;

                            // Check whether the command implements the initializable interface.
                            if (typeof(IRevitCommandInitializable).IsAssignableFrom(type))
                            {
                                // Create instance and initialize.
                                command = (IRevitCommand)Activator.CreateInstance(type);
                                ((IRevitCommandInitializable)command).Initialize(_uiApplication);
                            }
                            else
                            {
                                // Try searching for constructors that accept UIApplication.
                                var constructor = type.GetConstructor(new[] { typeof(UIApplication) });
                                if (constructor != null)
                                {
                                    command = (IRevitCommand)constructor.Invoke(new object[] { _uiApplication });
                                }
                                else
                                {
                                    // Use a parameterless constructor.
                                    command = (IRevitCommand)Activator.CreateInstance(type);
                                }
                            }

                            // Check whether the command name matches the configuration.
                            if (command.CommandName == config.CommandName)
                            {
                                _commandRegistry.RegisterCommand(command);
                                _logger.Info("Command instance aangemaakt [{0}]: {1}",
                                    command.CommandName, Path.GetFileName(assemblyPath));
                                break; // Exit the loop after finding a matching command.
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Error("Failed to create command instance [{0}]: {1}", type.FullName, ex.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to load command assembly: {0}", ex.Message);
            }
        }
    }
}
