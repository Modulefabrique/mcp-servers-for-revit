using Newtonsoft.Json;
using revit_mcp_plugin.Configuration;
using revit_mcp_plugin.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
namespace revit_mcp_plugin.UI
{
    /// <summary>
    /// Interaction logic for CommandSetSettingsPage.xaml
    /// </summary>
    public partial class CommandSetSettingsPage : Page
    {
        private ObservableCollection<CommandSet> commandSets;
        private ObservableCollection<CommandConfig> currentCommands;

        public CommandSetSettingsPage()
        {
            InitializeComponent();
            // Initialize data collections
            commandSets = new ObservableCollection<CommandSet>();
            currentCommands = new ObservableCollection<CommandConfig>();
            // Set data bindings
            CommandSetListBox.ItemsSource = commandSets;
            FeaturesListView.ItemsSource = currentCommands;
            // Load command sets
            LoadCommandSets();
            // Initial state
            NoSelectionTextBlock.Visibility = Visibility.Visible;
        }

        private void LoadCommandSets()
        {
            try
            {
                commandSets.Clear();
                string commandsDirectory = PathManager.GetCommandsDirectoryPath();
                string registryFilePath = PathManager.GetCommandRegistryFilePath();
                // 1. First load all command set folders, establish available command collections
                Dictionary<string, CommandSet> availableCommandSets = new Dictionary<string, CommandSet>();
                HashSet<string> availableCommandNames = new HashSet<string>();
                // Get all command set directories
                string[] commandSetDirectories = Directory.GetDirectories(commandsDirectory);
                foreach (var directory in commandSetDirectories)
                {
                    // Skip special folders or hidden folders
                    if (Path.GetFileName(directory).StartsWith("."))
                        continue;
                    string commandJsonPath = Path.Combine(directory, "command.json");
                    // If there's a command.json, load it
                    if (File.Exists(commandJsonPath))
                    {
                        string commandJson = File.ReadAllText(commandJsonPath);
                        var commandSetData = JsonConvert.DeserializeObject<CommandJson>(commandJson);
                        if (commandSetData != null)
                        {
                            var newCommandSet = new CommandSet
                            {
                                Name = commandSetData.Name,
                                Description = commandSetData.Description,
                                Commands = new List<CommandConfig>()
                            };

                            // Detecteer ondersteunde Revit-versies - bepaald aan de hand van jaartal-submappen
                            List<string> supportedVersions = new List<string>();
                            var versionDirectories = Directory.GetDirectories(directory)
                                .Select(Path.GetFileName)
                                .Where(name => int.TryParse(name, out _))
                                .ToList();

                            // Loop through each command
                            foreach (var command in commandSetData.Commands)
                            {
                                // Maak een commandoconfiguratie aan, waarbij de ondersteunde versies worden bepaald door de mappen te controleren
                                List<string> supportedCommandVersions = new List<string>();
                                string dllBasePath = null;

                                foreach (var version in versionDirectories)
                                {
                                    string versionDirectory = Path.Combine(directory, version);
                                    string versionDllPath = null;

                                    if (!string.IsNullOrEmpty(command.AssemblyPath))
                                    {
                                        // Als een relatief pad is opgegeven, zoek dan in de versie-submap
                                        versionDllPath = Path.Combine(versionDirectory, command.AssemblyPath);
                                        if (File.Exists(versionDllPath))
                                        {
                                            // Sla het basis-padsjabloon op
                                            if (dllBasePath == null)
                                            {
                                                // Haal het relatieve pad op om een sjabloon te maken
                                                dllBasePath = Path.Combine(commandSetData.Name, "{VERSION}", command.AssemblyPath);
                                            }
                                            supportedCommandVersions.Add(version);
                                        }
                                    }
                                    else
                                    {
                                        // Als er geen pad is opgegeven, zoek dan naar een willekeurige DLL in de versie-submap
                                        var dllFiles = Directory.GetFiles(versionDirectory, "*.dll");
                                        if (dllFiles.Length > 0)
                                        {
                                            versionDllPath = dllFiles[0]; // Gebruik de eerste gevonden DLL
                                            if (dllBasePath == null)
                                            {
                                                // Haal de DLL-bestandsnaam op
                                                string dllFileName = Path.GetFileName(versionDllPath);
                                                dllBasePath = Path.Combine(commandSetData.Name, "{VERSION}", dllFileName);
                                            }
                                            supportedCommandVersions.Add(version);
                                        }
                                    }
                                }

                                // Als ten minste één versie dit commando ondersteunt
                                if (supportedCommandVersions.Count > 0 && dllBasePath != null)
                                {
                                    // Maak de commandoconfiguratie aan
                                    var commandConfig = new CommandConfig
                                    {
                                        CommandName = command.CommandName,
                                        Description = command.Description,
                                        // Gebruik het pad met de versie-plaatshouder
                                        AssemblyPath = dllBasePath,
                                        Enabled = false,
                                        // Sla alle ondersteunde versies op
                                        SupportedRevitVersions = supportedCommandVersions.ToArray()
                                    };

                                    // Voeg toe aan de commandolijst
                                    newCommandSet.Commands.Add(commandConfig);
                                    availableCommandNames.Add(command.CommandName);
                                }
                            }

                            // Als er beschikbare commando's zijn, voeg toe aan de lijst met commandosets
                            if (newCommandSet.Commands.Any())
                            {
                                availableCommandSets[commandSetData.Name] = newCommandSet;
                            }
                        }
                    }
                }
                // 2. Load registry, update command enabled status, and clean up non-existent commands
                if (File.Exists(registryFilePath))
                {
                    string registryJson = File.ReadAllText(registryFilePath);
                    var registry = JsonConvert.DeserializeObject<CommandRegistryJson>(registryJson);
                    if (registry?.Commands != null)
                    {
                        // Keep only valid commands
                        List<CommandConfig> validCommands = new List<CommandConfig>();
                        foreach (var registryItem in registry.Commands)
                        {
                            if (availableCommandNames.Contains(registryItem.CommandName))
                            {
                                validCommands.Add(registryItem);
                                // Update the enabled status of this command in all command sets
                                foreach (var commandSet in availableCommandSets.Values)
                                {
                                    var command = commandSet.Commands.FirstOrDefault(c => c.CommandName == registryItem.CommandName);
                                    if (command != null)
                                    {
                                        command.Enabled = registryItem.Enabled;
                                    }
                                }
                            }
                        }
                        // If there are invalid commands, update the registry file
                        if (validCommands.Count != registry.Commands.Count)
                        {
                            registry.Commands = validCommands;
                            string updatedJson = JsonConvert.SerializeObject(registry, Formatting.Indented);
                            File.WriteAllText(registryFilePath, updatedJson);
                        }
                    }
                }
                // 3. Add command sets to the UI collection
                foreach (var commandSet in availableCommandSets.Values)
                {
                    commandSets.Add(commandSet);
                }
                // If no command sets found, display a message
                if (commandSets.Count == 0)
                {
                    MessageBox.Show("No command sets found. Please check if the Commands folder exists and contains valid command sets.",
                                  "No Command Sets", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading command sets: {ex.Message}", "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CommandSetListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            currentCommands.Clear();
            var selectedCommandSet = CommandSetListBox.SelectedItem as CommandSet;
            if (selectedCommandSet != null)
            {
                NoSelectionTextBlock.Visibility = Visibility.Collapsed;
                FeaturesHeaderTextBlock.Text = $"{selectedCommandSet.Name} - Command List";
                // Load commands from selected command set
                foreach (var command in selectedCommandSet.Commands)
                {
                    currentCommands.Add(command);
                }
            }
            else
            {
                NoSelectionTextBlock.Visibility = Visibility.Visible;
                FeaturesHeaderTextBlock.Text = "Command List";
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            // Save current selection state
            var selectedIndex = CommandSetListBox.SelectedIndex;
            // Reload command sets
            LoadCommandSets();
            // Restore selection
            if (selectedIndex >= 0 && selectedIndex < commandSets.Count)
            {
                CommandSetListBox.SelectedIndex = selectedIndex;
            }
        }

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            // Only operate on the currently displayed commands
            if (currentCommands.Count > 0)
            {
                foreach (var command in currentCommands)
                {
                    command.Enabled = true;
                }

                // Refresh the UI
                FeaturesListView.Items.Refresh();
            }
        }

        private void UnselectAllButton_Click(object sender, RoutedEventArgs e)
        {
            // Only operate on the currently displayed commands
            if (currentCommands.Count > 0)
            {
                foreach (var command in currentCommands)
                {
                    command.Enabled = false;
                }

                // Refresh the UI
                FeaturesListView.Items.Refresh();
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string registryFilePath = PathManager.GetCommandRegistryFilePath();
                // Lees het bestaande register om de volledige commando-informatie te behouden
                Dictionary<string, CommandConfig> existingCommandsDict = new Dictionary<string, CommandConfig>();
                if (File.Exists(registryFilePath))
                {
                    string registryJson = File.ReadAllText(registryFilePath);
                    var existingRegistry = JsonConvert.DeserializeObject<CommandRegistryJson>(registryJson);
                    if (existingRegistry?.Commands != null)
                    {
                        foreach (var cmd in existingRegistry.Commands)
                        {
                            existingCommandsDict[cmd.CommandName] = cmd;
                        }
                    }
                }
                // Maak een nieuw registry-object aan
                CommandRegistryJson registry = new CommandRegistryJson();
                registry.Commands = new List<CommandConfig>();
                // Verzamel alle 'ingeschakelde' commando's
                foreach (var commandSet in commandSets)
                {
                    // Probeer de ontwikkelaarsinformatie op te halen uit command.json
                    var commandSetDeveloper = new DeveloperInfo { Name = "Unspecified", Email = "Unspecified" };
                    string commandJsonPath = Path.Combine(PathManager.GetCommandsDirectoryPath(),
                        commandSet.Name, "command.json");
                    if (File.Exists(commandJsonPath))
                    {
                        try
                        {
                            string commandJson = File.ReadAllText(commandJsonPath);
                            var commandSetData = JsonConvert.DeserializeObject<CommandJson>(commandJson);
                            if (commandSetData != null)
                            {
                                commandSetDeveloper = commandSetData.Developer ?? commandSetDeveloper;
                            }
                        }
                        catch { /* Gebruik de standaardwaarde als het parsen mislukt */ }
                    }

                    foreach (var command in commandSet.Commands)
                    {
                        // Voeg alleen ingeschakelde commando's toe aan het register
                        if (command.Enabled)
                        {
                            CommandConfig newConfig;
                            // Controleer of het commando al bestaat in het eerdere register
                            if (existingCommandsDict.ContainsKey(command.CommandName))
                            {
                                // Indien aanwezig, behoud de bestaande informatie en werk alleen de ingeschakelde status en het padsjabloon bij
                                newConfig = existingCommandsDict[command.CommandName];
                                newConfig.Enabled = true;
                                newConfig.AssemblyPath = command.AssemblyPath;
                                newConfig.SupportedRevitVersions = command.SupportedRevitVersions;
                            }
                            else
                            {
                                // Als het een nieuw commando is, maak een nieuwe configuratie aan
                                newConfig = new CommandConfig
                                {
                                    CommandName = command.CommandName,
                                    AssemblyPath = command.AssemblyPath ?? "",
                                    Enabled = true,
                                    Description = command.Description,
                                    SupportedRevitVersions = command.SupportedRevitVersions,
                                    Developer = commandSetDeveloper
                                };
                            }
                            registry.Commands.Add(newConfig);
                        }
                    }
                }
                // Stel een samenvatting samen om weer te geven
                string enabledFeaturesText = "";
                int enabledCount = registry.Commands.Count;
                foreach (var command in registry.Commands)
                {
                    string commandSetName = commandSets
                        .FirstOrDefault(cs => cs.Commands.Any(c => c.CommandName == command.CommandName))?.Name ?? "Unknown";
                    string versions = command.SupportedRevitVersions != null && command.SupportedRevitVersions.Any()
                        ? $" (Revit {string.Join(", ", command.SupportedRevitVersions)})"
                        : "";
                    enabledFeaturesText += $"• {commandSetName}: {command.CommandName}\n";
                }
                // Serialiseer en sla op in een bestand
                string json = JsonConvert.SerializeObject(registry, Formatting.Indented);
                File.WriteAllText(registryFilePath, json);
                MessageBox.Show($"Command set settings successfully saved!\n\nEnabled {enabledCount} commands:\n{enabledFeaturesText}",
                              "Settings Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving settings: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start("explorer.exe", PathManager.GetCommandsDirectoryPath());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening Commands folder: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // Data models
    public class CommandSet
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<CommandConfig> Commands { get; set; } = new List<CommandConfig>();
    }
    // Configuration files
    public class CommandRegistryJson
    {
        public List<CommandConfig> Commands { get; set; } = new List<CommandConfig>();
    }

    public class CommandJson
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<CommandItemJson> Commands { get; set; } = new List<CommandItemJson>();
        public DeveloperInfo Developer { get; set; }
        public List<string> SupportedRevitVersions { get; set; } = new List<string>();
    }

    public class CommandItemJson
    {
        public string CommandName { get; set; }
        public string Description { get; set; }
        public string AssemblyPath { get; set; }
    }
}