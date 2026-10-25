using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.IO;
using System.Text;
using System.Windows.Data;
using CADLookup.Core.Contracts;
using CADLookup.Core.Domain;
using CADLookup.Infrastructure.Configuration;
using CADLookup.UI.Localization;
using CADLookup.UI.Mvvm;
using CADLookup.UI.Search;
using CADLookup.Infrastructure.Diagnostics;

namespace CADLookup.UI.ViewModels
{
    public sealed class MainViewModel : ObservableObject
    {
        private readonly ICadHostAdapter _host;
        private readonly IObjectInspector _inspector;
        private readonly Action? _requestSelection;
        private readonly Action<CadObjectRef>? _requestInspection;
        private readonly Action<string>? _requestHandleLookup;
        private readonly Action? _requestSnoopDatabase;
        private readonly Action<CadObjectRef>? _requestZoomTo;
        private readonly Action<CadObjectRef>? _requestSelectInDrawing;
        private readonly Action<string>? _requestDatabaseSearch;
        private readonly Action<CadObjectRef?>? _requestTransientHighlight;
        private string _searchText = string.Empty;
        private string _statusText = string.Empty;
        private string _handleQuery = string.Empty;
        private string _databaseSearchQuery = string.Empty;
        private bool _highlightEnabled;
        private bool _groupProperties;
        private string _selectedPropertySource = string.Empty;
        private string _objectFilter = string.Empty;
        private string _selectedGroupBy;
        private string _selectedStatusFilter = "AllStatuses";
        private string _theme;
        private CadObjectSnapshot? _snapshot;
        private PropertyRowViewModel? _selectedProperty;
        private int _historyIndex = -1;

        public event Action<string>? ThemeChanged;
        public event Action<string>? LanguageChanged;
        public event Action? SearchDialogRequested;

        public MainViewModel(ICadHostAdapter host, IObjectInspector inspector, LocalizationService localization,
            Action? requestSelection = null, Action<CadObjectRef>? requestInspection = null,
            Action<string>? requestHandleLookup = null, Action? requestSnoopDatabase = null,
            Action<CadObjectRef>? requestZoomTo = null, Action<CadObjectRef>? requestSelectInDrawing = null,
            Action<string>? requestDatabaseSearch = null, Action<CadObjectRef?>? requestTransientHighlight = null)
        {
            _host = host; _inspector = inspector; Localization = localization;
            _requestSelection = requestSelection;
            _requestInspection = requestInspection;
            _requestHandleLookup = requestHandleLookup;
            _requestSnoopDatabase = requestSnoopDatabase;
            _requestZoomTo = requestZoomTo;
            _requestSelectInDrawing = requestSelectInDrawing;
            _requestDatabaseSearch = requestDatabaseSearch;
            _requestTransientHighlight = requestTransientHighlight;
            _theme = UserSettingsStore.LoadTheme();
            _selectedGroupBy = UserSettingsStore.LoadGroupBy();

            Objects = new ObservableCollection<CadObjectRef>();
            VisibleObjects = new ObservableCollection<CadObjectRef>();
            ObjectTreeRoots = new ObservableCollection<ObjectTreeNode>();
            History = new ObservableCollection<CadObjectRef>();
            PropertySources = new ObservableCollection<string>();
            Properties = new ObservableCollection<PropertyRowViewModel>();
            Geometry = new ObservableCollection<GeometrySummary>();
            GroupByOptions = new ObservableCollection<string>();
            StatusFilterOptions = new ObservableCollection<string>();
            SearchResults = new ObservableCollection<CadObjectRef>();
            PropertiesView = new CollectionViewSource { Source = Properties }.View;
            _groupProperties = UserSettingsStore.LoadPropertyGrouping();
            ApplyPropertyGrouping();

            SelectCommand = new RelayCommand(SelectObjects);
            SnoopDatabaseCommand = new RelayCommand(SnoopDatabase);
            RefreshCommand = new RelayCommand(Refresh);
            LanguageCommand = new RelayCommand(ToggleLanguage);
            ToggleThemeCommand = new RelayCommand(ToggleTheme);
            ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty, () => !string.IsNullOrEmpty(SearchText));
            ExpandAllTreeCommand = new RelayCommand(() => SetTreeExpanded(true));
            CollapseAllTreeCommand = new RelayCommand(() => SetTreeExpanded(false));
            CopyCommand = new RelayCommand(CopyValue, () => SelectedProperty != null);
            CopyPathCommand = new RelayCommand(CopyPath, () => SelectedProperty?.Reference != null);
            CopyPropertyValueCommand = new RelayCommand(parameter => CopyPropertyValue(parameter as PropertyRowViewModel),
                parameter => parameter is PropertyRowViewModel);
            CopyPropertyPathCommand = new RelayCommand(parameter => CopyPropertyPath(parameter as PropertyRowViewModel),
                parameter => (parameter as PropertyRowViewModel)?.Reference != null);
            OpenPropertyCommand = new RelayCommand(parameter => OpenReference(parameter),
                parameter => ResolveReferenceFromParameter(parameter) != null);
            ExportCommand = new RelayCommand(Export, () => _snapshot != null);
            LookupHandleCommand = new RelayCommand(LookupHandle, () => !string.IsNullOrWhiteSpace(HandleQuery));
            BackCommand = new RelayCommand(() => NavigateHistory(_historyIndex - 1), () => _historyIndex > 0);
            ForwardCommand = new RelayCommand(() => NavigateHistory(_historyIndex + 1), () => _historyIndex >= 0 && _historyIndex + 1 < History.Count);
            NavigateHistoryCommand = new RelayCommand(parameter =>
            {
                if (parameter is CadObjectRef reference) NavigateToHistoryItem(reference);
            }, parameter => parameter is CadObjectRef);
            ZoomToObjectCommand = new RelayCommand(ZoomToDrawingTarget, parameter => CanTargetDrawingObject(parameter));
            SelectInDrawingCommand = new RelayCommand(SelectDrawingTarget, parameter => CanTargetDrawingObject(parameter));
            ToggleHighlightCommand = new RelayCommand(ToggleTransientHighlight, () => SupportsTransientHighlight);
            OpenSearchDialogCommand = new RelayCommand(RequestOpenSearchDialog, () => SupportsDatabaseSearch);
            SearchExecuteCommand = new RelayCommand(() => SearchDatabase(), () => SupportsDatabaseSearch && !string.IsNullOrWhiteSpace(DatabaseSearchQuery));
            ToggleGroupPropertiesCommand = new RelayCommand(ToggleGroupProperties);
            CopyHandleCommand = new RelayCommand(parameter => CopyHandle(parameter), parameter => ResolveHandleCopyTarget(parameter) != null);

