using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services
{
    public class GetCurrentViewInfoEventHandler : WaitableExternalEventHandlerBase
    {
        // Uitvoeringsresultaat
        public CurrentViewInfo ResultInfo { get; private set; }


        // Foutmelding bij mislukken (wordt teruggegeven in de response i.p.v. een dialoog te tonen)
        public string ErrorMessage { get; private set; }

        /// <summary>
        /// Zet het resultaat van een vorig verzoek terug. Aanroepen vóór het instellen van nieuwe parameters.
        /// </summary>
        public void ResetCompletion()
        {
            ResultInfo = null;
            ErrorMessage = null;
        }


        protected override void ExecuteCore(UIApplication app)
        {
            try
            {
                var uiDoc = app.ActiveUIDocument;
                var doc = uiDoc.Document;
                var activeView = doc.ActiveView;

                ResultInfo = new CurrentViewInfo
                {
#if REVIT2024_OR_GREATER
                    Id = activeView.Id.Value,
#else
                    Id = activeView.Id.GetValue(),
#endif
                    UniqueId = activeView.UniqueId,
                    Name = activeView.Name,
                    ViewType = activeView.ViewType.ToString(),
                    IsTemplate = activeView.IsTemplate,
                    Scale = activeView.Scale,
                    DetailLevel = activeView.DetailLevel.ToString(),
                };
            }
            catch (Exception ex)
            {
                ErrorMessage = "Ophalen van weergave-informatie mislukt: " + ex.Message;
            }
        }

        public override string GetName()
        {
            return "Huidige weergave-informatie ophalen";
        }
    }
}
