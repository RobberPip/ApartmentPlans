using ApartmentPlans.AuxiliaryСlasses;
using ApartmentPlans.Domain;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Clipper2Lib;
using System;
using System.Collections.Generic;
using System.Linq;
using JoinType = Clipper2Lib.JoinType;
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
        const double colorFactor = 0.95;
        private bool isErrorPlaceMarks = false;
        private bool isErrorExport = false;
        public UIApplication uiApp;
        public UIDocument uidoc;
        public Autodesk.Revit.ApplicationServices.Application app;
        public Document doc;
        public Dictionary<string, List<ElementId>> elementsToHideBoundingBox;
        private HashSet<string> exceptionViews = new HashSet<string>();
        private HashSet<string> exceptionPossibleViews = new HashSet<string>();
        private (List<PlaceMarkModel> Marks, List<string> ExeptionMarks) marks;
        public void CreatePlanApartment(List<WorkViewModel> views, PluginSettingsModel settings)
        {
            using (Transaction tx = new Transaction(doc, "Create cropped views"))
            {
                tx.Start();
                FailureHandlingOptions failureHandlingOptions = tx.GetFailureHandlingOptions();

                // Устанавливаем метод, который игнорирует все предупреждения
                failureHandlingOptions.SetFailuresPreprocessor(new IgnoreWarningsPreprocessor());

                // Применяем опции обработки ошибок к транзакции
                tx.SetFailureHandlingOptions(failureHandlingOptions);
                marks = CheckInstance.CheckMarksInProject(doc, settings);
                List<ViewPlan> templatesList = CheckInstance.CheckTemplatesInProject(doc, settings.ActiveTemplates);
                List<ApartmentModel> listApartment = new List<ApartmentModel>();
                var walls = new FilteredElementCollector(doc)
                    .OfClass(typeof(Wall))
                    .Where(d => settings.WallTypes.Select(q => q.element.Name).Contains(d.Name))
                    .ToList();
                using (SubTransaction subtx = new SubTransaction(doc))
                {
                    subtx.Start();
                    DeleteSeparators(views);
                    TempChangeRoom(walls, views, settings);
                    doc.Regenerate();
                    TakeRoomBorders(views, settings, listApartment);
                    subtx.RollBack();
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
                HashSet<View> MarkViews = new HashSet<View>();
                foreach (var apartment in listApartment)
                {
                    try
                    {
                        CurveLoop curveLoop = new CurveLoop();
                        foreach (Curve curve in apartment.contourInternalCurveArray)
                        {
                            curveLoop.Append(curve);
                        }
                        List<CurveLoop> tempList = new List<CurveLoop>() { curveLoop };
                        #region Создание вида с нарезанной квартирой
                        View apartmentView = doc.GetElement(apartment.activeView.Duplicate(ViewDuplicateOption.WithDetailing)) as View;
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

                        // Получаем все кривые из CurveLoop
                        List<Curve> curves = new List<Curve>(apartment.ContourСurveLoop);

                        // Проходим по всем кривым в контуре
                        for (int i = 0; i < curves.Count; i++)
                        {
                            Curve curve1 = curves[i];
                            Curve curve2 = curves[(i + 1) % curves.Count]; // следующая кривая, с учетом замыкания контура

                            if (!AreLinesPerpendicular(curve1, curve2))
                            {
                                exceptionPossibleViews.Add(apartment.roomGroup.Key);
                            }
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
                        if (settings.IsRotatePlans == true)
                        {
                            apartmentView.CropBoxVisible = true;
                            RotatePlan(apartmentView, apartment);
                            apartmentView.CropBoxVisible = false;
                        }
                        #endregion
                        #region Создание вида этажа с закрашиванием нарезанной квартиры
                        View floorPaintedView =
                            doc.GetElement(apartment.activeView.Duplicate(ViewDuplicateOption.Duplicate)) as View;
                        floorPaintedView.Name = apartmentView.Name + "_заливка_красная_" + apartment.roomGroup.Key;
                        CreateMaskAppartment(floorPaintedView, filledRegionType.Id, tempList, new Color(249, 209, 210));
                        List<BuiltInCategory> categoriesToHide = new List<BuiltInCategory>
                        {
                            BuiltInCategory.OST_FurnitureSystems,
                            BuiltInCategory.OST_Furniture,
                            BuiltInCategory.OST_MechanicalEquipment,
                            BuiltInCategory.OST_ElectricalEquipment,
                            BuiltInCategory.OST_ElectricalFixtures,
                            BuiltInCategory.OST_PlumbingFixtures,
                            BuiltInCategory.OST_SpecialityEquipment,
                            BuiltInCategory.OST_GenericModel
                        };
                        List<ElementId> furnitureToHide = new List<ElementId>();
                        foreach (BuiltInCategory category in categoriesToHide)
                        {
                            FilteredElementCollector collector = new FilteredElementCollector(doc, floorPaintedView.Id);
                            collector.OfCategory(category).WhereElementIsNotElementType();
                            furnitureToHide.AddRange(collector.ToElementIds());
                        }
                        furnitureToHide.AddRange(new FilteredElementCollector(doc, floorPaintedView.Id)
                                .OfCategory(BuiltInCategory.OST_Grids)
                                .OfClass(typeof(Grid))
                                .Select(g => g.Id));
                        if (furnitureToHide.Count > 0)
                        {
                            floorPaintedView.HideElements(furnitureToHide);
                        }
                        #endregion
                        #region Создание вида с маской нарезанной квартиры
                        View maskApartmentView =
                            doc.GetElement(floorPaintedView.Duplicate(ViewDuplicateOption.WithDetailing)) as View;
                        maskApartmentView.IsolateElementTemporary(CreateMaskAppartment(maskApartmentView, filledRegionType.Id, tempList, new Color(249, 209, 210)));
                        maskApartmentView.ConvertTemporaryHideIsolateToPermanent();
                        maskApartmentView.Name = apartmentView.Name + "_маска_" + apartment.roomGroup.Key;
                        #endregion
                        #region Создание вида этажа 
                        if (!floorNames.Contains(_tempName))
                        {
                            View floorView = doc.GetElement(apartment.activeView.Duplicate(ViewDuplicateOption.WithDetailing)) as View;
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
                            }
                        }

                        #endregion
                        #region Создание вида этажа с марками и цветовой заливкой
                        View floorMarkView =
                            doc.GetElement(floorPaintedView.Duplicate(ViewDuplicateOption.Duplicate)) as View;
                        floorMarkView.Name = apartmentView.Name + "_марки_" + apartment.roomGroup.Key;
                        MarkViews.Add(floorMarkView);
                        #endregion
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
                        for (int i = 0; i < templatesList.Count; i++)
                        {
                            iterationCount++;
                            if (iterationCount >= 2)
                            {
                                apartmentView = doc.GetElement(apartmentView.Duplicate(ViewDuplicateOption.WithDetailing)) as View;
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
                            }
                            apartmentView.ViewTemplateId = templatesList[i].Id;
                            floorPaintedView.ViewTemplateId = templatesList[i].Id;
                            floorMarkView.ViewTemplateId = templatesList[i].Id;
                            apartmentView.Name = nameViewApartment + "_" + templatesList[i].Name;
                            floorPaintedView.Name = nameViewFloorPainted + "_" + templatesList[i].Name;
                            floorMarkView.Name = nameViewFloorMark + "_" + templatesList[i].Name;
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
                            MarkViews.Add(floorMarkView);
                        }
                    }
                    catch
                    {
                        exceptionViews.Add(apartment.roomGroup.Key);
                    }
                }

                doc.Regenerate();
                Category annotationCategory = doc.Settings.Categories.get_Item(BuiltInCategory.OST_GenericAnnotation);
                foreach (View MarkView in MarkViews)
                {
                    try
                    {
                        MarkView.ViewTemplateId = ElementId.InvalidElementId;
                        CommandPlacementMarks.PlaceRoomMarks(doc, MarkView, marks.Marks, settings);
                        MarkView.SetCategoryHidden(annotationCategory.Id, false);
                        if (settings.TypesSquareTag != null)
                        {
                            CommandPlacementMarks.PlaceSquareMarks(doc, MarkView, settings.TypesSquareTag.element.Id, settings);
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
                            string pathPdf = CommandExport.ExportToPdf(doc, view, settings);
                            CommandExport.ConvertPdfToSvg(pathPdf);
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
                ExceptionShow();
            }
        }
        private void RotatePlan(View view, ApartmentModel apartment)
        {
            ViewCropRegionShapeManager vcrShapeMgr = view?.GetCropRegionShapeManager();
            ElementId typeId = view.GetTypeId();
            Element cropBoxElement = new FilteredElementCollector(doc, view.Id).WherePasses(new ElementIsElementTypeFilter(true))
            .Where(x => x.GetTypeId() == typeId && x.Name == view.Name && x.Id != view.Id).FirstOrDefault();
            BoundingBoxXYZ cropBox = view.CropBox;
            XYZ cropBoxCenter = (cropBox.Max + cropBox.Min) / 2.0;
            Line rotationAxis = Line.CreateBound(cropBoxCenter, cropBoxCenter + XYZ.BasisZ);
            ElementTransformUtils.RotateElement(doc, cropBoxElement.Id, rotationAxis, apartment.rotationAngleDegrees * (Math.PI / 180.0));
            IList<CurveLoop> cropShape = vcrShapeMgr.GetCropShape();
            CurveLoop cropRegionLoop = cropShape[0];
            CurveLoop rotatedCropRegionLoop = RotateCurveLoop(cropRegionLoop, cropBoxCenter, apartment.rotationAngleDegrees);
            vcrShapeMgr.SetCropShape(rotatedCropRegionLoop);
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
        private List<ApartmentModel> TakeRoomBorders(List<WorkViewModel> views, PluginSettingsModel settings, List<ApartmentModel> listApartment)
        {
            elementsToHideBoundingBox = new Dictionary<string, List<ElementId>>();
            foreach (var workView in views)
            {
                var allRooms = new FilteredElementCollector(doc, workView.View.Id)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .ToElements()
                    .GroupBy(room => room.LookupParameter(settings.NumApartParameter.Definition.Name).AsString());
                // Получение входных дверей
                List<FamilyInstance> doors = new FilteredElementCollector(doc, workView.View.Id)
                    .OfCategory(BuiltInCategory.OST_Doors)
                    .OfClass(typeof(FamilyInstance))
                    .Cast<FamilyInstance>()
                    .Where(d => settings.DoorTypes.Select(q => q.element.Id).Contains(d.Symbol.Id))
                    .ToList();

                var apartmentRooms = allRooms
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
                        if (doors.Any())
                        {
                            var filteredDoor = doors
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
                                elementsToHideBoundingBox.Add(roomGroup.Key, doorIds);
                            }
                            apartment.FilteredDoor = filteredDoor;
                            apartment.rotationAngleDegrees = angleDegrees;

                        }

                        Paths64 combinedPaths = new Paths64();
                        Paths64 internalPaths = new Paths64();
                        int currentIteration = 0;
                        int totalIterations = roomGroup.Count();
                        foreach (var element in roomGroup)
                        {
                            var room = (Room)element;

                            List<Curve> roomCurves = new List<Curve>();
                            List<Curve> internalRoomCurves = new List<Curve>();
                            var options = new SpatialElementBoundaryOptions();
                            options.SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Center;
                            var segments = room.GetBoundarySegments(options);
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
                                    switch (categoryValue)
                                    {
                                        case (int)BuiltInCategory.OST_Walls:
                                            {
                                                double offset = (doc.GetElement(boundarySegment.ElementId) as Wall).Width / 2;
                                                if ((doc.GetElement(boundarySegment.ElementId) as Wall).WallType.Kind == WallKind.Curtain)
                                                {
                                                    // Офсет для витражей подсчитанный вручную
                                                    offset += 0.595;
                                                }
                                                curve = curve.CreateOffset(offset, XYZ.BasisZ);
                                                break;
                                            }
                                        case (int)BuiltInCategory.OST_StructuralColumns:
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
                                        default:
                                            {
                                                if (settings.WallFacadesTypes.Select(q => q.element.Name).Contains(boundaryElement.Name) && settings.WallFacadesOffest != 0)
                                                {
                                                    double offset = settings.WallFacadesOffest / 304.8;
                                                    curve = curve.CreateOffset(offset, XYZ.BasisZ);
                                                }
                                                break;
                                            }
                                    }
                                }
                                roomCurves.Add(curve);
                            }
                            options.SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish;
                            segments = room.GetBoundarySegments(options);
                            foreach (BoundarySegment boundarySegment in segments.FirstOrDefault())
                            {
                                internalRoomCurves.Add(boundarySegment.GetCurve());
                            }
                            List<Curve> correctedRoomContour = CorrectedContour(roomCurves);
                            ClipperOffset offsetClipper = new ClipperOffset();
                            offsetClipper.AddPath(ToPath64(correctedRoomContour), JoinType.Miter, EndType.Polygon);
                            Paths64 offsetPaths = new Paths64();
                            offsetClipper.Execute(2 * 1000000, offsetPaths);

                            // Выполнение объединения
                            combinedPaths = Clipper.Union(
                                combinedPaths,
                                offsetPaths,
                                Clipper2Lib.FillRule.NonZero
                            );

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

                            // Возвращение офсета обратно только в последнем цикле
                            if (currentIteration == totalIterations)
                            {
                                ClipperOffset reverseOffsetClipper = new ClipperOffset();
                                reverseOffsetClipper.AddPaths(combinedPaths, JoinType.Miter, EndType.Polygon);
                                Paths64 reverseOffsetPaths = new Paths64();
                                reverseOffsetClipper.Execute(-1.980 * 1000000, reverseOffsetPaths);
                                combinedPaths = reverseOffsetPaths;

                            }
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
                        combinedPaths = Clipper.Union(combinedPaths, pathsToCombine, FillRule.NonZero);
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
        public List<Curve> CorrectedContour(List<Curve> curves)
        {
            List<Curve> newContour = new List<Curve>();
            Curve nextLine = curves[0].Clone();

            for (int i = 0; i < curves.Count; i++)
            {
                Curve currentLine = nextLine;
                nextLine = i < curves.Count - 1 ? curves[i + 1].Clone() : newContour[0];

                newContour.Add(currentLine);
                List<Curve> connectedLines = InsertLines((Line)currentLine, (Line)nextLine, out bool originalsChanged);
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

            return SimplifiedContour(newContour);
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
                return new List<Curve>
                {
                    Line.CreateBound(line1.GetEndPoint(1), line2.GetEndPoint(0)),
                };
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

        private List<Curve> SimplifiedContour(List<Curve> curves)
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
                        return new CurveArray();
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
        private static bool AreLinesPerpendicular(Curve curve1, Curve curve2)
        {
            // Получаем конечную точку первой кривой и начальную точку второй кривой
            XYZ endPoint1 = curve1.GetEndPoint(1);
            XYZ startPoint2 = curve2.GetEndPoint(0);

            // Убедимся, что кривые соединены в одной точке
            if (!endPoint1.IsAlmostEqualTo(startPoint2))
            {
                return false;
            }

            // Получаем направления кривых
            XYZ direction1 = (curve1.GetEndPoint(1) - curve1.GetEndPoint(0)).Normalize();
            XYZ direction2 = (curve2.GetEndPoint(1) - curve2.GetEndPoint(0)).Normalize();

            // Проверяем перпендикулярность (скалярное произведение равно нулю)
            double dotProduct = direction1.DotProduct(direction2);
            return Math.Abs(dotProduct) < 1e-6; // Учитываем возможные погрешности
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

    }
}