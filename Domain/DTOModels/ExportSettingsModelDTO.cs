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
        public string NameFloorParameterDTO { get => NameFloorParameter?.Definition.Name ?? _nameFloorParameterDTO; set => _nameFloorParameterDTO = value; }
        private string _nameFloorParameterDTO;
        [JsonInclude]
        public string NameProjectDTO { get => NameProject; set => NameProject = value; }
        [JsonInclude]
        public string PathToSaveDTO { get => PathToSave; set => PathToSave = value; }
        [JsonInclude]
        public bool IsСuttingBoundDTO { get => IsСuttingBound; set => IsСuttingBound = value; }
        [JsonInclude]
        public int OffsetLinkDTO { get => OffsetLink != 0 ? OffsetLink : _offsetLinkDTO; set => _offsetLinkDTO = value; }
        private int _offsetLinkDTO;
        [JsonInclude]
        public int OffsetLinkAdvanceDTO { get => OffsetLinkAdvance != 0 ? OffsetLinkAdvance : _offsetLinkAdvanceDTO; set => _offsetLinkAdvanceDTO = value; }
        private int _offsetLinkAdvanceDTO;
    }
}
