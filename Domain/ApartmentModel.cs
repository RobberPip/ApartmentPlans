using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace ApartmentPlans.Domain
{
    public class ApartmentModel
    {
        public IGrouping<string,Element> roomGroup;
        public FamilyInstance FilteredDoor {get; set; }
        public CurveArray contourCurveArray;
        public CurveArray contourInternalCurveArray;
        private CurveLoop _contourСurveLoop;
        public CurveLoop ContourСurveLoop
        {
            get
            {
                if (_contourСurveLoop == null)
                {
                    _contourСurveLoop = new CurveLoop();
                    foreach (Curve curve in contourCurveArray)
                    {
                        _contourСurveLoop.Append(curve);
                    }
                }
                return _contourСurveLoop;
            }
        }
        public View activeView;
        public double rotationAngleDegrees;
    }
}