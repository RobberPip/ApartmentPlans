using ApartmentPlans.Domain;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Input;

namespace ApartmentPlans.ViewModels
{
    public partial class MainVm
    {
        public bool _autoСolumns  = true;
        public bool _autoFacade = true;
        public bool AutoFacade
        {
            get => _autoFacade;
            set => SetProperty(ref _autoFacade, value);
        }
        public bool AutoСolumns
        {
            get => _autoСolumns;
            set => SetProperty(ref _autoСolumns, value);
        }
        private bool _isExport;
        private bool _isСuttingBound;
        private bool _isRotatePlans;
        private bool _isRotateFloors;
        private string _wallFacadesOffest;
        private string _pathToSave = "Путь: по умолачнию";
        private string _filterSortText;
        private string _filterTypeApartmentText;
        private string _filterTypesLegendText;
        private string _filterTypesSquareText;
        private string _filterNamesCaseText;
        private string _filterNameProjectText;
        private string _filterNamesSectionText;
        private string _filterNamesFloorText;
        private string _filterValueSortText;
        private string _filterNumApartText;
        private string _filterСolumnText;
        private string correctedPath;
        private SelectableParameterModel _selectedSortParameter;
        private SelectableParameterModel _selectedTypeApartmentParameter;
        private string _selectedValueSortParameter;
        private SelectableParameterModel _selectedNumApartParameter;
        private SelectableParameterModel _selectedNameCaseParameter;
        private SelectableParameterModel _selectedNameSectionParameter;
        private SelectableParameterModel _selectedNameFloorParameter;
        private SelectableParameterModel _selectedNameProjectParameter;
        private Parameter _selectedСolumnParameter;
        private View _selectedTypeLegend;
        private SelectableFamilyModel _selectedTypeSquare;
        private CollectionViewSource _filteredTypeApartmentParameters;
        private CollectionViewSource _filteredSortParameters;
        private CollectionViewSource _filteredNamesSectionParameters;
        private CollectionViewSource _filteredValueSortParameters;
        private CollectionViewSource _numApartParameters;
        private CollectionViewSource _filteredNamesCaseParameters;
        private CollectionViewSource _filteredNamesFloorParameters;
        private CollectionViewSource _filteredNameProjectParameters;
        private CollectionViewSource _filteredTypesLegend;
        private CollectionViewSource _filteredСolumnParameters;
        private CollectionViewSource _filteredTypesSquare;
        private RelayCommand _startCommand;
        private RelayCommand _exportCommand;
        private RelayCommand _choosePathToSaveCommand;
        private RelayCommand _saveSettignsCommand;
        private RelayCommand _uploadSettignsCommand;
        private int _selectedItemsCount;
        private string _labelDoorCountText;
        private string _labelLinkCountText;
        private string _labelWallCountText;
        private string _labelWallFacadesCountText;
        public int _offsetLink;
        public int _offsetLinkAdvance = 700;
        private System.Windows.Media.Color _selectedColor = System.Windows.Media.Color.FromRgb(249, 209, 210);

        public System.Windows.Media.Color SelectedColor
        {
            get => _selectedColor;
            set => SetProperty(ref _selectedColor, value);
        }

        public bool IsСuttingBound
        {
            get => _isСuttingBound;
            set => SetProperty(ref _isСuttingBound, value);
        }
        public int OffsetLinkAdvance
        {
            get => _offsetLinkAdvance;
            set => SetProperty(ref _offsetLinkAdvance, value);
        }
        public int OffsetLink
        {
            get => _offsetLink;
            set => SetProperty(ref _offsetLink, value);
        }
        public bool IsRotateFloors
        {
            get => _isRotateFloors;
            set => SetProperty(ref _isRotateFloors, value);
        }
        public bool IsRotatePlans
        {
            get => _isRotatePlans;
            set => SetProperty(ref _isRotatePlans, value);
        }
        public bool IsExport
        {
            get => _isExport;
            set => SetProperty(ref _isExport, value);
        }

