using System.IO;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using RevitMCPCommandSet.Utils;

namespace RevitMCPCommandSet.Services
{
    /// <summary>
    /// Exporteert een view (plattegrond, doorsnede, aanzicht, detail, 3D, tekenview, sheet, legenda)
    /// als PNG/JPG voor visuele analyse door het taalmodel.
    ///
    /// - Elk verzoek schrijft in een eigen, nieuwe map onder %TEMP%\RevitMCP\view-images\, zodat er
    ///   nooit een oud of ander bestand wordt opgepakt. De MCP-server verwijdert die map direct na het
    ///   inlezen; mappen ouder dan <see cref="OrphanMaxAge"/> (bijv. na een crash) ruimt deze handler bij
    ///   elk verzoek zelf op.
    /// - Inzoomen (elementIds of region) gebeurt op een tijdelijke kopie van de view met een crop region,
    ///   binnen een TransactionGroup die altijd wordt teruggedraaid: het model, de originele view en het
    ///   scherm van de gebruiker veranderen niet, en in een workshared model wordt niets van een ander
    ///   geleend.
    /// </summary>
    public class GetViewImageEventHandler : WaitableExternalEventHandlerBase
    {
        public const int DefaultSize = 1568;
        public const int MinSize = 200;
        public const int MaxSize = 4000;
        public const double DefaultPaddingMm = 1000;

        private const double MmPerFoot = 304.8;
        /// <summary>Kleinste breedte/hoogte van een uitsnede, zodat de crop region nooit ontaardt.</summary>
        private const double MinRegionMm = 100;
        private static readonly TimeSpan OrphanMaxAge = TimeSpan.FromHours(1);

        /// <summary>Tijdelijke hoofdmap; de MCP-server gebruikt hetzelfde pad (os.tmpdir()/RevitMCP/view-images).</summary>
        public static string TempRoot => Path.Combine(Path.GetTempPath(), "RevitMCP", "view-images");

        /// <summary>Views die Revit niet als afbeelding kan exporteren.</summary>
        private static readonly HashSet<ViewType> NonExportableTypes = new HashSet<ViewType>
        {
            ViewType.Undefined, ViewType.Internal, ViewType.ProjectBrowser, ViewType.SystemBrowser,
            ViewType.Schedule, ViewType.PanelSchedule, ViewType.ColumnSchedule,
            ViewType.Report, ViewType.CostReport, ViewType.LoadsReport, ViewType.PresureLossReport,
        };

        /// <summary>Views met een crop region, dus waarin ingezoomd kan worden.</summary>
        private static readonly HashSet<ViewType> CroppableTypes = new HashSet<ViewType>
        {
            ViewType.FloorPlan, ViewType.CeilingPlan, ViewType.EngineeringPlan, ViewType.AreaPlan,
            ViewType.Section, ViewType.Elevation, ViewType.Detail, ViewType.ThreeD,
        };

        public class Request
        {
            public long? ViewId { get; set; }
            public string ViewName { get; set; }
            public int Size { get; set; } = DefaultSize;
            public string Format { get; set; } = "auto";
            public List<long> ElementIds { get; set; }
            public double PaddingMm { get; set; } = DefaultPaddingMm;
            public RegionMm Region { get; set; }
        }

        /// <summary>Rechthoek in viewcoördinaten (mm): x naar rechts, y omhoog in het beeld.</summary>
        public class RegionMm
        {
            [JsonProperty("minX")] public double MinX { get; set; }
            [JsonProperty("minY")] public double MinY { get; set; }
            [JsonProperty("maxX")] public double MaxX { get; set; }
            [JsonProperty("maxY")] public double MaxY { get; set; }
        }

        public class Result
        {
            /// <summary>Volledig pad van het geëxporteerde bestand (in een eigen tijdelijke map).</summary>
            [JsonProperty("path")] public string Path { get; set; }
            [JsonProperty("viewId")] public long ViewId { get; set; }
            [JsonProperty("viewName")] public string ViewName { get; set; }
            [JsonProperty("viewType")] public string ViewType { get; set; }
            [JsonProperty("scale")] public int? Scale { get; set; }
            [JsonProperty("size")] public int Size { get; set; }
            [JsonProperty("zoomed")] public bool Zoomed { get; set; }

