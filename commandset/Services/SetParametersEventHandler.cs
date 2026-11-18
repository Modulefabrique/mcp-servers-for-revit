using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using MF_Utilities;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;
using System.Globalization;

namespace RevitMCPCommandSet.Services
{
    public class SetParametersEventHandler : WaitableExternalEventHandlerBase
    {
        // Aan te passen elementen + parameters
        public List<ElementParameterInput> Elements { get; set; }

        // Alleen een voorbeeld: wijzigingen worden uitgevoerd en direct teruggedraaid
        public bool DryRun { get; set; }

        // Uitvoeringsresultaat, per element
        public List<ElementParameterOutput> Result { get; private set; }


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
            Result = new List<ElementParameterOutput>();
            try
            {
                var doc = app.ActiveUIDocument.Document;
                if (Elements == null || Elements.Count == 0)
                {
                    return;
                }

                // Elementen opzoeken in het huidige document; niet-gevonden ID's slaan we apart op zodat
                // ze ook in het resultaat terugkomen (ParameterHelper krijgt alleen bestaande elementen
                // te zien).
                var elementChanges = new List<ElementParameterChange>();
                var missingElementIds = new List<string>();

                foreach (var input in Elements)
                {
                    Element element = ResolveElement(doc, input.ElementId);
                    if (element == null)
                    {
                        missingElementIds.Add(input.ElementId);
                        continue;
                    }

                    List<ParameterChange> changes = input.Parameters
                        .Select(p => new ParameterChange(p.Name, ConvertJTokenToValue(p.Value)))
                        .ToList();
                    elementChanges.Add(new ElementParameterChange(element, changes));
                }

                if (elementChanges.Count > 0)
                {
                    // Huidige waarden vastleggen, zodat het antwoord oud en nieuw naast elkaar toont
                    var oldValues = new Dictionary<(ElementId, string), string>();
                    foreach (var change in elementChanges)
                        foreach (var input in change.Changes)
                        {
                            // ParameterChange is hierboven altijd met een parameternaam (string) aangemaakt
                            string name = input.ParameterIdentifier as string;
                            if (name != null)
                                oldValues[(change.Element.Id, name)] = ReadDisplayValue(change.Element, name);
                        }

                    // Dry-run: zelf een transactie openen. ParameterHelper gebruikt dan die transactie
                    // (hij opent alleen een eigen transactie als het document nog niet wijzigbaar is),
                    // zodat alles na het uitlezen van de nieuwe waarden weer wordt teruggedraaid.
                    Transaction preview = null;
                    if (DryRun)
                    {
                        preview = new Transaction(doc, "Voorbeeld parameterwijziging");
                        preview.Start();
                    }

                    try
                    {
                        List<ElementParameterChangeResult> changeResults = ParameterHelper.SetParameters(elementChanges);
                        foreach (var changeResult in changeResults)
                        {
                            Element element = changeResult.Element;
                            Result.Add(new ElementParameterOutput
                            {
                                ElementId = element.Id.GetValue(),
                                Parameters = changeResult.ParameterResults.Select(pr => new ParameterResultOutput
                                {
                                    ParameterName = pr.ParameterName,
                                    Success = pr.Success,
                                    Message = pr.Message,
                                    OldValue = oldValues.TryGetValue((element.Id, pr.ParameterName), out string oldValue) ? oldValue : null,
                                    NewValue = ReadDisplayValue(element, pr.ParameterName)
                                }).ToList()
                            });
                        }
                    }
                    finally
                    {
                        if (preview != null)
                        {
                            preview.RollBackIfStarted();
                            preview.Dispose();
                        }
                    }
                }

                foreach (var missingId in missingElementIds)
                {
                    Result.Add(new ElementParameterOutput
                    {
                        ElementId = 0,
                        Parameters = new List<ParameterResultOutput>
                        {
                            new ParameterResultOutput
                            {
                                ParameterName = null,
                                Success = false,
                                Message = $"Element met ID '{missingId}' niet gevonden"
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Aanpassen van parameters mislukt: " + ex.Message;
            }
        }

        /// <summary>
        /// Leest de waarde van een parameter zoals Revit hem in het Properties-palet toont, of null als de
        /// parameter niet bestaat. Alleen voor weergave in het antwoord (oude/nieuwe waarde).
        /// </summary>
        private static string ReadDisplayValue(Element element, string parameterName)
        {
            if (element == null || string.IsNullOrEmpty(parameterName))
                return null;
            try
            {
                Parameter parameter = element.LookupParameter(parameterName);
                if (parameter == null || !parameter.HasValue)
                    return null;
                return parameter.StorageType == StorageType.String ? parameter.AsString() : parameter.AsValueString();
            }
            catch
            {
                return null;
            }
        }

        private static Element ResolveElement(Document doc, string elementIdString)
        {
            if (string.IsNullOrEmpty(elementIdString) || !long.TryParse(elementIdString, out long idValue))
            {
                return null;
            }

            return doc.GetElement(new ElementId(idValue));
        }

        /// <summary>
        /// Zet een JSON-waarde om naar het object-type dat ParameterHelper.SetParameter(s) verwacht.
        /// Booleans kent ParameterHelper niet (Ja/Nee-parameters verwachten een integer 0/1). Getallen
        /// geven we als echte int/double door en niet als tekst: ParameterHelper parst strings met
        /// double.TryParse in de huidige cultuur, waardoor "2.5" op een Nederlandse Windows als 25 zou
        /// worden gelezen. ParameterHelper zet int/double zelf om naar het StorageType van de parameter
        /// (zie ParameterHelper.TrySetValue).
        /// </summary>
        private static object ConvertJTokenToValue(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return null;
            }

            switch (token.Type)
            {
                case JTokenType.Boolean:
                    // Ja/Nee-parameters verwachten een integer 0/1
                    return token.Value<bool>() ? 1 : 0;

                case JTokenType.Integer:
                    // Newtonsoft levert hele getallen als long; ParameterHelper kent alleen int (en
                    // string). Past het niet in een int (groot ElementId), dan als tekst: voor hele
                    // getallen is het parsen cultuuronafhankelijk.
                    long longValue = token.Value<long>();
                    return longValue >= int.MinValue && longValue <= int.MaxValue
                        ? (object)(int)longValue
                        : longValue.ToString(CultureInfo.InvariantCulture);

                case JTokenType.Float:
                    return token.Value<double>();

                default:
                    return token.Value<string>();
            }
        }

        public override string GetName()
        {
            return "Parameters aanpassen";
        }
    }
}
