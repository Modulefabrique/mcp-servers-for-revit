using Newtonsoft.Json;

namespace RevitMCPCommandSet.Models.Common
{
    public class ViewElementsResult
    {
        public long ViewId { get; set; }
        public string ViewName { get; set; }
        public int TotalElementsInView { get; set; }
        /// <summary>Aantal elementen in deze pagina (gelijk aan returned).</summary>
        public int FilteredElementCount { get; set; }
        public List<ElementInfo> Elements { get; set; } = new List<ElementInfo>();

        /// <summary>Totaal aantal elementen dat aan de categorie-/zichtbaarheidsfilters voldoet (over alle pagina's).</summary>
        [JsonProperty("totalMatched")]
        public int TotalMatched { get; set; }

        /// <summary>Index van het eerste element in deze pagina.</summary>
        [JsonProperty("offset")]
        public int Offset { get; set; }

        /// <summary>Aantal elementen in deze pagina.</summary>
        [JsonProperty("returned")]
        public int Returned { get; set; }

        /// <summary>True als er na deze pagina nog elementen zijn.</summary>
        [JsonProperty("truncated")]
        public bool Truncated { get; set; }

        /// <summary>Offset voor de volgende pagina, of null als dit de laatste pagina is.</summary>
        [JsonProperty("nextOffset")]
        public int? NextOffset { get; set; }
    }
}
