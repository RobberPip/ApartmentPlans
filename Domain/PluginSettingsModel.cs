using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace ApartmentPlans.Domain
{
    public class PluginSettingsModel
    {
        public Parameter SortParameter { get; set; }
        public string ValueSortParameter { get; set; }
        public Parameter NumApartParameter { get; set; }
        public List<SelectableFamilyModel> WallTypes { get; set; }
        public List<SelectableFamilyModel> DoorTypes { get; set; }
        public List<SelectableFamilyModel> WallFacadesTypes { get; set; }
        public bool IsExport { get; set; }
        public bool IsRotatePlans { get; set; }
        public List<MultiSelectModel> ActiveTemplates { get; set; }
        public ExportSettingsModel ExportSettings { get; set; }
        public View TypeLegend { get; set; }
        public SelectableFamilyModel TypesSquareTag { get; set; }
        public Parameter ColumnParameter { get; set; }
        public Parameter TypeApartment { get; set; }
        public int WallFacadesOffest { get; set; }
        public List<RatioObjectModel<SelectableParameterModel>> ItemsSchedule { get; set; }
        public List<RatioObjectModel<MultiSelectModel>> ItemsMark { get; set; }
    }
}