            RefreshGroupByOptions();
            RefreshStatusOptions();
            LoadSelection(false);
        }

        public LocalizationService Localization { get; }
        public ObservableCollection<CadObjectRef> Objects { get; }
        public ObservableCollection<CadObjectRef> VisibleObjects { get; }
        public ObservableCollection<ObjectTreeNode> ObjectTreeRoots { get; }
        public ObservableCollection<CadObjectRef> History { get; }
        public ObservableCollection<string> PropertySources { get; }
        public ObservableCollection<PropertyRowViewModel> Properties { get; }
        public ObservableCollection<GeometrySummary> Geometry { get; }
        public ObservableCollection<string> GroupByOptions { get; }
        public ObservableCollection<string> StatusFilterOptions { get; }
        public ObservableCollection<CadObjectRef> SearchResults { get; }
        public ICollectionView PropertiesView { get; }

        public bool HasProperties => Properties.Count > 0;
        public bool HasSearchText => !string.IsNullOrEmpty(SearchText);
        public bool HasSearchResults => SearchResults.Count > 0;
        public bool HasCurrentObject => _snapshot != null;
        public bool SupportsZoomToObject => _host.Host.Capabilities.HasFlag(CadHostCapabilities.ZoomToObject);
        public bool SupportsSelectInDrawing => _host.Host.Capabilities.HasFlag(CadHostCapabilities.SelectInDrawing);
        public bool SupportsDatabaseSearch => _host.Host.Capabilities.HasFlag(CadHostCapabilities.DatabaseSearch);
        public bool SupportsTransientHighlight => _host.Host.Capabilities.HasFlag(CadHostCapabilities.TransientHighlight);

        public bool GroupPropertiesEnabled
        {
            get => _groupProperties;
            private set
            {
                if (Set(ref _groupProperties, value))
                    Raise(nameof(GroupPropertiesIcon));
            }
        }

        public string GroupPropertiesIcon => _groupProperties ? "▦" : "☰";

        public bool HighlightEnabled
        {
            get => _highlightEnabled;
            private set
            {
                if (Set(ref _highlightEnabled, value))
                    Raise(nameof(HighlightIcon));
            }
        }

        public string HighlightIcon => _highlightEnabled ? "◉" : "◎";

        public RelayCommand SelectCommand { get; }
        public RelayCommand SnoopDatabaseCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand LanguageCommand { get; }
        public RelayCommand ToggleThemeCommand { get; }
        public RelayCommand ClearSearchCommand { get; }
        public RelayCommand ExpandAllTreeCommand { get; }
        public RelayCommand CollapseAllTreeCommand { get; }
        public RelayCommand CopyCommand { get; }
        public RelayCommand CopyPathCommand { get; }
        public RelayCommand OpenPropertyCommand { get; }
        public RelayCommand CopyPropertyValueCommand { get; }
        public RelayCommand CopyPropertyPathCommand { get; }
        public RelayCommand ExportCommand { get; }
        public RelayCommand LookupHandleCommand { get; }
        public RelayCommand BackCommand { get; }
        public RelayCommand ForwardCommand { get; }
        public RelayCommand NavigateHistoryCommand { get; }
        public RelayCommand ZoomToObjectCommand { get; }
        public RelayCommand SelectInDrawingCommand { get; }
        public RelayCommand ToggleHighlightCommand { get; }
        public RelayCommand OpenSearchDialogCommand { get; }
        public RelayCommand SearchExecuteCommand { get; }
        public RelayCommand ToggleGroupPropertiesCommand { get; }
        public RelayCommand CopyHandleCommand { get; }

