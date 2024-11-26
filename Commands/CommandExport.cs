using ApartmentPlans.Domain;
using ApartmentPlans.Resources;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using ImageMagick;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using Path = System.IO.Path;
using StorageType = Autodesk.Revit.DB.StorageType;

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

        public static (bool isAppatament,string path) ExportToRevit(Document doc, ExportSettingsModel view, PluginSettingsModel settings)
        {
            ImageExportOptions options = new ImageExportOptions
            {
                PixelSize = settings.ExportSettings.OffsetLinkAdvance,
                ExportRange = ExportRange.SetOfViews,
                HLRandWFViewsFileType = ImageFileType.BMP,
                FitDirection = FitDirectionType.Horizontal
            };

            var exSettings = settings.ExportSettings;
            string viewName = view.ExportViewSheet.Name;
            string templateName = ExtractType(viewName);

            var roomCollector = new FilteredElementCollector(doc, view.ExportView.Id)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .Cast<Room>()
                .FirstOrDefault();

            List<string> joinName = new List<string>
            {
                exSettings.NameProject,
                exSettings.NameCaseParameter?.Definition?.Name != null
                    ? roomCollector?.LookupParameter(exSettings.NameCaseParameter.Definition.Name)?.AsValueString()
                    : null,
                exSettings.NameSectionParameter?.Definition?.Name != null
                    ? roomCollector?.LookupParameter(exSettings.NameSectionParameter.Definition.Name)?.AsValueString()
                    : null,
                exSettings.NameFloorParameter?.Definition?.Name != null
                    ? roomCollector?.LookupParameter(exSettings.NameFloorParameter.Definition.Name)?.AsValueString()
                    : null,
                view.NumberApartment,
                view.AdditionalWord,
                templateName,
            };

            string nameFile = string.Join("_", joinName.Where(name => !string.IsNullOrEmpty(name)));

            string targetFolderPath;
            if (exSettings.PathToSave.Contains("Путь: по умолчанию"))
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
                targetFolderPath = exSettings.PathToSave;
            }
            options.FilePath = Path.Combine(targetFolderPath, nameFile);
            options.SetViewsAndSheets(new List<ElementId> { view.ExportViewSheet.Id });
            string viewSheetName = view.ExportViewSheet.Title;
            doc.ExportImage(options);
            string exportedFile = Directory.GetFiles(targetFolderPath, "*.bmp")
                                    .FirstOrDefault(f => f.Contains(nameFile)); // Файл с именем листа
            bool isAppatament = view.ExportViewSheet != null
            && !new[] { "заливка_цветная", "марки", "общий_этаж" }
            .Any(word => view.ExportViewSheet.Name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0);



            // Формируем новый путь без имени листа
            string newFilePath = Path.Combine(targetFolderPath, nameFile);
            string oldFilePath = Path.Combine(targetFolderPath, viewSheetName);
            // Переименовываем файл
            File.Move(exportedFile, newFilePath);
            return (isAppatament, path:newFilePath);

        }
        public static void ConvertToSvg((bool isAppatment, string pathImage) imageModel, PluginSettingsModel settingsModel, int? customIndex =null)
        {
            WaitForFile(imageModel.pathImage);
            // Обрабатываем изображение и конвертируем в SVG
            using (var image = new MagickImage(imageModel.pathImage))
            {
                image.Format = MagickFormat.Svg;
                string svgPath = $"{imageModel.pathImage}.svg";
                image.Write(svgPath);

                // Загружаем SVG и модифицируем атрибуты
                XDocument xdoc = XDocument.Load(svgPath);
                XElement svgElement = xdoc.Root;

                // Обновление атрибутов width и height
                if (!settingsModel.ExportSettings.IsСuttingBound)
                {
                    svgElement.SetAttributeValue("width", settingsModel.ExportSettings.OffsetLinkAdvance);
                    svgElement.SetAttributeValue("height", settingsModel.ExportSettings.OffsetLinkAdvance);
                }

                // Обработка атрибута viewBox
                string viewBoxValue = svgElement.Attribute("viewBox")?.Value;
                if (!string.IsNullOrEmpty(viewBoxValue))
                {
                    string[] viewBoxParts = viewBoxValue.Split(' ');
                    if (viewBoxParts.Length == 4)
                    {
                        double minX = double.Parse(viewBoxParts[0]);
                        double minY = double.Parse(viewBoxParts[1]);
                        double width = double.Parse(viewBoxParts[2]);
                        double height = double.Parse(viewBoxParts[3]);

                        // Уменьшаем размеры viewBox для квартир
                        if (imageModel.isAppatment) 
                        {
                            minX = 20;
                            minY = 20;
                            width -= 40;
                            height -= 40;
                        }
                        // Задание оступов
                        if (settingsModel.ExportSettings.OffsetLink !=0)
                        {
                            int count = settingsModel.ExportSettings.OffsetLink;
                            minX -= count;
                            minY -= count;
                            width += count*2;
                            height += count * 2;
                        }
                        // Если в даьнейшем пригодится - сейчас не используется
                        if (customIndex!=null)
                        {
                            minX = (double)customIndex;
                            minY = (double)customIndex;
                            width -= (double)customIndex*2;
                            height -= (double)customIndex*2;
                        }
                        // Обновляем атрибут viewBox
                        svgElement.SetAttributeValue("viewBox", $"{minX} {minY} {width} {height}");
                    }
                }

                // Сохраняем обновленный SVG файл
                xdoc.Save(svgPath);

                // Удаляем временный файл изображения
                File.Delete(imageModel.pathImage);
            }
        }
        private static string CreateExternalSharedParamFile(string sharedParameterFile)
        {
            string tempFolder = System.IO.Path.GetTempPath();
            string fullPath = System.IO.Path.Combine(tempFolder, sharedParameterFile);
            System.IO.FileStream fileStream = System.IO.File.Create(fullPath);

            fileStream.Close();
            return fullPath;
        }

        // Метод для ожидания доступа к файлу
        private static void WaitForFile(string filePath)
        {
            const int maxRetries = 20; // Максимальное количество попыток
            const int delay = 2000; // Задержка между попытками в миллисекундах

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    // Пытаемся открыть файл с возможностью записи
                    using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        return; // Если получилось открыть файл, выходим из метода
                    }
                }
                catch (IOException)
                {
                    // Если файл занят, ждем перед следующей попыткой
                    Thread.Sleep(delay);
                }
            }

            throw new IOException($"Файл {filePath} недоступен после {maxRetries} попыток.");
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
            var rooms = new FilteredElementCollector(doc)
                                            .OfCategory(BuiltInCategory.OST_Rooms)
                                            .WhereElementIsNotElementType()
                                            .OfClass(typeof(SpatialElement))
                                            .Cast<Room>()
                                            .ToList();
            // Создание нового вида спецификации
            ViewSchedule roomSchedule = ViewSchedule.CreateSchedule(doc, new ElementId(BuiltInCategory.OST_Rooms));
            roomSchedule.Name = newName;
            Category roomsCategory = Category.GetCategory(doc, BuiltInCategory.OST_Rooms);

            // Получаем параметр по имени
            foreach (var param in settings.ItemsSchedule)
            {
                if (param != null)
                {
                    try
                    {
                        switch (param)
                        {
                            case var n when n.SelectedObjectParameter.Name.Contains("Адрес проекта"):
                                {
                                    var docParam = doc.ProjectInformation.Parameters
                               .Cast<Parameter>()
                               .FirstOrDefault(p => p.Definition.Name.Contains(param.SelectedObjectParameter.Name));
                                    if (docParam != null)
                                    {
                                        string value = docParam.AsString();
                                        string fileName = "ФОП_temp.txt";
                                        string originalFile = string.Empty;

                                        fileName = CreateExternalSharedParamFile(fileName);
                                        originalFile = doc.Application.SharedParametersFilename;
                                        doc.Application.SharedParametersFilename = fileName;
                                        DefinitionFile file = doc?.Application?.OpenSharedParameterFile();
                                        if (file != null)
                                        {
                                            var group = file.Groups.Create("temp");
                                            var defenition = group.Definitions.Create(new ExternalDefinitionCreationOptions(param.SelectedObjectParameter.Name, SpecTypeId.String.Text));
                                            var categorySet = doc.Application.Create.NewCategorySet();
                                            categorySet.Insert(Category.GetCategory(doc, BuiltInCategory.OST_Rooms));
                                            var binding = doc.Application.Create.NewInstanceBinding(categorySet);
                                            doc.ParameterBindings.Insert(defenition, binding, BuiltInParameterGroup.PG_DATA);
                                            doc.Application.SharedParametersFilename = originalFile;
                                            foreach (Room room in rooms)
                                            {
                                                Parameter roomParam = room.LookupParameter(param.SelectedObjectParameter.Name);
                                                if (roomParam != null && roomParam.StorageType == StorageType.String)
                                                {
                                                    roomParam.Set(value);
                                                }
                                            }
                                            param.SelectedObjectParameter.parameter = rooms.FirstOrDefault().LookupParameter(param.SelectedObjectParameter.Name);
                                        }
                                    }
                                }
                                break;
                            case var n when n.Name.Contains("Номер п/п секции (размещения)"):
                                {
                                    foreach (Room room in rooms)
                                    {
                                        Parameter pr = room.LookupParameter(n.SelectedObjectParameter.Name);
                                        var d = pr.AsValueString();
                                        if (pr != null && (string.IsNullOrEmpty(pr.AsValueString()) || pr.AsValueString() == "0"))
                                        {
                                            switch (pr.StorageType)
                                            {
                                                case StorageType.String:
                                                    pr.Set("1");
                                                    break;

                                                case StorageType.Integer:
                                                    pr.Set(1);
                                                    break;

                                                case StorageType.Double:
                                                    pr.Set(1.0);
                                                    break;
                                            }
                                        }
                                    }
                                    param.SelectedObjectParameter.parameter = rooms.FirstOrDefault().LookupParameter(param.SelectedObjectParameter.Name);
                                }
                                break;
                        }
                        IList<SchedulableField> schedulableFields = roomSchedule.Definition.GetSchedulableFields();
                        SchedulableField nameField = schedulableFields.FirstOrDefault(field => field.GetName(doc).
                        Equals(param.SelectedObjectParameter.parameter.Definition.Name, StringComparison.OrdinalIgnoreCase));
                        ScheduleField scheduleField = roomSchedule.Definition.AddField(nameField);
                        scheduleField.ColumnHeading = param.Name;
                        FormatOptions formatOptions = scheduleField.GetFormatOptions();
                        ForgeTypeId typeId = scheduleField.GetSpecTypeId();
                        if (formatOptions != null)
                        {
                            formatOptions.UseDefault = false;
                            formatOptions.RoundingMethod = RoundingMethod.Nearest;
                            switch (typeId)
                            {
                                case var _ when typeId == SpecTypeId.Area:
                                    formatOptions.SetUnitTypeId(UnitTypeId.SquareMeters);
                                    break;
                                case var _ when typeId == SpecTypeId.Volume:
                                    formatOptions.SetUnitTypeId(UnitTypeId.CubicMeters);
                                    break;
                            }
                            formatOptions.SetSymbolTypeId(new ForgeTypeId(""));
                            formatOptions.Accuracy = 0.01;
                            scheduleField.SetFormatOptions(formatOptions);

                        }
                    }
                    catch
                    {
                        Console.WriteLine("Параметр не может быть добавлен");
                    }
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

            // Читаем CSV и сохраняем в XLSX
            string finalFilePath = Path.Combine(targetFolderPath, roomSchedule.Name + ".xlsx");
            ConvertCsvToXlsx(tempFilePath, finalFilePath);
            // Удалить временный файл
            File.Delete(tempFilePath);
        }
        public static void ConvertCsvToXlsx(string csvFilePath, string xlsxFilePath)
        {
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Sheet1");

                var csvLines = File.ReadAllLines(csvFilePath, Encoding.UTF8);
                for (int row = 0; row < csvLines.Length; row++)
                {
                    var columns = csvLines[row].Split(';');
                    for (int col = 0; col < columns.Length; col++)
                    {
                        var cellValue = columns[col]
                            .Replace('.', ',')      // Заменяем точки на запятые
                            .Replace("\"", "")      // Удаляем двойные кавычки
                            .Replace("'", "");      // Удаляем одинарные кавычки

                        if (double.TryParse(cellValue, out double numericValue))
                        {
                            worksheet.Cells[row + 1, col + 1].Value = numericValue;

                            // Устанавливаем формат числовой: без десятичных для целых и с десятичными для дробных
                            if (numericValue % 1 == 0)
                            {
                                worksheet.Cells[row + 1, col + 1].Style.Numberformat.Format = "#,##0"; // Для целых чисел
                            }
                            else
                            {
                                worksheet.Cells[row + 1, col + 1].Style.Numberformat.Format = "#,##0.00"; // Для дробных чисел
                            }
                        }
                        else
                        {
                            // Если преобразование не удалось, сохраняем как текст
                            worksheet.Cells[row + 1, col + 1].Value = cellValue;
                        }


                    }
                }

                // Сохраняем в XLSX
                FileInfo excelFile = new FileInfo(xlsxFilePath);
                package.SaveAs(excelFile);
            }
        }
        public static void ExportSheetModule(Document doc, List<WorkViewModel> ViewsSheet, PluginSettingsModel settings)
        {
            Autodesk.Revit.DB.View view = null;
            XmlDocument xmlDocument = new XmlDocument();
            foreach (var activeViewSheet in ViewsSheet.Where(q => q.IsSelected))
            {
                settings.ExportSettings.NumberApartment = null;
                settings.ExportSettings.AdditionalWord = null;
                ViewSheet viewSheet = activeViewSheet.View as ViewSheet;
                settings.ExportSettings.ExportViewSheet = viewSheet;
                ICollection<ElementId> dependentElements = viewSheet.GetDependentElements(null);
                List<Viewport> viewports = dependentElements
                  .Select(id => doc.GetElement(id))
                  .OfType<Viewport>()
                  .ToList();
                foreach (Viewport viewport in viewports)
                {
                    var element = doc.GetElement(viewport.ViewId);
                    if (element is View)
                    {
                        view = element as View;
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
                    (bool isAppatment,string path) image = ExportToRevit(doc, settings.ExportSettings, settings);
                    ConvertToSvg(image, settings);

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
