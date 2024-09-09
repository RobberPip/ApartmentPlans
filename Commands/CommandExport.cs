using ApartmentPlans.Domain;
using ApartmentPlans.Resources;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using PdfToSvg;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

namespace ApartmentPlans
{
    public static class CommandExport
    {
        private static string ExtractType(string templateName)
        {
            List<string> types = new List<string>
            {
                ValidNamesResources.TypePremium,
                ValidNamesResources.TypeComfort,
                ValidNamesResources.TypeBusiness
            };

            foreach (string type in types)
            {
                if (templateName.IndexOf(type, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return type;
                }
            }

            return string.Empty;
        }

        public static string ExportToPdf(Document doc, ExportSettingsModel view, PluginSettingsModel settings)
        {
            PDFExportOptions options = new PDFExportOptions();
            var exSettigs = settings.ExportSettings;
            string viewName = view.ExportViewSheet.Name;
            string templateName = ExtractType(viewName);
            var roomCollector = new FilteredElementCollector(doc, view.ExportView.Id)
            .OfCategory(BuiltInCategory.OST_Rooms)
            .Cast<Room>().FirstOrDefault();
            List<string> joinName = new List<string>
            {
                exSettigs.NameProject,
                exSettigs.NameCaseParameter?.Definition.Name != null ? roomCollector.LookupParameter(exSettigs.NameCaseParameter.Definition.Name)?.AsValueString() : null,
                exSettigs.NameSectionParameter?.Definition.Name != null ? roomCollector.LookupParameter(exSettigs.NameSectionParameter.Definition.Name)?.AsValueString() : null,
                exSettigs.NameFloorParameter?.Definition.Name != null ? roomCollector.LookupParameter(exSettigs.NameFloorParameter.Definition.Name)?.AsValueString() : null,
                view.NumberApartment,
                view.AdditionalWord,
                templateName,
            };
            string nameFile = string.Join("_", joinName.Where(name => !string.IsNullOrEmpty(name)));
            options.FileName = nameFile != string.Empty ? nameFile : "Output";

            string targetFolderPath;
            if (exSettigs.PathToSave.Contains("Путь: по умолачнию"))
            {
                string tempFolderPath = Path.GetTempPath();
                targetFolderPath = Path.Combine(tempFolderPath, "RevitExport");
                if (!Directory.Exists(targetFolderPath))
                {
                    Directory.CreateDirectory(targetFolderPath);
                }
            }
            else
            {
                targetFolderPath = exSettigs.PathToSave;
            }
            doc.Export(targetFolderPath, new List<ElementId>() { view.ExportViewSheet.Id }, options);
            return targetFolderPath + $"\\{options.FileName}";
        }
        public static void ConvertPdfToSvg(string path)
        {
            // Открываем PDF и сохраняем каждую страницу как SVG
            using (var docs = PdfDocument.Open($"{path}.pdf"))
            {
                foreach (var page in docs.Pages)
                {
                    page.SaveAsSvg($"{path}_temp.svg");
                }
            }

            // Прочитаем SVG файл как XML документ
            XmlDocument xmlDocument = new XmlDocument();
            xmlDocument.Load($"{path}_temp.svg");

            // Получаем корневой элемент SVG
            XmlElement svgElement = xmlDocument.DocumentElement;

            // Изменяем размеры SVG
            svgElement.SetAttribute("width", "1400");
            svgElement.SetAttribute("height", "1400");

            // Изменяем атрибут viewBox
            string viewBoxValue = svgElement.GetAttribute("viewBox");
            if (!string.IsNullOrEmpty(viewBoxValue))
            {
                string[] viewBoxParts = viewBoxValue.Split(' ');
                if (viewBoxParts.Length == 4)
                {
                    if (float.TryParse(viewBoxParts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                        float.TryParse(viewBoxParts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                        float.TryParse(viewBoxParts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float width) &&
                        float.TryParse(viewBoxParts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float height))
                    {
                        // Устанавливаем первые два значения в 12.5
                        x = 12.5f;
                        y = 12.5f;

                        // Устанавливаем последние два значения уменьшенными на 25
                        width -= 25;
                        height -= 25;

                        // Обновляем атрибут viewBox
                        svgElement.SetAttribute("viewBox",
                        $"{x.ToString(CultureInfo.InvariantCulture)} " +
                        $"{y.ToString(CultureInfo.InvariantCulture)} " +
                        $"{width.ToString(CultureInfo.InvariantCulture)} " +
                        $"{height.ToString(CultureInfo.InvariantCulture)}");

                    }
                }
            }

            // Сохраняем измененный SVG в файл
            xmlDocument.Save($"{path}.svg");

            // Удаляем временные файлы
            File.Delete($"{path}.pdf");
            File.Delete($"{path}_temp.svg");
        }

        public static void ExportCSV(Document doc, PluginSettingsModel settings)
        {
            if (settings.ItemsSchedule.Count == 0)
            {
                return;
            }
            var schedules = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSchedule))
            .Cast<ViewSchedule>()
            .ToList();
            HashSet<string> existingNames = new HashSet<string>(schedules.Select(s => s.Name));

            string baseName = "Экспликация помещений";
            string newName = baseName;
            int index = 1;
            while (existingNames.Contains(newName))
            {
                newName = $"{baseName} ({index})";
                index++;
            }
            // Создание нового вида спецификации
            ViewSchedule roomSchedule = ViewSchedule.CreateSchedule(doc, new ElementId(BuiltInCategory.OST_Rooms));
            roomSchedule.Name = newName;
            Category roomsCategory = Category.GetCategory(doc, BuiltInCategory.OST_Rooms);
            IList<SchedulableField> schedulableFields = roomSchedule.Definition.GetSchedulableFields();
            // Получаем параметр по имени
            foreach (var param in settings.ItemsSchedule)
            {
                if (param != null)
                {
                    SchedulableField nameField = schedulableFields.FirstOrDefault(field => field.GetName(doc).Equals(param.SelectedObjectParameter.parameter.Definition.Name, StringComparison.OrdinalIgnoreCase));
                    ScheduleField scheduleField = roomSchedule.Definition.AddField(nameField);
                    scheduleField.ColumnHeading = param.Name;
                }
            }
            var exSettigs = settings.ExportSettings;
            string targetFolderPath;
            if (exSettigs.PathToSave.Contains("Путь: по умолачнию"))
            {
                string tempFolderPath = Path.GetTempPath();
                targetFolderPath = Path.Combine(tempFolderPath, "RevitExport");
                if (!Directory.Exists(targetFolderPath))
                {
                    Directory.CreateDirectory(targetFolderPath);
                }
            }
            else
            {
                targetFolderPath = exSettigs.PathToSave;
            }
            // Экспорт спецификации в csv
            string tempFilePath = Path.Combine(targetFolderPath, roomSchedule.Name + "_temp.csv");
            ViewScheduleExportOptions opt = new ViewScheduleExportOptions() { FieldDelimiter = ";" };
            roomSchedule.Export(targetFolderPath, roomSchedule.Name + "_temp.csv", opt);

            // Прочитать временный CSV файл и сохранить его в UTF-16 LE
            string finalFilePath = Path.Combine(targetFolderPath, roomSchedule.Name + ".csv");
            ConvertCsvToUtf16Le(tempFilePath, finalFilePath);

            // Удалить временный файл
            File.Delete(tempFilePath);
        }
        private static void ConvertCsvToUtf16Le(string inputFilePath, string outputFilePath)
        {
            // Прочитать содержимое CSV файла с оригинальной кодировкой
            string[] csvLines = File.ReadAllLines(inputFilePath, System.Text.Encoding.Default);

            // Записать содержимое в новый CSV файл с кодировкой UTF-16 LE
            File.WriteAllLines(outputFilePath, csvLines, System.Text.Encoding.Unicode);
        }
        public static void ExportSheetModule(Document doc, List<WorkViewModel> ViewsSheet, PluginSettingsModel settings)
        {
            Autodesk.Revit.DB.View view = null;
            foreach (var activeViewSheet in ViewsSheet.Where(q => q.IsSelected))
            {
                settings.ExportSettings.NumberApartment = null;
                settings.ExportSettings.AdditionalWord=null;
                ViewSheet viewSheet = activeViewSheet.View as ViewSheet;
                settings.ExportSettings.ExportViewSheet = viewSheet;
                ICollection<ElementId> dependentElements = viewSheet.GetDependentElements(null);
                List<Viewport> viewports = dependentElements
                  .Select(id => doc.GetElement(id))
                  .OfType<Viewport>()
                  .ToList();
                foreach (var viewport in viewports)
                {
                    var element = doc.GetElement(viewport.ViewId);
                    if (element is ViewPlan)
                    {
                        view = element as ViewPlan;
                    }
                }

                if (view != null)
                {
                    settings.ExportSettings.ExportView = view;

                    if (viewSheet.Name.Contains("_кв_"))
                    {
                        var match = Regex.Match(viewSheet.Name, @"_кв_(.*?)_");

                        if (match.Success)
                        {
                            // Извлекаем номер квартиры из группы захвата
                            settings.ExportSettings.NumberApartment = match.Groups[1].Value;
                        }
                        
                    }
                    switch (viewSheet.Name)
                    {
                        case string name when name.Contains("_марки_"):
                            settings.ExportSettings.AdditionalWord = "marks";
                            break;
                        case string name when name.Contains("_заливка_"):
                            settings.ExportSettings.AdditionalWord = "mask";
                            break;
                        case string name when name.Contains("_общий_этаж_"):
                            break;
                        case string name when !name.Contains("_кв_"):
                            settings.ExportSettings.AdditionalWord = viewSheet.Name;
                            break;
                    }
                    string pathPdf = ExportToPdf(doc, settings.ExportSettings, settings);
                    ConvertPdfToSvg(pathPdf);
                }
            }
            using (Transaction tx = new Transaction(doc, "Export"))
            {
                tx.Start();
                ExportCSV(doc, settings);
                tx.Commit();
            }
        }
    }
}
