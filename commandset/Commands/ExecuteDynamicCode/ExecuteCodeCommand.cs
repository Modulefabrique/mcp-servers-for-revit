using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.ExecuteDynamicCode
{
    /// <summary>
    /// Commandoklasse die de uitvoering van code afhandelt
    /// </summary>
    public class ExecuteCodeCommand : ExternalEventCommandBase
    {
        private ExecuteCodeEventHandler _handler => (ExecuteCodeEventHandler)Handler;

        public override string CommandName => "send_code_to_revit";

        public ExecuteCodeCommand(UIApplication uiApp)
            : base(new ExecuteCodeEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                // Parametervalidatie
                if (!parameters.ContainsKey("code"))
                {
                    throw new ArgumentException("Missing required parameter: 'code'");
                }

                // Code en parameters parsen
                string code = parameters["code"].Value<string>();
                JArray parametersArray = parameters["parameters"] as JArray;
                object[] executionParameters = parametersArray?.ToObject<object[]>() ?? Array.Empty<object>();
                string transactionMode = parameters["transactionMode"]?.Value<string>() ?? ExecuteCodeEventHandler.TransactionModeAuto;

                // Uitvoeringsparameters instellen
                _handler.SetExecutionParameters(code, executionParameters, transactionMode);

                // Extern event activeren en wachten op voltooiing
                if (RaiseAndWaitForCompletion(60000)) // 1 minuut time-out
                {
                    return _handler.ResultInfo;
                }
                else
                {
                    throw new TimeoutException("Time-out bij uitvoeren van code");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Uitvoeren van code mislukt: {ex.Message}", ex);
            }
        }
    }
}
