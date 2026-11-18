using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;
using RevitMCPCommandSet.Utils;

namespace RevitMCPCommandSet.Services
{
    public class DeleteElementEventHandler : WaitableExternalEventHandlerBase
    {
        // Uitvoeringsresultaat
        public bool IsSuccess { get; private set; }

        // Aantal succesvol verwijderde elementen
        public int DeletedCount { get; private set; }
        // Foutmelding bij mislukken (wordt teruggegeven in de response i.p.v. een dialoog te tonen)
        public string ErrorMessage { get; private set; }
        // Waarschuwing voor ongeldige of niet-bestaande ID's
        public string Warning { get; private set; }
        // Array met de te verwijderen elementen-ID's
        public string[] ElementIds { get; set; }
        // Alleen een voorbeeld tonen, niets verwijderen
        public bool DryRun { get; set; }
        // Resultaat bij DryRun
        public DeletePreview Preview { get; private set; }

        /// <summary>
        /// Zet het resultaat van een vorig verzoek terug. Aanroepen vóór het instellen van nieuwe parameters.
        /// </summary>
        public void ResetCompletion()
        {
            ErrorMessage = null;
            Warning = null;
            Preview = null;
        }

        protected override void ExecuteCore(UIApplication app)
        {
            try
            {
                var doc = app.ActiveUIDocument.Document;
                DeletedCount = 0;
                if (ElementIds == null || ElementIds.Length == 0)
                {
                    IsSuccess = false;
                    ErrorMessage = "Lijst met element-ID's is leeg";
                    return;
                }
                // Maak een verzameling van te verwijderen elementen-ID's
                List<ElementId> elementIdsToDelete = new List<ElementId>();
                List<string> invalidIds = new List<string>();
                foreach (var idStr in ElementIds)
                {
                    if (long.TryParse(idStr, out long elementIdValue))
                    {
                        var elementId = new ElementId(elementIdValue);
                        // Controleer of het element bestaat
                        if (doc.GetElement(elementId) != null)
                        {
                            elementIdsToDelete.Add(elementId);
                        }
                        else
                        {
                            invalidIds.Add(idStr);
                        }
                    }
                    else
                    {
                        invalidIds.Add(idStr);
                    }
                }
                if (invalidIds.Count > 0)
                {
                    Warning = $"De volgende ID's zijn ongeldig of het element bestaat niet: {string.Join(", ", invalidIds)}";
                }
                // Voer de verwijdering uit als er te verwijderen elementen zijn
                if (elementIdsToDelete.Count > 0 && DryRun)
                {
                    Preview = DeletePreviewUtils.Preview(doc, elementIdsToDelete);
                    IsSuccess = true;
                }
                else if (elementIdsToDelete.Count > 0)
                {
                    using (var transaction = new Transaction(doc, "Delete Elements"))
                    {
                        transaction.Start();

                        // Verwijder elementen in bulk
                        ICollection<ElementId> deletedIds = doc.Delete(elementIdsToDelete);
                        DeletedCount = deletedIds.Count;

                        transaction.CommitOrThrow();
                    }
                    IsSuccess = true;
                }
                else
                {
                    ErrorMessage = "Er zijn geen geldige elementen om te verwijderen"
                        + (Warning != null ? $" ({Warning})" : "");
                    IsSuccess = false;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Verwijderen van elementen mislukt: " + ex.Message;
                IsSuccess = false;
            }
        }
        public override string GetName()
        {
            return "Element verwijderen";
        }
    }
}
