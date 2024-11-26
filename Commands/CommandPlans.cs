using ApartmentPlans.AuxiliaryСlasses;
using ApartmentPlans.Domain;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Clipper2Lib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using JoinType = Clipper2Lib.JoinType;
using Line = Autodesk.Revit.DB.Line;
using MessageBox = System.Windows.Forms.MessageBox;
using View = Autodesk.Revit.DB.View;

namespace ApartmentPlans
{
    public class CommandPlans
    {
        public CommandPlans(UIApplication application)
        {
            uiApp = application;
            uidoc = uiApp.ActiveUIDocument;
            app = uiApp.Application;
            doc = uidoc.Document;
        }
        public void TransactionBypassingWarnings(Transaction tx)
        {
            FailureHandlingOptions failureHandlingOptions = tx.GetFailureHandlingOptions();

            // Устанавливаем метод, который игнорирует все предупреждения
            failureHandlingOptions.SetFailuresPreprocessor(new IgnoreWarningsPreprocessor());

            // Применяем опции обработки ошибок к транзакции
            tx.SetFailureHandlingOptions(failureHandlingOptions);
        }
        const double colorFactor = 0.95;
        private bool isErrorPlaceMarks = false;
        private bool isErrorExport = false;
        public UIApplication uiApp;
        public UIDocument uidoc;
        public Autodesk.Revit.ApplicationServices.Application app;
        public Autodesk.Revit.DB.Document doc;
        public Dictionary<string, List<ElementId>> elementsToHideBoundingBox;
        private HashSet<string> exceptionViews = new HashSet<string>();
        private HashSet<string> exceptionPossibleViews = new HashSet<string>();
        private (List<PlaceMarkModel> Marks, List<string> ExeptionMarks) marks;
        private List<ViewPlan> templatesList;
        private List<ApartmentModel> listApartment;
        private Stopwatch stopwatch = new Stopwatch();
        public bool CheckForCurves(List<Curve> contourCurves)
        {
            for (int i = 0; i < contourCurves.Count; i++)
            {
                Curve curve1 = contourCurves[i];
                Curve curve2 = contourCurves[(i + 1) % contourCurves.Count]; // Соседняя линия, с учетом зацикливания

                // Получаем направление каждой линии (касательные векторы)
                XYZ direction1 = curve1.GetEndPoint(1) - curve1.GetEndPoint(0);
                XYZ direction2 = curve2.GetEndPoint(1) - curve2.GetEndPoint(0);

                // Нормализуем векторы
                direction1 = direction1.Normalize();
                direction2 = direction2.Normalize();

                // Рассчитываем угол между векторами (в радианах)
                double angle = direction1.AngleTo(direction2);

                // Преобразуем угол в градусы
                double angleDegrees = angle * (180 / Math.PI);

                // Проверка на параллельность (угол близок к 0 или 180 градусов)
                if (Math.Abs(angleDegrees) < 0.1 || Math.Abs(angleDegrees - 180) < 0.1)
                {
                    // Если угол очень близок к 0° или 180°, значит линии параллельные, пропускаем их
                    continue;
                }

                // Если угол не близок к 90°, то считаем, что линия "кривая"
                if (Math.Abs(angleDegrees - 90) > 0.1) // Погрешность на небольшие отклонения
                {
                    return true; // Найдена "кривая" линия
                }
            }

            return false; // Все линии под 90 градусов или параллельные
        }
        // Метод объединения BoundingBoxXYZ
        BoundingBoxXYZ CombineBoundingBoxes(List<BoundingBoxXYZ> bboxList)
        {
            if (bboxList == null || bboxList.Count == 0)
                return null;

            // Инициализация минимальных и максимальных точек с первыми значениями
            XYZ minPoint = new XYZ(double.MaxValue, double.MaxValue, double.MaxValue);
            XYZ maxPoint = new XYZ(double.MinValue, double.MinValue, double.MinValue);

            foreach (var bbox in bboxList)
            {
                if (bbox == null)
                    continue;

                // Обновление минимальных значений
                minPoint = new XYZ(
                    Math.Min(minPoint.X, bbox.Min.X),
                    Math.Min(minPoint.Y, bbox.Min.Y),
                    Math.Min(minPoint.Z, bbox.Min.Z)
                );

                // Обновление максимальных значений
                maxPoint = new XYZ(
                    Math.Max(maxPoint.X, bbox.Max.X),
                    Math.Max(maxPoint.Y, bbox.Max.Y),
                    Math.Max(maxPoint.Z, bbox.Max.Z)
                );
            }

            // Создание нового BoundingBoxXYZ
            BoundingBoxXYZ combinedBbox = new BoundingBoxXYZ
            {
                Min = minPoint,
                Max = maxPoint
            };

            return combinedBbox;
        }
        public void CreatePlanApartment(List<WorkViewModel> views, PluginSettingsModel settings)
        {

            using (TransactionGroup transGroup = new TransactionGroup(doc))
            {

                transGroup.Start("Create cropped views");
                stopwatch.Start();
                using (Transaction tx = new Transaction(doc, "SettigsInstances"))
                {
                    tx.Start();

                    marks = CheckInstance.CheckMarksInProject(doc, settings);
                    templatesList = CheckInstance.CheckTemplatesInProject(doc, settings.ActiveTemplates);
                    listApartment = new List<ApartmentModel>();
                    TurnOnLinkFacades(settings.LinkTypes);
                    tx.Commit();
                }
                var walls = new FilteredElementCollector(doc)
                        .OfClass(typeof(Wall))
                        .Where(d => settings.WallTypes.Select(q => q.element.Name).Contains(d.Name))
                        .ToList();
                if (settings.AutoСolumns)
                {
                    var structuralColumns = new FilteredElementCollector(doc)
                            .OfClass(typeof(FamilyInstance))
                            .OfCategory(BuiltInCategory.OST_StructuralColumns)
                            .ToList();
                    // walls.AddRange(structuralColumns);
                }
                List<WorkViewModel> workViewModels = new List<WorkViewModel>();
                foreach (var workView in views)
                {
                    var workViewModel = new WorkViewModel();
                    workViewModel.allRooms = new FilteredElementCollector(doc, workView.View.Id)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .ToElements()
                    .GroupBy(room => room.LookupParameter(settings.NumApartParameter.Definition.Name).AsString());
                    workViewModel.doors = new FilteredElementCollector(doc, workView.View.Id)
                    .OfCategory(BuiltInCategory.OST_Doors)
                    .OfClass(typeof(FamilyInstance))
                    .Cast<FamilyInstance>()
                    .Where(d => settings.DoorTypes.Select(q => q.element.Id).Contains(d.Symbol.Id))
                    .ToList();

                    workViewModel.View = workView.View;
                    workViewModels.Add(workViewModel);
                }
                using (Transaction tx = new Transaction(doc, "GetBorders"))
                {

                    TransactionBypassingWarnings(tx);
                    tx.Start();
                    DeleteSeparators(views);
                    TempChangeRoom(walls, views, settings);
                    doc.Regenerate();
                    TakeRoomBorders(workViewModels, settings, listApartment);
                    tx.RollBack();
                }

                var collectorFillPatterns = new FilteredElementCollector(doc)
                    .OfClass(typeof(FillPatternElement))
                    .GroupBy(id => id.Id);
                FilteredElementCollector validPattern = new FilteredElementCollector(doc).OfClass(typeof(FilledRegionType));
                FilledRegionType filledRegionType =
                    validPattern.FirstOrDefault(q => q.get_Parameter(BuiltInParameter.FOREGROUND_ANY_PATTERN_ID_PARAM)
                    .AsElementId() == collectorFillPatterns.FirstOrDefault().Key) as
                        FilledRegionType;
                List<ExportSettingsModel> viewsExport = new List<ExportSettingsModel>();
                List<string> floorNames = new List<string>();
                HashSet<(View PaintedView, View MarkView)> FloorViews = new HashSet<(View PaintedView, View MarkView)>();
                bool isExistsViewFloor = true;
                foreach (var apartment in listApartment)
                {
                    using (Transaction tx = new Transaction(doc, $"CreateApartment {apartment.roomGroup.Key}"))
                    {
                        try
                        {
                            TransactionBypassingWarnings(tx);
                            tx.Start();
                            var floorsRotate = new List<View>();
                            CurveLoop curveLoop = new CurveLoop();
                            foreach (Curve curve in apartment.contourInternalCurveArray)
                            {
                                curveLoop.Append(curve);
                            }
                            View apartmentView = doc.GetElement(apartment.activeView.Duplicate(ViewDuplicateOption.WithDetailing)) as View;
                            View floorPaintedView =
                                doc.GetElement(apartmentView.Duplicate(ViewDuplicateOption.Duplicate)) as View;
                            List<BoundingBoxXYZ> bboxList = new List<BoundingBoxXYZ>();
                            // Ровнаяем границу подрезку для корректной выгрузки SVG
                            if (settings.LinkTypes.Count > 0)
                            {
                                foreach (var link in settings.LinkTypes)
                                {
                                    RevitLinkType linkType = doc.GetElement(link.element.Id) as RevitLinkType;
                                    FilteredElementCollector collector1 = new FilteredElementCollector(doc)
                                .OfClass(typeof(RevitLinkInstance));

                                    RevitLinkInstance linkInstance = null;
                                    foreach (RevitLinkInstance instance in collector1)
                                    {
                                        if (instance.GetTypeId() == linkType.Id)
                                        {
                                            linkInstance = instance;
                                            break;
                                        }
                                    }

                                    bboxList.Add(linkInstance.get_BoundingBox(floorPaintedView));
                                }
                            }
                            else
                            {
                                FilteredElementCollector collectors = new FilteredElementCollector(doc, floorPaintedView.Id);
                                ICollection<Element> elements = collectors.WhereElementIsNotElementType().OfClass(typeof(Wall)).ToElements();
                                foreach (Element element in elements)
                                {
                                    // Получаем BoundingBox каждого элемента
                                    bboxList.Add(element.get_BoundingBox(null));
                                }
                            }
                            floorPaintedView.CropBox = CombineBoundingBoxes(bboxList);
                            doc.Regenerate();
                            View maskApartmentView =
                               doc.GetElement(floorPaintedView.Duplicate(ViewDuplicateOption.WithDetailing)) as View;
                            View floorMarkView = doc.GetElement(floorPaintedView.Duplicate(ViewDuplicateOption.Duplicate)) as View;
                            View floorView = null;
                            if (isExistsViewFloor)
                            {
                                floorView = doc.GetElement(floorPaintedView.Duplicate(ViewDuplicateOption.Duplicate)) as View;
                                isExistsViewFloor = false;
                            }
                            // Копирование видов происходит от созданных дублей,
                            // так как бывает момент что Revit перестаёт применять шаблон к виду от которого сделано множественное копирование
                            List<CurveLoop> tempList = new List<CurveLoop>() { curveLoop };
                            #region Создание вида с нарезанной квартирой
                            FilteredElementCollector collector = new FilteredElementCollector(doc, apartmentView.Id);
                            collector.OfCategory(BuiltInCategory.OST_MechanicalEquipment).WhereElementIsNotElementType();
                            ICollection<ElementId> elementIds = collector.ToElementIds();

                            // Получаем элемент вида (View) и скрываем в нем все собранные элементы
                            if (elementIds.Any())
                            {
                                apartmentView.HideElements(elementIds);
                            }

                            if (elementsToHideBoundingBox != null && elementsToHideBoundingBox.Count > 0)
                            {
                                if (elementsToHideBoundingBox.TryGetValue(apartment.roomGroup.Key, out var elements))
                                {
                                    apartmentView.HideElements(elements);
                                }

                            }
                            ViewCropRegionShapeManager vcrShapeMgr = apartmentView?.GetCropRegionShapeManager();
                            apartmentView.CropBoxActive = true;
                            vcrShapeMgr?.SetCropShape(apartment.ContourСurveLoop);
                            if (apartment.ContourСurveLoop.Count() < 2)
                            {
                                throw new InvalidOperationException("Контур должен содержать как минимум две кривые.");
                            }
                            if (CheckForCurves(new List<Curve>(apartment.ContourСurveLoop)))
                            {
                                exceptionPossibleViews.Add(apartment.roomGroup.Key);
                            }
                            string _tempName = apartment.activeView.Name;
                            apartmentView.Name = apartment.activeView.Name + "_кв_" + apartment.roomGroup.Key;

                            vcrShapeMgr.TopAnnotationCropOffset = 0;
                            vcrShapeMgr.LeftAnnotationCropOffset = 0;
                            vcrShapeMgr.RightAnnotationCropOffset = 0;
                            vcrShapeMgr.BottomAnnotationCropOffset = 0;
                            Parameter param = apartmentView.LookupParameter("Обрезать аннотации");
                            param.Set(1);
                            // Сбор всех элементов для скрытия
                            List<ElementId> elementsToHide = new List<ElementId>();
                            var allRooms = new FilteredElementCollector(doc)
                            .OfCategory(BuiltInCategory.OST_Rooms)
                            .ToElements()
                            .GroupBy(room => room.LookupParameter(settings.NumApartParameter.Definition.Name).AsString());

                            elementsToHide.AddRange(
                            allRooms
                                .SelectMany(g => g)
                                .Where(e => e.LookupParameter(settings.NumApartParameter.Definition.Name).AsString() != apartment.roomGroup.Key)
                                .Select(e => e.Id));

                            // Добавить все оси
                            elementsToHide.AddRange(
                                new FilteredElementCollector(doc, apartmentView.Id)
                                    .OfCategory(BuiltInCategory.OST_Grids)
                                    .OfClass(typeof(Grid))
                                    .Select(g => g.Id));
                            // Добавить все марки дверей
                            elementsToHide.AddRange(
                            new FilteredElementCollector(doc, apartmentView.Id)
                                .OfCategory(BuiltInCategory.OST_DoorTags)
                                .OfClass(typeof(FamilyInstance))
                                .Select(dt => dt.Id)
);
                            // Добавить все размеры
                            elementsToHide.AddRange(
                                new FilteredElementCollector(doc, apartmentView.Id)
                                    .OfCategory(BuiltInCategory.OST_Dimensions)
                                    .OfClass(typeof(Dimension))
                                    .Select(d => d.Id));

                            // Добавить все высотные отметки
                            elementsToHide.AddRange(
                                new FilteredElementCollector(doc, apartmentView.Id)
                                    .OfCategory(BuiltInCategory.OST_SpotElevations)
                                    .Select(l => l.Id));
                            // Скрыть все собранные элементы
                            if (elementsToHide.Count > 0)
                            {
                                apartmentView.HideElements(elementsToHide);
                            }

                            #endregion
                            #region Создание вида этажа с закрашиванием нарезанной квартиры
                            floorPaintedView.Name = apartmentView.Name + "_заливка_цветная";
                            System.Windows.Media.Color colorFill = settings.ColorFill;
                            CreateMaskAppartment(floorPaintedView, filledRegionType.Id, tempList, new Color(colorFill.R, colorFill.G, colorFill.B));
                            #endregion
                            #region Создание вида с маской нарезанной квартиры
                            maskApartmentView.IsolateElementTemporary(CreateMaskAppartment(maskApartmentView, filledRegionType.Id, tempList, new Color(colorFill.R, colorFill.G, colorFill.B)));
                            maskApartmentView.ConvertTemporaryHideIsolateToPermanent();
                            maskApartmentView.Name = apartmentView.Name + "_маска";
                            floorsRotate.Add(maskApartmentView);
                            #endregion
                            #region Создание вида этажа 
                            if (!floorNames.Contains(_tempName) && floorView != null)
                            {
                                floorView.Name = _tempName + "_общий_этаж";
                                floorNames.Add(_tempName);
                                string nameViewFloor = floorView.Name;
                                viewsExport.Add(new ExportSettingsModel
                                {
                                    ExportView = floorView,
                                });
                                int iteration = 0;
                                foreach (var template in templatesList)
                                {
                                    iteration++;
                                    if (iteration >= 2)
                                    {
                                        floorView = doc.GetElement(floorView.Duplicate(ViewDuplicateOption.WithDetailing)) as View;
                                        viewsExport.Add(new ExportSettingsModel
                                        {
                                            ExportView = floorView,
                                        });
                                    }
                                    floorView.ViewTemplateId = template.Id;
                                    floorView.Name = nameViewFloor + "_" + template.Name;
                                    floorsRotate.Add(floorView);
                                }
                            }
                            #endregion
                            #region Создание вида этажа с марками и цветовой заливкой
                            floorMarkView.Name = apartmentView.Name + "_марки";
                            #endregion
                            FloorViews.Add((floorPaintedView, floorMarkView));
                            int iterationCount = 0;
                            // Присвоение шаблонов
                            string nameViewApartment = apartmentView.Name;
                            string nameViewFloorPainted = floorPaintedView.Name;
                            string nameViewFloorMark = floorMarkView.Name;

                            viewsExport.Add(new ExportSettingsModel
                            {
                                NumberApartment = apartment.roomGroup.Key,
                                ExportView = apartmentView,
                            });
                            viewsExport.Add(new ExportSettingsModel
                            {
                                NumberApartment = apartment.roomGroup.Key,
                                ExportView = floorPaintedView,
                                AdditionalWord = "mask"
                            });
                            viewsExport.Add(new ExportSettingsModel
                            {
                                NumberApartment = apartment.roomGroup.Key,
                                ExportView = floorMarkView,
                                AdditionalWord = "marks"
                            });
                            var viewsRotate = new List<View>();
                            floorsRotate.AddRange([floorPaintedView, floorMarkView]);
                            for (int i = 0; i < templatesList.Count; i++)
                            {

                                iterationCount++;
                                if (iterationCount >= 2)
                                {

                                    apartmentView = doc.GetElement(apartmentView.Duplicate(ViewDuplicateOption.Duplicate)) as View;
                                    floorPaintedView = doc.GetElement(floorPaintedView.Duplicate(ViewDuplicateOption.Duplicate)) as View;
                                    CreateMaskAppartment(floorPaintedView, filledRegionType.Id, tempList, new Color(249, 209, 210));
                                    floorMarkView = doc.GetElement(floorMarkView.Duplicate(ViewDuplicateOption.Duplicate)) as View;
                                    viewsExport.Add(new ExportSettingsModel
                                    {
                                        NumberApartment = apartment.roomGroup.Key,
                                        ExportView = apartmentView,
                                    });
                                    viewsExport.Add(new ExportSettingsModel
                                    {
                                        NumberApartment = apartment.roomGroup.Key,
                                        ExportView = floorPaintedView,
                                        AdditionalWord = "mask"
                                    });
                                    viewsExport.Add(new ExportSettingsModel
                                    {
                                        NumberApartment = apartment.roomGroup.Key,
                                        ExportView = floorMarkView,
                                        AdditionalWord = "marks"
                                    });
                                    floorsRotate.AddRange([floorPaintedView, floorMarkView]);
                                }

                                apartmentView.ViewTemplateId = templatesList[i].Id;
                                floorPaintedView.ViewTemplateId = templatesList[i].Id;
                                floorMarkView.ViewTemplateId = templatesList[i].Id;
                                apartmentView.Name = nameViewApartment + "_" + templatesList[i].Name;
                                floorPaintedView.Name = nameViewFloorPainted + "_" + templatesList[i].Name;
                                floorMarkView.Name = nameViewFloorMark + "_" + templatesList[i].Name;
                                doc.Regenerate();
                                if (settings.TypeApartment != null)
                                {
                                    ColorFillScheme scheme = doc.GetElement(templatesList[i].GetColorFillSchemeId(new ElementId(BuiltInCategory.OST_Rooms))) as ColorFillScheme;
                                    IList<ColorFillSchemeEntry> entries = scheme.GetEntries();
                                    Room room = new FilteredElementCollector(doc, floorMarkView.Id)
                                    .OfCategory(BuiltInCategory.OST_Rooms)
                                    .Where(e => e.LookupParameter(settings.NumApartParameter.Definition.Name).AsValueString() == apartment.roomGroup.Key)
                                    .Cast<Room>()
                                    .FirstOrDefault();
                                    ColorFillSchemeEntry entry = entries.FirstOrDefault(q => q.GetStringValue() == room.LookupParameter(settings.TypeApartment.Definition.Name).AsValueString());
                                    Color colorEntry = entry.Color;
                                    Color color = new Color(
                                    (byte)(colorEntry.Red * colorFactor),
                                    (byte)(colorEntry.Green * colorFactor),
                                    (byte)(colorEntry.Blue * colorFactor));
                                    CreateMaskAppartment(floorMarkView, filledRegionType.Id, tempList, color);
                                }
                                FloorViews.Add((floorPaintedView, floorMarkView));

                                viewsRotate.Add(apartmentView);
                            }
                            var rotationConfigs = new[]
                            {
                                new { IsRotate = settings.IsRotatePlans, Views = viewsRotate, Model = apartment },
                                new { IsRotate = settings.IsRotateFloors, Views = floorsRotate, Model = new ApartmentModel { rotationAngleDegrees = 90 }}
                            };

                            foreach (var config in rotationConfigs)
                            {
                                if (config.IsRotate)
                                {
                                    foreach (var view in config.Views)
                                    {
                                        RotatePlan(view, config.Model);
                                    }
                                }
                            }

                            tx.Commit();
                        }

                        catch
                        {
                            exceptionViews.Add(apartment.roomGroup.Key);
                            if (tx.HasStarted())
                            {
                                tx.Commit();
                            }

                        }
                    }

                }
                using (Transaction tx = new Transaction(doc, "PlaceMarks and Export"))
                 {
                     List<BuiltInCategory> categoriesToHide = new List<BuiltInCategory>
                         {
                             BuiltInCategory.OST_FurnitureSystems,
                             BuiltInCategory.OST_Furniture,
                             BuiltInCategory.OST_MechanicalEquipment,
                             BuiltInCategory.OST_ElectricalEquipment,
                             BuiltInCategory.OST_ElectricalFixtures,
                             BuiltInCategory.OST_PlumbingFixtures,
                             BuiltInCategory.OST_SpecialityEquipment,
                             BuiltInCategory.OST_GenericModel,
                             BuiltInCategory.OST_Grids,
                         };
                     TransactionBypassingWarnings(tx);
                     tx.Start();
                     Category annotationCategory = doc.Settings.Categories.get_Item(BuiltInCategory.OST_GenericAnnotation);
                     foreach (var view in FloorViews)
                     {
                         try
                         {
                             view.MarkView.ViewTemplateId = ElementId.InvalidElementId;
                             view.PaintedView.ViewTemplateId = ElementId.InvalidElementId;
                             foreach (var category in categoriesToHide)
                             {
                                 Category cat = Category.GetCategory(doc, category);
                                 view.MarkView.SetCategoryHidden(cat.Id, true);
                                 view.PaintedView.SetCategoryHidden(cat.Id, true);
                             }
                             Category catFloors = Category.GetCategory(doc, BuiltInCategory.OST_Floors);
                             view.PaintedView.SetCategoryHidden(catFloors.Id, true);
                             CommandPlacementMarks.PlaceRoomMarks(doc, view.MarkView, marks.Marks, settings);
                             view.MarkView.SetCategoryHidden(annotationCategory.Id, false);
                             if (settings.TypesSquareTag != null)
                             {
                                 CommandPlacementMarks.PlaceSquareMarks(doc, view.MarkView, settings.TypesSquareTag.element.Id, settings);
                             }
                         }
                         catch
                         {
                             isErrorPlaceMarks = true;
                         }
                     }
                     try
                     {
                         viewsExport = CommandViewSheetBuilder.CreateViewSheet(doc, viewsExport, settings);
                         if (settings.IsExport)
                         {
                             CommandExport.ExportCSV(doc, settings);
                         }

                         tx.Commit();
                         if (settings.IsExport)
                         {
                             foreach (var view in viewsExport)
                             {
                                 (bool isAppatment, string path) image = CommandExport.ExportToRevit(doc, view, settings);
                                 CommandExport.ConvertToSvg(image, settings);
                             }
                         }
                     }
                     catch
                     {
                         isErrorExport = true;
                         if (tx.HasStarted())
                         {
                             tx.Commit();
                         }
                     }
                 }
                transGroup.Assimilate();
            }
            try
            {
                try
                {
                    doc.Save();
                }
                catch
                {
                    string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string docTitle = doc.Title;
                    string newFileName = $"{docTitle}_ProjectApartmentPlans.rvt";
                    string newFilePath = System.IO.Path.Combine(desktopPath, newFileName);
                    // Проверяем, включена ли совместная работа (worksharing) для документа
                    if (doc.IsWorkshared)
                    {
                        SaveAsOptions saveAsOptions = new SaveAsOptions
                        {
                            OverwriteExistingFile = true
                        };


                        WorksharingSaveAsOptions worksharingOptions = new WorksharingSaveAsOptions
                        {
                            SaveAsCentral = true
                        };
                        saveAsOptions.SetWorksharingOptions(worksharingOptions);
                        ModelPath modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(newFilePath);
                        doc.SaveAs(modelPath, saveAsOptions);

                    }
                    else
                    {
                        // Если совместная работа не включена, используем обычные SaveAsOptions
                        SaveAsOptions options = new SaveAsOptions
                        {
                            OverwriteExistingFile = true
                        };
                        doc.SaveAs(newFilePath, options);

                    }
                    MessageBox.Show($"Файл проекта сохранён на рабочем столе с именем \"{docTitle}_ProjectApartmentPlans.rvt\"");

                }

            }
            catch
            {
                MessageBox.Show("Произошла ошибка с сохранением файла!");
            }
            stopwatch.Stop();
            MessageBox.Show($"Время работы плагина: {stopwatch.Elapsed}");
            ExceptionShow();

        }
        private void TurnOnLinkFacades(List<SelectableFamilyModel> links)
        {
            foreach (SelectableFamilyModel linkEl in links)
            {
                RevitLinkType link = linkEl.element as RevitLinkType;
                Parameter param = link.get_Parameter(BuiltInParameter.WALL_ATTR_ROOM_BOUNDING);
                param.Set(1);
            }
        }
        private void RotatePlan(View view, ApartmentModel apartment)
        {

            doc.Regenerate();
            view.CropBoxVisible = true;
            SubTransaction subTx = new SubTransaction(doc);
            subTx.Start();
            try
            {
                ViewCropRegionShapeManager vcrShapeMgr = view?.GetCropRegionShapeManager();
                ElementId typeId = view.GetTypeId();
                Element cropBoxElement = new FilteredElementCollector(doc, view.Id).WherePasses(new ElementIsElementTypeFilter(true))
                .Where(x => x.GetTypeId() == typeId && x.Name == view.Name && x.Id != view.Id).FirstOrDefault();
                if (cropBoxElement == null)
                {
                    int id = view.Id.IntegerValue + 2;
                    var element = doc.GetElement(new ElementId(id));
                    if (element.Name == view.Name)
                    {
                        cropBoxElement = element;
                    }
                }
                if (cropBoxElement != null)
                {
                    BoundingBoxXYZ cropBox = view.CropBox;
                    XYZ cropBoxCenter = (cropBox.Max + cropBox.Min) / 2.0;
                    Line rotationAxis = Line.CreateBound(cropBoxCenter, cropBoxCenter + XYZ.BasisZ);
                    ElementTransformUtils.RotateElement(doc, cropBoxElement.Id, rotationAxis, apartment.rotationAngleDegrees * (Math.PI / 180.0));
                    IList<CurveLoop> cropShape = vcrShapeMgr.GetCropShape();
                    if (cropShape.Count > 0)
                    {
                        CurveLoop cropRegionLoop = cropShape[0];
                        CurveLoop rotatedCropRegionLoop = RotateCurveLoop(cropRegionLoop, cropBoxCenter, apartment.rotationAngleDegrees);
                        vcrShapeMgr.SetCropShape(rotatedCropRegionLoop);
                        view.CropBoxVisible = false;
                        subTx.Commit();
                    }
                    else
                    {
                        subTx.RollBack();
                    }
                }
            }
            catch
            {
                Console.WriteLine("Вид нельзя повернуть!");
                if (subTx.HasStarted())
                {
                    subTx.RollBack();
                }
            }
        }
        private CurveLoop RotateCurveLoop(CurveLoop curveLoop, XYZ center, double rotationAngleDegrees)
        {
            CurveLoop rotatedLoop = new CurveLoop();
            double rotationAngleRadians = rotationAngleDegrees * (Math.PI / 180.0);
            Transform rotationTransform = Transform.CreateRotationAtPoint(XYZ.BasisZ, -rotationAngleRadians, center);

            foreach (Curve curve in curveLoop)
            {
                Curve rotatedCurve = curve.CreateTransformed(rotationTransform);
                rotatedLoop.Append(rotatedCurve);
            }

            return rotatedLoop;
        }
        private void ExceptionShow()
        {
            var messages = new List<string>();

            if (exceptionViews.Count > 0)
            {
                messages.Add("Не обработанные виды с номером квартир: " + string.Join(", ", exceptionViews));
            }

            if (exceptionPossibleViews.Count > 0)
            {
                messages.Add("Возможные ошибки с номером квартир: " + string.Join(", ", exceptionPossibleViews));
            }

            if (marks.ExeptionMarks.Count > 0)
            {
                messages.Add("Марок с данным названием нет в проекте: " + string.Join(", ", marks.ExeptionMarks));
            }

            if (isErrorPlaceMarks)
            {
                messages.Add("Произошла ошибка при расстановке марок!");
            }

            if (isErrorExport)
            {
                messages.Add("Произошла ошибка с выгрузкой!");
            }

            if (messages.Count > 0)
            {
                MessageBox.Show(string.Join("\n", messages));
            }
        }

