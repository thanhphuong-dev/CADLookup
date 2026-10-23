using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using CADLookup.Core.Contracts;
using CADLookup.Core.Domain;

namespace CADLookup.Core.Application
{
    /// <summary>Reads public CLR properties without invoking methods and isolates failures per property.</summary>
    public static class ReflectionMemberReader
    {
        public sealed class CyclicMemberPathException : InvalidOperationException
        {
            public CyclicMemberPathException(string message) : base(message) { }
        }

        public static IReadOnlyList<CadProperty> Read(object target, CadObjectRef rootReference,
            ReadOptions options, IEnumerable<object> ancestors,
            Func<object, CadObjectRef?>? nativeReferenceFactory = null,
            Func<object, object, bool>? sameNativeObject = null)
        {
            var result = new List<CadProperty>();
            var ancestorList = ancestors.ToList();
            if (IsSimple(target.GetType()))
            {
                result.Add(new CadProperty("Value", FriendlyType(target.GetType()), FormatValue(target), "Reflection"));
                return result;
            }
            var collection = target as IEnumerable;
            if (collection != null && !(target is string))
            {
                ReadCollection(collection, target, rootReference, options, ancestorList,
                    nativeReferenceFactory, sameNativeObject, result);
                return result;
            }

            PropertyInfo[] members;
            MethodInfo[] methods;
            EventInfo[] events;
            try
            {
                members = target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.GetIndexParameters().Length == 0 && p.GetGetMethod(false) != null)
                    .OrderBy(p => p.Name, StringComparer.Ordinal)
                    .ToArray();
                methods = target.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => !m.IsSpecialName && m.DeclaringType != typeof(object))
                    .OrderBy(m => m.Name, StringComparer.Ordinal)
                    .ToArray();
                events = target.GetType().GetEvents(BindingFlags.Public | BindingFlags.Instance)
                    .OrderBy(e => e.Name, StringComparer.Ordinal)
                    .ToArray();
            }
            catch (Exception ex)
            {
                result.Add(new CadProperty("Properties", target.GetType().FullName ?? target.GetType().Name,
                    ex.Message, "Reflection", PropertyStatus.EvaluationError));
                return result;
            }

