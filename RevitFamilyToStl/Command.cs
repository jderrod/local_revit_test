using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.IO;
using System.Linq;

namespace RevitFamilyToStl
{
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            Document doc = uiApp.ActiveUIDocument.Document;

            string assemblyLocation = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string familyPath = Path.Combine(assemblyLocation, "3X8X_panel_v1_2025_06_26.rfa");
            string stlExportPath = Path.Combine(assemblyLocation, "output.stl");
            string csvExportPath = Path.Combine(assemblyLocation, "instance_parameters.csv");

            if (!File.Exists(familyPath))
            {
                message = "Family file not found at: " + familyPath;
                TaskDialog.Show("Error", message);
                return Result.Failed;
            }

            // --- Get User Input ---
            double panelHeightInches, panelWidthInches;
            int floorClearanceSelector;
            bool isInlineLeft, isInlineRight;
            string workOrderNumber, componentId, seriesId;

            using (var form = new ParameterInputForm())
            {
                if (form.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return Result.Cancelled;
                }

                if (!double.TryParse(form.PanelHeightValue, out panelHeightInches) ||
                    !double.TryParse(form.PanelWidthValue, out panelWidthInches))
                {
                    message = "Invalid numeric input for Height or Width.";
                    TaskDialog.Show("Error", message);
                    return Result.Failed;
                }

                floorClearanceSelector = form.FloorClearanceSelectorValue;
                isInlineLeft = form.IsInlineLeft;
                isInlineRight = form.IsInlineRight;
                workOrderNumber = form.WorkOrderNumber;
                componentId = form.ComponentId;
                seriesId = form.SeriesId;
            }

            FamilyInstance instance = null;
            using (Transaction trans = new Transaction(doc, "Create and Modify Family Instance"))
            {
                trans.Start();

                Family family;
                if (!doc.LoadFamily(familyPath, out family))
                {
                    message = "Could not load the family.";
                    TaskDialog.Show("Error", message);
                    trans.RollBack();
                    return Result.Failed;
                }

                FamilySymbol familySymbol = doc.GetElement(family.GetFamilySymbolIds().First()) as FamilySymbol;
                if (!familySymbol.IsActive) { familySymbol.Activate(); }

                Level level = new FilteredElementCollector(doc).OfClass(typeof(Level)).FirstElement() as Level;
                if (level == null) { /* error handling */ return Result.Failed; }

                instance = doc.Create.NewFamilyInstance(XYZ.Zero, familySymbol, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                doc.Regenerate();

                // Set parameters from form input
                SetParameter(instance, "panel_height_desired", panelHeightInches / 12.0); // to feet
                SetParameter(instance, "panel_width_desired", panelWidthInches / 12.0); // to feet
                // panel_thickness is read-only
                SetParameter(instance, "panel_floor_clearance_selector", floorClearanceSelector);
                SetParameter(instance, "panel_inline_left_side", isInlineLeft ? 1 : 0); // bool to int
                SetParameter(instance, "panel_inline_right_side", isInlineRight ? 1 : 0); // bool to int
                SetParameter(instance, "panel_work_order_number", workOrderNumber);
                SetParameter(instance, "panel_component_id", componentId);
                SetParameter(instance, "panel_series_id", seriesId);
                
                trans.Commit();
            }

            // --- Export to CSV and STL ---
            ExportParametersToCsv(instance, csvExportPath);
            ExportInstanceToStl(instance, stlExportPath, ref message);

            TaskDialog.Show("Success", $"Instance created. STL and CSV exported to:\n{assemblyLocation}");
            return Result.Succeeded;
        }

        private void SetParameter(FamilyInstance instance, string name, object value)
        {
            Parameter param = instance.LookupParameter(name);
            if (param != null && !param.IsReadOnly)
            {
                try
                {
                    if (value is double d) param.Set(d);
                    else if (value is int i) param.Set(i);
                    else if (value is string s) param.Set(s);
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("Parameter Error", $"Could not set '{name}' to '{value}'. Error: {ex.Message}");
                }
            }
            else
            {
                TaskDialog.Show("Warning", $"Could not find or access parameter: '{name}'.");
            }
        }

        private void ExportParametersToCsv(FamilyInstance instance, string path)
        {
            try
            {
                var csv = new System.Text.StringBuilder();
                csv.AppendLine("Parameter Name,Parameter Value");
                foreach (Parameter p in instance.Parameters.Cast<Parameter>().OrderBy(p => p.Definition.Name))
                {
                    string val = p.AsValueString() ?? p.AsString() ?? "(No Value)";
                    if (val.Contains(",")) val = $"\"{val}\"";
                    csv.AppendLine($"{p.Definition.Name},{val}");
                }
                File.WriteAllText(path, csv.ToString());
            }
            catch { /* Silent fail or log */ }
        }

        private bool ExportInstanceToStl(FamilyInstance instance, string path, ref string message)
        {
            Document doc = instance.Document;
            View3D view = new FilteredElementCollector(doc).OfClass(typeof(View3D)).Cast<View3D>().FirstOrDefault(v => !v.IsTemplate);
            if (view == null)
            {
                message = "No 3D view found for STL export.";
                return false;
            }

            try
            {
                var options = new STLExportOptions { ViewId = view.Id };
                string dir = Path.GetDirectoryName(path);
                string name = Path.GetFileName(path);
                Directory.CreateDirectory(dir);
                doc.Export(dir, name, options);
                return true;
            }
            catch (Exception ex)
            {
                message = "Failed to export STL: " + ex.Message;
                return false;
            }
        }
    }
}