        public ElementId CreateMaskAppartment(View view, ElementId fillType, List<CurveLoop> border, Color color)
        {
            // Создаем область заливки
            FilledRegion filledRegion = FilledRegion.Create(doc, fillType, view.Id, border);
            FilledRegionType filledRegionType = doc.GetElement(filledRegion.GetTypeId()) as FilledRegionType;

            // Устанавливаем параметр маскирования в false (без маскирования)
            Parameter maskingParameter = filledRegionType.get_Parameter(BuiltInParameter.FILLED_REGION_MASKING);
            maskingParameter.Set(0);

            // Создаем настройки графики для замены
            OverrideGraphicSettings overrideSettings = new OverrideGraphicSettings();
            overrideSettings.SetProjectionLineColor(color);
            overrideSettings.SetSurfaceBackgroundPatternColor(color); // Устанавливаем цвет заливки

            // Применяем настройки
            view.SetElementOverrides(filledRegion.Id, overrideSettings);

            return filledRegion.Id;
        }
        private void TempChangeRoom(List<Element> walls, List<WorkViewModel> views, PluginSettingsModel settings)
        {
            foreach (var wall in walls)
            {
                if (doc.GetElement(wall.GroupId) is Group group)
                {
                    group.UngroupMembers();
                }
                wall.Pinned = false;
                doc.Delete(wall.Id);
            }
            foreach (var workView in views)
            {
                List<FamilyInstance> doors = new FilteredElementCollector(doc, workView.View.Id)
                    .OfCategory(BuiltInCategory.OST_Doors)
                    .OfClass(typeof(FamilyInstance))
                    .Cast<FamilyInstance>()
                    .Where(d => settings.DoorTypes.Select(q => q.element.Id).Contains(d.Symbol.Id))
                    .ToList();
                IList<Element> planRegions = new FilteredElementCollector(doc, workView.View.Id)
                    .OfCategory(BuiltInCategory.OST_PlanRegion)
                    .WhereElementIsNotElementType()
                    .ToElements();
                foreach (var door in doors)
                {
                    Room toRoom = door.ToRoom;
                    string roomName = toRoom.get_Parameter(BuiltInParameter.ROOM_NAME).AsValueString();
                    bool opensIntoEntryway = toRoom != null && roomName.Equals("Коридор", StringComparison.OrdinalIgnoreCase);
                    if (!opensIntoEntryway)
                    {
                        // Отзеркалить дверь, если не открывается в прихожую
                        door.flipFacing();
                    }
                }
                foreach (Element planRegion in planRegions)
                {
                    doc.Delete(planRegion.Id);
                }
            }


        }
        public List<Curve> RemoveUnclosedCurves(List<Curve> correctedRoomContour)
        {
            List<Curve> closedCurves = new List<Curve>();

            foreach (Curve curve in correctedRoomContour)
            {
                XYZ startPoint = curve.GetEndPoint(0);
                XYZ endPoint = curve.GetEndPoint(1);

                bool startConnected = correctedRoomContour
                    .Where(c => c != curve)
                    .Any(c => PointsAreEqual(c.GetEndPoint(0), startPoint) || PointsAreEqual(c.GetEndPoint(1), startPoint));

                bool endConnected = correctedRoomContour
                    .Where(c => c != curve)
                    .Any(c => PointsAreEqual(c.GetEndPoint(0), endPoint) || PointsAreEqual(c.GetEndPoint(1), endPoint));

                if (startConnected && endConnected)
                {
                    closedCurves.Add(curve);
                }
            }

            return closedCurves;
        }

