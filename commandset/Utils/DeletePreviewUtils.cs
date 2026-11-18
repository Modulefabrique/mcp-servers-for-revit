using Autodesk.Revit.DB;
using Newtonsoft.Json;

namespace RevitMCPCommandSet.Utils
{
    /// <summary>Korte beschrijving van een element in een verwijdervoorbeeld.</summary>
    public class ElementSummary
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("category")]
        public string Category { get; set; }
    }

    /// <summary>Resultaat van een verwijdervoorbeeld (dryRun): wat er verwijderd zou worden.</summary>
    public class DeletePreview
    {
        [JsonProperty("dryRun")]
        public bool DryRun => true;

        /// <summary>De opgegeven elementen die verwijderd zouden worden.</summary>
        [JsonProperty("wouldDelete")]
        public List<ElementSummary> WouldDelete { get; set; } = new List<ElementSummary>();

        /// <summary>
        /// Aantal elementen dat Revit daarnaast automatisch mee zou verwijderen (bijv. gehoste deuren en
        /// ramen, labels, maatvoering die naar de elementen verwijst).
        /// </summary>
        [JsonProperty("dependentCount")]
        public int DependentCount { get; set; }

        /// <summary>De eerste afhankelijke elementen (maximaal <see cref="DeletePreviewUtils.MaxListedDependents"/>).</summary>
        [JsonProperty("dependents")]
        public List<ElementSummary> Dependents { get; set; } = new List<ElementSummary>();

        /// <summary>Totaal aantal elementen dat verwijderd zou worden (opgegeven + afhankelijk).</summary>
        [JsonProperty("totalCount")]
        public int TotalCount { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }

    /// <summary>
    /// Voorbeeld van een verwijderactie zonder het model te wijzigen: de verwijdering wordt in een
    /// transactie uitgevoerd en direct teruggedraaid. Zo komt ook naar voren wat Revit automatisch mee
    /// zou verwijderen, wat met alleen de opgegeven id's niet te zien is.
    /// </summary>
    public static class DeletePreviewUtils
    {
        public const int MaxListedDependents = 50;

        public static DeletePreview Preview(Document doc, ICollection<ElementId> elementIds)
        {
            var preview = new DeletePreview();
            var requested = new HashSet<ElementId>(elementIds);

            foreach (ElementId id in elementIds)
                preview.WouldDelete.Add(Describe(doc, id));

            ICollection<ElementId> deletedIds;
            using (var transaction = new Transaction(doc, "Voorbeeld verwijderen"))
            {
                transaction.Start();
                try
                {
                    deletedIds = doc.Delete(elementIds);
                }
                finally
                {
                    transaction.RollBackIfStarted();
                }
            }

            // Na de rollback bestaan de afhankelijke elementen weer, dus nu zijn ze te beschrijven
            List<ElementId> dependents = deletedIds.Where(id => !requested.Contains(id)).ToList();
            preview.DependentCount = dependents.Count;
            preview.Dependents = dependents
                .Take(MaxListedDependents)
                .Select(id => Describe(doc, id))
                .ToList();
            preview.TotalCount = deletedIds.Count;
            preview.Message =
                $"Voorbeeld: er is niets verwijderd. Verwijderen zou {preview.WouldDelete.Count} opgegeven element(en) " +
                $"en {preview.DependentCount} afhankelijk(e) element(en) verwijderen ({preview.TotalCount} in totaal). " +
                "Voer de actie opnieuw uit met dryRun=false om echt te verwijderen.";
            return preview;
        }

        private static ElementSummary Describe(Document doc, ElementId id)
        {
            Element element = doc.GetElement(id);
            return new ElementSummary
            {
                Id = id.GetValue(),
                Name = element?.Name,
                Category = element?.Category?.Name
            };
        }
    }
}
