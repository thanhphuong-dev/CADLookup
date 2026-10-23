using CADLookup.Core.Application;
using CADLookup.Core.Contracts;
using CADLookup.Core.Domain;
using Xunit;

namespace CADLookup.Core.Tests;

public sealed class ObjectInspectorTests
{
    [Fact]
    public void Inspect_limits_properties_and_marks_truncation()
    {
        var adapter = new FakeAdapter(Enumerable.Range(0, 4)
            .Select(i => new CadProperty("P" + i, "Int32", i.ToString(), "Test")));

        var result = new ObjectInspector(adapter).Inspect(adapter.Reference, new InspectionOptions(maxProperties: 2));

        Assert.Equal(3, result.Properties.Count);
        Assert.Equal(PropertyStatus.Truncated, result.Properties[^1].Status);
        Assert.Equal(3, result.Snapshot.Properties.Count);
    }

    [Fact]
    public void Inspect_passes_depth_collection_and_extended_data_limits_to_adapter()
    {
        var adapter = new FakeAdapter(Array.Empty<CadProperty>());

        new ObjectInspector(adapter).Inspect(adapter.Reference,
            new InspectionOptions(maxProperties: 17, maxDepth: 4, includeExtendedData: false));

        Assert.NotNull(adapter.LastOptions);
        Assert.Equal(17, adapter.LastOptions!.MaxProperties);
        Assert.Equal(4, adapter.LastOptions.MaxDepth);
        Assert.False(adapter.LastOptions.IncludeExtendedData);
    }

    [Fact]
    public void Object_reference_keeps_member_path_and_reports_navigation_depth()
    {
        var reference = new CadObjectRef("Fake", "sample.dwg", "2A", "db:2A", "Point", "p:Location/p:Origin");

        Assert.Equal(2, reference.NavigationDepth);
        Assert.Contains("p:Location/p:Origin", reference.DisplayName);
    }

    [Fact]
    public void Reflection_reader_keeps_working_when_one_property_getter_throws()
    {
        var source = new ThrowingMember();
        var reference = CreateReference();

        var properties = ReflectionMemberReader.Read(source, reference, new ReadOptions(), new object[] { source });

        Assert.Contains(properties, p => p.Name == nameof(ThrowingMember.GoodValue) && p.Status == PropertyStatus.NotEvaluated);
        Assert.Contains(properties, p => p.Name == nameof(ThrowingMember.BrokenValue) && p.Status == PropertyStatus.NotEvaluated);
        Assert.Equal(0, source.GetterCalls);
        Assert.Equal(42, ReflectionMemberReader.ResolvePath(source,
            ReflectionMemberReader.AppendProperty("", nameof(ThrowingMember.GoodValue)), new List<object> { source }));
        Assert.Equal(1, source.GetterCalls);
        Assert.Throws<InvalidOperationException>(() => ReflectionMemberReader.ResolvePath(source,
            ReflectionMemberReader.AppendProperty("", nameof(ThrowingMember.BrokenValue)), new List<object> { source }));
    }

    [Fact]
    public void Reflection_reader_marks_cycles_and_honors_collection_limit()
    {
        var node = new CycleNode();
        var cycle = ReflectionMemberReader.Read(node, CreateReference(), new ReadOptions(), new object[] { node });
        var collection = ReflectionMemberReader.Read(new[] { 10, 20, 30 }, CreateReference(),
            new ReadOptions(maxCollectionItems: 2), Array.Empty<object>());

        Assert.Contains(cycle, p => p.Name == nameof(CycleNode.Self) && p.Status == PropertyStatus.NotEvaluated);
        Assert.Throws<ReflectionMemberReader.CyclicMemberPathException>(() => ReflectionMemberReader.ResolvePath(node,
            ReflectionMemberReader.AppendProperty("", nameof(CycleNode.Self)), new List<object> { node }));
        Assert.Equal(3, collection.Count);
        Assert.Equal(PropertyStatus.Truncated, collection[^1].Status);
    }

