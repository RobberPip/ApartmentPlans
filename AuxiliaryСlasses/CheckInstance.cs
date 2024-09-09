using ApartmentPlans.Domain;
using ApartmentPlans.Resources;
using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ApartmentPlans
{
    public static class CheckInstance
    {
        public static List<ViewPlan> CheckTemplatesInProject(Document doc, List<MultiSelectModel> templates)
        {
            var views = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewPlan))
                    .Cast<ViewPlan>()
                    .Where(plan => plan.IsTemplate && templates.Select(template => template.Name).Contains(plan.Name));
            Category detailItemCategory = Category.GetCategory(doc, BuiltInCategory.OST_DetailComponents);
            foreach (var view in views)
            {
                if (detailItemCategory != null)
                {
                    // Создаем пустые настройки графической замены
                    OverrideGraphicSettings ogs = new OverrideGraphicSettings();

                    // Устанавливаем пустые настройки для этой категории в шаблоне вида
                    view.SetCategoryOverrides(detailItemCategory.Id, ogs);
                }
            }
            return views.ToList();
        }
        public static (List<PlaceMarkModel> Marks, List<string> ExecptionMarks) CheckMarksInProject(Document doc, PluginSettingsModel settings)
        {
            var marksInProject = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_GenericAnnotation)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(x => x is AnnotationSymbolType)
                .Cast<AnnotationSymbolType>()
                .ToList();

            var matchedElements = new List<PlaceMarkModel>();
            var unmatchedItems = new List<RatioObjectModel<MultiSelectModel>>(settings.ItemsMark);

            // Используем HashSet для быстрого поиска совпадений
            var unmatchedItemsSet = new HashSet<string>(unmatchedItems.Select(item => item.Name));
            var itemsToRemove = new List<RatioObjectModel<MultiSelectModel>>();

            foreach (var markInProject in marksInProject)
            {
                if (unmatchedItemsSet.Contains(markInProject.Name))
                {
                    var matchedItem = unmatchedItems.First(item => item.Name == markInProject.Name);
                    var matchedItems = matchedItem.CheckedObjects;
                    var namesRoom = matchedItems.Select(co => co.Name).ToList();
                    matchedElements.Add(new PlaceMarkModel
                    {
                        mark = markInProject,
                        namesRoom = namesRoom
                    });

                    itemsToRemove.Add(matchedItem);
                    unmatchedItemsSet.Remove(markInProject.Name);
                }
            }

            // Удаляем элементы после завершения цикла
            foreach (var item in itemsToRemove)
            {
                unmatchedItems.Remove(item);
            }

            var exceptionMarks = unmatchedItems.Select(q => q.Name).ToList();

            return (Marks: matchedElements, ExecptionMarks: exceptionMarks);
        }

    }
}
