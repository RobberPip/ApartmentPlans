using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApartmentPlans.Domain
{
    public class PlaceMarkModel
    {
        public AnnotationSymbolType mark;
        public List<string> namesRoom = new List<string>();
    }
}