        public string HostName => _host.Host.DisplayName;
        public string HostCapabilities => _host.Host.Capabilities.ToString();
        public string DocumentName => _host.GetActiveDocument()?.Name ?? Localization["NoDrawing"];

        public string Theme
        {
            get => _theme;
            set
            {
                if (Set(ref _theme, value))
                {
                    UserSettingsStore.SaveTheme(value);
                    Raise(nameof(ThemeIcon));
                    Raise(nameof(ThemeText));
                    ThemeChanged?.Invoke(value);
                }
            }
        }

        public string ThemeIcon => _theme == "Dark" ? "☼" : "☾";
        public string ThemeText => _theme == "Dark" ? Localization["Light"] : Localization["Dark"];

        public string SelectedGroupBy
        {
            get
            {
                if (_selectedGroupBy == "Layer") return Localization["GroupByLayer"];
                if (_selectedGroupBy == "Flat") return Localization["GroupByFlat"];
                return Localization["GroupByType"];
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                string mode;
                if (value == Localization["GroupByLayer"]) mode = "Layer";
                else if (value == Localization["GroupByFlat"]) mode = "Flat";
                else mode = "Type";

                if (Set(ref _selectedGroupBy, mode))
                {
                    UserSettingsStore.SaveGroupBy(mode);
                    RebuildObjectTree();
                }
            }
        }