            /// <summary>
            /// Wat het beeld (ongeveer) beslaat in viewcoördinaten (mm), of null als dat onbekend is
            /// (view zonder actieve crop region, perspectief, sheet, tekenview).
            /// </summary>
            [JsonProperty("region")] public RegionMm Region { get; set; }

            /// <summary>True als de viewcoördinaten gelijk zijn aan de model-X/Y (niet-gedraaide plattegrond).</summary>
            [JsonProperty("regionMatchesModelXY")] public bool? RegionMatchesModelXY { get; set; }

            [JsonProperty("missingElementIds", NullValueHandling = NullValueHandling.Ignore)]
            public List<long> MissingElementIds { get; set; }
        }

        private Request _request;

        public Result ResultInfo { get; private set; }

        // Foutmelding bij mislukken (wordt teruggegeven in de response i.p.v. een dialoog te tonen)
        public string ErrorMessage { get; private set; }

        public void SetRequest(Request request)
        {
            _request = request;
            ResultInfo = null;
            ErrorMessage = null;
        }

        protected override void ExecuteCore(UIApplication app)
        {
            string folder = null;
            try
            {
                var uiDoc = app.ActiveUIDocument;
                if (uiDoc == null)
                {
                    ErrorMessage = "Er is geen document geopend in Revit.";
                    return;
                }
                var doc = uiDoc.Document;

                SweepOrphanFolders();

                var req = _request;
                int size = Math.Max(MinSize, Math.Min(MaxSize, req.Size));
                bool zoom = (req.ElementIds != null && req.ElementIds.Count > 0) || req.Region != null;
                if (req.ElementIds != null && req.ElementIds.Count > 0 && req.Region != null)
                {
                    ErrorMessage = "Geef óf elementIds óf region op, niet allebei.";
                    return;
                }

                View view = ResolveView(doc, req);
                if (view == null)
                    return; // ErrorMessage is gezet

                folder = Path.Combine(TempRoot, Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(folder);

                var result = new Result
                {
                    ViewId = view.Id.GetValue(),
                    ViewName = view.Name,
                    ViewType = view.ViewType.ToString(),
                    Scale = SafeScale(view),
                    Size = size,
                    Zoomed = zoom,
                };

                if (zoom)
                {
                    if (!ExportZoomed(doc, view, req, size, folder, result))
                    {
                        DeleteFolder(folder);
                        return; // ErrorMessage is gezet
                    }
                }
                else
                {
                    if (CanReportCrop(view) && view.CropBoxActive)
                        FillRegion(result, view.CropBox, view.CropBox.Min.X, view.CropBox.Min.Y, view.CropBox.Max.X, view.CropBox.Max.Y);
                    Export(doc, view, size, req.Format, folder);
                }

                result.Path = FindExportedFile(folder);
                if (result.Path == null)
                {
                    DeleteFolder(folder);
                    ErrorMessage = $"Revit heeft geen afbeelding gemaakt voor view '{view.Name}' (is de view leeg?).";
                    return;
                }

                ResultInfo = result;
            }
            catch (Exception ex)
            {
                if (folder != null)
                    DeleteFolder(folder);
                ErrorMessage = "Exporteren van de view mislukt: " + ex.Message;
            }
        }

        /// <summary>Zoekt de view op id of naam (hoofdletterongevoelig), of neemt de actieve view.</summary>
        private View ResolveView(Document doc, Request req)
        {
            View view;
            if (req.ViewId.HasValue)
            {
                view = doc.GetElement(new ElementId(req.ViewId.Value)) as View;
                if (view == null)
                {
                    ErrorMessage = $"Geen view gevonden met id {req.ViewId.Value}. {AvailableViewsHint(doc)}";
                    return null;
                }
            }
            else if (!string.IsNullOrWhiteSpace(req.ViewName))
            {
                string name = req.ViewName.Trim();
                var matches = ExportableViews(doc)
                    .Where(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (matches.Count == 0)
                {
                    ErrorMessage = $"Geen exporteerbare view gevonden met de naam '{name}'. {AvailableViewsHint(doc)}";
                    return null;
                }
                if (matches.Count > 1)
                {
                    ErrorMessage = $"Meerdere views heten '{name}'; geef viewId op. Kandidaten: " +
                                   string.Join("; ", matches.Select(Describe));
                    return null;
                }
                view = matches[0];
            }
            else
            {
                view = doc.ActiveView;
                if (view == null)
                {
                    ErrorMessage = "Er is geen actieve view.";
                    return null;
                }
            }

            if (!IsExportable(view))
            {
                ErrorMessage = $"View '{view.Name}' ({view.ViewType}) kan niet als afbeelding worden geëxporteerd " +
                               $"(templates, schedules en interne views kunnen niet). {AvailableViewsHint(doc)}";
                return null;
            }
            return view;
        }

        private static bool IsExportable(View view) =>
            !view.IsTemplate && !NonExportableTypes.Contains(view.ViewType) && view.CanBePrinted;

        private static IEnumerable<View> ExportableViews(Document doc) =>
            new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>().Where(IsExportable);

        private static int? SafeScale(View view)
        {
            if (view is ViewSheet)
                return null;
            try { return view.Scale; }
            catch { return null; }
        }

        private static string Describe(View v) => $"'{v.Name}' ({v.ViewType}, id {v.Id.GetValue()})";

        private static string AvailableViewsHint(Document doc)
        {
            var views = ExportableViews(doc).OrderBy(v => v.ViewType.ToString()).ThenBy(v => v.Name).ToList();
            const int max = 25;
            string list = string.Join("; ", views.Take(max).Select(Describe));
            string more = views.Count > max ? $" (en nog {views.Count - max} andere)" : "";
            return $"Beschikbare views: {list}{more}.";
        }

        private static bool CanReportCrop(View view) =>
            CroppableTypes.Contains(view.ViewType) && !(view is View3D v3 && v3.IsPerspective);

        /// <summary>
        /// Maakt een tijdelijke kopie van de view, zet daarop de gevraagde uitsnede als crop region,
        /// exporteert die kopie en draait alles terug.
        /// </summary>
        private bool ExportZoomed(Document doc, View view, Request req, int size, string folder, Result result)
        {
            if (!CanReportCrop(view))
            {
                ErrorMessage = $"Inzoomen kan niet in een view van het type {view.ViewType}" +
                               (view is View3D ? " met perspectief" : "") +
                               "; vraag de view op zonder elementIds/region.";
                return false;
            }

            ViewDuplicateOption option;
            if (view.CanViewBeDuplicated(ViewDuplicateOption.WithDetailing))
                option = ViewDuplicateOption.WithDetailing;
            else if (view.CanViewBeDuplicated(ViewDuplicateOption.Duplicate))
                option = ViewDuplicateOption.Duplicate;
            else
            {
                ErrorMessage = $"View '{view.Name}' kan niet tijdelijk worden gekopieerd om in te zoomen.";
                return false;
            }

            using (var group = new TransactionGroup(doc, "MCP: view-afbeelding (tijdelijk)"))
            {
                group.Start();
                try
                {
                    View copy;
                    using (var t = new Transaction(doc, "MCP: tijdelijke uitsnede"))
                    {
                        t.Start();
                        var failureOptions = t.GetFailureHandlingOptions();
                        failureOptions.SetFailuresPreprocessor(new SwallowWarnings());
                        t.SetFailureHandlingOptions(failureOptions);

                        copy = (View)doc.GetElement(view.Duplicate(option));

                        // Een scope box legt de crop region vast; op de kopie loskoppelen
                        var scopeBox = copy.get_Parameter(BuiltInParameter.VIEWER_VOLUME_OF_INTEREST_CROP);
                        if (scopeBox != null && !scopeBox.IsReadOnly)
                            scopeBox.Set(ElementId.InvalidElementId);

                        if (!ApplyCrop(doc, view, copy, req, result))
                            return false; // group wordt in finally teruggedraaid

                        t.CommitOrThrow();
                    }

                    Export(doc, copy, size, req.Format, folder);
                    return true;
                }
                finally
                {
                    group.RollBack();
                }
            }
        }

        /// <summary>Rekent de uitsnede uit in de coördinaten van de crop box en zet die op de kopie.</summary>
        private bool ApplyCrop(Document doc, View original, View copy, Request req, Result result)
        {
            BoundingBoxXYZ crop = copy.CropBox;
            Transform toModel = crop.Transform;
            Transform toView = toModel.Inverse;
            double minX, minY, maxX, maxY;
            double minZ = crop.Min.Z, maxZ = crop.Max.Z;

            if (req.ElementIds != null && req.ElementIds.Count > 0)
            {
                minX = minY = minZ = double.MaxValue;
                maxX = maxY = maxZ = double.MinValue;
                var missing = new List<long>();
                bool any = false;

                foreach (long id in req.ElementIds)
                {
                    Element element = doc.GetElement(new ElementId(id));
                    BoundingBoxXYZ box = element?.get_BoundingBox(original) ?? element?.get_BoundingBox(null);
                    if (box == null)
                    {
                        missing.Add(id);
                        continue;
                    }
                    foreach (XYZ corner in Corners(box))
                    {
                        XYZ p = toView.OfPoint(box.Transform.OfPoint(corner));
                        minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X);
                        minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y);
                        minZ = Math.Min(minZ, p.Z); maxZ = Math.Max(maxZ, p.Z);
                    }
                    any = true;
                }

                if (!any)
                {
                    ErrorMessage = "Geen van de opgegeven elementen bestaat of heeft een omhullende: " +
                                   string.Join(", ", req.ElementIds);
                    return false;
                }
                if (missing.Count > 0)
                    result.MissingElementIds = missing;

                double pad = Math.Max(0, req.PaddingMm) / MmPerFoot;
                minX -= pad; minY -= pad; maxX += pad; maxY += pad;

                // Alleen in 3D bepaalt de diepte van de crop box mee wat zichtbaar is; daar de elementen
                // erin opnemen. In doorsneden/aanzichten blijft de diepte (far clip) van de view gelden.
                if (copy is View3D)
                {
                    minZ = Math.Min(minZ - pad, crop.Min.Z);
                    maxZ = Math.Max(maxZ + pad, crop.Max.Z);
                }
                else
                {
                    minZ = crop.Min.Z;
                    maxZ = crop.Max.Z;
                }
            }
            else
            {
                var r = req.Region;
                minX = Math.Min(r.MinX, r.MaxX) / MmPerFoot;
                maxX = Math.Max(r.MinX, r.MaxX) / MmPerFoot;
                minY = Math.Min(r.MinY, r.MaxY) / MmPerFoot;
                maxY = Math.Max(r.MinY, r.MaxY) / MmPerFoot;
            }

            // Minimale afmeting, gecentreerd
            double minExtent = MinRegionMm / MmPerFoot;
            if (maxX - minX < minExtent) { double c = (minX + maxX) / 2; minX = c - minExtent / 2; maxX = c + minExtent / 2; }
            if (maxY - minY < minExtent) { double c = (minY + maxY) / 2; minY = c - minExtent / 2; maxY = c + minExtent / 2; }

            copy.CropBoxActive = true;
            copy.CropBoxVisible = false;
            copy.CropBox = new BoundingBoxXYZ
            {
                Transform = toModel,
                Min = new XYZ(minX, minY, minZ),
                Max = new XYZ(maxX, maxY, maxZ),
            };

            // Annotatie-crop aan, anders vallen tags/maten buiten de uitsnede ook in het beeld
            var annotationCrop = copy.get_Parameter(BuiltInParameter.VIEWER_ANNOTATION_CROP_ACTIVE);
            if (annotationCrop != null && !annotationCrop.IsReadOnly)
                annotationCrop.Set(1);

            FillRegion(result, crop, minX, minY, maxX, maxY);
            return true;
        }

