using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;

namespace ApartmentPlans.Domain
{
    public class RatioObjectModel<T> : INotifyPropertyChanged
    {
        private string _filterObjectText;
        private T _selectedObjectParameter;
        private CollectionViewSource _filteredObjectParameters;
        [JsonInclude]
        public ObservableCollection<T> ObjectParameters { get; set; } = new ObservableCollection<T>();
        [JsonInclude]
        public string Name { get; set; }

        
        public List<MultiSelectModel> CheckedObjects
        {
            get => ObjectParameters.OfType<MultiSelectModel>().Where(q => q.IsChecked).ToList();
        }

        public string FilterObjectText
        {
            get => _filterObjectText;
            set
            {
                if (_filterObjectText != value)
                {
                    _filterObjectText = value;
                    OnPropertyChanged(nameof(FilterObjectText));
                    FilteredObjectParameters.View.Refresh();
                }
            }
        }
        [JsonInclude]
        public T SelectedObjectParameter
        {
            get => _selectedObjectParameter;
            set
            {
                if (!Equals(_selectedObjectParameter, value))
                {
                    _selectedObjectParameter = value;
                    OnPropertyChanged(nameof(SelectedObjectParameter));
                }
            }
        }
        public CollectionViewSource FilteredObjectParameters
        {
            get
            {
                if (_filteredObjectParameters == null)
                {
                    _filteredObjectParameters = new CollectionViewSource { Source = ObjectParameters };

                    _filteredObjectParameters.Filter += (s, e) =>
                    {
                        if (string.IsNullOrWhiteSpace(FilterObjectText))
                        {
                            e.Accepted = true;
                            return;
                        }
                        if (e.Item is SelectableParameterModel item)
                        {
                            e.Accepted = item.Name.IndexOf(FilterObjectText, StringComparison.OrdinalIgnoreCase) >= 0;
                        }
                        else if (e.Item is MultiSelectModel itemTemplate)
                        {
                            e.Accepted = itemTemplate.Name.IndexOf(FilterObjectText, StringComparison.OrdinalIgnoreCase) >= 0;
                        }
                        else
                        {
                            e.Accepted = false;
                        }
                    };

                    // Подписка на изменение состояния объектов
                    foreach (var item in ObjectParameters.OfType<MultiSelectModel>())
                    {
                        item.PropertyChanged += OnMultiSelectModelPropertyChanged;
                    }

                    // Сохраняем состояние отмеченных элементов
                    SaveCheckedStates();

                    // Применяем фильтр
                    _filteredObjectParameters.View.Refresh();

                    // Восстанавливаем состояние отмеченных элементов
                    RestoreCheckedStates();

                    // Явно устанавливаем CurrentItem в null
                    _filteredObjectParameters.View.MoveCurrentToPosition(-1);
                }
                return _filteredObjectParameters;
            }
        }

        private void OnMultiSelectModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MultiSelectModel.IsChecked))
            {
                // Ваш метод обработки изменений свойства IsChecked
                HaveValueChecked= CheckedObjects.Count > 0 ? "✔️" : "✖";
            }
        }
        public string HaveValueChecked
        {
            get => _haveValueChecked;
            set
            {
                if (_haveValueChecked != value)
                {
                    _haveValueChecked = value;
                    OnPropertyChanged(nameof(HaveValueChecked));
                }
            }
        }
        private string _haveValueChecked = "✖";
        private void SaveCheckedStates()
        {
            foreach (var item in ObjectParameters.OfType<MultiSelectModel>())
            {
                item.WasChecked = item.IsChecked; // Сохраняем состояние
            }
        }

        private void RestoreCheckedStates()
        {
            foreach (var item in FilteredObjectParameters.View.OfType<MultiSelectModel>())
            {
                // Восстанавливаем состояние
                if (ObjectParameters.OfType<MultiSelectModel>().Contains(item))
                {
                    var originalItem = ObjectParameters.OfType<MultiSelectModel>().FirstOrDefault(x => x.Name == item.Name);
                    if (originalItem != null)
                    {
                        item.IsChecked = originalItem.WasChecked;
                    }
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
