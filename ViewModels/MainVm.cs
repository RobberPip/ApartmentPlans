using ApartmentPlans.Domain;
using ApartmentPlans.Resources;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ApartmentPlans.ViewModels
{
    public partial class MainVm : INotifyPropertyChanged
    {
        public MainVm(UIApplication application)
        {
            

            uiApp = application;
            uidoc = uiApp.ActiveUIDocument;
            app = uiApp.Application;
            doc = uidoc.Document;
            LoadData();
            _externalEventHandler = new ExternalEventHandler();
            _externalEvent = ExternalEvent.Create(_externalEventHandler);
        }

        private JsonSerializerSettings settingsJSON = new JsonSerializerSettings
        {
            ContractResolver = new IncludeOnlyContractResolver(),
            Formatting = Formatting.Indented
        };
        private void LoadData()
        {
            IEnumerable<ViewPlan> views = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewPlan))
                    .Cast<ViewPlan>()
                    .Where(plan => !plan.IsTemplate);
            IEnumerable<ViewSheet> viewsSheet = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .Where(plan => !plan.IsTemplate);
            foreach (var view in views)
            {
                WorkViewModel workView = new WorkViewModel() { View = view };
                Views.Add(workView);
            }
            _tempViews = new ObservableCollection<WorkViewModel>(Views);
            foreach (var view in Views)
            {
                view.PropertyChanged += View_PropertyChanged;
            }

            foreach (var view in viewsSheet)
            {
                WorkViewModel workView = new WorkViewModel() { View = view };
                ViewsSheet.Add(workView);
            }
            _tempViewsSheet = new ObservableCollection<WorkViewModel>(ViewsSheet);
            foreach (var view in ViewsSheet)
            {
                view.PropertyChanged += View_PropertyChanged;
            }

            roomCollector = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Rooms);

            var doorTypeCollector = new FilteredElementCollector(doc)
            .OfClass(typeof(FamilySymbol))
            .OfCategory(BuiltInCategory.OST_Doors)
            .Cast<FamilySymbol>()
            .ToList();

            var wallTypeCollector = new FilteredElementCollector(doc)
            .OfClass(typeof(WallType))
            .Cast<WallType>();

            var linkCollector = new FilteredElementCollector(doc)
            .OfClass(typeof(RevitLinkType))
            .Cast<RevitLinkType>();

            var columnTypeCollector = new FilteredElementCollector(doc)
            .OfClass(typeof(FamilySymbol))
            .OfCategory(BuiltInCategory.OST_StructuralColumns)
            .Cast<FamilySymbol>()
            .ToList();

            var viewsLegendsCollector = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Views)
            .Cast<Autodesk.Revit.DB.View>()
            .Where(q => q.ViewType == ViewType.Legend);

            var squareCollector = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_RoomTags)
            .WhereElementIsElementType()
            .Cast<RoomTagType>();

            var room = roomCollector.FirstOrDefault();
            var allParameters = GetAllGeneralParameters(room);
            var listParams = new ObservableCollection<SelectableParameterModel>(GetSelectableParametersWithUniqueNames(allParameters));
            var UniqueParameters = new ObservableCollection<SelectableParameterModel>(listParams);

            // Создаем новый параметр
            var newParameter = new SelectableParameterModel { Name = "Адрес проекта" };

            // Добавляем новый параметр, если его еще нет в списке
            if (UniqueParameters.FirstOrDefault(param => param.Name == newParameter.Name) == null)
            {
                UniqueParameters.Add(newParameter);
            }

            ItemsSchedule = new ObservableCollection<RatioObjectModel<SelectableParameterModel>>
            {
                new RatioObjectModel<SelectableParameterModel> { Name = "Строительный адрес", ObjectParameters = UniqueParameters },
                new RatioObjectModel<SelectableParameterModel> { Name = "Тип объекта", ObjectParameters = listParams },
                new RatioObjectModel<SelectableParameterModel> { Name = "Номер корпуса (дома)", ObjectParameters = listParams },
                new RatioObjectModel<SelectableParameterModel> { Name = "Номер п/п секции (размещения)", ObjectParameters = listParams },
                new RatioObjectModel<SelectableParameterModel> { Name = "Этаж (ряд)", ObjectParameters = listParams },
                new RatioObjectModel<SelectableParameterModel> { Name = "Комнат", ObjectParameters = listParams },
                new RatioObjectModel<SelectableParameterModel> { Name = "Условный номер", ObjectParameters = listParams },
                new RatioObjectModel<SelectableParameterModel> { Name = "Площадь по проекту (без летних)", ObjectParameters = listParams },
                new RatioObjectModel<SelectableParameterModel> { Name = "Площадь по проекту общая (расчетная)", ObjectParameters = listParams },
                new RatioObjectModel<SelectableParameterModel> { Name = "Площадь по проекту жилая", ObjectParameters = listParams },
                new RatioObjectModel<SelectableParameterModel> { Name = "Высота потолков", ObjectParameters = listParams }
            };

            var distinctRoomNames = roomCollector.Select(q => q.get_Parameter(BuiltInParameter.ROOM_NAME).AsValueString())
                .Distinct()
                .ToList();
            var marksInProject = new FilteredElementCollector(doc)
               .OfCategory(BuiltInCategory.OST_GenericAnnotation)
               .OfClass(typeof(FamilySymbol))
               .Cast<FamilySymbol>()
               .Where(x => x is AnnotationSymbolType)
               .Cast<AnnotationSymbolType>()
               .ToList();
            var ItemsMarkTemp = new ObservableCollection<RatioObjectModel<MultiSelectModel>>
            {
                new RatioObjectModel<MultiSelectModel> { Name = ValidNamesResources.MarkBathRoom},
                new RatioObjectModel<MultiSelectModel> { Name = ValidNamesResources.MarkBedRoom},
                new RatioObjectModel<MultiSelectModel> { Name = ValidNamesResources.MarkChildishRoom},
                new RatioObjectModel<MultiSelectModel> { Name = ValidNamesResources.MarkDressingRoom },
                new RatioObjectModel<MultiSelectModel> { Name = ValidNamesResources.MarkKitchenRoom },
                new RatioObjectModel<MultiSelectModel> { Name = ValidNamesResources.MarkLaundryRoom },
                new RatioObjectModel<MultiSelectModel> { Name = ValidNamesResources.MarkLivingRoom },
                new RatioObjectModel<MultiSelectModel> { Name = ValidNamesResources.MarkOfficeRoom },
                new RatioObjectModel<MultiSelectModel> { Name = ValidNamesResources.MarkSaunaRoom },
                new RatioObjectModel<MultiSelectModel> { Name = ValidNamesResources.MarkShowerRoom },
                new RatioObjectModel<MultiSelectModel> { Name = ValidNamesResources.MarkSinkRoom },
                new RatioObjectModel<MultiSelectModel> { Name = ValidNamesResources.MarkStoreRoom },
                new RatioObjectModel<MultiSelectModel> { Name = ValidNamesResources.MarkTerraceRoom }
            };
            var markNamesSet = new HashSet<string>(marksInProject.Select(m => m.Name));
            ItemsMark = new ObservableCollection<RatioObjectModel<MultiSelectModel>>(
                ItemsMarkTemp.Where(item => markNamesSet.Any(markName => item.Name.Contains(markName)))
            );

            // Предварительно создаем коллекцию уникальных имен комнат
            var roomModels = distinctRoomNames.Select(name => new MultiSelectModel { Name = name }).ToList();

            // Добавляем каждый объект roomModels к ObjectParameters каждого элемента в ItemsMark
            foreach (var itemMark in ItemsMark)
            {
                foreach (var roomName in distinctRoomNames)
                {
                    itemMark.ObjectParameters.Add(new MultiSelectModel() { Name = roomName });
                }
            }
            SortParameters = listParams;
            NameProjectParameters = listParams;
            NamesCaseParameters = listParams;
            NamesSectionParameters = listParams;
            NamesFloorParameters = listParams;
            NumApartParameters = listParams;
            TypeApartmentParameters = listParams;
            TypesSquare = new ObservableCollection<SelectableFamilyModel>(
            squareCollector.Select(symbol => new SelectableFamilyModel { element = symbol, Name = symbol.FamilyName + "_" + symbol.Name }));
            СolumnParameters = new ObservableCollection<Parameter>(GetAllParameters(columnTypeCollector.FirstOrDefault()));
            TypesLegend = new ObservableCollection<Autodesk.Revit.DB.View>(viewsLegendsCollector);
            DoorTypes = new ObservableCollection<SelectableFamilyModel>(
            doorTypeCollector.Select(symbol => new SelectableFamilyModel { element = symbol }));
            WallTypes = new ObservableCollection<SelectableFamilyModel>(
            wallTypeCollector.Select(symbol => new SelectableFamilyModel { element = symbol }));
            WallFacadesTypes = new ObservableCollection<SelectableFamilyModel>(
            wallTypeCollector.Select(symbol => new SelectableFamilyModel { element = symbol }));
            FilesFacde = new ObservableCollection<SelectableFamilyModel>(
            linkCollector.Select(symbol => new SelectableFamilyModel { element = symbol }));
            IEnumerable<ViewPlan> templates = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewPlan))
                .Cast<ViewPlan>()
                .Where(plan => plan.IsTemplate);

            var validNames = new[]
            {
                ValidNamesResources.TypePremium,
                ValidNamesResources.TypeComfort,
                ValidNamesResources.TypeBusiness
            };

            TemplateList = new ObservableCollection<MultiSelectModel>(
                validNames.Select(name => new MultiSelectModel
                {
                    Name = name,
                    IsEnabled = templates.Any(v => v.Name == name)
                })
            );

        }
        public void UpdateValidValuesSortParameters(string parameter)
        {
            List<string> valueParameters = roomCollector.Select(dt => dt.LookupParameter(parameter).AsString()).Distinct().ToList();
            if (valueParameters.Count != 0)
            {
                ValueSortParameters.Clear();
                foreach (string value in valueParameters)
                {
                    ValueSortParameters.Add(value);
                }
            }
        }
        private IEnumerable<SelectableParameterModel> GetSelectableParametersWithUniqueNames(IEnumerable<Parameter> parameters)
        {
            return parameters
                .GroupBy(param => param.Definition.Name)
                .SelectMany(group =>
                {
                    if (group.Count() > 1)
                    {
                        return group.Select(param => new SelectableParameterModel
                        {
                            parameter = param,
                            Name = $"{param.Definition.Name}_{param.Id}"
                        });
                    }
                    else
                    {
                        var param = group.First();
                        return new[] { new SelectableParameterModel { parameter = param } };
                    }
                });
        }
        private IList<Parameter> GetAllGeneralParameters(Element element)
        {
            IList<Parameter> sharedParameters = new List<Parameter>();
            if (element != null)
            {
                foreach (Parameter param in element.Parameters)
                {
                    if (param.IsShared)
                    {
                        sharedParameters.Add(param);
                    }
                }
            }
            return sharedParameters;
        }
        private IList<Parameter> GetAllParameters(Element element)
        {
            IList<Parameter> parameters = element != null ? element.Parameters.Cast<Parameter>().ToList() : new List<Parameter>();
            return parameters;
        }
        public void StartAction()
        {
            CommandPlans command = new CommandPlans(uiApp);
            PluginSettingsModel settings = CreatePluginSettingsModel();
            bool hasDuplicates = settings.ItemsSchedule
            .GroupBy(x => x.SelectedObjectParameter)
            .Any(g => g.Count() > 1);

            if (hasDuplicates)
            {
                // Если есть дубликаты, выходим из метода
                MessageBox.Show("Параметры в спецификации потвторяются!");
                return; 
            }
            var views = new List<WorkViewModel>(Views.Where(q => q.IsSelected));
            var selectedTempViews = _tempViews.Where(q => q.IsSelected);

            views = views.Union(selectedTempViews).ToList();
            command.CreatePlanApartment(views, settings);
            

        }
        private PluginSettingsModel CreatePluginSettingsModel()
        {
            string _nameProject = FilterNameProjectText;
            if (SelectedNameProjectParameter != null && SelectedNameProjectParameter.Name == FilterNameProjectText)
            {
                Room room = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Rooms).Cast<Room>().FirstOrDefault();
                Parameter param = room.LookupParameter(SelectedNameProjectParameter.parameter.Definition.Name);
                _nameProject = param.AsValueString();
            };
            return new PluginSettingsModel()
            {
                ItemsSchedule = ItemsSchedule != null ? new List<RatioObjectModel<SelectableParameterModel>>(ItemsSchedule.Where(q => q.SelectedObjectParameter != null)) :
                new List<RatioObjectModel<SelectableParameterModel>>(),
                ItemsMark = ItemsMark != null ? ItemsMark.Where(item => item.ObjectParameters != null && item.ObjectParameters.OfType<MultiSelectModel>()
                .Any(parameter => parameter.IsChecked)).ToList() : new List<RatioObjectModel<MultiSelectModel>>(),
                SortParameter = SelectedSortParameter?.parameter ?? null,
                ValueSortParameter = SelectedValueSortParameter ?? null,
                NumApartParameter = SelectedNumApartParameter?.parameter ?? null,
                WallTypes = WallTypes != null ? new List<SelectableFamilyModel>(WallTypes.Where(q => q.IsSelected == true)) : new List<SelectableFamilyModel>(),
                DoorTypes = DoorTypes != null ? new List<SelectableFamilyModel>(DoorTypes.Where(q => q.IsSelected == true)) : new List<SelectableFamilyModel>(),
                LinkTypes = FilesFacde != null ? new List<SelectableFamilyModel>(FilesFacde.Where(q => q.IsSelected == true)) : new List<SelectableFamilyModel>(),
                WallFacadesTypes = WallFacadesTypes != null ? new List<SelectableFamilyModel>(WallFacadesTypes.Where(q => q.IsSelected == true)) : new List<SelectableFamilyModel>(),
                ActiveTemplates = TemplateList != null ? new List<MultiSelectModel>(TemplateList.Where(q => q.IsChecked == true)) : new List<MultiSelectModel>(),
                IsExport = IsExport,
                AutoFacade = AutoFacade,
                AutoСolumns = AutoСolumns,
                IsRotatePlans = IsRotatePlans,
                IsRotateFloors = IsRotateFloors,
                TypeLegend = SelectedTypeLegend ?? null,
                ColumnParameter = SelectedСolumnParameter ?? null,
                TypeApartment = SelectedTypeApartmentParameter?.parameter ?? null,
                TypesSquareTag = SelectedTypeSquare ?? null,
                ColorFill = SelectedColor,
                WallFacadesOffest = int.TryParse(WallFacadesOffest, out var parsedWallFacadesOffest) ? parsedWallFacadesOffest : 0,
                ExportSettings = new ExportSettingsModel()
                {
                    OffsetLink = OffsetLink,
                    OffsetLinkAdvance = OffsetLinkAdvance,
                    NameCaseParameter = SelectedNameCaseParameter?.parameter ?? null,
                    NameFloorParameter = SelectedNameFloorParameter?.parameter ?? null,
                    NameSectionParameter = SelectedNameSectionParameter?.parameter ?? null,
                    NameProject = _nameProject ?? string.Empty,
                    PathToSave = !string.IsNullOrEmpty(correctedPath) ? correctedPath : _pathToSave ?? string.Empty,
                    IsСuttingBound = IsСuttingBound,
                }
            };
        }
        public void ExportAction()
        {
            string _nameProject = FilterNameProjectText;
            if (SelectedNameProjectParameter != null && SelectedNameProjectParameter.Name == FilterNameProjectText)
            {
                Room room = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Rooms).Cast<Room>().FirstOrDefault();
                Parameter param = room.LookupParameter(SelectedNameProjectParameter.parameter.Definition.Name);
                _nameProject = param.AsString();
            };
            PluginSettingsModel settings = new PluginSettingsModel()
            {
                ItemsSchedule = ItemsSchedule != null ? new List<RatioObjectModel<SelectableParameterModel>>(ItemsSchedule.Where(q => q.SelectedObjectParameter != null)) :
                new List<RatioObjectModel<SelectableParameterModel>>(),
                ExportSettings = new ExportSettingsModel()
                {
                    IsСuttingBound = IsСuttingBound,
                    OffsetLink = OffsetLink,
                    OffsetLinkAdvance = OffsetLinkAdvance,
                    NameCaseParameter = SelectedNameCaseParameter?.parameter ?? null,
                    NameFloorParameter = SelectedNameFloorParameter?.parameter ?? null,
                    NameSectionParameter = SelectedNameSectionParameter?.parameter ?? null,
                    NameProject = _nameProject ?? string.Empty,
                    PathToSave = !string.IsNullOrEmpty(correctedPath) ? correctedPath : _pathToSave ?? string.Empty,
                }
            };
            var views = new List<WorkViewModel>(ViewsSheet.Where(q => q.IsSelected));
            var selectedTempViews = _tempViewsSheet.Where(q => q.IsSelected);

            views = views.Union(selectedTempViews).ToList();

            CommandExport.ExportSheetModule(doc, views, settings);

        }
        public RelayCommand StartCommand
        {
            get
            {
                return _startCommand ??
                  (_startCommand = new RelayCommand(obj =>
                  {
                      _externalEventHandler.SetAction(StartAction);
                      _externalEvent.Raise();
                      //StartAction();


                  },

                  canExecute: obj =>
                  {
                      bool parametersNotNull = new object[] { SelectedSortParameter, SelectedValueSortParameter, SelectedNumApartParameter }
                              .All(param => param != null);
                      var views = new List<WorkViewModel>(Views.Where(q => q.IsSelected));
                      foreach (var view in _tempViews.Where(q => q.IsSelected))
                      {
                          views.Add(view);
                      }
                      bool anyViewSelected = views.Any(v => v.IsSelected);
                      return parametersNotNull && anyViewSelected;
                  }));
            }
        }
        public RelayCommand СhoosePathToSaveCommand
        {
            get
            {
                return _choosePathToSaveCommand ??
                  (_choosePathToSaveCommand = new RelayCommand(obj =>
                  {
                      FolderBrowserDialog browser = new FolderBrowserDialog();
                      browser.ShowDialog();
                      string selectedPath = browser.SelectedPath;
                      correctedPath = selectedPath.Replace(@"\\", @"\");
                      PathToSave = "Путь: " + correctedPath;
                  }));
            }
        }
        public RelayCommand ExportCommand
        {
            get
            {
                return _exportCommand ??
                  (_exportCommand = new RelayCommand(obj =>
                  {
                      try
                      {
                           _externalEventHandler.SetAction(ExportAction);
                           _externalEvent.Raise();
                          //ExportAction();
                      }
                      catch
                      {
                          MessageBox.Show("Произошла ошибка с выгрузкой CSV и наименованием SVG файла!");
                      }
                  },
                  canExecute: obj =>
                  {
                      var views = new List<WorkViewModel>(ViewsSheet.Where(q => q.IsSelected));
                      foreach (var view in _tempViewsSheet.Where(q => q.IsSelected))
                      {
                          views.Add(view);
                      }
                      bool anyViewSelected = views.Any(v => v.IsSelected);
                      return anyViewSelected;
                  }));
            }
        }
        public RelayCommand SaveSettingsCommand
        {
            get
            {
                return _saveSettignsCommand ??
                  (_saveSettignsCommand = new RelayCommand(obj =>
                  {
                      try
                      {
                          PluginSettingsModelDTO settingsDTO = new PluginSettingsModelDTO(CreatePluginSettingsModel());
                          using (SaveFileDialog saveFileDialog = new SaveFileDialog())
                          {
                              saveFileDialog.Title = "Сохранить файл как"; // Заголовок окна
                              if (saveFileDialog.ShowDialog() == DialogResult.OK)
                              {
                                  string fileName = saveFileDialog.FileName;

                                  // Проверяем, если расширение уже присутствует
                                  if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                                  {
                                      fileName += ".json";
                                  }

                                  string json = JsonConvert.SerializeObject(settingsDTO, settingsJSON);
                                  File.WriteAllText(fileName, json);

                                  MessageBox.Show($"Файл сохранен: {fileName}");
                              }
                          }

                      }
                      catch
                      {
                          MessageBox.Show("Произошла ошибка с сохранением пресета!");
                      }
                  }));
            }
        }
        public RelayCommand UploadSettingsCommand
        {
            get
            {
                return _uploadSettignsCommand ??
                  (_uploadSettignsCommand = new RelayCommand(obj =>
                  {
                      try
                      {
                          using (OpenFileDialog openFileDialog = new OpenFileDialog())
                          {
                              openFileDialog.Filter = "JSON Files (*.json)|*.json";
                              openFileDialog.Title = "Загрузить файл настроек"; // Заголовок окна
                              if (openFileDialog.ShowDialog() == DialogResult.OK)
                              {
                                  string configPath = openFileDialog.FileName;
                                  string jsonFromFile = File.ReadAllText(configPath);
                                  PluginSettingsModelDTO settingsDTO = JsonConvert.DeserializeObject<PluginSettingsModelDTO>(jsonFromFile, settingsJSON);
                                  CreatePluginPreset(settingsDTO);
                                  MessageBox.Show($"Файл настроек загружен!");
                              }
                          }
                      }
                      catch
                      {
                          MessageBox.Show("Произошла ошибка с загрузкой пресета!");
                      }
                  }));
            }
        }
        private void CreatePluginPreset(PluginSettingsModelDTO settingsDTO)
        {
            SelectedSortParameter = SortParameters.FirstOrDefault(q => q.Name == settingsDTO.SortParameterDTO);
            SelectedValueSortParameter = ValueSortParameters.FirstOrDefault(q => q == settingsDTO.ValueSortParameterDTO);
            SelectedNumApartParameter = NumApartParameters.FirstOrDefault(q => q.Name == settingsDTO.NumApartParameterDTO);
            WallTypes.Where(wallType => settingsDTO.WallTypesDTO.Any(dto => dto.Name == wallType.Name)).ToList().ForEach(wallType => wallType.IsSelected = true);
            LabelWallCountText = "Выбрано: " + WallTypes.Count(q => q.IsSelected == true).ToString();
            DoorTypes.Where(doorType => settingsDTO.DoorTypesDTO.Any(dto => dto.Name == doorType.Name)).ToList().ForEach(doorType => doorType.IsSelected = true);
            LabelDoorCountText = "Выбрано: " + DoorTypes.Count(q => q.IsSelected == true).ToString();
            FilesFacde.Where(linkType => settingsDTO.LinkTypesDTO.Any(dto => dto.Name == linkType.Name)).ToList().ForEach(linkType => linkType.IsSelected = true);
            LabelLinkCountText = "Выбрано: " + FilesFacde.Count(q => q.IsSelected == true).ToString();
            WallFacadesTypes.Where(q => settingsDTO.WallFacadesTypesDTO.Any(dto => dto.Name == q.Name)).ToList().ForEach(q => q.IsSelected = true);
            LabelWallFacadesCountText = "Выбрано: " + WallFacadesTypes.Count(q => q.IsSelected == true).ToString();
            SelectedColor = settingsDTO.ColorFillDTO;
            IsExport = settingsDTO.IsExportDTO;
            AutoСolumns = settingsDTO.AutoСolumnsDTO;
            AutoFacade = settingsDTO.AutoFacadeDTO;
            IsRotatePlans = settingsDTO.IsRotatePlansDTO;
            IsRotateFloors = settingsDTO.IsRotateFloorsDTO;
            TemplateList.Where(q => settingsDTO.ActiveTemplatesDTO.Any(dto => dto.Name == q.Name&& q.IsEnabled==true)).ToList().ForEach(q => q.IsChecked = true);
            SelectedTypeLegend = TypesLegend.FirstOrDefault(q => q.Name == settingsDTO.TypeLegendDTO);
            SelectedTypeSquare = TypesSquare.FirstOrDefault(q => q.Name == settingsDTO.TypesSquareTagDTO);
            SelectedСolumnParameter = СolumnParameters.FirstOrDefault(q => q.Definition.Name == settingsDTO.ColumnParameterDTO);
            SelectedTypeApartmentParameter = TypeApartmentParameters.FirstOrDefault(q => q.Name == settingsDTO.TypeApartmentDTO);
            WallFacadesOffest = settingsDTO.WallFacadesOffestDTO.ToString();
            ItemsSchedule
              .Where(item => settingsDTO.ItemsScheduleDTO
                  .Any(dto => dto.Name == item.Name &&
                              item.ObjectParameters
                                  .Any(p => p.Name == dto.SelectedObjectParameter.Name)))
              .ToList()
              .ForEach(item => item.SelectedObjectParameter =
                  item.ObjectParameters
                      .FirstOrDefault(p => p.Name == settingsDTO.ItemsScheduleDTO
                          .FirstOrDefault(dto => dto.Name == item.Name)?
                          .SelectedObjectParameter.Name));
            if (settingsDTO.ItemsMarkDTO.Count > 0)
            {
                
                foreach (var item in ItemsMark)
                {
                    var dto = settingsDTO.ItemsMarkDTO.FirstOrDefault(x => x.Name == item.Name);
                    if (dto != null)
                    {
                        foreach (var parameter in item.ObjectParameters)
                        {
                            if (dto.CheckedObjects.Any(w => w.Name == parameter.Name))
                            {
                                parameter.IsChecked = true;
                                item.HaveValueChecked = "✔️";
                            }
                        }
                    }
                }



            }
            SelectedNameCaseParameter = NamesCaseParameters.FirstOrDefault(q => q.Name == settingsDTO.ExportSettingsDTO.NameCaseParameterDTO);
            SelectedNameSectionParameter = NamesSectionParameters.FirstOrDefault(q => q.Name == settingsDTO.ExportSettingsDTO.NameSectionParameterDTO);
            SelectedNameFloorParameter = NamesFloorParameters.FirstOrDefault(q => q.Name == settingsDTO.ExportSettingsDTO.NameFloorParameterDTO);
            PathToSave = settingsDTO.ExportSettingsDTO.PathToSaveDTO;
            OffsetLink = settingsDTO.ExportSettingsDTO.OffsetLinkDTO;
            OffsetLinkAdvance = settingsDTO.ExportSettingsDTO.OffsetLinkAdvanceDTO;
            IsСuttingBound = settingsDTO.ExportSettingsDTO.IsСuttingBoundDTO;
            var _nameProject = NameProjectParameters.FirstOrDefault(q => q.Name == settingsDTO.ExportSettingsDTO.NameProjectDTO);
            if (_nameProject == null)
            {
                FilterNameProjectText = settingsDTO.ExportSettingsDTO.NameProjectDTO;
            }
            else
            {
                SelectedNameProjectParameter = _nameProject;
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        private void View_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(WorkViewModel.IsSelected))
            {
                SelectedItemsCount = Views.Count(v => v.IsSelected);
                if (SelectedItemsCount == 0) {
                    SelectedItemsCount = ViewsSheet.Count(v => v.IsSelected);
                }
            }
        }
        private void SetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            if (!EqualityComparer<T>.Default.Equals(field, value))
            {
                field = value;
                OnPropertyChanged(propertyName);
            }
        }

    }
}
