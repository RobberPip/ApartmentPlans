using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApartmentPlans.Domain
{
    public class MultiSelectModel : INotifyPropertyChanged
    {
        private bool _isChecked;
        private bool _wasChecked;
        public bool IsEnabled { get; set; }
        [JsonInclude]
        public string Name { get; set; }
        public bool WasChecked
        {
            get => _wasChecked;
            set
            {
                if (_wasChecked != value)
                {
                    _wasChecked = value;
                    OnPropertyChanged(nameof(WasChecked));
                }
            }
        }
        [JsonInclude]
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    OnPropertyChanged(nameof(IsChecked));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
