using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;

namespace RevitMCPCommandSet.Commands.Access
{
    /// <summary>
    /// Exporteert een view als afbeelding naar een eigen tijdelijke map en geeft het pad terug.
    /// De MCP-server leest het bestand, stuurt het als afbeelding naar het taalmodel en verwijdert de map.
    /// </summary>
    public class GetViewImageCommand : RevitCommandBase
    {
        private GetViewImageEventHandler _handler => (GetViewImageEventHandler)Handler;

        public override string CommandName => "get_view_image";

        public GetViewImageCommand(UIApplication uiApp)
            : base(new GetViewImageEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                var request = new GetViewImageEventHandler.Request
                {
                    ViewId = parameters?["viewId"]?.Value<long?>(),
                    ViewName = parameters?["viewName"]?.Value<string>(),
                    Size = parameters?["size"]?.Value<int?>() ?? GetViewImageEventHandler.DefaultSize,
                    Format = parameters?["format"]?.Value<string>() ?? "auto",
                    ElementIds = parameters?["elementIds"]?.ToObject<List<long>>(),
                    PaddingMm = parameters?["paddingMm"]?.Value<double?>() ?? GetViewImageEventHandler.DefaultPaddingMm,
                    Region = parameters?["region"]?.Type == JTokenType.Object
                        ? new GetViewImageEventHandler.RegionMm
                        {
                            MinX = parameters["region"]["minX"].Value<double>(),
                            MinY = parameters["region"]["minY"].Value<double>(),
                            MaxX = parameters["region"]["maxX"].Value<double>(),
                            MaxY = parameters["region"]["maxY"].Value<double>(),
                        }
                        : null,
                };

                _handler.SetRequest(request);

                // Een zware view kan lang duren om te exporteren
                if (RaiseAndWaitForCompletion(CommandTimeouts.Heavy))
                {
                    if (_handler.ErrorMessage != null)
                        throw new Exception(_handler.ErrorMessage);
                    return _handler.ResultInfo;
                }

                throw new TimeoutException("Time-out bij exporteren van de view-afbeelding");
            }
            catch (Exception ex)
            {
                throw new Exception($"View-afbeelding maken mislukt: {ex.Message}");
            }
        }
    }
}
