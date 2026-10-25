using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using CADLookup.Infrastructure.Configuration;

namespace CADLookup.UI.Localization
{
    public sealed class LocalizationService : INotifyPropertyChanged
    {
        private string _language = UserSettingsStore.LoadLanguage();
        public event PropertyChangedEventHandler? PropertyChanged;
        public string Language
        {
            get => _language;
            set
            {
                var normalized = value == "en" ? "en" : "vi";
                if (_language == normalized) return;
                _language = normalized;
                UserSettingsStore.SaveLanguage(_language);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
            }
        }
        public string this[string key] => Resolve(key, Language);
        public string PropertiesLoaded(int count) => string.Format(CultureInfo.CurrentCulture, this["PropertiesLoaded"], count);
        public string ObjectCount(int count) => string.Format(CultureInfo.CurrentCulture, this["ObjectCount"], count);
        public string ExportedFile(string path) => string.Format(CultureInfo.CurrentCulture, this["ExportedFile"], path);
        public string LocalizeStatus(string status) => this["PropertyStatus." + status];
        public string LocalizeSource(string source) => this["PropertySource." + source];
        public string GetSourceKey(string display)
        {
            var sources = new[] { "Identity", "Runtime", "Entity", "Geometry", "Relationship", "Extended data", "Reflection", "Methods", "Events", "Collection", "Inspector", "Properties", "Text", "Block", "Block definition", "Layer", "Dimension" };
            return sources.FirstOrDefault(source => string.Equals(LocalizeSource(source), display, StringComparison.OrdinalIgnoreCase)) ?? display;
        }
        public void Toggle() => Language = Language == "vi" ? "en" : "vi";
        private static string Resolve(string key, string language)
        {
            var vi = language == "vi";
            switch (key)
            {
                case "AppTitle": return "CADLookup";
                case "Objects": return vi ? "Đối tượng" : "Objects";
                case "ObjectCount": return vi ? "{0} đối tượng" : "{0} objects";
                case "Properties": return vi ? "Thuộc tính" : "Properties";
                case "Search": return vi ? "Tìm thuộc tính…" : "Search properties…";
                case "Select": return vi ? "Chọn đối tượng" : "Select objects";
                case "Refresh": return vi ? "Làm mới" : "Refresh";
                case "Handle": return vi ? "Handle" : "Handle";
                case "PropertiesLoaded": return vi ? "Đã tải {0} thuộc tính" : "Loaded {0} properties";
                case "ExportedFile": return vi ? "Đã xuất {0}" : "Exported {0}";
                case "Type": return vi ? "Loại" : "Type";
                case "Value": return vi ? "Giá trị" : "Value";
                case "Source": return vi ? "Nguồn" : "Source";
                case "Status": return vi ? "Trạng thái" : "Status";
                case "Layer": return vi ? "Layer" : "Layer";
                case "Language": return vi ? "EN" : "VI";
                case "LanguageHint": return vi ? "Đổi ngôn ngữ Việt / Anh" : "Switch between Vietnamese and English";
                case "Vietnamese": return vi ? "Tiếng Việt" : "Vietnamese";
                case "English": return vi ? "Tiếng Anh" : "English";
                case "LanguagePrompt": return vi ? "\nNgôn ngữ [VI/EN] <{0}>: " : "\nLanguage [VI/EN] <{0}>: ";
                case "LanguageChanged": return vi ? "Đã đổi ngôn ngữ sang {0}." : "Language changed to {0}.";
                case "LanguageAlready": return vi ? "Ngôn ngữ hiện tại đã là {0}." : "The current language is already {0}.";
                case "LanguageInvalid": return vi ? "Lựa chọn không hợp lệ. Hãy nhập VI hoặc EN." : "Invalid choice. Enter VI or EN.";
                case "RibbonPanelTitle": return vi ? "Lệnh AutoCAD" : "AutoCAD Commands";
                case "WpfForm": return vi ? "FORM WPF" : "WPF FORM";
                case "RibbonInspect": return vi ? "Tra cứu" : "Inspect";
                case "RibbonInspectHint": return vi ? "Mở CADLookup để xem thông tin và thuộc tính của đối tượng đang chọn." : "Open CADLookup to inspect the selected object's data and properties.";
                case "RibbonSelect": return vi ? "Chọn đối tượng" : "Select";
                case "RibbonSelectHint": return vi ? "Chọn một hoặc nhiều đối tượng trên bản vẽ để tra cứu." : "Select one or more drawing objects to inspect.";
                case "RibbonSnoop": return vi ? "Database" : "Snoop DB";
                case "RibbonSnoopHint": return vi ? "Duyệt các bảng và từ điển trong cơ sở dữ liệu bản vẽ." : "Browse tables and dictionaries in the drawing database.";
                case "RibbonRefresh": return vi ? "Làm mới" : "Refresh";
                case "RibbonRefreshHint": return vi ? "Cập nhật dữ liệu của đối tượng đang xem." : "Refresh data for the objects currently being inspected.";
                case "RibbonLanguageHint": return vi ? "Chuyển ngôn ngữ giao diện CADLookup giữa tiếng Việt và tiếng Anh." : "Switch the CADLookup interface between Vietnamese and English.";
                case "FormSelectHint": return vi ? "Nút trong Form WPF: chọn đối tượng. AutoCAD sẽ chạy CADLOOKUPSELECT." : "WPF form button: select objects. AutoCAD runs CADLOOKUPSELECT.";
                case "FormSnoopHint": return vi ? "Nút trong Form WPF: duyệt database. AutoCAD sẽ chạy CADLOOKUPSNOOPDB." : "WPF form button: browse the database. AutoCAD runs CADLOOKUPSNOOPDB.";
                case "FormRefreshHint": return vi ? "Nút trong Form WPF: làm mới dữ liệu đang xem." : "WPF form button: refresh the current inspection.";
                case "FormHandleHint": return vi ? "Tra cứu handle từ Form WPF; AutoCAD dùng CADLOOKUPINSPECT để đọc đối tượng." : "Look up a handle from the WPF form; AutoCAD uses CADLOOKUPINSPECT to read it.";
                case "FormThemeHint": return vi ? "Thao tác trong Form WPF: đổi giao diện sáng/tối." : "WPF form action: switch the light/dark theme.";
                case "FormLanguageHint": return vi ? "Thao tác trong Form WPF: chuyển Việt/Anh." : "WPF form action: switch Vietnamese/English.";
                case "ContextInspect": return vi ? "CADLookup - Tra cứu đối tượng..." : "CADLookup - Inspect object...";
                case "ContextDatabase": return vi ? "CADLookup - Duyệt Database..." : "CADLookup - Browse Database...";
                case "ContextInspectSelected": return vi ? "CADLookup - Tra cứu đối tượng đã chọn" : "CADLookup - Inspect selected object";
                case "NoLayer": return vi ? "(Không có Layer)" : "(No Layer)";
                case "UnknownType": return vi ? "(Không rõ kiểu)" : "(Unknown type)";
                case "Minimize": return vi ? "Thu nhỏ" : "Minimize";
                case "Maximize": return vi ? "Phóng to" : "Maximize";
                case "Close": return vi ? "Đóng" : "Close";
                case "NoSelection": return vi ? "Chưa có đối tượng. Hãy chọn đối tượng trong CAD." : "No objects loaded. Select objects in CAD.";
                case "Copy": return vi ? "Sao chép giá trị" : "Copy value";
                case "CopyPath": return vi ? "Sao chép đường dẫn" : "Copy path";
                case "OpenReference": return vi ? "Mở đối tượng liên quan" : "Open related object";
                case "CopiedValue": return vi ? "Đã sao chép giá trị." : "Value copied.";
                case "CopiedPath": return vi ? "Đã sao chép đường dẫn đối tượng." : "Object path copied.";
                case "Export": return vi ? "Xuất dữ liệu" : "Export data";
                case "Name": return vi ? "Tên" : "Name";
                case "TypeHeader": return vi ? "Kiểu" : "Type";
                case "ValueHeader": return vi ? "Giá trị" : "Value";
                case "SourceHeader": return vi ? "Nguồn" : "Source";
                case "StatusHeader": return vi ? "Trạng thái" : "Status";
                case "Geometry": return vi ? "Hình học" : "Geometry";
                case "NoGeometry": return vi ? "Không có hình học để hiển thị." : "No drawable geometry.";
                case "NoObject": return vi ? "Chưa chọn đối tượng" : "No object selected";
                case "RootObject": return vi ? "Đối tượng gốc" : "Root object";
                case "Reference": return vi ? "THAM CHIẾU" : "REFERENCE";
                case "Document": return vi ? "Bản vẽ" : "Drawing";
                case "ObjectId": return vi ? "Object ID" : "Object ID";
                case "Path": return vi ? "Đường dẫn" : "Path";
                case "State": return vi ? "Trạng thái" : "State";
                case "EmptyProperties": return vi ? "Chọn đối tượng để xem các thuộc tính có thể tra cứu." : "Select an object to view inspectable properties.";
                case "SelectObjectHint": return vi ? "Chọn một đối tượng để xem dữ liệu" : "Select an object to inspect its data";
                case "FilterObjects": return vi ? "Lọc handle, kiểu hoặc layer…" : "Filter handle, type, or layer…";
                case "Inspector": return vi ? "TRÌNH TRA CỨU ĐỐI TƯỢNG" : "OBJECT INSPECTOR";
                case "PropertyDescription": return vi ? "Tên, kiểu dữ liệu, giá trị và nguồn thuộc tính" : "Property name, type, value, and source";
                case "GeometryKind": return vi ? "Thành phần hình học" : "Geometry";
                case "Summary": return vi ? "Thông tin" : "Summary";
                case "HandlePlaceholder": return vi ? "Tra theo handle" : "Find by handle";
                case "Go": return vi ? "Đi" : "Go";
                case "Back": return vi ? "Quay lại" : "Back";
                case "Forward": return vi ? "Tiếp" : "Forward";
                case "HandleNotFound": return vi ? "Không tìm thấy handle trong bản vẽ đang mở." : "Handle was not found in the active drawing.";
                case "NoDrawing": return vi ? "Không có bản vẽ đang hoạt động." : "No active drawing.";
                case "AllSources": return vi ? "Tất cả nhóm" : "All sources";
                case "ClickToEvaluate": return vi ? "Nhấn đúp để đọc" : "Double-click to evaluate";
                case "PropertyStatus.Available": return vi ? "Đã đọc" : "Available";
                case "PropertyStatus.NotEvaluated": return vi ? "Chưa đọc" : "Not evaluated";
                case "PropertyStatus.Unsupported": return vi ? "Chỉ mô tả" : "Catalog only";
                case "PropertyStatus.EvaluationError": return vi ? "Lỗi đọc" : "Read error";
                case "PropertyStatus.Truncated": return vi ? "Đã giới hạn" : "Limited";
                case "PropertyStatus.CyclicReference": return vi ? "Vòng tham chiếu" : "Cycle";
                case "PropertySource.Identity": return vi ? "Định danh" : "Identity";
                case "PropertySource.Runtime": return vi ? "Runtime" : "Runtime";
                case "PropertySource.Entity": return vi ? "Đối tượng CAD" : "Entity";
                case "PropertySource.Geometry": return vi ? "Hình học" : "Geometry";
                case "PropertySource.Relationship": return vi ? "Quan hệ" : "Relationship";
                case "PropertySource.Extended data": return vi ? "Dữ liệu mở rộng" : "Extended data";
                case "PropertySource.Reflection": return vi ? "Thuộc tính API" : "API properties";
                case "PropertySource.Methods": return vi ? "Phương thức" : "Methods";
                case "PropertySource.Events": return vi ? "Sự kiện" : "Events";
                case "PropertySource.Collection": return vi ? "Danh sách" : "Collection";
                case "PropertySource.Inspector": return vi ? "Bộ tra cứu" : "Inspector";
                case "PropertySource.Properties": return vi ? "Thuộc tính" : "Properties";
                case "PropertySource.Text": return vi ? "Văn bản" : "Text";
                case "PropertySource.Block": return vi ? "Block" : "Block";
                case "PropertySource.Block definition": return vi ? "Định nghĩa block" : "Block definition";
                case "PropertySource.Layer": return "Layer";
                case "PropertySource.Dimension": return vi ? "Kích thước" : "Dimension";
                case "Theme": return vi ? "Chủ đề" : "Theme";
                case "Dark": return vi ? "Tối" : "Dark";
                case "Light": return vi ? "Sáng" : "Light";
                case "GroupBy": return vi ? "Nhóm theo" : "Group by";
                case "GroupByType": return vi ? "Theo Loại đối tượng" : "By Object Type";
                case "GroupByLayer": return vi ? "Theo Layer" : "By Layer";
                case "GroupByFlat": return vi ? "Danh sách phẳng" : "Flat List";
                case "StatusFilter": return vi ? "Trạng thái" : "Status";
                case "AllStatuses": return vi ? "Tất cả trạng thái" : "All Statuses";
                case "StatusEvaluated": return vi ? "Đã nạp" : "Evaluated";
                case "StatusDeferred": return vi ? "Chưa nạp (Nhấn đúp)" : "Deferred";
                case "StatusError": return vi ? "Lỗi / Cảnh báo" : "Errors / Warnings";
                case "FilterResults": return vi ? "Hiển thị {0} / {1}" : "Showing {0} of {1}";
                case "ClearFilter": return vi ? "Xóa tìm kiếm" : "Clear search";
                case "ExpandAll": return vi ? "Mở rộng" : "Expand all";
                case "CollapseAll": return vi ? "Thu gọn" : "Collapse all";
                case "SnoopDatabase": return vi ? "Duyệt Database" : "Snoop Database";
                case "SnoopDatabaseHint": return vi ? "Duyệt các bảng Block, Layer, TextStyle, DimStyle, NOD..." : "Browse all database symbol tables and dictionaries";
                case "DatabaseTablesLoaded": return vi ? "Đã tải {0} đối tượng từ Database" : "Loaded {0} database objects";
                case "SearchDrawing": return vi ? "Tìm kiếm" : "Search";
                case "SearchDrawingHint": return vi ? "Tìm đối tượng trong toàn bộ bản vẽ theo handle, loại, layer, tên block hoặc nội dung văn bản (Ctrl+Shift+F)." : "Find drawing objects by handle, type, layer, block name, or text content (Ctrl+Shift+F).";
                case "SearchWindowSubtitle": return vi ? "TÌM TRONG BẢN VẼ" : "SEARCH DRAWING";
                case "SearchPlaceholder": return vi ? "handle, loại, layer, text… (cách nhau bởi dấu cách)" : "handle, type, layer, text… (space separated)";
                case "SearchExecute": return vi ? "Tìm" : "Search";
                case "SearchHint": return vi ? "Mỗi từ khóa là một điều kiện AND. So khớp bỏ qua dấu tiếng Việt." : "Tokens are AND-combined. Vietnamese diacritics are ignored.";
                case "SearchResultsLoaded": return vi ? "Tìm thấy {0} đối tượng khớp từ khóa." : "Found {0} matching objects.";
                case "SearchNoResults": return vi ? "Không có đối tượng nào khớp từ khóa." : "No objects match the query.";
                case "SearchEmpty": return vi ? "Nhập từ khóa rồi nhấn Tìm." : "Type a query and press Search.";
                case "SearchRunning": return vi ? "Đang quét bản vẽ…" : "Scanning the drawing…";
                case "SearchResultHint": return vi ? "Nhấn đúp vào kết quả để tra cứu chi tiết." : "Double-click a result to inspect it.";
                case "SelectInDrawing": return vi ? "Chọn trong bản vẽ" : "Select in drawing";
                case "SelectInDrawingHint": return vi ? "Bôi đen đối tượng đang tra cứu trong bản vẽ (F6)." : "Highlight the inspected object in the drawing (F6).";
                case "ZoomToObject": return vi ? "Zoom tới" : "Zoom to";
                case "ZoomToObjectHint": return vi ? "Zoom bản vẽ tới phạm vi của đối tượng đang tra cứu (F7)." : "Zoom the drawing to the inspected object's extents (F7).";
                case "MarkedInDrawing": return vi ? "Đã chọn đối tượng trong bản vẽ." : "Object selected in the drawing.";
                case "ZoomedToObject": return vi ? "Đã zoom tới phạm vi đối tượng." : "Zoomed to the object's extents.";
                case "NotGraphical": return vi ? "Đối tượng không có hình học để zoom." : "The object has no geometry to zoom to.";
                case "FeatureUnsupported": return vi ? "Host hiện tại không hỗ trợ tính năng này." : "The current host does not support this feature.";
                case "HighlightInDrawing": return vi ? "Gạch sáng" : "Highlight";
                case "HighlightInDrawingHint": return vi ? "Vẽ gạch sáng tạm thời (transient) lên đối tượng đang tra cứu trong bản vẽ, không sửa database (F8)." : "Draw a temporary transient highlight over the inspected object without touching the database (F8).";
                case "HighlightOn": return vi ? "Đã bật gạch sáng cho đối tượng đang tra cứu." : "Highlight enabled for the inspected object.";
                case "HighlightOff": return vi ? "Đã tắt gạch sáng." : "Highlight disabled.";
                case "GroupProperties": return vi ? "Nhóm thuộc tính theo nguồn" : "Group properties by source";
                case "CopyHandle": return vi ? "Sao chép handle" : "Copy handle";
                case "CopiedHandle": return vi ? "Đã sao chép handle." : "Handle copied.";
                case "History": return vi ? "Đối tượng đã xem" : "Recent objects";
                case "GetStarted": return vi ? "Bắt đầu tra cứu" : "Start inspecting";
                case "GetStartedHint": return vi ? "Chọn đối tượng trong bản vẽ rồi bấm nút trên thanh công cụ phía trên, hoặc gõ một lệnh bên dưới." : "Select objects in the drawing and use the toolbar above, or type one of the commands below.";
                case "GuideInspect": return vi ? "Tra cứu thuộc tính của đối tượng đang chọn." : "Inspect the currently selected objects.";
                case "GuideSelect": return vi ? "Chọn lại đối tượng mới ngay trên bản vẽ." : "Pick new objects directly in the drawing.";
                case "GuideSearch": return vi ? "Tìm đối tượng trong toàn bộ bản vẽ theo từ khóa." : "Search the whole drawing for objects by keyword.";
                case "GuideSnoop": return vi ? "Duyệt các bảng và từ điển của database bản vẽ." : "Browse the drawing database tables and dictionaries.";
                case "ShortcutsHint": return vi ? "Phím tắt:  F5 Làm mới  ·  F6 Chọn  ·  F7 Zoom  ·  F8 Gạch sáng  ·  Ctrl+Shift+F Tìm kiếm" : "Shortcuts:  F5 Refresh  ·  F6 Select  ·  F7 Zoom  ·  F8 Highlight  ·  Ctrl+Shift+F Search";
                case "FullValueToolTip": return vi ? "Nhấp đúp để đánh giá thành viên trì hoãn." : "Double-click to evaluate a deferred member.";
                case "RibbonSearch": return vi ? "Tìm kiếm" : "Search";
                case "RibbonSearchHint": return vi ? "Tìm đối tượng trong toàn bộ bản vẽ theo từ khóa (handle, loại, layer, text)." : "Search the whole drawing for objects by keyword (handle, type, layer, text).";
                case "ContextSearch": return vi ? "CADLookup - Tìm kiếm đối tượng..." : "CADLookup - Search objects...";
                default: return key;
            }
        }
    }
}