        private static IEnumerable<XYZ> Corners(BoundingBoxXYZ b)
        {
            foreach (double x in new[] { b.Min.X, b.Max.X })
                foreach (double y in new[] { b.Min.Y, b.Max.Y })
                    foreach (double z in new[] { b.Min.Z, b.Max.Z })
                        yield return new XYZ(x, y, z);
        }

        private static void FillRegion(Result result, BoundingBoxXYZ crop, double minX, double minY, double maxX, double maxY)
        {
            result.Region = new RegionMm
            {
                MinX = Math.Round(minX * MmPerFoot),
                MinY = Math.Round(minY * MmPerFoot),
                MaxX = Math.Round(maxX * MmPerFoot),
                MaxY = Math.Round(maxY * MmPerFoot),
            };
            Transform t = crop.Transform;
            result.RegionMatchesModelXY =
                t.BasisX.IsAlmostEqualTo(XYZ.BasisX) && t.BasisY.IsAlmostEqualTo(XYZ.BasisY) &&
                Math.Abs(t.Origin.X) < 1e-6 && Math.Abs(t.Origin.Y) < 1e-6;
        }

        /// <summary>
        /// Exporteert één view. <paramref name="size"/> is de lange zijde in pixels; de verhouding van
        /// de view (Outline) bepaalt of dat de breedte of de hoogte is.
        /// </summary>
        private static void Export(Document doc, View view, int size, string format, string folder)
        {
            FitDirectionType fitDirection = FitDirectionType.Horizontal;
            try
            {
                BoundingBoxUV outline = view.Outline;
                if (outline.Max.V - outline.Min.V > outline.Max.U - outline.Min.U)
                    fitDirection = FitDirectionType.Vertical;
            }
            catch
            {
                // Geen outline beschikbaar: breedte aanhouden
            }

            // auto: lijnwerk (verborgen lijnen/draadmodel) als PNG, gearceerde/realistische views als JPG
            string f = (format ?? "auto").ToLowerInvariant();
            ImageFileType lineType = f == "jpg" || f == "jpeg" ? ImageFileType.JPEGMedium : ImageFileType.PNG;
            ImageFileType shadedType = f == "png" ? ImageFileType.PNG : ImageFileType.JPEGMedium;

            var options = new ImageExportOptions
            {
                ExportRange = ExportRange.SetOfViews,
                FilePath = Path.Combine(folder, "view"),
                HLRandWFViewsFileType = lineType,
                ShadowViewsFileType = shadedType,
                ZoomType = ZoomFitType.FitToPage,
                FitDirection = fitDirection,
                PixelSize = size,
                ImageResolution = ImageResolution.DPI_150,
            };
            options.SetViewsAndSheets(new List<ElementId> { view.Id });
            doc.ExportImage(options);
        }

