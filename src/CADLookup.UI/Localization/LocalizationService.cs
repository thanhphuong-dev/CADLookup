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
                default: return key;
            }
        }
    }
}