            foreach (var member in members)
            {
                if (result.Count >= options.MaxProperties) break;
                try
                {
                    result.Add(CreateDeferredProperty(member, rootReference, options));
                }
                catch (Exception ex)
                {
                    var actual = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
                    result.Add(new CadProperty(member.Name, FriendlyType(member.PropertyType), actual.Message,
                        "Reflection", PropertyStatus.EvaluationError));
                }
            }
            foreach (var method in methods)
            {
                if (result.Count >= options.MaxProperties) break;
                result.Add(new CadProperty(method.Name, "Method", FormatMethod(method), "Methods", PropertyStatus.Unsupported));
            }
            foreach (var eventInfo in events)
            {
                if (result.Count >= options.MaxProperties) break;
                var handlerType = eventInfo.EventHandlerType;
                result.Add(new CadProperty(eventInfo.Name, "Event", handlerType?.FullName ?? "Unknown handler", "Events", PropertyStatus.Unsupported));
            }
            AddTruncationMarker(result, members.Length + methods.Length + events.Length, options.MaxProperties);
            return result;
        }

        public static object ResolvePath(object root, string path, IList<object> ancestors,
            Func<object, object, bool>? sameNativeObject = null)
        {
            object current = root;
            if (string.IsNullOrEmpty(path)) return current;
            foreach (var segment in path.Split('/'))
            {
                if (segment.StartsWith("p:", StringComparison.Ordinal))
                {
                    var name = Uri.UnescapeDataString(segment.Substring(2));
                    var property = current.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                    if (property == null || property.GetIndexParameters().Length != 0 || property.GetGetMethod(false) == null)
                        throw new InvalidOperationException("Member is no longer available: " + name);
                    try { current = property.GetValue(current, null) ?? throw new InvalidOperationException("Member value is null: " + name); }
                    catch (TargetInvocationException ex) when (ex.InnerException != null) { throw ex.InnerException; }
                }
                else if (segment.StartsWith("i:", StringComparison.Ordinal) && current is IEnumerable enumerable && !(current is string))
                {
                    var index = int.Parse(segment.Substring(2), CultureInfo.InvariantCulture);
                    current = GetItem(enumerable, index) ?? throw new InvalidOperationException("Collection item is no longer available or is null.");
                }
                else throw new InvalidOperationException("Unsupported member path.");
                if (ancestors.Any(a => ReferenceEquals(a, current) ||
                    (sameNativeObject != null && sameNativeObject(a, current))))
                    throw new CyclicMemberPathException("The member path resolves to an object already present earlier in this path.");
                ancestors.Add(current);
            }
            return current;
        }

        public static string AppendProperty(string path, string name) =>
            (string.IsNullOrEmpty(path) ? string.Empty : path + "/") + "p:" + Uri.EscapeDataString(name);

        public static string AppendIndex(string path, int index) =>
            (string.IsNullOrEmpty(path) ? string.Empty : path + "/") + "i:" + index.ToString(CultureInfo.InvariantCulture);

        private static void ReadCollection(IEnumerable collection, object target, CadObjectRef rootReference,
            ReadOptions options, IList<object> ancestors, Func<object, CadObjectRef?>? nativeReferenceFactory,
            Func<object, object, bool>? sameNativeObject, IList<CadProperty> result)
        {
            var index = 0;
            try
            {
                foreach (var item in collection)
                {
                    if (index >= options.MaxCollectionItems || result.Count >= options.MaxProperties)
                    {
                        result.Add(new CadProperty("…", "", "Additional collection items omitted", "Collection", PropertyStatus.Truncated));
                        break;
                    }
                    var itemIndex = index++;
                    result.Add(item == null
                        ? new CadProperty("[" + itemIndex + "]", "Object", "null", "Collection")
                        : CreateProperty("[" + itemIndex + "]", item.GetType(), item,
                            AppendIndex(rootReference.MemberPath, itemIndex), "Collection", rootReference,
                            options, ancestors, nativeReferenceFactory, sameNativeObject));
                }
            }
            catch (Exception ex)
            {
                result.Add(new CadProperty("Collection enumeration", target.GetType().FullName ?? target.GetType().Name,
                    ex.Message, "Collection", PropertyStatus.EvaluationError));
            }
        }

        private static CadProperty CreateProperty(string name, Type declaredType, object value, string path,
            string source, CadObjectRef rootReference, ReadOptions options, IList<object> ancestors,
            Func<object, CadObjectRef?>? nativeReferenceFactory, Func<object, object, bool>? sameNativeObject)
        {
            var type = value.GetType();
            if (IsSimple(type)) return new CadProperty(name, FriendlyType(type), FormatValue(value), source);
            if (ancestors.Any(a => ReferenceEquals(a, value) || (sameNativeObject != null && sameNativeObject(a, value))))
                return new CadProperty(name, FriendlyType(type), "Cycle detected", source, PropertyStatus.CyclicReference);

            var reference = nativeReferenceFactory?.Invoke(value);
            if (reference == null)
            {
                var depth = string.IsNullOrEmpty(path) ? 0 : path.Split('/').Length;
                if (depth > options.MaxDepth)
                    return new CadProperty(name, FriendlyType(declaredType), "Maximum inspection depth reached", source, PropertyStatus.Truncated);
                reference = new CadObjectRef(rootReference.HostName, rootReference.DocumentId, rootReference.Handle,
                    rootReference.NativeId, FriendlyType(type), path, rootReference.Layer);
            }
            var isCollection = value is IEnumerable && !(value is string);
            return new CadProperty(name, FriendlyType(type), isCollection ? "<Collection>" : "<" + FriendlyType(type) + ">",
                source, PropertyStatus.Available, reference);
        }

        private static CadProperty CreateDeferredProperty(PropertyInfo member, CadObjectRef rootReference, ReadOptions options)
        {
            var path = AppendProperty(rootReference.MemberPath, member.Name);
            if (path.Split('/').Length > options.MaxDepth)
                return new CadProperty(member.Name, FriendlyType(member.PropertyType), "Maximum inspection depth reached",
                    "Reflection", PropertyStatus.Truncated);
            var reference = new CadObjectRef(rootReference.HostName, rootReference.DocumentId, rootReference.Handle,
                rootReference.NativeId, FriendlyType(member.PropertyType), path, rootReference.Layer);
            return new CadProperty(member.Name, FriendlyType(member.PropertyType), "Click to evaluate",
                "Reflection", PropertyStatus.NotEvaluated, reference);
        }

        private static object? GetItem(IEnumerable enumerable, int index)
        {
            if (index < 0) throw new InvalidOperationException("Collection index is invalid.");
            if (enumerable is IList list) return index < list.Count ? list[index] : null;
            var position = 0;
            foreach (var item in enumerable) if (position++ == index) return item;
            return null;
        }

        private static void AddTruncationMarker(IList<CadProperty> result, int total, int maximum)
        {
            if (total > maximum && result.Count >= maximum)
                result.Add(new CadProperty("…", "", "Property limit reached", "Reflection", PropertyStatus.Truncated));
        }

        public static bool IsSimple(Type type) => type.IsPrimitive || type.IsEnum || type == typeof(string) ||
            type == typeof(decimal) || type == typeof(DateTime) || type == typeof(DateTimeOffset) ||
            type == typeof(Guid) || type == typeof(TimeSpan) || type == typeof(Type);

        private static string FriendlyType(Type type) => type.FullName ?? type.Name;

        private static string FormatMethod(MethodInfo method)
        {
            var parameters = string.Join(", ", method.GetParameters().Select(parameter =>
                FriendlyType(parameter.ParameterType) + " " + parameter.Name));
            return FriendlyType(method.ReturnType) + " " + method.Name + "(" + parameters + ")";
        }

        private static string FormatValue(object value)
        {
            try
            {
                if (value is string) return (string)value;
                if (value is IFormattable formattable) return formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty;
                return value.ToString() ?? string.Empty;
            }
            catch (Exception ex) { return "<ToString failed: " + ex.Message + ">"; }
        }
    }
}