        private bool PointsAreEqual(XYZ p1, XYZ p2)
        {
            const double tolerance = 0.1;
            return Math.Abs(p1.X - p2.X) < tolerance
                && Math.Abs(p1.Y - p2.Y) < tolerance
                && Math.Abs(p1.Z - p2.Z) < tolerance;
        }

        public List<Curve> RemoveNonParallelCurves(List<Curve> primaryCurves, List<Curve> secondaryCurves)
        {
            List<Curve> parallelCurves = new List<Curve>();

            foreach (Curve secondaryCurve in secondaryCurves)
            {
                // Получаем вектор направления для текущей линии из второстепенного списка
                XYZ secondaryDirection = (secondaryCurve.GetEndPoint(1) - secondaryCurve.GetEndPoint(0)).Normalize();

                // Проверяем параллельность с любой линией из основного списка
                bool isParallel = primaryCurves.Any(primaryCurve =>
                {
                    XYZ primaryDirection = (primaryCurve.GetEndPoint(1) - primaryCurve.GetEndPoint(0)).Normalize();
                    // Проверка на параллельность путем сравнения векторов
                    return secondaryDirection.IsAlmostEqualTo(primaryDirection) ||
                           secondaryDirection.IsAlmostEqualTo(-primaryDirection);
                });

                // Если линия параллельна хотя бы одной линии из основного списка, добавляем ее в новый список
                if (isParallel)
                {
                    parallelCurves.Add(secondaryCurve);
                }
            }

            return parallelCurves;
        }
        public List<Curve> StraightenRoomCurves(List<Curve> roomCurves)
        {
            List<Curve> straightenedCurves = new List<Curve>();

            foreach (Curve curve in roomCurves)
            {
                // Проверяем, является ли кривая линией
                if (curve is Line line)
                {
                    XYZ start = line.GetEndPoint(0);
                    XYZ end = line.GetEndPoint(1);

                    // Проверка, совпадают ли координаты X или Y между стартовой и конечной точками
                    bool isAlignedX = Math.Round(start.X, 5) == Math.Round(end.X, 5);
                    bool isAlignedY = Math.Round(start.Y, 5) == Math.Round(end.Y, 5);

                    // Если хотя бы одна координата уже совпадает, добавляем линию без изменений
                    if (isAlignedX || isAlignedY)
                    {
                        straightenedCurves.Add(line);
                        continue;
                    }

                    // Рассчитываем отклонения по X и Y
                    double deltaX = Math.Abs(end.X - start.X);
                    double deltaY = Math.Abs(end.Y - start.Y);

                    // Выравниваем по оси с меньшим отклонением
                    if (deltaX < deltaY)
                    {
                        // Приводим точки к одной линии по оси X (выравниваем по Y)
                        XYZ newEnd = new XYZ(start.X, end.Y, start.Z);
                        line = Line.CreateBound(start, newEnd) as Line;
                    }
                    else
                    {
                        // Приводим точки к одной линии по оси Y (выравниваем по X)
                        XYZ newEnd = new XYZ(end.X, start.Y, start.Z);
                        line = Line.CreateBound(start, newEnd) as Line;
                    }

                    // Добавляем выпрямленную линию в новый список
                    straightenedCurves.Add(line);
                }
                else
                {
                    // Обработка для других типов кривых, если требуется
                    straightenedCurves.Add(curve);
                }
            }

            return straightenedCurves;
        }
        private List<ApartmentModel> TakeRoomBorders(List<WorkViewModel> workViewModels, PluginSettingsModel settings, List<ApartmentModel> listApartment)
        {
            elementsToHideBoundingBox = new Dictionary<string, List<ElementId>>();
            foreach (var workView in workViewModels)
            {
                var apartmentRooms = workView.allRooms
                            .SelectMany(group => group)
                .Where(room => room.LookupParameter(settings.SortParameter.Definition.Name).AsString() == settings.ValueSortParameter)
                .GroupBy(room => room.LookupParameter(settings.NumApartParameter.Definition.Name).AsString());

                foreach (var roomGroup in apartmentRooms)
                {
                    try
                    {
                        ApartmentModel apartment = new ApartmentModel();
                        List<CurveLoop> curveArrays = new List<CurveLoop>();
                        List<Curve> finalCurves = new List<Curve>();
                        List<Curve> loopDoor = null;
                        var number = roomGroup.FirstOrDefault().LookupParameter(settings.NumApartParameter.Definition.Name).AsString();
                        List<Curve> columnCurves = new List<Curve>();

                        if (workView.doors.Any())
                        {
                            var filteredDoor = workView.doors
                            .FirstOrDefault(q =>
                                (q.ToRoom != null && q.ToRoom.LookupParameter(settings.NumApartParameter.Definition.Name).AsString() == number) ||
                                (q.FromRoom != null && q.FromRoom.LookupParameter(settings.NumApartParameter.Definition.Name).AsString() == number));
                            XYZ facingOrientation = filteredDoor.FacingOrientation;
                            // Проверка ориентации двери (север, юг и т.д.)
                            double angle = facingOrientation.AngleTo(XYZ.BasisY);
                            XYZ crossProduct = facingOrientation.CrossProduct(XYZ.BasisY);
                            double angleDegrees = crossProduct.Z < 0 ? angle * (180.0 / Math.PI) : -angle * (180.0 / Math.PI);
                            loopDoor = GetCurveLoopFromBoundingBox(filteredDoor?.get_BoundingBox(workView.View));
                            BoundingBoxXYZ doorBoundingBox = filteredDoor.get_BoundingBox(workView.View);

                            Outline outline = new Outline(doorBoundingBox.Min, doorBoundingBox.Max);
                            BoundingBoxIntersectsFilter boundingBoxFilter = new BoundingBoxIntersectsFilter(outline);
                            var doorIds = new FilteredElementCollector(doc, workView.View.Id)
                            .WherePasses(boundingBoxFilter)
                            .Excluding(new ElementId[] { filteredDoor.Id })
                            .WhereElementIsNotElementType()
                            .OfCategory(BuiltInCategory.OST_Doors)
                            .Select(e => e.Id)
                            .ToList();
                            if (doorIds.Count != 0)
                            {
                                if (elementsToHideBoundingBox.ContainsKey(roomGroup.Key))
                                {
                                    // Ключ уже существует, обновляем значение
                                    elementsToHideBoundingBox[roomGroup.Key] = doorIds;
                                }
                                else
                                {
                                    // Ключ не существует, добавляем новый элемент
                                    elementsToHideBoundingBox.Add(roomGroup.Key, doorIds);
                                }

                            }
                            apartment.FilteredDoor = filteredDoor;
                            apartment.rotationAngleDegrees = angleDegrees;

                        }

                        Paths64 combinedPaths = new Paths64();
                        Paths64 internalPaths = new Paths64();
                        int currentIteration = 0;
                        int totalIterations = roomGroup.Count();
                        List<Curve> correctedRoomContour = new List<Curve>();
                        foreach (var element in roomGroup)
                        {
                            var room = (Room)element;

                            List<Curve> roomCurves = new List<Curve>();
                            List<Curve> internalRoomCurves = new List<Curve>();
                            var options = new SpatialElementBoundaryOptions();
                            options.SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Center;
                            var segments = room.GetBoundarySegments(options);
                            var initCurves = new List<Curve>();

                            if (segments == null || segments.Count == 0)
                            {
                                continue;
                            }
                            foreach (var boundarySegment in segments.FirstOrDefault())
                            {
                                Curve curve = boundarySegment.GetCurve();
                                if (boundarySegment.ElementId.IntegerValue != -1 &&
                                boundarySegment.ElementId.IntegerValue != default)
                                {
                                    var boundaryElement = doc.GetElement(boundarySegment.ElementId);
                                    int categoryValue = boundaryElement.Category.Id.IntegerValue;
                                    if (categoryValue == (int)BuiltInCategory.OST_RvtLinks &&
                                    !settings.LinkTypes.Any(q => boundaryElement.Name.Contains(q.element.Name)))
                                    {
                                        RevitLinkInstance instance = boundaryElement as RevitLinkInstance;
                                        Document linkDoc = instance.GetLinkDocument();
                                        boundaryElement = linkDoc.GetElement(boundarySegment.LinkElementId);
                                        categoryValue = boundaryElement.Category.Id.IntegerValue;
                                    }

                                    switch (categoryValue)
                                    {
                                        case (int)BuiltInCategory.OST_Walls:
                                            {
                                                double offset = (boundaryElement as Wall).Width / 2;
                                                if ((boundaryElement as Wall).WallType.Kind == WallKind.Curtain)
                                                {
                                                    // Офсет для витражей подсчитанный вручную
                                                    offset += 0.595;
                                                }
                                                initCurves.Add(curve);
                                                curve = curve.CreateOffset(offset, XYZ.BasisZ);
                                                break;
                                            }
                                        case (int)BuiltInCategory.OST_StructuralColumns:
                                            {

                                                if (!settings.AutoСolumns)
                                                {
                                                    if (settings.ColumnParameter == null)
                                                    {
                                                        // В случае если параметр не задан, ширина колонны будет равняться 300м
                                                        curve = curve.CreateOffset(0.995, XYZ.BasisZ);
                                                    }
                                                    else
                                                    {
                                                        curve = curve.CreateOffset(settings.ColumnParameter.AsDouble(), XYZ.BasisZ);
                                                    }
                                                    break;
                                                }
                                                continue;
                                            }
                                    }
                                    if (!settings.AutoFacade)
                                    {
                                        if (settings.WallFacadesTypes.Select(q => q.element.Name).Contains(boundaryElement.Name) && settings.WallFacadesOffest != 0 || categoryValue == (int)BuiltInCategory.OST_RvtLinks)
                                        {
                                            double offset = settings.WallFacadesOffest / 304.8;
                                            curve = curve.CreateOffset(offset, XYZ.BasisZ);
                                        }
                                    }
                                    else if (categoryValue == (int)BuiltInCategory.OST_RvtLinks && boundaryElement is RevitLinkInstance linkElement)
                                    {
                                        var linbBbox = linkElement.get_BoundingBox(workView.View);
                                        Document linkDoc = linkElement.GetLinkDocument();
                                        double offsetValue = 0.5;
                                        Line line = OffsetAndCreateNewLineOnPlan(linkDoc, offsetValue, curve, linbBbox);
                                        curve = line;
                                    }
                                }
                                roomCurves.Add(curve);
                            }
                            // Выпрямляю кривые линии
                            roomCurves = StraightenRoomCurves(roomCurves);
                            options.SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish;
                            segments = room.GetBoundarySegments(options);
                            foreach (BoundarySegment boundarySegment in segments.FirstOrDefault())
                            {
                                internalRoomCurves.Add(boundarySegment.GetCurve());

                            }
                            // Получаю контур 2-мя итерациями (1-Контур замыкается, 2-Удаляются лишние линии)
                            correctedRoomContour = CorrectedContour(roomCurves, workView.View);
                            correctedRoomContour = CorrectedContour(correctedRoomContour, workView.View);
                            // Библиотека чистит контур и делает его ровным
                            combinedPaths = Clipper.Union(

                                combinedPaths,
                                new Paths64() { ToPath64(correctedRoomContour) },

                                Clipper2Lib.FillRule.Positive
                            );
                            // Удаляю не замкнутые линии
                            correctedRoomContour = RemoveUnclosedCurves(correctedRoomContour);
                            // Выполнение объединения - чистится контур
                            combinedPaths = Clipper.Union(

                                combinedPaths,
                                new Paths64() { ToPath64(correctedRoomContour) },

                                Clipper2Lib.FillRule.Positive
                            );

                            List<Curve> _roomCurves = new List<Curve>();
                            foreach (Curve curve in FromPaths64(combinedPaths, 0.0))
                            {
                                _roomCurves.Add(curve);
                            }
                            // Удаляются линии которые не были параллельны начальному контуру (потенциально кривые)
                            _roomCurves = RemoveNonParallelCurves(initCurves, _roomCurves);
                            // Замыкаем контур
                            correctedRoomContour = CorrectedContour(_roomCurves);
                            ClipperOffset offsetInternalClipper = new ClipperOffset();
                            offsetInternalClipper.AddPath(ToPath64(internalRoomCurves), JoinType.Miter, EndType.Polygon);
                            Paths64 offsetInternalPaths = new Paths64();
                            offsetInternalClipper.Execute(2 * 1000000, offsetInternalPaths);
                            internalPaths = Clipper.Union(
                                internalPaths,
                                offsetInternalPaths,
                                Clipper2Lib.FillRule.NonZero
                            );
                            currentIteration++;
                        }
                        ClipperOffset internalOffset = new ClipperOffset();
                        internalOffset.AddPaths(internalPaths, JoinType.Miter, EndType.Polygon);
                        Paths64 offsetInternalReturnPaths = new Paths64();
                        internalOffset.Execute(-2 * 1000000, offsetInternalReturnPaths);
                        internalPaths = offsetInternalReturnPaths;
                        //Добавление контура двери и колонн
                        Paths64 pathsToCombine = new Paths64 { ToPath64(columnCurves) };
                        pathsToCombine.AddRange(new[] { loopDoor }
                            .Where(x => x != null)
                            .Select(ToPath64));
                        combinedPaths = Clipper.Union(new Paths64() { ToPath64(correctedRoomContour) }, pathsToCombine, FillRule.NonZero);
                        apartment.roomGroup = roomGroup;
                        apartment.contourInternalCurveArray = FromPaths64(internalPaths, 0.0);
                        apartment.contourCurveArray = FromPaths64(combinedPaths, 0.0);
                        apartment.activeView = workView.View;
                        listApartment.Add(apartment);

                    }
                    catch
                    {
                        exceptionViews.Add(roomGroup.Key);
                    }
                }
            }
            return listApartment;
        }
        private void DeleteSeparators(List<WorkViewModel> views)
        {
            var blackListSeparatorsIds = new List<ElementId>();
            foreach (var workView in views)
            {
                var uniqueRooms = new FilteredElementCollector(doc, workView.View.Id)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .Where(room => room.Name.Contains("Лоджия") || room.Name.Contains("Балкон"));
                foreach (Room room in uniqueRooms)
                {
                    var segments = room.GetBoundarySegments(new SpatialElementBoundaryOptions());
                    if (segments == null || segments.Count == 0)
                    {
                        continue;
                    }
                    foreach (var boundarySegment in segments.FirstOrDefault())
                    {
                        if (boundarySegment.ElementId.IntegerValue != -1 &&
                    boundarySegment.ElementId.IntegerValue != default)
                        {
                            var elementBoundary = doc.GetElement(boundarySegment.ElementId);
                            if (elementBoundary.Category.Id == new ElementId(-2000066))
                            {
                                blackListSeparatorsIds.Add(elementBoundary.Id);
                            }
                        }
                    }
                }
                List<ElementId> collector = new FilteredElementCollector(doc, workView.View.Id)
                .OfCategory(BuiltInCategory.OST_RoomSeparationLines)
                .Select(q => q.Id)
                .Except(blackListSeparatorsIds)
                .ToList();
                doc.Delete(collector);
            }
        }
        public List<Curve> CorrectedContour(List<Curve> curves, View view = null)
        {
            List<Curve> newContour = new List<Curve>();
            Curve nextLine = curves[0].Clone();
            for (int i = 0; i < curves.Count; i++)
            {
                try
                {
                    Curve currentLine = nextLine;
                    //doc.Create.NewDetailCurve(view, currentLine);
                    nextLine = i < curves.Count - 1 ? curves[i + 1].Clone() : newContour[0];

                    newContour.Add(currentLine);

                    List<Curve> connectedLines = InsertLines((Line)currentLine, (Line)nextLine, out bool originalsChanged);
                    /*foreach (Curve line in connectedLines)
                    {
                        doc.Create.NewDetailCurve(view, line);
                    }*/
                    if (originalsChanged && connectedLines.Count > 0)
                    {
                        newContour[newContour.Count - 1] = connectedLines[0];
                        nextLine = connectedLines[1];
                    }

                    foreach (Curve line in connectedLines)
                    {
                        newContour.Add(line);
                    }
                }
                catch
                {

                }
            }
            foreach (Curve line in newContour)
            {
                //doc.Create.NewDetailCurve(view, line);
            }
            return SimplifiedContour(newContour, view);
        }

