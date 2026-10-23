namespace CADLookup.Core.Contracts
{
    public sealed class SelectionRequest
    {
        public SelectionRequest(bool usePreselectedObjects = true, bool allowInteractiveSelection = true)
        {
            UsePreselectedObjects = usePreselectedObjects;
            AllowInteractiveSelection = allowInteractiveSelection;
        }

        public bool UsePreselectedObjects { get; }
        public bool AllowInteractiveSelection { get; }
    }
}
