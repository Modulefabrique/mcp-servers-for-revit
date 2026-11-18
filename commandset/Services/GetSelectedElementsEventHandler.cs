using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPSDK.API.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitMCPCommandSet.Services
{
    public class GetSelectedElementsEventHandler : WaitableExternalEventHandlerBase
    {
        // Uitvoeringsresultaat
        public List<Models.Common.ElementInfo> ResultElements { get; private set; }


        // Beperk het aantal geretourneerde elementen
        public int? Limit { get; set; }

        // Foutmelding bij mislukken (wordt teruggegeven in de response i.p.v. een dialoog te tonen)
        public string ErrorMessage { get; private set; }

        /// <summary>
        /// Zet het resultaat van een vorig verzoek terug. Aanroepen vóór het instellen van nieuwe parameters.
        /// </summary>
        public void ResetCompletion()
        {
            ErrorMessage = null;
        }


        protected override void ExecuteCore(UIApplication app)
        {
            try
            {
                var uiDoc = app.ActiveUIDocument;
                var doc = uiDoc.Document;

                // Haal de momenteel geselecteerde elementen op
                var selectedIds = uiDoc.Selection.GetElementIds();
                var selectedElements = selectedIds.Select(id => doc.GetElement(id)).ToList();

                // Pas de aantalslimiet toe
                if (Limit.HasValue && Limit.Value > 0)
                {
                    selectedElements = selectedElements.Take(Limit.Value).ToList();
                }

                // Converteer naar een lijst van ElementInfo
                ResultElements = selectedElements.Select(element => new ElementInfo
                {
#if REVIT2024_OR_GREATER
                    Id = element.Id.Value,
#else
                    Id = element.Id.GetValue(),
#endif
                    UniqueId = element.UniqueId,
                    Name = element.Name,
                    Category = element.Category?.Name
                }).ToList();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Ophalen van geselecteerde elementen mislukt: " + ex.Message;
                ResultElements = new List<Models.Common.ElementInfo>();
            }
        }

        public override string GetName()
        {
            return "Geselecteerde elementen ophalen";
        }
    }
}