        public string SelectedStatusFilter
        {
            get => Localization[_selectedStatusFilter];
            set
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                var key = value == Localization["StatusEvaluated"] ? "StatusEvaluated" :
                    value == Localization["StatusDeferred"] ? "StatusDeferred" :
                    value == Localization["StatusError"] ? "StatusError" : "AllStatuses";
                if (Set(ref _selectedStatusFilter, key))
                    ApplyFilter();
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (Set(ref _searchText, value))
                {
                    ClearSearchCommand.Refresh();
                    Raise(nameof(HasSearchText));
                    ApplyFilter();
                }
            }
        }

        public string ObjectFilter
        {
            get => _objectFilter;
            set
            {
                if (Set(ref _objectFilter, value))
                    ApplyObjectFilter();
            }
        }

        public string SelectedPropertySource
        {
            get => string.IsNullOrEmpty(_selectedPropertySource) ? Localization["AllSources"] : Localization.LocalizeSource(_selectedPropertySource);
            set
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                var key = string.IsNullOrEmpty(value) || value == Localization["AllSources"] ? string.Empty : Localization.GetSourceKey(value);
                if (Set(ref _selectedPropertySource, key)) ApplyFilter();
            }
        }

        public string HandleQuery
        {
            get => _handleQuery;
            set { if (Set(ref _handleQuery, value)) LookupHandleCommand.Refresh(); }
        }

        public string DatabaseSearchQuery
        {
            get => _databaseSearchQuery;
            set { if (Set(ref _databaseSearchQuery, value)) SearchExecuteCommand.Refresh(); }
        }

        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }
        public string Language => Localization.Language;
        public string ObjectSummary => _snapshot == null ? string.Empty : _snapshot.TypeName + "  |  " + _snapshot.Reference.Handle + "  |  " + _snapshot.Layer;
        public IReadOnlyList<GeometryFigure> CurrentFigures => _snapshot?.Figures ?? Array.Empty<GeometryFigure>();
        public string SelectedHandle => _snapshot?.Reference.Handle ?? "—";
        public string SelectedTypeName => _snapshot?.TypeName ?? Localization["NoObject"];
        public string SelectedLayer => _snapshot?.Layer ?? "—";
        public string SelectedState => _snapshot?.State ?? "—";
        public string SelectedDocumentId => _snapshot?.Reference.DocumentId ?? "—";
        public string SelectedNativeId => _snapshot?.Reference.NativeId ?? "—";
        public string SelectedReferencePath => string.IsNullOrEmpty(_snapshot?.Reference.MemberPath) ? Localization["RootObject"] : _snapshot!.Reference.MemberPath;
        public CadObjectRef? CurrentReference => _snapshot?.Reference;

        public string FilterSummary => _snapshot == null ? string.Empty :
            string.Format(Localization["FilterResults"], Properties.Count, _snapshot.Properties.Count);

        public PropertyRowViewModel? SelectedProperty
        {
            get => _selectedProperty;
            set
            {
                if (!Set(ref _selectedProperty, value)) return;
                CopyCommand.Refresh();
                CopyPathCommand.Refresh();
                ZoomToObjectCommand.Refresh();
                SelectInDrawingCommand.Refresh();
            }
        }

        public void SelectObject(CadObjectRef? reference, bool addHistory = true)
        {
            if (reference == null) return;
            ClearInspection();
            try
            {
                var result = _inspector.Inspect(reference, new InspectionOptions());
                if (addHistory) AddToHistory(reference);
                _snapshot = result.Snapshot;
                ExportCommand.Refresh();
                Properties.Clear();
                foreach (var property in result.Properties) Properties.Add(new PropertyRowViewModel(property, Localization));
                Geometry.Clear();
                foreach (var item in result.Snapshot.Geometry) Geometry.Add(item);
                UpdatePropertySources(result.Properties);
                Raise(nameof(ObjectSummary));
            Raise(nameof(HasCurrentObject));
            Raise(nameof(CurrentFigures));
                Raise(nameof(SelectedHandle));
                Raise(nameof(SelectedTypeName));
                Raise(nameof(SelectedLayer));
                Raise(nameof(SelectedState));
                Raise(nameof(SelectedDocumentId));
                Raise(nameof(SelectedNativeId));
                Raise(nameof(SelectedReferencePath));
                ApplyFilter();
                ZoomToObjectCommand.Refresh();
                SelectInDrawingCommand.Refresh();
                if (HighlightEnabled) ApplyTransientHighlight();
                StatusText = Localization.PropertiesLoaded(result.Properties.Count);
            }
            catch (Exception ex)
            {
                FileLogger.Error("Inspect object", _host.Host, _host.GetActiveDocument(), ex, reference.Handle);
                var failure = new CadProperty("Inspection", "Error", ex.Message, "Inspector", PropertyStatus.EvaluationError);
                Properties.Add(new PropertyRowViewModel(failure, Localization));
                Raise(nameof(HasProperties));
                StatusText = ex.Message;
            }
        }

        public void RequestSelectObject(CadObjectRef reference)
        {
            if (_requestInspection == null) SelectObject(reference);
            else _requestInspection(reference);
        }

        public void UpdateSelectionFromHost(bool interactive)
        {
            LoadSelection(interactive);
        }

        public void ClearForDocumentClose(string documentId)
        {
            if (_snapshot == null || !string.Equals(_snapshot.Reference.DocumentId, documentId, StringComparison.OrdinalIgnoreCase)) return;
            ResetTransientHighlight();
            Objects.Clear();
            VisibleObjects.Clear();
            ObjectTreeRoots.Clear();
            History.Clear();
            SearchResults.Clear();
            Raise(nameof(HasSearchResults));
            _historyIndex = -1;
            BackCommand.Refresh();
            ForwardCommand.Refresh();
            ClearInspection();
            Raise(nameof(DocumentName));
            StatusText = Localization["NoDrawing"];
        }

        public void InspectFromHost(string handle)
        {
            var reference = Objects.FirstOrDefault(x => x.Handle.Equals(handle, StringComparison.OrdinalIgnoreCase))
                ?? _host.FindObjectByHandle(handle);
            if (reference == null) { StatusText = Localization["HandleNotFound"]; return; }
            SelectObject(reference);
        }

        public void NavigateToSelectedProperty()
        {
            OpenProperty(SelectedProperty);
        }

        private CadObjectRef? ResolveDrawingTarget(object? parameter) => parameter switch
        {
            PropertyRowViewModel row => row.Reference ?? _snapshot?.Reference,
            ObjectTreeNode node => node.Reference ?? _snapshot?.Reference,
            _ => _snapshot?.Reference
        };

        private static CadObjectRef? ResolveReferenceFromParameter(object? parameter) => parameter switch
        {
            PropertyRowViewModel row => row.Reference,
            ObjectTreeNode node => node.Reference,
            CadObjectRef reference => reference,
            _ => null
        };

        private static CadObjectRef? ResolveHandleCopyTarget(object? parameter) => parameter switch
        {
            PropertyRowViewModel row => row.Reference,
            ObjectTreeNode node => node.Reference,
            CadObjectRef reference => reference,
            _ => null
        };

        private void OpenReference(object? parameter)
        {
            var reference = ResolveReferenceFromParameter(parameter);
            if (reference != null) RequestSelectObject(reference);
        }

        private void CopyHandle(object? parameter)
        {
            var reference = ResolveHandleCopyTarget(parameter);
            if (reference == null) return;
            System.Windows.Clipboard.SetText(reference.Handle);
            StatusText = Localization["CopiedHandle"];
        }

        private void ToggleGroupProperties()
        {
            GroupPropertiesEnabled = !GroupPropertiesEnabled;
            UserSettingsStore.SavePropertyGrouping(GroupPropertiesEnabled);
            ApplyPropertyGrouping();
        }

        private void ApplyPropertyGrouping()
        {
            var groups = PropertiesView.GroupDescriptions;
            groups.Clear();
            if (_groupProperties)
                groups.Add(new PropertyGroupDescription(nameof(PropertyRowViewModel.Source)));
        }

        private bool CanTargetDrawingObject(object? parameter) => ResolveDrawingTarget(parameter) != null;

        private void ZoomToDrawingTarget(object? parameter)
        {
            var target = ResolveDrawingTarget(parameter);
            if (target == null) return;
            if (!SupportsZoomToObject) { StatusText = Localization["FeatureUnsupported"]; return; }
            try
            {
                if (_requestZoomTo == null) _host.ZoomToObject(target);
                else _requestZoomTo(target);
            }
            catch (Exception ex)
            {
                FileLogger.Error("Zoom to object", _host.Host, _host.GetActiveDocument(), ex, target.Handle);
                StatusText = ex.Message;
            }
        }

        private void SelectDrawingTarget(object? parameter)
        {
            var target = ResolveDrawingTarget(parameter);
            if (target == null) return;
            if (!SupportsSelectInDrawing) { StatusText = Localization["FeatureUnsupported"]; return; }
            try
            {
                if (_requestSelectInDrawing == null) _host.SelectInDrawing(target);
                else _requestSelectInDrawing(target);
            }
            catch (Exception ex)
            {
                FileLogger.Error("Select in drawing", _host.Host, _host.GetActiveDocument(), ex, target.Handle);
                StatusText = ex.Message;
            }
        }

        public void RequestOpenSearchDialog() => SearchDialogRequested?.Invoke();

        private void ToggleTransientHighlight()
        {
            if (!SupportsTransientHighlight) { StatusText = Localization["FeatureUnsupported"]; return; }
            HighlightEnabled = !HighlightEnabled;
            ApplyTransientHighlight();
            StatusText = Localization[HighlightEnabled ? "HighlightOn" : "HighlightOff"];
        }

        private void ApplyTransientHighlight()
        {
            var target = HighlightEnabled ? _snapshot?.Reference : null;
            if (_requestTransientHighlight == null) _host.SetTransientHighlight(target);
            else _requestTransientHighlight(target);
        }

        /// <summary>Turns the transient highlight off and clears it in the drawing (window close, document close).</summary>
        public void ResetTransientHighlight()
        {
            if (!HighlightEnabled) return;
            HighlightEnabled = false;
            ApplyTransientHighlight();
        }

        private void SearchDatabase()
        {
            var query = DatabaseSearchQuery.Trim();
            if (string.IsNullOrEmpty(query)) return;
            if (!SupportsDatabaseSearch) { StatusText = Localization["FeatureUnsupported"]; return; }
            StatusText = Localization["SearchRunning"];
            try
            {
                if (_requestDatabaseSearch == null) ShowDatabaseSearchResults(_host.SearchObjects(new SearchRequest(query)));
                else _requestDatabaseSearch(query);
            }
            catch (Exception ex)
            {
                FileLogger.Error("Search database", _host.Host, _host.GetActiveDocument(), ex);
                StatusText = ex.Message;
            }
        }

        /// <summary>Called by the host command after SearchObjects ran in a valid document context.</summary>
        public void ShowDatabaseSearchResults(IReadOnlyList<CadObjectRef> results)
        {
            SearchResults.Clear();
            foreach (var item in results) SearchResults.Add(item);
            Raise(nameof(HasSearchResults));
            StatusText = results.Count == 0
                ? Localization["SearchNoResults"]
                : string.Format(Localization["SearchResultsLoaded"], results.Count);
        }

        public void OpenSearchResult(CadObjectRef? reference)
        {
            if (reference != null) RequestSelectObject(reference);
        }

        private void ToggleTheme()
        {
            Theme = Theme == "Dark" ? "Light" : "Dark";
        }

        private void SetTreeExpanded(bool expanded)
        {
            foreach (var root in ObjectTreeRoots)
                root.SetExpandedRecursive(expanded);
        }

        private void RefreshGroupByOptions()
        {
            GroupByOptions.Clear();
            GroupByOptions.Add(Localization["GroupByType"]);
            GroupByOptions.Add(Localization["GroupByLayer"]);
            GroupByOptions.Add(Localization["GroupByFlat"]);
            Raise(nameof(SelectedGroupBy));
        }

        private void RefreshStatusOptions()
        {
            StatusFilterOptions.Clear();
            StatusFilterOptions.Add(Localization["AllStatuses"]);
            StatusFilterOptions.Add(Localization["StatusEvaluated"]);
            StatusFilterOptions.Add(Localization["StatusDeferred"]);
            StatusFilterOptions.Add(Localization["StatusError"]);
            Raise(nameof(SelectedStatusFilter));
        }

        private void SelectObjects()
        {
            if (_requestSelection == null) LoadSelection(true);
            else _requestSelection();
        }

        private void SnoopDatabase()
        {
            if (_requestSnoopDatabase != null) _requestSnoopDatabase();
            else SnoopDatabaseFromHost();
        }

        public void SnoopDatabaseFromHost()
        {
            try
            {
                var items = _host.SnoopDatabase();
                if (items.Count == 0)
                {
                    StatusText = Localization["NoDrawing"];
                    return;
                }
                Objects.Clear();
                foreach (var item in items) Objects.Add(item);
                ApplyObjectFilter();
                if (Objects.Count > 0) SelectObject(Objects[0]);
                StatusText = string.Format(Localization["DatabaseTablesLoaded"], items.Count);
            }
            catch (Exception ex)
            {
                FileLogger.Error("Snoop database", _host.Host, _host.GetActiveDocument(), ex);
                StatusText = ex.Message;
            }
        }

        private void LookupHandle()
        {
            var handle = HandleQuery.Trim();
            if (string.IsNullOrEmpty(handle)) return;
            if (_requestHandleLookup == null) InspectFromHost(handle);
            else _requestHandleLookup(handle);
        }

        private void AddToHistory(CadObjectRef reference)
        {
            if (_historyIndex >= 0 && SameReference(History[_historyIndex], reference)) return;
            while (History.Count > _historyIndex + 1) History.RemoveAt(History.Count - 1);
            History.Add(reference);
            _historyIndex = History.Count - 1;
            BackCommand.Refresh();
            ForwardCommand.Refresh();
        }

        private void NavigateToHistoryItem(CadObjectRef reference)
        {
            for (var i = History.Count - 1; i >= 0; i--)
            {
                if (SameReference(History[i], reference)) { NavigateHistory(i); return; }
            }
            RequestSelectObject(reference);
        }

        private void NavigateHistory(int index)
        {
            if (index < 0 || index >= History.Count) return;
            _historyIndex = index;
            BackCommand.Refresh();
            ForwardCommand.Refresh();
            SelectObject(History[index], false);
        }

        private static bool SameReference(CadObjectRef left, CadObjectRef right) =>
            string.Equals(left.DocumentId, right.DocumentId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(left.Handle, right.Handle, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(left.MemberPath, right.MemberPath, StringComparison.Ordinal);

        private void Refresh()
        {
            Raise(nameof(DocumentName));
            if (_snapshot != null && _requestInspection != null) _requestInspection(_snapshot.Reference);
            else if (_requestSelection == null) { LoadSelection(false); if (Objects.Count > 0) SelectObject(Objects[0]); }
            else _requestSelection();
        }

        private void LoadSelection(bool interactive)
        {
            try
            {
                var activeDocument = _host.GetActiveDocument();
                if (Objects.Count > 0 && (activeDocument == null || !string.Equals(Objects[0].DocumentId, activeDocument.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    Objects.Clear();
                    VisibleObjects.Clear();
                    ObjectTreeRoots.Clear();
                    Properties.Clear();
                    Geometry.Clear();
                    History.Clear();
                    _historyIndex = -1;
                    BackCommand.Refresh();
                    ForwardCommand.Refresh();
                    ClearInspection();
                }
                var selected = _host.SelectObjects(new SelectionRequest(true, interactive));
                if (selected.Count == 0)
                {
                    StatusText = Localization["NoSelection"];
                    return;
                }
                Objects.Clear();
                foreach (var item in selected) Objects.Add(item);
                ApplyObjectFilter();
                SelectObject(Objects[0]);
            }
            catch (Exception ex) { FileLogger.Error("Select objects", _host.Host, _host.GetActiveDocument(), ex); StatusText = ex.Message; }
        }

        private void ClearInspection()
        {
            _snapshot = null;
            Properties.Clear();
            Raise(nameof(HasProperties));
            Raise(nameof(FilterSummary));
            Geometry.Clear();
            UpdatePropertySources(Array.Empty<CadProperty>());
            SelectedProperty = null;
            ExportCommand.Refresh();
            Raise(nameof(ObjectSummary));
            Raise(nameof(HasCurrentObject));
            Raise(nameof(CurrentFigures));
            Raise(nameof(SelectedHandle));
            Raise(nameof(SelectedTypeName));
            Raise(nameof(SelectedLayer));
            Raise(nameof(SelectedState));
            Raise(nameof(SelectedDocumentId));
            Raise(nameof(SelectedNativeId));
            Raise(nameof(SelectedReferencePath));
            ZoomToObjectCommand.Refresh();
            SelectInDrawingCommand.Refresh();
        }

        private void ApplyFilter()
        {
            if (_snapshot == null) return;
            var query = SearchText?.Trim() ?? string.Empty;
            var matches = _snapshot.Properties.Where(p =>
                SearchMatcher.Matches(query, p.Name, p.Value, p.TypeName))
                .Where(p => string.IsNullOrEmpty(_selectedPropertySource) ||
                    _selectedPropertySource == Localization["AllSources"] ||
                    string.Equals(p.Source, _selectedPropertySource, StringComparison.OrdinalIgnoreCase));

            if (_selectedStatusFilter != "AllStatuses")
            {
                if (_selectedStatusFilter == "StatusEvaluated")
                    matches = matches.Where(p => p.Status == PropertyStatus.Available);
                else if (_selectedStatusFilter == "StatusDeferred")
                    matches = matches.Where(p => p.Status == PropertyStatus.NotEvaluated);
                else if (_selectedStatusFilter == "StatusError")
                    matches = matches.Where(p => p.Status == PropertyStatus.EvaluationError ||
                        p.Status == PropertyStatus.CyclicReference ||
                        p.Status == PropertyStatus.Unsupported);
            }

            Properties.Clear();
            foreach (var property in matches.Take(500)) Properties.Add(new PropertyRowViewModel(property, Localization));
            Raise(nameof(HasProperties));
            Raise(nameof(FilterSummary));
        }

        private void ApplyObjectFilter()
        {
            var query = ObjectFilter.Trim();
            var matches = Objects.Where(item =>
                SearchMatcher.Matches(query, item.Handle, item.TypeName, item.Layer));
            VisibleObjects.Clear();
            foreach (var item in matches) VisibleObjects.Add(item);
            RebuildObjectTree();
        }

        private void RebuildObjectTree()
        {
            ObjectTreeRoots.Clear();
            if (VisibleObjects.Count == 0) return;

            if (_selectedGroupBy == "Layer")
            {
                var groups = VisibleObjects
                    .GroupBy(x => string.IsNullOrWhiteSpace(x.Layer) ? Localization["NoLayer"] : x.Layer)
                    .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

                foreach (var group in groups)
                {
                    var groupNode = new ObjectTreeNode(group.Key, group.Count().ToString(), true, null,
                        subtitle: Localization.ObjectCount(group.Count()), icon: "▰");
                    foreach (var item in group)
                    {
                        var itemNode = new ObjectTreeNode(item.TypeName, item.Handle, false, item,
                            subtitle: item.Layer, icon: GetTypeIcon(item.TypeName));
                        groupNode.Children.Add(itemNode);
                    }
                    ObjectTreeRoots.Add(groupNode);
                }
            }
            else if (_selectedGroupBy == "Flat")
            {
                foreach (var item in VisibleObjects)
                {
                    var itemNode = new ObjectTreeNode(item.TypeName, item.Handle, false, item,
                        subtitle: item.Layer, icon: GetTypeIcon(item.TypeName));
                    ObjectTreeRoots.Add(itemNode);
                }
            }
            else // Default: "Type"
            {
                var groups = VisibleObjects
                    .GroupBy(x => string.IsNullOrWhiteSpace(x.TypeName) ? Localization["UnknownType"] : x.TypeName)
                    .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

                foreach (var group in groups)
                {
                    var groupNode = new ObjectTreeNode(group.Key, group.Count().ToString(), true, null,
                        subtitle: Localization.ObjectCount(group.Count()), icon: GetTypeIcon(group.Key));
                    foreach (var item in group)
                    {
                        var itemNode = new ObjectTreeNode(item.Handle, item.Layer, false, item,
                            subtitle: item.TypeName, icon: GetTypeIcon(item.TypeName));
                        groupNode.Children.Add(itemNode);
                    }
                    ObjectTreeRoots.Add(groupNode);
                }
            }
        }

        private static string GetTypeIcon(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)) return "◇";
            var lower = typeName.ToLowerInvariant();
            if (lower.Contains("line")) return "╱";
            if (lower.Contains("circle") || lower.Contains("arc")) return "◯";
            if (lower.Contains("polyline")) return "⌁";
            if (lower.Contains("block")) return "▦";
            if (lower.Contains("text")) return "T";
            if (lower.Contains("dimension")) return "↔";
            if (lower.Contains("layer")) return "▤";
            if (lower.Contains("hatch")) return "▧";
            if (lower.Contains("dictionary")) return "▣";
            if (lower.Contains("style")) return "Aa";
            if (lower.Contains("ellipse") || lower.Contains("spline")) return "∿";
            return "◇";
        }

        private void UpdatePropertySources(System.Collections.Generic.IEnumerable<CadProperty> properties)
        {
            var selected = _selectedPropertySource;
            PropertySources.Clear();
            PropertySources.Add(Localization["AllSources"]);
            var sourceKeys = properties.Select(p => p.Source).Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var source in sourceKeys) PropertySources.Add(Localization.LocalizeSource(source));
            _selectedPropertySource = sourceKeys.Contains(selected, StringComparer.OrdinalIgnoreCase) ? selected : string.Empty;
            Raise(nameof(SelectedPropertySource));
        }

        private void ToggleLanguage()
        {
            SetLanguage(Localization.Language == "vi" ? "en" : "vi");
        }

        public void SetLanguage(string language)
        {
            var target = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? "en" :
                string.Equals(language, "vi", StringComparison.OrdinalIgnoreCase) ? "vi" : null;
            if (target == null || target == Localization.Language) return;

            Localization.Language = target;
            Raise(nameof(Language));
            Raise(nameof(DocumentName));
            Raise(nameof(ThemeText));
            Raise(nameof(SelectedTypeName));
            Raise(nameof(SelectedReferencePath));
            Raise(nameof(SelectedStatusFilter));
            RefreshGroupByOptions();
            RefreshStatusOptions();
            UpdatePropertySources(_snapshot?.Properties ?? Array.Empty<CadProperty>());
            ApplyFilter();
            RebuildObjectTree();
            StatusText = string.Format(Localization["LanguageChanged"],
                Localization[target == "vi" ? "Vietnamese" : "English"]);
            LanguageChanged?.Invoke(target);
        }

        private void CopyValue()
        {
            CopyPropertyValue(SelectedProperty);
        }

        private void CopyPath()
        {
            CopyPropertyPath(SelectedProperty);
        }

        private void OpenProperty(PropertyRowViewModel? property)
        {
            if (property?.Reference != null) RequestSelectObject(property.Reference);
        }

        private void CopyPropertyValue(PropertyRowViewModel? property)
        {
            if (property == null) return;
            System.Windows.Clipboard.SetText(property.Value ?? string.Empty);
            StatusText = Localization["CopiedValue"];
        }

        private void CopyPropertyPath(PropertyRowViewModel? property)
        {
            if (property?.Reference == null) return;
            var reference = property.Reference;
            var text = reference.HostName + ":" + reference.DocumentId + ":" + reference.Handle +
                (string.IsNullOrEmpty(reference.MemberPath) ? string.Empty : "/" + reference.MemberPath);
            System.Windows.Clipboard.SetText(text);
            StatusText = Localization["CopiedPath"];
        }

        private void Export()
        {
            if (_snapshot == null) return;
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|CSV files (*.csv)|*.csv",
                FilterIndex = 1,
                FileName = "cadlookup-" + _snapshot.Reference.Handle + ".json"
            };
            if (dialog.ShowDialog() != true) return;
            try
            {
                var content = string.Equals(Path.GetExtension(dialog.FileName), ".csv", StringComparison.OrdinalIgnoreCase)
                    ? SerializeCsv(_snapshot) : SerializeJson(_snapshot);
                File.WriteAllText(dialog.FileName, content, new UTF8Encoding(false));
                StatusText = Localization.ExportedFile(dialog.FileName);
            }
            catch (Exception ex) { FileLogger.Error("Export inspection", _host.Host, _host.GetActiveDocument(), ex, _snapshot.Reference.Handle); StatusText = ex.Message; }
        }

        private static string SerializeJson(CadObjectSnapshot snapshot)
        {
            var json = new StringBuilder();
            json.Append("{\n  \"host\": ").Append(Json(snapshot.Reference.HostName));
            json.Append(",\n  \"document\": ").Append(Json(snapshot.Reference.DocumentId));
            json.Append(",\n  \"handle\": ").Append(Json(snapshot.Reference.Handle));
            json.Append(",\n  \"type\": ").Append(Json(snapshot.TypeName));
            json.Append(",\n  \"layer\": ").Append(Json(snapshot.Layer));
            json.Append(",\n  \"state\": ").Append(Json(snapshot.State));
            json.Append(",\n  \"properties\": [");
            for (var i = 0; i < snapshot.Properties.Count; i++)
            {
                var property = snapshot.Properties[i];
                json.Append(i == 0 ? "\n    {" : ",\n    {")
                    .Append("\"name\": ").Append(Json(property.Name))
                    .Append(", \"type\": ").Append(Json(property.TypeName))
                    .Append(", \"value\": ").Append(Json(property.Value))
                    .Append(", \"memberPath\": ").Append(Json(property.Reference?.MemberPath))
                    .Append(", \"source\": ").Append(Json(property.Source))
                    .Append(", \"status\": ").Append(Json(property.Status.ToString())).Append("}");
            }
            json.Append(snapshot.Properties.Count == 0 ? "]" : "\n  ]");
            json.Append(",\n  \"geometry\": [");
            for (var i = 0; i < snapshot.Geometry.Count; i++)
            {
                var item = snapshot.Geometry[i];
                json.Append(i == 0 ? "\n    {" : ",\n    {")
                    .Append("\"kind\": ").Append(Json(item.Kind))
                    .Append(", \"summary\": ").Append(Json(item.Summary)).Append("}");
            }
            json.Append(snapshot.Geometry.Count == 0 ? "]\n}" : "\n  ]\n}");
            return json.ToString();
        }

        private static string SerializeCsv(CadObjectSnapshot snapshot)
        {
            var csv = new StringBuilder();
            csv.AppendLine("host,document,handle,objectType,layer,state,name,type,value,source,status,memberPath");
            foreach (var property in snapshot.Properties)
            {
                csv.Append(Csv(snapshot.Reference.HostName)).Append(',')
                    .Append(Csv(snapshot.Reference.DocumentId)).Append(',')
                    .Append(Csv(snapshot.Reference.Handle)).Append(',')
                    .Append(Csv(snapshot.TypeName)).Append(',')
                    .Append(Csv(snapshot.Layer)).Append(',')
                    .Append(Csv(snapshot.State)).Append(',')
                    .Append(Csv(property.Name)).Append(',')
                    .Append(Csv(property.TypeName)).Append(',')
                    .Append(Csv(property.Value)).Append(',')
                    .Append(Csv(property.Source)).Append(',')
                    .Append(Csv(property.Status.ToString())).Append(',')
                    .Append(Csv(property.Reference?.MemberPath)).AppendLine();
            }
            return csv.ToString();
        }

        private static string Csv(string? value)
        {
            var text = value ?? string.Empty;
            return "\"" + text.Replace("\"", "\"\"").Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", " ") + "\"";
        }

        private static string Json(string? value)
        {
            var text = value ?? string.Empty;
            var result = new StringBuilder(text.Length + 2).Append('"');
            foreach (var character in text)
            {
                switch (character)
                {
                    case '"': result.Append("\\\""); break;
                    case '\\': result.Append("\\\\"); break;
                    case '\n': result.Append("\\n"); break;
                    case '\r': result.Append("\\r"); break;
                    case '\t': result.Append("\\t"); break;
                    default: if (character < 32) result.Append("\\u").Append(((int)character).ToString("x4")); else result.Append(character); break;
                }
            }
            return result.Append('"').ToString();
        }
    }
}
