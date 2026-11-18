using Newtonsoft.Json;

namespace RevitMCPCommandSet.Models.Common;

/// <summary>
/// Resultaat van een query met paging. Naast de gegevens van de opgevraagde pagina altijd het totaal
/// aantal gevonden elementen en of er meer zijn, zodat het taalmodel weet dat het antwoord onvolledig
/// is en zelf de volgende pagina kan opvragen (met <see cref="NextOffset"/>).
/// </summary>
public class PagedAIResult<T> : AIResult<T>
{
    /// <summary>Standaard aantal elementen per pagina als er geen limiet is opgegeven.</summary>
    public const int DefaultLimit = 100;

    /// <summary>Hoogst toegestane limiet per pagina.</summary>
    public const int MaxLimit = 1000;

    /// <summary>Totaal aantal elementen dat aan de criteria voldoet (over alle pagina's).</summary>
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

    /// <summary>
    /// Bepaalt de effectieve limiet: standaard <see cref="DefaultLimit"/>, begrensd op <see cref="MaxLimit"/>.
    /// </summary>
    public static int ResolveLimit(int? requested)
    {
        if (!requested.HasValue || requested.Value <= 0)
            return DefaultLimit;
        return requested.Value > MaxLimit ? MaxLimit : requested.Value;
    }

    /// <summary>Vult de paging-velden op basis van het totaal, de offset en de grootte van de pagina.</summary>
    public void SetPaging(int totalMatched, int offset, int returned)
    {
        TotalMatched = totalMatched;
        Offset = offset;
        Returned = returned;
        Truncated = offset + returned < totalMatched;
        NextOffset = Truncated ? offset + returned : (int?)null;
    }
}