        /// <summary>Revit verzint zelf de bestandsnaam; in de eigen, lege map is er precies één afbeelding.</summary>
        private static string FindExportedFile(string folder)
        {
            return new DirectoryInfo(folder).GetFiles()
                .Where(f => f.Extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
                            || f.Extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                            || f.Extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault()?.FullName;
        }

        /// <summary>Verwijdert achtergebleven mappen (bijv. na een crash van de MCP-server).</summary>
        private static void SweepOrphanFolders()
        {
            try
            {
                if (!Directory.Exists(TempRoot))
                    return;
                DateTime limit = DateTime.UtcNow - OrphanMaxAge;
                foreach (var dir in new DirectoryInfo(TempRoot).GetDirectories())
                {
                    if (dir.CreationTimeUtc < limit)
                        DeleteFolder(dir.FullName);
                }
            }
            catch
            {
                // Opruimen is best effort
            }
        }

        private static void DeleteFolder(string folder)
        {
            try
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }
            catch
            {
                // Best effort; de sweep probeert het later opnieuw
            }
        }

        /// <summary>
        /// Verwijdert waarschuwingen in de tijdelijke transactie, zodat geen dialoog het verzoek blokkeert.
        /// Fouten blijven staan; de transactie wordt dan teruggedraaid en dat wordt als fout gemeld.
        /// </summary>
        private class SwallowWarnings : IFailuresPreprocessor
        {
            public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
            {
                foreach (FailureMessageAccessor message in failuresAccessor.GetFailureMessages())
                {
                    if (message.GetSeverity() == FailureSeverity.Warning)
                        failuresAccessor.DeleteWarning(message);
                }
                return FailureProcessingResult.Continue;
            }
        }

        public override string GetName()
        {
            return "View-afbeelding exporteren";
        }
    }
}
