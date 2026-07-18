using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitMCPCommandSet.Models.Common
{
    /// <summary>
    /// Definieert de bewerkingstypen die op elementen kunnen worden uitgevoerd
    /// </summary>
    public enum ElementOperationType
    {
        /// <summary>
        /// Element selecteren
        /// </summary>
        Select,

        /// <summary>
        /// Selectiekader
        /// </summary>
        SelectionBox,

        /// <summary>
        /// Kleur en vulling van het element instellen
        /// </summary>
        SetColor,

        /// <summary>
        /// Transparantie van het element instellen
        /// </summary>
        SetTransparency,

        /// <summary>
        /// Element verwijderen
        /// </summary>
        Delete,

        /// <summary>
        /// Element verbergen
        /// </summary>
        Hide,

        /// <summary>
        /// Element tijdelijk verbergen
        /// </summary>
        TempHide,

        /// <summary>
        /// Element isoleren (alleen dit element weergeven)
        /// </summary>
        Isolate,

        /// <summary>
        /// Verbergen van element ongedaan maken
        /// </summary>
        Unhide,

        /// <summary>
        /// Isolatie herstellen (alle elementen weergeven)
        /// </summary>
        ResetIsolate,
    }


    /// <summary>
    /// Instellingen voor het bewerken van elementen
    /// </summary>
    public class OperationSetting
    {
        /// <summary>
        /// Lijst met Id's van de te bewerken elementen
        /// </summary>
        [JsonProperty("elementIds")]
        public List<int> ElementIds = new List<int>();

        /// <summary>
        /// De uit te voeren actie, bevat de string-waarde van de ElementOperationType-enum
        /// </summary>
        [JsonProperty("action")]
        public string Action { get; set; } = "Select";

        /// <summary>
        /// Transparantiewaarde (0-100); hoe hoger de waarde, hoe transparanter
        /// </summary>
        [JsonProperty("transparencyValue")]
        public int TransparencyValue { get; set; } = 50;

        /// <summary>
        /// Kleur van het element (RGB-formaat), standaard rood
        /// </summary>
        [JsonProperty("colorValue")]
        public int[] ColorValue { get; set; } = new int[] { 255, 0, 0 }; // Standaard rood
    }
}
