using ApartmentPlans.Domain;
using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Windows.Media.Media3D;

namespace ApartmentPlans
{
    public static class CommandViewSheetBuilder
    {
        public static List<ExportSettingsModel> CreateViewSheet(Document doc, List<ExportSettingsModel> viewsForAddSheet, PluginSettingsModel settings)
        {
            foreach (var view in viewsForAddSheet)
            {
                ViewSheet sheet = ViewSheet.Create(doc, ElementId.InvalidElementId);
                sheet.Name = view.ExportView.Name;
                XYZ center = XYZ.Zero;  // центр листа

                // Создаем видовое окно на листе, отображающее основной вид
                Viewport viewPort = Viewport.Create(doc, sheet.Id, view.ExportView.Id, center);
                Outline viewBox = viewPort.GetBoxOutline();
                XYZ legendPoint = new XYZ(viewBox.MinimumPoint.X + 0.2, viewBox.MinimumPoint.Y - 0.02, 0);
                if (settings.TypeLegend != null)
                {
                    Viewport.Create(doc, sheet.Id, settings.TypeLegend.Id, legendPoint);
                }

                // Для отключения выносной надписи
                ElementType viewPortType = doc.GetElement(viewPort.GetTypeId()) as ElementType;
                viewPortType.get_Parameter(BuiltInParameter.VIEWPORT_ATTR_SHOW_LABEL).Set(0);
                view.ExportViewSheet = sheet;
            }
            return viewsForAddSheet;
        }
    }
}