        private List<Curve> InsertLines(Line line1, Line line2, out bool linesChanged)
        {
            List<Curve> inserted = new List<Curve>();
            linesChanged = false;
            if (line1.GetEndPoint(1).IsAlmostEqualTo(line2.GetEndPoint(0), 1e-7))
            {
                return new List<Curve>();
            }

            if (AreLinesParallel(line1, line2))
            {
                try
                {
                    return new List<Curve>
                {
                    Line.CreateBound(line1.GetEndPoint(1), line2.GetEndPoint(0)),
                };
                }
                catch
                {
                    return new List<Curve>
                {
                    line1
                };
                }
            }
            else if (GetLinesIntersection(line1, line2, out XYZ point))
            {
                try
                {
                    linesChanged = true;
                    return new List<Curve>
                    {
                        Line.CreateBound(line1.GetEndPoint(0), point),
                        Line.CreateBound(point, line2.GetEndPoint(1)),
                    };
                }
                catch
                {
                }
            }

            return new List<Curve>();
        }

        private List<Curve> SimplifiedContour(List<Curve> curves, View view = null)
        {
            if (curves == null || curves.Count < 2)
            {
                // Недостаточно линий для выполнения операции
                return curves;
            }

            List<Curve> mergedCurves = new List<Curve>();
            for (int i = 0; i < curves.Count; i++)
            {
                Curve currentCurve = curves[i];
                Line currentLine = currentCurve as Line;
                if (currentLine == null || i >= curves.Count - 2)
                {
                    mergedCurves.Add(currentCurve);
                    continue;
                }

                if (i < curves.Count - 1)
                {
                    XYZ endPoint = currentLine.GetEndPoint(1);
                    for (int j = i + 1; j < curves.Count; j++)
                    {
                        Curve nextCurve = curves[j];
                        if (nextCurve as Line == null)
                        {
                            break;
                        }

                        if (AreCurvesParallel(currentLine, nextCurve as Line))
                        {
                            endPoint = nextCurve.GetEndPoint(1);
                            i++;
                            continue;
                        }
                        else
                        {
                            break;
                        }
                    }

                    Line mergedLine = Line.CreateBound(currentLine.GetEndPoint(0), endPoint);
                    mergedCurves.Add(mergedLine);
                }
                else
                {
                    // Последняя линия
                    Curve firstCurve = mergedCurves[0];

                    if (firstCurve as Line == null || AreCurvesParallel(currentLine, firstCurve as Line))
                    {
                        // Линии параллельны, заменяем их одной линией
                        Line mergedLine = Line.CreateBound(currentLine.GetEndPoint(0), firstCurve.GetEndPoint(1));
                        mergedCurves.Add(mergedLine);
                        mergedCurves.RemoveAt(0);
                    }
                    else
                    {
                        mergedCurves.Add(currentLine);
                    }
                }
            }

            return mergedCurves;
        }

