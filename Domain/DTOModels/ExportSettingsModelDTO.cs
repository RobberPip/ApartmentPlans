using ApartmentPlans.Domain;
using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApartmentPlans
{
    public class ExportSettingsModelDTO: ExportSettingsModel
    {
        public ExportSettingsModelDTO(ExportSettingsModel baseSettings)
        {
            if (baseSettings != null)
            {
                this.CopyPropertiesFrom(baseSettings);
            }
        }
        [JsonInclude]
        public string NameCaseParameterDTO { get=>NameCaseParameter?.Definition.Name ?? _nameCaseParameterDTO; set => _nameCaseParameterDTO = value; }
        private string _nameCaseParameterDTO;
        [JsonInclude]
        public string NameSectionParameterDTO { get => NameSectionParameter?.Definition.Name ?? _nameSectionParameterDTO; set => _nameSectionParameterDTO = value; }
        private string _nameSectionParameterDTO;
        [JsonInclude]
        public string NameFloorParameterDTO { get => NameSectionParameter?.Definition.Name ?? _nameFloorParameterDTO; set => _nameFloorParameterDTO = value; }
        private string _nameFloorParameterDTO;
        [JsonInclude]
        public string NameProjectDTO { get => NameProject; set => NameProject = value; }
        [JsonInclude]
        public string PathToSaveDTO { get => PathToSave; set => PathToSave = value; }
    }
}