    [Fact]
    public void Reflection_reader_catalogs_methods_and_events_without_invoking_them()
    {
        var source = new MemberCatalog();

        var properties = ReflectionMemberReader.Read(source, CreateReference(), new ReadOptions(), new object[] { source });

        Assert.Contains(properties, p => p.Name == nameof(MemberCatalog.Compute) && p.Source == "Methods" && p.Status == PropertyStatus.Unsupported);
        Assert.Contains(properties, p => p.Name == nameof(MemberCatalog.Changed) && p.Source == "Events" && p.Status == PropertyStatus.Unsupported);
        Assert.Equal(0, source.InvocationCount);
    }

    [Fact]
    public void Resolve_path_reads_nested_properties_and_collection_items()
    {
        var root = new NestedModel { Child = new NestedChild { Values = new[] { "zero", "one" } } };
        var path = ReflectionMemberReader.AppendIndex(
            ReflectionMemberReader.AppendProperty(ReflectionMemberReader.AppendProperty("", nameof(NestedModel.Child)), nameof(NestedChild.Values)), 1);

        var result = ReflectionMemberReader.ResolvePath(root, path, new List<object> { root });

        Assert.Equal("one", result);
    }

    [Fact]
    public void Reflection_reader_stops_creating_routes_at_the_depth_limit()
    {
        var root = new NestedModel { Child = new NestedChild { Values = new[] { "x" } } };
        var reference = CreateReference();
        var rootMembers = ReflectionMemberReader.Read(root, reference, new ReadOptions(maxDepth: 1), new object[] { root });
        var childReference = Assert.Single(rootMembers, p => p.Name == nameof(NestedModel.Child)).Reference!;
        var ancestors = new List<object> { root };
        var child = ReflectionMemberReader.ResolvePath(root, childReference.MemberPath, ancestors);
        var childMembers = ReflectionMemberReader.Read(child, childReference, new ReadOptions(maxDepth: 1), ancestors);

        Assert.Contains(childMembers, p => p.Name == nameof(NestedChild.Values) && p.Status == PropertyStatus.Truncated && p.Reference == null);
    }

    private static CadObjectRef CreateReference() => new("Fake", "sample.dwg", "1A", "db:1A", "Root");

    private sealed class ThrowingMember
    {
        public int GetterCalls { get; private set; }
        public int GoodValue { get { GetterCalls++; return 42; } }
        public string BrokenValue => throw new InvalidOperationException("getter failed");
    }

    private sealed class CycleNode
    {
        public CycleNode Self => this;
    }

    private sealed class MemberCatalog
    {
        public int InvocationCount { get; private set; }
        public event EventHandler? Changed { add { } remove { } }
        public int Compute(int value) { InvocationCount++; return value * 2; }
    }

    private sealed class NestedModel { public NestedChild Child { get; set; } = new(); }
    private sealed class NestedChild { public string[] Values { get; set; } = Array.Empty<string>(); }

    private sealed class FakeAdapter : ICadHostAdapter
    {
        public FakeAdapter(IEnumerable<CadProperty> properties)
        {
            Reference = new CadObjectRef("Fake", "sample.dwg", "1A", "db:1A", "Line");
            Snapshot = new CadObjectSnapshot(Reference, "Line", "0", "Available", properties);
        }

        public CadObjectRef Reference { get; }
        public CadObjectSnapshot Snapshot { get; }
        public ReadOptions? LastOptions { get; private set; }
        public CadHostInfo Host => new("Fake", "1", "test");
        public CadDocumentInfo GetActiveDocument() => new("sample.dwg", "sample.dwg", "sample.dwg");
        public IReadOnlyList<CadObjectRef> SelectObjects(SelectionRequest request) => new[] { Reference };
        public CadObjectRef? FindObjectByHandle(string handle) => Reference;
        public CadObjectSnapshot ReadObject(CadObjectRef objectRef, ReadOptions options)
        {
            LastOptions = options;
            return Snapshot;
        }
    }
}
