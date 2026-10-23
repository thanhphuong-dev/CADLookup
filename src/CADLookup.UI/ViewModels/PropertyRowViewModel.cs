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
        }
        public string Name { get; }
        public string Type { get; }
        public string Value { get; }
        public string Source { get; }
        public string Status { get; }
        public CADLookup.Core.Contracts.CadObjectRef? Reference { get; }
        public string ReferencePath { get; }
    }
}
