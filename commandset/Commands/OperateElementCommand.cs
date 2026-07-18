using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitMCPCommandSet.Commands
{
    public class OperateElementCommand : ExternalEventCommandBase
    {
        private OperateElementEventHandler _handler => (OperateElementEventHandler)Handler;

        /// <summary>
        /// Commandonaam
        /// </summary>
        public override string CommandName => "operate_element";

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="uiApp">Revit UIApplication</param>
        public OperateElementCommand(UIApplication uiApp)
            : base(new OperateElementEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                OperationSetting data = new OperationSetting();
                // Parameters parsen
                data = parameters["data"].ToObject<OperationSetting>();
                if (data == null)
                    throw new ArgumentNullException(nameof(data), "AI-invoerdata is leeg");

                // Puntvormige elementparameters instellen
                _handler.SetParameters(data);

                // Extern event activeren en wachten op voltooiing
                if (RaiseAndWaitForCompletion(10000))
                {
                    return _handler.Result;
                }
                else
                {
                    throw new TimeoutException("Time-out bij bewerken van element");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Bewerken van element mislukt: {ex.Message}");
            }
        }
    }
}
