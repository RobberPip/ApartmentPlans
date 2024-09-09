using ApartmentPlans.Domain;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ApartmentPlans
{
    public static class CommandPlacementMarks
    {
        private static View _view;
        public static void PlaceSquareMarks(Document doc, View view, ElementId markId, PluginSettingsModel settings)
        {
            _view = view;
            var rooms = new FilteredElementCollector(doc, view.Id)
            .OfCategory(BuiltInCategory.OST_Rooms)
            .Where(e => e.LookupParameter(settings.SortParameter.Definition.Name).AsValueString() == settings.ValueSortParameter)
            .Cast<Room>();
            foreach (Room room in rooms)
            {
                var boundarySegments = room.GetBoundarySegments(new SpatialElementBoundaryOptions());
                double? maxX = null;
                List<XYZ> points = new List<XYZ>();
                double epsilon = 1e-9; // Предполагаемое значение epsilon

                foreach (var boundarySegment in boundarySegments)
                {
                    foreach (BoundarySegment segment in boundarySegment)
                    {
                        Curve curve = segment.GetCurve();
                        XYZ[] endpoints = { curve.GetEndPoint(0), curve.GetEndPoint(1) };

                        foreach (var point in endpoints)
                        {
                            double xValue = point.X;

                            if (maxX == null || xValue > maxX.Value + epsilon)
                            {
                                maxX = xValue;
                                points.Clear(); // Очистка списка, так как нашли новую максимальную точку X
                                points.Add(point);
                            }
                            else if (maxX.HasValue && Math.Abs(xValue - maxX.Value) < epsilon)
                            {
                                points.Add(point);
                            }
                        }
                    }
                }

                // Найдите точку с минимальным значением Y среди точек с максимальным значением X
                XYZ topRight = points.OrderBy(p => p.Y).FirstOrDefault();
                XYZ offset = new XYZ(-1, 1, 0);

                XYZ offsetBottomRight = topRight + offset;
                UV tagLocation = new UV(offsetBottomRight.X, offsetBottomRight.Y);
                RoomTag newTag = doc.Create.NewRoomTag(new LinkElementId(room.Id), tagLocation, _view.Id);
                newTag.ChangeTypeId(markId);
            }
        }
        public static void PlaceRoomMarks(Document doc, View view, List<PlaceMarkModel> marks, PluginSettingsModel settings)
        {
            _view = view;
            foreach (var markModel in marks)
            {
                var rooms = new FilteredElementCollector(doc, view.Id)
                           .OfCategory(BuiltInCategory.OST_Rooms)
                           .Where(room => markModel.namesRoom.Contains(room.get_Parameter(BuiltInParameter.ROOM_NAME).AsValueString()))
                           .Cast<Room>();

                foreach (Room room in rooms)
                {
                    if (markModel.mark != null)
                    {
                        BoundingBoxXYZ boundingBox = room.get_BoundingBox(null);
                        XYZ centerPoint = (boundingBox.Min + boundingBox.Max) / 2;
                        doc.Create.NewFamilyInstance(centerPoint, markModel.mark, view);
                    }
                }
            }

        }
    }

}
