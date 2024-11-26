using ApartmentPlans.ViewModels;
using Autodesk.Revit.DB;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApartmentPlans.Domain
{
    public class PluginSettingsModelDTO: PluginSettingsModel
    {
        public PluginSettingsModelDTO(PluginSettingsModel baseSettings)
        {
            if (baseSettings != null)
            {
                this.CopyPropertiesFrom(baseSettings);

                ExportSettingsDTO = baseSettings?.ExportSettings != null ? new ExportSettingsModelDTO(baseSettings.ExportSettings) :null;
            }
        }
      
        [JsonInclude]
        public string SortParameterDTO { get => SortParameter?.Definition.Name ?? _sortParameterDTO; set => _sortParameterDTO = value; }
        private string _sortParameterDTO;
        [JsonInclude]
        public string ValueSortParameterDTO { get => ValueSortParameter ?? _valueSortParameterDTO; set => _valueSortParameterDTO = value; }
        private string _valueSortParameterDTO;
        [JsonInclude]
        public string NumApartParameterDTO { get => NumApartParameter?.Definition.Name ?? _numApartParameterNameDTO; set => _numApartParameterNameDTO = value; }
        private string _numApartParameterNameDTO;
        [JsonInclude]
        public List<SelectableFamilyModel> WallTypesDTO { get => WallTypes; set => WallTypes = value; }
        [JsonInclude]
        public List<SelectableFamilyModel> DoorTypesDTO { get => DoorTypes; set => DoorTypes = value; }
        [JsonInclude]
        public List<SelectableFamilyModel> LinkTypesDTO { get => LinkTypes; set => LinkTypes = value; }
        [JsonInclude]
        public List<SelectableFamilyModel> WallFacadesTypesDTO { get => WallFacadesTypes; set => WallFacadesTypes = value; }
        [JsonInclude]
        public bool IsExportDTO { get=>IsExport; set => IsExport = value; } 
        [JsonInclude]
        public bool IsRotatePlansDTO { get=>IsRotatePlans; set => IsRotatePlans = value; }
        [JsonInclude]
        public bool IsRotateFloorsDTO { get => IsRotateFloors; set => IsRotateFloors = value; }
        [JsonInclude]
        public List<MultiSelectModel> ActiveTemplatesDTO { get => ActiveTemplates; set => ActiveTemplates = value; }
        [JsonInclude]
        public ExportSettingsModelDTO ExportSettingsDTO { get; set; }
        [JsonInclude]
        public string TypeLegendDTO { get => TypeLegend?.Name ?? _typeLegendDTO; set => _typeLegendDTO = value; }
        private string _typeLegendDTO;
        [JsonInclude]
        public string TypesSquareTagDTO { get => TypesSquareTag?.Name ?? _typesSquareTagDTO; set => _typesSquareTagDTO = value; }
        private string _typesSquareTagDTO;
        [JsonInclude]
        public string ColumnParameterDTO { get => ColumnParameter?.Definition.Name ?? _columnParameterDTO; set => _columnParameterDTO = value; }
        private string _columnParameterDTO;
        [JsonInclude]
        public string TypeApartmentDTO { get => TypeApartment?.Definition.Name ?? _typeApartmentDTO; set => _typeApartmentDTO = value; }
        private string _typeApartmentDTO;
        [JsonInclude]
        public int WallFacadesOffestDTO { get => WallFacadesOffest!=0 ? WallFacadesOffest: _wallFacadesOffestDTO; set => _wallFacadesOffestDTO = value; }
        private int _wallFacadesOffestDTO;
        [JsonInclude]
        public List<RatioObjectModel<SelectableParameterModel>> ItemsScheduleDTO { get => ItemsSchedule; set => ItemsSchedule = value; }
        [JsonInclude]
        public List<RatioObjectModel<MultiSelectModel>> ItemsMarkDTO { get => ItemsMark; set => ItemsMark = value; }
        [JsonInclude]
        public bool AutoСolumnsDTO { get => AutoСolumns; set => AutoСolumns = value; }
        [JsonInclude]
        public bool AutoFacadeDTO { get => AutoFacade; set => AutoFacade = value; }
        [JsonInclude]
        public System.Windows.Media.Color ColorFillDTO
        {
            get => ColorFill;
            set => ColorFill = value;
        }

    }
}
