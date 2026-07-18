using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands
{
    public class TagWallsCommand : ExternalEventCommandBase
    {
        private TagWallsEventHandler _handler => (TagWallsEventHandler)Handler;

        /// <summary>
        /// Commandonaam
        /// </summary>
        public override string CommandName => "tag_walls";

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="uiApp">Revit UIApplication</param>
        public TagWallsCommand(UIApplication uiApp)
            : base(new TagWallsEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                // Parameters parsen
                bool useLeader = false;
                if (parameters["useLeader"] != null)
                {
                    useLeader = parameters["useLeader"].ToObject<bool>();
                }

                string tagTypeId = null;
                if (parameters["tagTypeId"] != null)
                {
                    tagTypeId = parameters["tagTypeId"].ToString();
                }

                // Labelparameters instellen
                _handler.SetParameters(useLeader, tagTypeId);

                // Extern event activeren en wachten op voltooiing
                if (RaiseAndWaitForCompletion(10000))
                {
                    return _handler.TaggingResults;
                }
                else
                {
                    throw new TimeoutException("Time-out bij labelen van wanden");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Labelen van wanden mislukt: {ex.Message}");
            }
        }
    }
}