using Autodesk.Revit.DB;
using System.ComponentModel;

namespace ApartmentPlans.Domain
{
    public class SelectableParameterModel : INotifyPropertyChanged
    {
        public Parameter parameter {  get; set; }
        private string _name;
        [JsonInclude]
        public string Name
        {
            get => _name ?? parameter?.Definition.Name;
            set => _name = value;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
