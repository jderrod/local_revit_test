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

            if (!File.Exists(familyPath))
            {
                message = "Family file not found at: " + familyPath;
                TaskDialog.Show("Error", message);
                return Result.Failed;
            }

            Family family = null;
            using (Transaction tempTrans = new Transaction(doc, "Load Family for Inspection"))
            {
                tempTrans.Start();
                doc.LoadFamily(familyPath, out family);
                if (family == null)
                {
                    message = "Could not load the family.";
                    TaskDialog.Show("Error", message);
                    tempTrans.RollBack();
                    return Result.Failed;
                }
                FamilySymbol symbol = doc.GetElement(family.GetFamilySymbolIds().FirstOrDefault()) as FamilySymbol;
                if (symbol != null)
                {
                    var parameterNames = new System.Text.StringBuilder();
                    parameterNames.AppendLine("--- Available Family Parameters ---");
                    foreach (Parameter param in symbol.Parameters)
                    {
                        parameterNames.AppendLine($"- {param.Definition.Name}");
                    }
                    string logPath = Path.Combine(assemblyLocation, "parameter_log.txt");
                    System.IO.File.WriteAllText(logPath, parameterNames.ToString());
                }

                // Roll back the transaction used for inspection
                tempTrans.RollBack();
            }

            string heightRange = "72\" - 96\"";
            string widthRange = "5\" - 80\"";

            // --- Get User Input ---
            double panelHeightInches = 0;
            double panelWidthInches = 0;
            using (var form = new ParameterInputForm(heightRange, widthRange))
            {
                if (form.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    if (!double.TryParse(form.PanelHeightValue, out panelHeightInches) || !double.TryParse(form.PanelWidthValue, out panelWidthInches))
                    {
                        message = "Invalid input. Please enter numeric values for height and width.";
                        TaskDialog.Show("Error", message);
                        return Result.Failed;
                    }
                }
                else
                {
                    return Result.Cancelled;
                }
            }

            // --- Create Family Instance in a Transaction ---
            using (Transaction trans = new Transaction(doc, "Create Family Instance"))
            {
                trans.Start();

                // Reload the family in this transaction
                doc.LoadFamily(familyPath, out family);
                FamilySymbol familySymbol = doc.GetElement(family.GetFamilySymbolIds().FirstOrDefault()) as FamilySymbol;

                if (!familySymbol.IsActive)
                {
                    familySymbol.Activate();
                    doc.Regenerate();
                }

                Level level = new FilteredElementCollector(doc).OfClass(typeof(Level)).FirstOrDefault() as Level;
                if (level == null)
                {
                    message = "Could not find a valid Level to place the family instance.";
                    TaskDialog.Show("Error", message);
                    trans.RollBack();
                    return Result.Failed;
                }

                FamilyInstance instance = doc.Create.NewFamilyInstance(XYZ.Zero, familySymbol, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                if (instance == null)
                {
                    message = "Failed to create the family instance.";
                    TaskDialog.Show("Error", message);
                    trans.RollBack();
                    return Result.Failed;
                }

                doc.Regenerate(); // Needed to ensure parameters are ready to be set

                // Convert inches to feet for Revit's internal units
                double panelHeightFeet = panelHeightInches / 12.0;
                double panelWidthFeet = panelWidthInches / 12.0;

                // Set parameters with robust checks
                Parameter heightParam = instance.LookupParameter("panel_height_desired");
                if (heightParam != null && !heightParam.IsReadOnly)
                {
                    heightParam.Set(panelHeightFeet);
                }
                else
                {
                    TaskDialog.Show("Warning", "Could not find or set the 'panel_height_desired' parameter. Please check the family definition.");
                }

                Parameter widthParam = instance.LookupParameter("panel_width_desired");
                if (widthParam != null && !widthParam.IsReadOnly)
                {
                    widthParam.Set(panelWidthFeet);
                }
                else
                {
                    TaskDialog.Show("Warning", "Could not find or set the 'panel_width_desired' parameter. Please check the family definition.");
                }

                trans.Commit();
            }

            // --- Export STL after transaction ---
            View3D view3D = new FilteredElementCollector(doc).OfClass(typeof(View3D)).Cast<View3D>().FirstOrDefault(v => !v.IsTemplate);
            if (view3D == null)
            {
                message = "Could not find a 3D view to export.";
                TaskDialog.Show("Error", message);
                return Result.Failed;
            }

            try
            {
                var stlOptions = new STLExportOptions { ViewId = view3D.Id };
                string exportFolder = Path.GetDirectoryName(stlExportPath);
                Directory.CreateDirectory(exportFolder);
                string exportFileName = Path.GetFileName(stlExportPath);
                doc.Export(exportFolder, exportFileName, stlOptions);
                TaskDialog.Show("Success", $"Instance created and exported to:\n{stlExportPath}");
            }
            catch (Exception ex)
            {
                message = $"Failed to export STL file. {ex.Message}";
                TaskDialog.Show("Error", message);
                return Result.Failed;
            }

            return Result.Succeeded;
        }
    }
}