        public string PathToSave
        {
            get => _pathToSave;
            set => SetProperty(ref _pathToSave, value);
        }
        public string LabelDoorCountText
        {
            get => _labelDoorCountText;
            set => SetProperty(ref _labelDoorCountText, value);
        }
        public string LabelLinkCountText
        {
            get => _labelLinkCountText;
            set => SetProperty(ref _labelLinkCountText, value);
        }
        public string LabelWallCountText
        {
            get => _labelWallCountText;
            set => SetProperty(ref _labelWallCountText, value);
        }
        public string LabelWallFacadesCountText
        {
            get => _labelWallFacadesCountText;
            set => SetProperty(ref _labelWallFacadesCountText, value);
        }
        public int SelectedItemsCount
        {
            get => _selectedItemsCount;
            set => SetProperty(ref _selectedItemsCount, value);
        }
        public string WallFacadesOffest
        {
            get => _wallFacadesOffest;
            set => SetProperty(ref _wallFacadesOffest, value);
        }

        public ObservableCollection<WorkViewModel> _tempViews = new ObservableCollection<WorkViewModel>();
        public ObservableCollection<WorkViewModel> _tempViewsSheet = new ObservableCollection<WorkViewModel>();
        public ObservableCollection<WorkViewModel> Views { get; set; } = new ObservableCollection<WorkViewModel>();
        public ObservableCollection<WorkViewModel> ViewsSheet { get; set; } = new ObservableCollection<WorkViewModel>();
        public ObservableCollection<SelectableParameterModel> SortParameters { get; set; } = new ObservableCollection<SelectableParameterModel>();
        public ObservableCollection<SelectableParameterModel> TypeApartmentParameters { get; set; } = new ObservableCollection<SelectableParameterModel>();
        public ObservableCollection<SelectableParameterModel> NamesCaseParameters { get; set; } = new ObservableCollection<SelectableParameterModel>();
        public ObservableCollection<SelectableParameterModel> NameProjectParameters { get; set; } = new ObservableCollection<SelectableParameterModel>();
        public ObservableCollection<SelectableParameterModel> NamesSectionParameters { get; set; } = new ObservableCollection<SelectableParameterModel>();
        public ObservableCollection<SelectableParameterModel> NamesFloorParameters { get; set; } = new ObservableCollection<SelectableParameterModel>();
        public ObservableCollection<SelectableParameterModel> NumApartParameters { get; set; } = new ObservableCollection<SelectableParameterModel>();
        public ObservableCollection<SelectableFamilyModel> DoorTypes { get; set; } = new ObservableCollection<SelectableFamilyModel>();
        public ObservableCollection<SelectableFamilyModel> WallTypes { get; set; } = new ObservableCollection<SelectableFamilyModel>();
        public ObservableCollection<SelectableFamilyModel> FilesFacde { get; set; } = new ObservableCollection<SelectableFamilyModel>();
        public ObservableCollection<SelectableFamilyModel> WallFacadesTypes { get; set; } = new ObservableCollection<SelectableFamilyModel>();
        public ObservableCollection<SelectableFamilyModel> TypesSquare { get; set; } = new ObservableCollection<SelectableFamilyModel>();
        public ObservableCollection<MultiSelectModel> TemplateList { get; set; }
        public ObservableCollection<RatioObjectModel<SelectableParameterModel>> ItemsSchedule { get; set; } = new ObservableCollection<RatioObjectModel<SelectableParameterModel>>();
        public ObservableCollection<RatioObjectModel<MultiSelectModel>> ItemsMark { get; set; } = new ObservableCollection<RatioObjectModel<MultiSelectModel>>();
        public ObservableCollection<View> TypesLegend { get; set; } = new ObservableCollection<View>();
        public ObservableCollection<string> ValueSortParameters { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<Parameter> СolumnParameters { get; set; } = new ObservableCollection<Parameter>();
        

        public string FilterNamesCaseText
        {
            get => _filterNamesCaseText;
            set
            {
                if (_filterNamesCaseText != value)
                {
                    _filterNamesCaseText = value;
                    OnPropertyChanged(nameof(FilterNamesCaseText));
                    FilteredNamesCaseParameters.View.Refresh();
                }
            }
        }
        public string FilterNamesSectionText
        {
            get => _filterNamesSectionText;
            set
            {
                if (_filterNamesSectionText != value)
                {
                    _filterNamesSectionText = value;
                    OnPropertyChanged(nameof(FilterNamesSectionText));
                    FilteredNamesSectionParameters.View.Refresh();
                }
            }
        }
        public string FilterNameProjectText
        {
            get => _filterNameProjectText;
            set
            {
                if (_filterNameProjectText != value)
                {
                    _filterNameProjectText = value;
                    OnPropertyChanged(nameof(FilterNameProjectText));
                    FilteredNameProjectParameters.View.Refresh();
                }
            }
        }
        public string FilterNamesFloorText
        {
            get => _filterNamesFloorText;
            set
            {
                if (_filterNamesFloorText != value)
                {
                    _filterNamesFloorText = value;
                    OnPropertyChanged(nameof(FilterNamesFloorText));
                    FilteredNamesFloorParameters.View.Refresh();
                }
            }
        }
        public string FilterSortText
        {
            get => _filterSortText;
            set
            {
                if (_filterSortText != value)
                {
                    _filterSortText = value;
                    OnPropertyChanged(nameof(FilterSortText));
                    FilteredSortParameters.View.Refresh();
                }
            }
        }
        public string FilterTypeApartmentText
        {
            get => _filterTypeApartmentText;
            set
            {
                if (_filterTypeApartmentText != value)
                {
                    _filterTypeApartmentText = value;
                    OnPropertyChanged(nameof(FilterTypeApartmentText));
                    FilteredTypeApartmentParameters.View.Refresh();
                }
            }
        }
        public string FilterValueSortText
        {
            get => _filterValueSortText;
            set
            {
                if (_filterValueSortText != value)
                {
                    _filterValueSortText = value;
                    OnPropertyChanged(nameof(FilterValueSortText));
                    FilteredValueSortParameters.View.Refresh();
                }
            }
        }
        public string FilterNumApartText
        {
            get => _filterNumApartText;
            set
            {
                if (_filterNumApartText != value)
                {
                    _filterNumApartText = value;
                    OnPropertyChanged(nameof(FilterNumApartText));
                    FilteredNumApartParameters.View.Refresh();
                }
            }
        }
        public string FilterTypesLegendText
        {
            get => _filterTypesLegendText;
            set
            {
                if (_filterTypesLegendText != value)
                {
                    _filterTypesLegendText = value;
                    OnPropertyChanged(nameof(FilterTypesLegendText));
                    FilteredTypesLegend.View.Refresh();
                }
            }
        }
        public string FilterTypesSquareText
        {
            get => _filterTypesSquareText;
            set
            {
                if (_filterTypesSquareText != value)
                {
                    _filterTypesSquareText = value;
                    OnPropertyChanged(nameof(FilterTypesSquareText));
                    FilteredTypesSquare.View.Refresh();
                }
            }
        }
        public string FilterСolumnText
        {
            get => _filterСolumnText;
            set
            {
                if (_filterСolumnText != value)
                {
                    _filterСolumnText = value;
                    OnPropertyChanged(nameof(FilterСolumnText));
                    FilteredСolumnParameters.View.Refresh();
                }
            }
        }
        public SelectableParameterModel SelectedSortParameter
        {
            get => _selectedSortParameter;
            set
            {
                SetProperty(ref _selectedSortParameter, value);
                CommandManager.InvalidateRequerySuggested(); // Обновляет состояние команды
            }
        }
        public string SelectedValueSortParameter
        {
            get => _selectedValueSortParameter;
            set
            {
                SetProperty(ref _selectedValueSortParameter, value);
                // Предварительно собираем уникальные имена комнат до цикла
                if (SelectedSortParameter != null && SelectedValueSortParameter != null)
                {
                    var distinctRoomNames = roomCollector
                        .Where(q => q.LookupParameter(SelectedSortParameter?.parameter.Definition.Name).AsString() == SelectedValueSortParameter)
                        .Select(q => q.get_Parameter(BuiltInParameter.ROOM_NAME).AsValueString())
                        .Distinct()
                        .ToList();

                    foreach (var item in ItemsMark)
                    {
                        var multiSelectModels = distinctRoomNames
                        .Select(name => new MultiSelectModel { Name = name })
                        .ToList();
                        item.ObjectParameters.Clear();
                        foreach (var multiSelectModel in multiSelectModels)
                        {
                            item.ObjectParameters.Add(multiSelectModel);
                        }
                    }
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }
        public SelectableParameterModel SelectedNumApartParameter
        {
            get => _selectedNumApartParameter;
            set
            {
                SetProperty(ref _selectedNumApartParameter, value);
                CommandManager.InvalidateRequerySuggested(); // Обновляет состояние команды
            }
        }
        public SelectableParameterModel SelectedNameCaseParameter
        {
            get => _selectedNameCaseParameter;
            set => SetProperty(ref _selectedNameCaseParameter, value);
        }
        public SelectableParameterModel SelectedNameProjectParameter
        {
            get => _selectedNameProjectParameter;
            set => SetProperty(ref _selectedNameProjectParameter, value);
        }
        public SelectableParameterModel SelectedNameSectionParameter
        {
            get => _selectedNameSectionParameter;
            set => SetProperty(ref _selectedNameSectionParameter, value);
        }
        public SelectableParameterModel SelectedNameFloorParameter
        {
            get => _selectedNameFloorParameter;
            set => SetProperty(ref _selectedNameFloorParameter, value);
        }
        public Parameter SelectedСolumnParameter
        {
            get => _selectedСolumnParameter;
            set => SetProperty(ref _selectedСolumnParameter, value);
        }
        public SelectableParameterModel SelectedTypeApartmentParameter
        {
            get => _selectedTypeApartmentParameter;
            set => SetProperty(ref _selectedTypeApartmentParameter, value);
        }
        public View SelectedTypeLegend
        {
            get => _selectedTypeLegend;
            set => SetProperty(ref _selectedTypeLegend, value);
        }
        public SelectableFamilyModel SelectedTypeSquare
        {
            get => _selectedTypeSquare;
            set => SetProperty(ref _selectedTypeSquare, value);
        }

        public CollectionViewSource FilteredSortParameters
        {
            get
            {
                if (_filteredSortParameters == null)
                {
                    _filteredSortParameters = new CollectionViewSource { Source = SortParameters };
                    _filteredSortParameters.Filter += (s, e) =>
                    {
                        if (string.IsNullOrWhiteSpace(FilterSortText))
                        {
                            e.Accepted = true;
                            return;
                        }
                        if (e.Item is SelectableParameterModel item)
                        {
                            e.Accepted = item.Name.IndexOf(FilterSortText, StringComparison.OrdinalIgnoreCase) >= 0;
                        }
                        else
                        {
                            e.Accepted = false;
                        }
                    };
                    _filteredSortParameters.View.MoveCurrentToPosition(-1);
                }
                return _filteredSortParameters;
            }
        }
        public CollectionViewSource FilteredValueSortParameters
        {
            get
            {
                if (_filteredValueSortParameters == null)
                {
                    _filteredValueSortParameters = new CollectionViewSource { Source = ValueSortParameters };
                    _filteredValueSortParameters.Filter += (s, e) =>
                    {
                        if (string.IsNullOrWhiteSpace(FilterValueSortText))
                        {
                            e.Accepted = true;
                            return;
                        }
                        if (e.Item is string item)
                        {
                            e.Accepted = item.IndexOf(FilterValueSortText, StringComparison.OrdinalIgnoreCase) >= 0;
                        }
                        else
                        {
                            e.Accepted = false;
                        }
                    };
                    _filteredValueSortParameters.View.MoveCurrentToPosition(-1);
                }
                return _filteredValueSortParameters;
            }
        }
        public CollectionViewSource FilteredNumApartParameters
        {
            get
            {
                if (_numApartParameters == null)
                {
                    _numApartParameters = new CollectionViewSource { Source = NumApartParameters };
                    _numApartParameters.Filter += (s, e) =>
                    {
                        if (string.IsNullOrWhiteSpace(FilterNumApartText))
                        {
                            e.Accepted = true;
                            return;
                        }
                        if (e.Item is SelectableParameterModel item)
                        {
                            e.Accepted = item.Name.IndexOf(FilterNumApartText, StringComparison.OrdinalIgnoreCase) >= 0;
                        }
                        else
                        {
                            e.Accepted = false;
                        }
                    };
                    _numApartParameters.View.MoveCurrentToPosition(-1);
                }
                return _numApartParameters;
            }
        }
        public CollectionViewSource FilteredNamesCaseParameters
        {
            get
            {
                if (_filteredNamesCaseParameters == null)
                {
                    _filteredNamesCaseParameters = new CollectionViewSource { Source = NamesCaseParameters };
                    _filteredNamesCaseParameters.Filter += (s, e) =>
                    {
                        if (string.IsNullOrWhiteSpace(FilterNamesCaseText))
                        {
                            e.Accepted = true;
                            return;
                        }
                        if (e.Item is SelectableParameterModel item)
                        {
                            e.Accepted = item.Name.IndexOf(FilterNamesCaseText, StringComparison.OrdinalIgnoreCase) >= 0;
                        }
                        else
                        {
                            e.Accepted = false;
                        }
                    };
                    _filteredNamesCaseParameters.View.MoveCurrentToPosition(-1);
                }
                return _filteredNamesCaseParameters;
            }
        }
        public CollectionViewSource FilteredNamesSectionParameters 
        {
            get
            {
                if (_filteredNamesSectionParameters == null)
                {
                    _filteredNamesSectionParameters = new CollectionViewSource { Source = NamesSectionParameters };
                    _filteredNamesSectionParameters.Filter += (s, e) =>
                    {
                        if (string.IsNullOrWhiteSpace(FilterNamesSectionText))
                        {
                            e.Accepted = true;
                            return;
                        }
                        if (e.Item is SelectableParameterModel item)
                        {
                            e.Accepted = item.Name.IndexOf(FilterNamesSectionText, StringComparison.OrdinalIgnoreCase) >= 0;
                        }
                        else
                        {
                            e.Accepted = false;
                        }
                        _filteredNamesSectionParameters.View.MoveCurrentToPosition(-1);
                    };
                }
                return _filteredNamesSectionParameters;
            }
        }
        public CollectionViewSource FilteredNamesFloorParameters
        {
            get
            {
                if (_filteredNamesFloorParameters == null)
                {
                    _filteredNamesFloorParameters = new CollectionViewSource { Source = NamesFloorParameters };
                    _filteredNamesFloorParameters.Filter += (s, e) =>
                    {
                        if (string.IsNullOrWhiteSpace(FilterNamesFloorText))
                        {
                            e.Accepted = true;
                            return;
                        }
                        if (e.Item is SelectableParameterModel item)
                        {
                            e.Accepted = item.Name.IndexOf(FilterNamesFloorText, StringComparison.OrdinalIgnoreCase) >= 0;
                        }
                        else
                        {
                            e.Accepted = false;
                        }
                    };
                    _filteredNamesFloorParameters.View.MoveCurrentToPosition(-1);
                }
                return _filteredNamesFloorParameters;
            }
        }
        public CollectionViewSource FilteredNameProjectParameters
        {
            get
            {
                if (_filteredNameProjectParameters == null)
                {
                    _filteredNameProjectParameters = new CollectionViewSource { Source = NameProjectParameters };
                    _filteredNameProjectParameters.Filter += (s, e) =>
                    {
                        if (string.IsNullOrWhiteSpace(FilterNameProjectText))
                        {
                            e.Accepted = true;
                            return;
                        }
                        if (e.Item is SelectableParameterModel item)
                        {
                            e.Accepted = item.Name.IndexOf(FilterNameProjectText, StringComparison.OrdinalIgnoreCase) >= 0;
                        }
                        else
                        {
                            e.Accepted = false;
                        }
                    };
                    _filteredNameProjectParameters.View.MoveCurrentToPosition(-1);
                }
                return _filteredNameProjectParameters;
            }
        }
        public CollectionViewSource FilteredTypesLegend
        {
            get
            {
                if (_filteredTypesLegend == null)
                {
                    _filteredTypesLegend = new CollectionViewSource { Source = TypesLegend };
                    _filteredTypesLegend.Filter += (s, e) =>
                    {
                        if (string.IsNullOrWhiteSpace(FilterTypesLegendText))
                        {
                            e.Accepted = true;
                            return;
                        }
                        if (e.Item is View item)
                        {
                            e.Accepted = item.Name.IndexOf(FilterTypesLegendText, StringComparison.OrdinalIgnoreCase) >= 0;
                        }
                        else
                        {
                            e.Accepted = false;
                        }
                    };
                    _filteredTypesLegend.View.MoveCurrentToPosition(-1);
                }
                return _filteredTypesLegend;
            }
        }
        public CollectionViewSource FilteredTypesSquare
        {
            get
            {
                if (_filteredTypesSquare == null)
                {
                    _filteredTypesSquare = new CollectionViewSource { Source = TypesSquare };
                    _filteredTypesSquare.Filter += (s, e) =>
                    {
                        if (string.IsNullOrWhiteSpace(FilterTypesSquareText))
                        {
                            e.Accepted = true;
                            return;
                        }
                        if (e.Item is SelectableFamilyModel item)
                        {
                            e.Accepted = item.Name.IndexOf(FilterTypesSquareText, StringComparison.OrdinalIgnoreCase) >= 0;
                        }
                        else
                        {
                            e.Accepted = false;
                        }
                    };
                    _filteredTypesSquare.View.MoveCurrentToPosition(-1);
                }
                return _filteredTypesSquare;
            }
        }
        public CollectionViewSource FilteredСolumnParameters
        {
            get
            {
                if (_filteredСolumnParameters == null)
                {
                    _filteredСolumnParameters = new CollectionViewSource { Source = СolumnParameters };
                    _filteredСolumnParameters.Filter += (s, e) =>
                    {
                        if (string.IsNullOrWhiteSpace(FilterСolumnText))
                        {
                            e.Accepted = true;
                            return;
                        }
                        if (e.Item is Parameter item)
                        {
                            e.Accepted = item.Definition.Name.IndexOf(FilterСolumnText, StringComparison.OrdinalIgnoreCase) >= 0;
                        }
                        else
                        {
                            e.Accepted = false;
                        }
                    };
                    _filteredСolumnParameters.View.MoveCurrentToPosition(-1);
                }
                return _filteredСolumnParameters;
            }
        }
        public CollectionViewSource FilteredTypeApartmentParameters
        {
            get
            {
                if (_filteredTypeApartmentParameters == null)
                {
                    _filteredTypeApartmentParameters = new CollectionViewSource { Source = TypeApartmentParameters };
                    _filteredTypeApartmentParameters.Filter += (s, e) =>
                    {
                        if (string.IsNullOrWhiteSpace(FilterTypeApartmentText))
                        {
                            e.Accepted = true;
                            return;
                        }
                        if (e.Item is SelectableParameterModel item)
                        {
                            e.Accepted = item.Name.IndexOf(FilterTypeApartmentText, StringComparison.OrdinalIgnoreCase) >= 0;
                        }
                        else
                        {
                            e.Accepted = false;
                        }
                    };
                    _filteredTypeApartmentParameters.View.MoveCurrentToPosition(-1);
                }
                return _filteredTypeApartmentParameters;
            }
        }

        public UIApplication uiApp;
        public UIDocument uidoc;
        public Autodesk.Revit.ApplicationServices.Application app;
        public Document doc;
        public FilteredElementCollector roomCollector;

        private ExternalEvent _externalEvent;
        private ExternalEventHandler _externalEventHandler;
    }
}