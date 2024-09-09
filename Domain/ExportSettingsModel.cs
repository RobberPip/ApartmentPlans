using Autodesk.Revit.DB;
using System;

namespace ApartmentPlans.Domain
{
    public class ExportSettingsModel
    {
        public Parameter NameCaseParameter {get; set; }
        public Parameter NameSectionParameter { get; set; }
        public Parameter NameFloorParameter { get; set; }
        public string NameProject { get; set; }
        public string PathToSave { get; set; }
        public string NumberApartment { get; set; }
        public View ExportView { get; set; }
        public View ExportViewSheet { get; set; }
        public string AdditionalWord { get; set; }
    }
}
