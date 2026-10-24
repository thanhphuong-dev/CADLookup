using System.Collections.ObjectModel;
using CADLookup.Core.Contracts;
using CADLookup.UI.Mvvm;

namespace CADLookup.UI.ViewModels
{
    public sealed class ObjectTreeNode : ObservableObject
    {
        private bool _isExpanded = true;
        private bool _isSelected;

        public ObjectTreeNode(string title, string countBadge, bool isGroup, CadObjectRef? reference = null, string subtitle = "", string icon = "")
        {
            Title = title;
            CountBadge = countBadge;
            IsGroup = isGroup;
            Reference = reference;
            Subtitle = subtitle;
            Icon = icon;
            Children = new ObservableCollection<ObjectTreeNode>();
        }

        public string Title { get; }
        public string Subtitle { get; }
        public string CountBadge { get; }
        public bool IsGroup { get; }
        public string Icon { get; }
        public CadObjectRef? Reference { get; }
        public ObservableCollection<ObjectTreeNode> Children { get; }

        public bool IsExpanded
        {
            get => _isExpanded;
            set => Set(ref _isExpanded, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => Set(ref _isSelected, value);
        }

        public void SetExpandedRecursive(bool expanded)
        {
            IsExpanded = expanded;
            foreach (var child in Children)
            {
                child.SetExpandedRecursive(expanded);
            }
        }
    }
}
