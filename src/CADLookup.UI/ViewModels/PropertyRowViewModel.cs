using CADLookup.Core.Domain;
using CADLookup.UI.Localization;

namespace CADLookup.UI.ViewModels
{
    public sealed class PropertyRowViewModel
    {
        public PropertyRowViewModel(CadProperty property, LocalizationService localization)
        {
            Name = property.Name; Type = property.TypeName;
            Value = property.Status == PropertyStatus.NotEvaluated ? localization["ClickToEvaluate"] : property.Value;
            Source = localization.LocalizeSource(property.Source);
            Status = localization.LocalizeStatus(property.Status.ToString());
            Reference = property.Reference;
            ReferencePath = property.Reference == null ? string.Empty : property.Reference.MemberPath;
            RawStatus = property.Status;
            IsDeferred = property.Status == PropertyStatus.NotEvaluated;
            IsError = property.Status == PropertyStatus.EvaluationError || property.Status == PropertyStatus.CyclicReference;
            IsUnsupported = property.Status == PropertyStatus.Unsupported;
            IsLimited = property.Status == PropertyStatus.Truncated;
            HasReference = property.Reference != null;
            ToolTip = BuildToolTip(property, localization);
        }
        public string Name { get; }
        public string Type { get; }
        public string Value { get; }
        public string Source { get; }
        public string Status { get; }
        public CADLookup.Core.Contracts.CadObjectRef? Reference { get; }
        public string ReferencePath { get; }
        public PropertyStatus RawStatus { get; }
        public bool IsDeferred { get; }
        public bool IsError { get; }
        public bool IsUnsupported { get; }
        public bool IsLimited { get; }
        public bool HasReference { get; }
        public string ToolTip { get; }

        private static string BuildToolTip(CadProperty property, LocalizationService localization)
        {
            var builder = new System.Text.StringBuilder(property.Name);
            builder.Append("\n").Append(localization["Type"]).Append(": ").Append(property.TypeName);
            builder.Append("\n").Append(localization["Value"]).Append(": ").Append(property.Value);
            builder.Append("\n").Append(localization["Status"]).Append(": ").Append(localization.LocalizeStatus(property.Status.ToString()));
            if (!string.IsNullOrEmpty(property.Reference?.MemberPath))
                builder.Append("\n").Append(localization["Path"]).Append(": ").Append(property.Reference.MemberPath);
            if (property.Status == PropertyStatus.NotEvaluated)
                builder.Append("\n").Append(localization["FullValueToolTip"]);
            return builder.ToString();
        }
    }
}
