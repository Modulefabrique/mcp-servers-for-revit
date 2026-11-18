using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services
{
    public class GetParametersEventHandler : WaitableExternalEventHandlerBase
    {
        // Op te vragen elementen; ParameterNames is per element al ingevuld (eigen lijst of de algemene)
        public List<ElementParameterRequestInput> Elements { get; set; }

        // Uitvoeringsresultaat, per element
        public List<ElementParameterValuesOutput> Result { get; private set; }


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
            Result = new List<ElementParameterValuesOutput>();
            try
            {
                var doc = app.ActiveUIDocument.Document;
                if (Elements == null || Elements.Count == 0)
                {
                    return;
                }

                foreach (var input in Elements)
                {
                    ParameterReadUtils.TryParseElementId(input.ElementId, out long requestedId);
                    Element element = ParameterReadUtils.ResolveElement(doc, input.ElementId);
                    if (element == null)
                    {
                        Result.Add(new ElementParameterValuesOutput
                        {
                            ElementId = requestedId,
                            Found = false,
                            Message = $"Element met ID '{input.ElementId}' niet gevonden"
                        });
                        continue;
                    }

                    // Eerst zoeken op het element zelf (instance), daarna op zijn familietype
                    ElementType elementType = ParameterReadUtils.GetElementType(doc, element);

                    var elementOutput = new ElementParameterValuesOutput
                    {
                        ElementId = element.Id.GetValue(),
                        Found = true,
                        TypeId = elementType?.Id.GetValue()
                    };

                    string ownSource = element is ElementType
                        ? ParameterReadUtils.SourceType
                        : ParameterReadUtils.SourceInstance;

                    foreach (var parameterName in input.ParameterNames ?? new List<string>())
                    {
                        Parameter parameter = ParameterReadUtils.FindVisibleParameter(element, parameterName);
                        if (parameter != null)
                        {
                            elementOutput.Parameters.Add(ParameterReadUtils.ReadParameter(doc, parameter, ownSource));
                            continue;
                        }

                        Parameter typeParameter = elementType != null
                            ? ParameterReadUtils.FindVisibleParameter(elementType, parameterName)
                            : null;
                        if (typeParameter != null)
                        {
                            elementOutput.Parameters.Add(ParameterReadUtils.ReadParameter(doc, typeParameter, ParameterReadUtils.SourceType));
                            continue;
                        }

                        elementOutput.Parameters.Add(new ParameterValueOutput
                        {
                            Name = parameterName,
                            Found = false,
                            Message = $"Parameter '{parameterName}' niet gevonden op element of familietype"
                        });
                    }

                    Result.Add(elementOutput);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Ophalen van parameters mislukt: " + ex.Message;
            }
        }

        public override string GetName()
        {
            return "Parameters ophalen";
        }
    }
}
