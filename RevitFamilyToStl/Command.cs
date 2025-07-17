using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace RevitFamilyToStl
{
    public class JsonInput
    {
        [JsonProperty("panels")]
        public List<Dictionary<string, object>> Panels { get; set; }
    }

    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;
            var doc = uiDoc.Document;

            var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var inputDir = Path.Combine(assemblyLocation, "input");
            var outputDir = Path.Combine(assemblyLocation, "output");
            Directory.CreateDirectory(outputDir);

            var jsonFilePath = Path.Combine(inputDir, "panel_template_1.json");
            var familyFilePath = Path.Combine(inputDir, "3X8X_panel_v1_2025_06_26.rfa");

            if (!File.Exists(jsonFilePath)) { message = "JSON input file not found."; return Result.Failed; }
            if (!File.Exists(familyFilePath)) { message = "RFA family file not found."; return Result.Failed; }

            try
            {
                var jsonText = File.ReadAllText(jsonFilePath);
                var jsonInput = JsonConvert.DeserializeObject<JsonInput>(jsonText);
                if (jsonInput?.Panels == null || !jsonInput.Panels.Any())
                {
                    message = "No panels found in JSON input.";
                    return Result.Failed;
                }

                FamilySymbol symbol = LoadAndGetFamilySymbol(doc, familyFilePath);
                if (symbol == null) { message = "Could not load or find family symbol."; return Result.Failed; }

                foreach (var panelData in jsonInput.Panels)
                {
                    ElementId instanceId = null;
                    try
                    {
                        string panelId = panelData.ContainsKey("panel_id") ? panelData["panel_id"].ToString() : Guid.NewGuid().ToString();
                        var stlFilePath = Path.Combine(outputDir, $"{panelId}.stl");
                        var csvFilePath = Path.Combine(outputDir, $"{panelId}_parameters.csv");

                        // ===== Create Instance Transaction =====
                        FamilyInstance instance;
                        using (var t1 = new Transaction(doc, $"Create Panel {panelId}"))
                        {
                            t1.Start();
                            instance = doc.Create.NewFamilyInstance(XYZ.Zero, symbol, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                            doc.Regenerate();
                            SetParameters(instance, panelData);
                            doc.Regenerate();
                            instanceId = instance.Id;
                            t1.Commit();
                        }

                        // ===== Post-Transaction Exports =====
                        instance = doc.GetElement(instanceId) as FamilyInstance;
                        if (instance == null) throw new InvalidOperationException($"Failed to retrieve instance {panelId}.");

                        if (File.Exists(stlFilePath)) File.Delete(stlFilePath);
                        if (File.Exists(csvFilePath)) File.Delete(csvFilePath);

                        ExportToStl(doc, instance, stlFilePath);
                        ExportToCsv(instance, csvFilePath);
                    }
                    finally
                    {
                        // ===== Cleanup Transaction =====
                        if (instanceId != null && doc.GetElement(instanceId) != null)
                        {
                            using (var t2 = new Transaction(doc, $"Delete Panel {instanceId}"))
                            {
                                t2.Start();
                                doc.Delete(instanceId);
                                t2.Commit();
                            }
                        }
                    }
                }

                TaskDialog.Show("Success", $"{jsonInput.Panels.Count} panels processed successfully.");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = $"An error occurred: {ex.ToString()}";
                TaskDialog.Show("Error", message);
                return Result.Failed;
            }
        }

        private FamilySymbol LoadAndGetFamilySymbol(Document doc, string familyFilePath)
        {
            Family family = new FilteredElementCollector(doc).OfClass(typeof(Family)).FirstOrDefault(f => f.Name == Path.GetFileNameWithoutExtension(familyFilePath)) as Family;

            if (family == null)
            {
                using (var t = new Transaction(doc, "Load Family"))
                {
                    t.Start();
                    if (!doc.LoadFamily(familyFilePath, out family))
                    {
                        t.RollBack();
                        return null;
                    }
                    t.Commit();
                }
            }

            var symbol = doc.GetElement(family.GetFamilySymbolIds().First()) as FamilySymbol;
            if (symbol != null && !symbol.IsActive)
            {
                using (var t = new Transaction(doc, "Activate Symbol"))
                {
                    t.Start();
                    symbol.Activate();
                    doc.Regenerate();
                    t.Commit();
                }
            }
            return symbol;
        }

        private void SetParameters(FamilyInstance instance, Dictionary<string, object> parameters)
        {
            foreach (var item in parameters)
            {
                string paramName = item.Key;
                // Remap height and width to the correct 'desired' parameters in the family
                if (paramName == "panel_height") paramName = "panel_height_desired";
                if (paramName == "panel_width") paramName = "panel_width_desired";

                var p = instance.LookupParameter(paramName);
                if (p != null && !p.IsReadOnly)
                {
                    // Convert inches from JSON to internal feet units for Revit
                    if (p.StorageType == StorageType.Double && (paramName.Contains("height") || paramName.Contains("width") || paramName.Contains("clearance")))
                    {
                        p.Set(UnitUtils.ConvertToInternalUnits(Convert.ToDouble(item.Value), UnitTypeId.Inches));
                    }
                    else if (p.StorageType == StorageType.Integer)
                    {
                        p.Set(Convert.ToInt32(item.Value));
                    }
                    else if (p.StorageType == StorageType.String)
                    {
                        p.Set(item.Value.ToString());
                    }
                }
            }

            // Handle special floor clearance logic
            if (parameters.TryGetValue("panel_floor_clearance_selector", out var clearanceValue))
            {
                var selector = Convert.ToInt32(clearanceValue);
                string[] clearanceParams = { "1\"_clearance", "4.5\"_clearance", "9\"_clearance", "12\"_clearance" };
                for (int i = 0; i < clearanceParams.Length; i++)
                {
                    var p = instance.LookupParameter(clearanceParams[i]);
                    if (p != null) p.Set(i + 1 == selector ? 1 : 0);
                }
            }
        }

        private void ExportToStl(Document doc, FamilyInstance instance, string filePath)
        {
            var view = new FilteredElementCollector(doc).OfClass(typeof(View3D)).Cast<View3D>().FirstOrDefault(v => !v.IsTemplate);
            if (view == null) throw new InvalidOperationException("No 3D view found for STL export.");

            var exportOptions = new STLExportOptions { ViewId = view.Id };

            // View isolation is a model modification and requires a transaction.
            using (var t = new Transaction(doc, "Isolate for STL Export"))
            {
                t.Start();
                using (var tempViewIsolation = new TemporaryViewModeDisabler(view))
                {
                    view.IsolateElementsTemporary(new List<ElementId> { instance.Id });
                    // The export must happen while the view is isolated.
                    doc.Export(Path.GetDirectoryName(filePath), Path.GetFileName(filePath), exportOptions);
                }
                t.Commit();
            }
        }

        private void ExportToCsv(FamilyInstance instance, string filePath)
        {
            var csv = new StringBuilder();
            csv.AppendLine("Parameter Name,Parameter Value");

            foreach (Parameter param in instance.Parameters)
            {
                if (param == null || !param.HasValue) continue;
                var name = param.Definition.Name;
                string value;
                if (param.StorageType == StorageType.Double)
                {
                    value = param.AsValueString() ?? UnitUtils.ConvertFromInternalUnits(param.AsDouble(), param.GetUnitTypeId()).ToString();
                }
                else
                {
                    value = param.AsValueString() ?? param.AsString() ?? "(No Value)";
                }
                csv.AppendLine($"{Quote(name)},{Quote(value)}");
            }

            File.WriteAllText(filePath, csv.ToString());
        }

        private string Quote(string s) => $"\"{s.Replace("\"", "\"\"")}\"";

        private class TemporaryViewModeDisabler : IDisposable
        {
            private readonly View _view;
            public TemporaryViewModeDisabler(View view) { _view = view; }
            public void Dispose() { _view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate); }
        }
    }
}