        private bool AreCurvesParallel(Line curve1, Line curve2)
        {
            if (curve1.Direction.IsAlmostEqualTo(curve2.Direction)) return true;
            return false;
        }

        bool AreLinesParallel(Line line1, Line line2)
        {
            // Получаем направляющие векторы линий
            XYZ direction1 = line1.Direction;
            XYZ direction2 = line2.Direction;

            // Проверяем параллельность направляющих векторов
            // Эпсилон для учета численной точности
            double epsilon = 1e-9;
            if (direction1.IsAlmostEqualTo(direction2, epsilon) || direction1.IsAlmostEqualTo(-direction2, epsilon))
            {
                // Линии параллельны (или противоположны)
                return true;
            }
            else
            {
                // Линии не параллельны
                return false;
            }
        }

        private bool GetLinesIntersection(Line line1, Line line2, out XYZ point)
        {
            try
            {
                Line tempLine1 = line1.Clone() as Line;
                Line tempLine2 = line2.Clone() as Line;

                tempLine1.MakeUnbound();
                tempLine2.MakeUnbound();
                IntersectionResultArray iResult = new IntersectionResultArray();
                SetComparisonResult result = tempLine1.Intersect(tempLine2, out iResult);
                if (result == SetComparisonResult.Overlap && iResult != null && iResult.Size > 0)
                {
                    point = iResult.get_Item(0).XYZPoint;
                    return true;
                }

                point = new XYZ();
                return false;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
            }

            point = new XYZ();
            return false;
        }

