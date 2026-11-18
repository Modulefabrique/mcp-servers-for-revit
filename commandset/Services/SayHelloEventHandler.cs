using Autodesk.Revit.UI;

namespace RevitMCPCommandSet.Services
{
    public class SayHelloEventHandler : WaitableExternalEventHandlerBase
    {

        public string Message { get; set; } = "Hello MCP!";

        protected override void ExecuteCore(UIApplication app)
        {
            // Bewust een dialoog: dit is een testcommand (staat standaard uit in register.ts)
            TaskDialog.Show("Revit MCP", Message);
        }

        public override string GetName()
        {
            return "Say Hello";
        }
    }
}
