using System;
using Autodesk.Revit.UI;
using System.Reflection;
using System.Windows.Media.Imaging;



namespace revit_mcp_plugin.Core
{
    public class Application : IExternalApplication
    {
        // Referentie naar de toggle-knop zodat MCPServiceConnection het icoon kan bijwerken
        // nadat de server is gestart/gestopt.
        private static PushButton _toggleButton;

        public Result OnStartup(UIControlledApplication application)
        {
            // aanmaken ribbontab niet nodig. bestaat al vanuit modulefabrique addin
            string myRibbon = "ModuleFabrique";
            RibbonPanel mcpPanel;
            try
            {
                mcpPanel = application.CreateRibbonPanel(myRibbon,"MCP");
            }
            catch (Exception)
            {
                throw new Exception("Ribbonpanel 'MCP' kan niet worden aangemaakt. is modulefabrique addin al geladen?");
            }
            
            PushButtonData pushButtonData = new PushButtonData("ID_EXCMD_TOGGLE_REVIT_MCP", "Revit MCP\r\n Switch",
                Assembly.GetExecutingAssembly().Location, "revit_mcp_plugin.Core.MCPServiceConnection");
            pushButtonData.ToolTip = "Open / Close mcp server";
            _toggleButton = mcpPanel.AddItem(pushButtonData) as PushButton;

            // Toon direct de juiste icoon-status: de server start niet automatisch op, dus dicht (zwart).
            UpdateToggleButtonIcon(isRunning: false);

            //PushButtonData mcp_settings_pushButtonData = new PushButtonData("ID_EXCMD_MCP_SETTINGS", "Settings",
            //    Assembly.GetExecutingAssembly().Location, "revit_mcp_plugin.Core.Settings");
            //mcp_settings_pushButtonData.ToolTip = "MCP Settings";
            //mcp_settings_pushButtonData.Image = new BitmapImage(new Uri("/RevitMCPPlugin;component/Core/Ressources/settings-16.png", UriKind.RelativeOrAbsolute));
            //mcp_settings_pushButtonData.LargeImage = new BitmapImage(new Uri("/RevitMCPPlugin;component/Core/Ressources/settings-32.png", UriKind.RelativeOrAbsolute));
            //mcpPanel.AddItem(mcp_settings_pushButtonData);

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                if (SocketService.Instance.IsRunning)
                {
                    SocketService.Instance.Stop();
                }
            }
            catch { }

            return Result.Succeeded;
        }

        /// <summary>
        /// Werkt het icoon van de toggle-knop bij zodat visueel zichtbaar is of de mcp-server
        /// open (orange) of dicht (black) is. Wordt aangeroepen vanuit MCPServiceConnection nadat
        /// de server is gestart/gestopt, en één keer bij het opstarten van de add-in.
        /// </summary>
        internal static void UpdateToggleButtonIcon(bool isRunning)
        {
            if (_toggleButton == null)
                return;

            string color = isRunning ? "orange" : "black";

            _toggleButton.Image = new BitmapImage(new Uri(
                $"/RevitMCPPlugin;component/Core/Ressources/mcp-server-{color}-16.png", UriKind.RelativeOrAbsolute));
            _toggleButton.LargeImage = new BitmapImage(new Uri(
                $"/RevitMCPPlugin;component/Core/Ressources/mcp-server-{color}-32.png", UriKind.RelativeOrAbsolute));
        }
    }
}