        private List<Curve> GetCurveLoopFromBoundingBox(BoundingBoxXYZ boundingBox)
        {
            XYZ min = boundingBox.Min;
            XYZ max = boundingBox.Max;
            List<XYZ> points = new List<XYZ>
            {
                new XYZ(min.X, min.Y, min.Z),
                new XYZ(max.X, min.Y, min.Z),
                new XYZ(max.X, max.Y, min.Z),
                new XYZ(min.X, max.Y, min.Z)
            };
            List<Curve> curves = new List<Curve>();
            for (int i = 0; i < points.Count; i++)
            {
                XYZ start = points[i];
                XYZ end = points[(i + 1) % points.Count];
                curves.Add(Line.CreateBound(start, end));
            }

            return curves;
        }

        private Path64 ToPath64(IEnumerable<Curve> curveLoop)
        {
            Path64 result = new Path64();
            foreach (Curve curve in curveLoop)
            {
                XYZ point = curve.GetEndPoint(0);
                // Масштабируем точки для Clipper
                Point64 point64 = new Point64((int)(point.X * 1000000), (int)(point.Y * 1000000));
                result.Add(point64);
            }

            return result;
        }

        private CurveArray FromPaths64(Paths64 contour, double elevation)
        {
            CurveLoop result = new CurveLoop();
            // Преобразуем результат обратно в формат Revit
            foreach (Path64 path in contour)
            {
                for (int i = 0; i < path.Count - 1; i++)
                {
                    // Масштабируем точки обратно в Revit
                    XYZ start = new XYZ(path[i].X / 1000000.0, path[i].Y / 1000000.0, elevation);
                    XYZ end = new XYZ(path[i + 1].X / 1000000.0, path[i + 1].Y / 1000000.0, elevation);
                    try
                    {
                        result.Append(Line.CreateBound(start, end));
                    }
                    catch
                    {
                        continue;
                    }
                }
            }

            // Если в котуре больше одной линии
            // стоит проверить замкнутость контура 
            if (result.NumberOfCurves() > 1)
            {
                XYZ startPoint = result.First().GetEndPoint(0);
                XYZ endPoint = result.Last().GetEndPoint(1);
                if (!startPoint.IsAlmostEqualTo(endPoint) || result.IsOpen())
                    result.Append(Line.CreateBound(endPoint, startPoint));
            }

            return ConvertCurveLoopToCurveArray(result);
        }


