using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services
{
    public class GetCurrentViewElementsEventHandler : WaitableExternalEventHandlerBase
    {
        // Standaardlijst met modelcategorieën
        private readonly List<string> _defaultModelCategories = new List<string>
        {
            "OST_Walls",
            "OST_Doors",
            "OST_Windows",
            "OST_Furniture",
            "OST_Columns",
            "OST_Floors",
            "OST_Roofs",
            "OST_Stairs",
            "OST_StructuralFraming",
            "OST_Ceilings",
            "OST_MEPSpaces",
            "OST_Rooms"
        };
        // Standaardlijst met annotatiecategorieën
        private readonly List<string> _defaultAnnotationCategories = new List<string>
        {
            "OST_Dimensions",
            "OST_TextNotes",
            "OST_GenericAnnotation",
            "OST_WallTags",
            "OST_DoorTags",
            "OST_WindowTags",
            "OST_RoomTags",
            "OST_AreaTags",
            "OST_SpaceTags",
            "OST_ViewportLabels",
            "OST_TitleBlocks"
        };

        // Queryparameters
        private List<string> _modelCategoryList;
        private List<string> _annotationCategoryList;
        private bool _includeHidden;
        // Paginagrootte; zonder waarde geldt PagedAIResult.DefaultLimit (zie PagedAIResult.ResolveLimit)
        private int? _limit;
        private int _offset;

        // Uitvoeringsresultaat
        public ViewElementsResult ResultInfo { get; private set; }


        // Stel de queryparameters in
        public void SetQueryParameters(List<string> modelCategoryList, List<string> annotationCategoryList, bool includeHidden, int? limit, int offset = 0)
        {
            _modelCategoryList = modelCategoryList;
            _annotationCategoryList = annotationCategoryList;
            _includeHidden = includeHidden;
            _limit = limit;
            _offset = Math.Max(0, offset);
            ResultInfo = null;
            ErrorMessage = null;
        }

        // Foutmelding bij mislukken (wordt teruggegeven in de response i.p.v. een dialoog te tonen)
        public string ErrorMessage { get; private set; }


        protected override void ExecuteCore(UIApplication app)
        {
            try
            {
                var uiDoc = app.ActiveUIDocument;
                var doc = uiDoc.Document;
                var activeView = doc.ActiveView;


                // Voeg alle categorieën samen
                List<string> allCategories = new List<string>();
                if (_modelCategoryList == null && _annotationCategoryList == null)
                {
                    allCategories.AddRange(_defaultModelCategories);
                    allCategories.AddRange(_defaultAnnotationCategories);
                }
                else
                {
                    allCategories.AddRange(_modelCategoryList ?? new List<string>());
                    allCategories.AddRange(_annotationCategoryList ?? new List<string>());
                }

                // Haal alle elementen in de huidige weergave op
                var collector = new FilteredElementCollector(doc, activeView.Id)
                    .WhereElementIsNotElementType();

                // Haal alle elementen op
                IList<Element> elements = collector.ToElements();

                // Filteren op categorie
                if (allCategories.Count > 0)
                {
                    // Zet stringcategorieën om naar enums
                    List<BuiltInCategory> builtInCategories = new List<BuiltInCategory>();
                    foreach (string categoryName in allCategories)
                    {
                        if (Enum.TryParse(categoryName, out BuiltInCategory category))
                        {
                            builtInCategories.Add(category);
                        }
                    }
                    // Als de categorieën succesvol zijn geparsed, gebruik dan het categoriefilter
                    if (builtInCategories.Count > 0)
                    {
                        ElementMulticategoryFilter categoryFilter = new ElementMulticategoryFilter(builtInCategories);
                        elements = new FilteredElementCollector(doc, activeView.Id)
                            .WhereElementIsNotElementType()
                            .WherePasses(categoryFilter)
                            .ToElements();
                    }
                }

                // Filter verborgen elementen
                if (!_includeHidden)
                {
                    elements = elements.Where(e => !e.IsHidden(activeView)).ToList();
                }

                // Paging: standaardlimiet plus totaal en truncated in het antwoord
                int totalMatched = elements.Count;
                int limit = PagedAIResult<object>.ResolveLimit(_limit);
                elements = elements.Skip(_offset).Take(limit).ToList();
                bool truncated = _offset + elements.Count < totalMatched;

                // Bouw het resultaat op
                var elementInfos = elements.Select(e => new ElementInfo
                {
#if REVIT2024_OR_GREATER
                    Id = e.Id.Value,
#else
                    Id = e.Id.GetValue(),
#endif
                    UniqueId = e.UniqueId,
                    Name = e.Name,
                    Category = e.Category?.Name ?? "unknow",
                    Properties = GetElementProperties(e)
                }).ToList();

                ResultInfo = new ViewElementsResult
                {
#if REVIT2024_OR_GREATER
                    ViewId = activeView.Id.Value,
#else
                    ViewId = activeView.Id.GetValue(),
#endif
                    ViewName = activeView.Name,
                    TotalElementsInView = new FilteredElementCollector(doc, activeView.Id).GetElementCount(),
                    FilteredElementCount = elementInfos.Count,
                    Elements = elementInfos,
                    TotalMatched = totalMatched,
                    Offset = _offset,
                    Returned = elementInfos.Count,
                    Truncated = truncated,
                    NextOffset = truncated ? _offset + elementInfos.Count : (int?)null
                };
            }
            catch (Exception ex)
            {
                ErrorMessage = "Ophalen van weergave-elementen mislukt: " + ex.Message;
            }
        }

        private Dictionary<string, string> GetElementProperties(Element element)
        {
            var properties = new Dictionary<string, string>();

            // Voeg algemene eigenschappen toe
#if REVIT2024_OR_GREATER
            properties.Add("ElementId", element.Id.Value.ToString());
#else
            properties.Add("ElementId", element.Id.GetValue().ToString());
#endif
            if (element.Location != null)
            {
                // Coördinaten en lengtes in mm, cultuuronafhankelijk geformatteerd (punt als decimaalteken)
                if (element.Location is LocationPoint locationPoint)
                {
                    var point = locationPoint.Point;
                    properties.Add("LocationX", Mm(point.X));
                    properties.Add("LocationY", Mm(point.Y));
                    properties.Add("LocationZ", Mm(point.Z));
                }
                else if (element.Location is LocationCurve locationCurve)
                {
                    var curve = locationCurve.Curve;
                    XYZ start = curve.GetEndPoint(0);
                    XYZ end = curve.GetEndPoint(1);
                    properties.Add("Start", $"{Mm(start.X)}, {Mm(start.Y)}, {Mm(start.Z)}");
                    properties.Add("End", $"{Mm(end.X)}, {Mm(end.Y)}, {Mm(end.Z)}");
                    properties.Add("Length", Mm(curve.Length));
                }
            }

            // Haal veelgebruikte parameterwaarden op
            var commonParams = new[] { "Comments", "Mark", "Level", "Family", "Type" };
            foreach (var paramName in commonParams)
            {
                Parameter param = element.LookupParameter(paramName);
                if (param != null && !param.IsReadOnly)
                {
                    if (param.StorageType == StorageType.String)
                        properties.Add(paramName, param.AsString() ?? "");
                    else if (param.StorageType == StorageType.Double)
                        properties.Add(paramName, AIElementFilterEventHandler.FormatDimensionValue(param));
                    else if (param.StorageType == StorageType.Integer)
                        properties.Add(paramName, param.AsInteger().ToString());
                    else if (param.StorageType == StorageType.ElementId)
#if REVIT2024_OR_GREATER
                        properties.Add(paramName, param.AsElementId().Value.ToString());
#else
                        properties.Add(paramName, param.AsElementId().GetValue().ToString());
#endif
                }
            }

            return properties;
        }

        // Interne voet naar mm, als tekst met punt als decimaalteken
        private static string Mm(double feet)
        {
            return Math.Round(feet * 304.8, 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public override string GetName()
        {
            return "Elementen in huidige weergave ophalen";
        }
    }
}