        private CurveArray ConvertCurveLoopToCurveArray(CurveLoop curveLoop)
        {
            CurveArray curveArray = new CurveArray();
            foreach (Curve curve in curveLoop)
            {
                curveArray.Append(curve);
            }

            return curveArray;
        }
        private XYZ DetermineOffsetDirection(Line line)
        {
            // Получаем начальные и конечные точки линии
            XYZ startPoint = line.GetEndPoint(0);
            XYZ endPoint = line.GetEndPoint(1);

            // Вычисляем разницу по X и Y
            double deltaX = Math.Abs(endPoint.X - startPoint.X);
            double deltaY = Math.Abs(endPoint.Y - startPoint.Y);

            // Определяем направление смещения
            if (deltaX < deltaY)
            {
                // Смещение по оси X (вправо или влево)
                return XYZ.BasisX;
            }
            else
            {
                // Смещение по оси Y (вверх или вниз)
                return XYZ.BasisY;
            }
        }
        private Line FindOffsetLineWithoutIntersections(Document doc, Line line, double offsetValue, XYZ direction, BoundingBoxXYZ linkBbox)
        {
            int maxIterations = 100;
            int currentIteration = 0;
            bool isIntersecting = true;
            Outline outline = new Outline(linkBbox.Min, linkBbox.Max);
            IList<Element> elementsInBoundingBox = null;
            BoundingBoxIntersectsFilter bboxFilter = new BoundingBoxIntersectsFilter(outline);
            elementsInBoundingBox = new FilteredElementCollector(doc)
               .WherePasses(bboxFilter)
               .ToElements();
            // Смещаем линию до тех пор, пока она не перестанет пересекать элементы
            while (isIntersecting && currentIteration < maxIterations)
            {
                if (IsLineIntersectingElements(doc, line, direction, elementsInBoundingBox))
                {
                    line = OffsetLine(line, offsetValue, direction);
                    currentIteration++;
                }
                else
                {
                    isIntersecting = false;
                }
            }
            return line;
        }
        private bool IsLineIntersectingElements(Document doc, Line line, XYZ direction, IList<Element> elementsInBoundingBox)
        {
            // Получаем начальные и конечные точки линии
            XYZ point1 = line.GetEndPoint(0);
            XYZ point2 = line.GetEndPoint(1);

            // Находим центр линии
            XYZ centerPoint = (point1 + point2) / 2;

            // Определяем длину откладываемого отрезка
            double segmentLength = 0.5; // Длина отрезка в 1 см

            // Находим конечную точку отрезка в направлении заданного вектора
            XYZ offsetPoint = centerPoint + direction.Normalize() * segmentLength;

            // Определяем минимальные и максимальные точки для создания Outline
            double minX = Math.Min(centerPoint.X, offsetPoint.X);
            double minY = Math.Min(centerPoint.Y, offsetPoint.Y);
            double minZ = Math.Min(centerPoint.Z, offsetPoint.Z);

            double maxX = Math.Max(centerPoint.X, offsetPoint.X);
            double maxY = Math.Max(centerPoint.Y, offsetPoint.Y);
            double maxZ = Math.Max(centerPoint.Z, offsetPoint.Z);

            // Добавляем небольшой запас, чтобы избежать создания пустого Outline
            double tolerance = 0.001; // 1 см для точности
            XYZ minPointWithOffset = new XYZ(minX - tolerance, minY - tolerance, minZ - tolerance);
            XYZ maxPointWithOffset = new XYZ(maxX + tolerance, maxY + tolerance, maxZ + tolerance);

            // Создаем Outline с использованием этих точек
            Outline outline = new Outline(minPointWithOffset, maxPointWithOffset);
            BoundingBoxIntersectsFilter bboxFilter = new BoundingBoxIntersectsFilter(outline);

            // Собираем элементы, которые пересекают BoundingBox
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            ICollection<Element> intersectingElements = collector.WherePasses(bboxFilter)
                                                                 .WhereElementIsNotElementType()
                                                                 .ToElements();
            int count = 0;
            foreach (var element in intersectingElements)
            {
                if (element.Category != null &&
               (element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Cameras ||
                element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_TextNotes ||
                element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Dimensions ||
                element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Levels ||
                element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Grids ||
                element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_SectionBox ||
                element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Tags))
                {
                    continue;
                }

                if (elementsInBoundingBox.Any(e => e.Id == element.Id))
                {
                    count++;
                }
            }

            return count > 0;
        }
        private Line OffsetLine(Line line, double offsetValue, XYZ direction)
        {
            var curve = line.CreateOffset(offsetValue, XYZ.BasisZ);
            return Line.CreateBound(curve.GetEndPoint(0), curve.GetEndPoint(1));
        }
        public Line OffsetAndCreateNewLineOnPlan(Document linkDock, double offsetValue, Curve curve, BoundingBoxXYZ linbBbox)
        {
            try
            {
                Line selectedLine = curve as Line;
                XYZ direction = DetermineOffsetDirection(selectedLine);
                Line offsetLine = FindOffsetLineWithoutIntersections(linkDock, selectedLine, offsetValue, direction, linbBbox);
                return offsetLine;
            }
            catch
            {
                return null;
            }
        }
    }
}