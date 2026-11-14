using System.Text;
using System.Text.RegularExpressions;

namespace DbLive.Schema;

public sealed record SchemaObjectChange
{
	internal SchemaObjectChange(SchemaObject before, SchemaObject after)
	{
		Before = before;
		After = after;
	}

	public SchemaObject Before { get; }

	public SchemaObject After { get; }
}

/// <summary>
/// The difference between two snapshots of one database: objects missing (in the earlier snapshot only),
/// extra (in the later one only) and changed (in both, with different definitions).
/// </summary>
public sealed partial class SchemaDiff
{
	// longer definitions are cut in the text; a changed one is shown around its first difference.
	private const int MaxDefinitionLength = 160;
	private const int ExcerptContextLength = 40;

	private SchemaDiff(IReadOnlyList<SchemaObject> missing, IReadOnlyList<SchemaObject> extra, IReadOnlyList<SchemaObjectChange> changed)
	{
		Missing = missing;
		Extra = extra;
		Changed = changed;
	}

	public IReadOnlyList<SchemaObject> Missing { get; }

	public IReadOnlyList<SchemaObject> Extra { get; }

	public IReadOnlyList<SchemaObjectChange> Changed { get; }

	public bool IsEmpty => Missing.Count == 0 && Extra.Count == 0 && Changed.Count == 0;

	public static SchemaDiff Compare(SchemaSnapshot before, SchemaSnapshot after)
	{
		List<SchemaObject> missing = [];
		List<SchemaObjectChange> changed = [];

		foreach (SchemaObject beforeObject in before.Objects)
		{
			SchemaObject? afterObject = after.Find(beforeObject.Type, beforeObject.Name);

			if (afterObject is null)
			{
				missing.Add(beforeObject);
			}
			else if (!string.Equals(beforeObject.Definition, afterObject.Definition, StringComparison.Ordinal))
			{
				changed.Add(new SchemaObjectChange(beforeObject, afterObject));
			}
		}

		List<SchemaObject> extra = after.Objects.Where(o => before.Find(o.Type, o.Name) is null).ToList();

		return new SchemaDiff(
			Sort(missing),
			Sort(extra),
			changed.OrderBy(c => c.Before.Name, StringComparer.Ordinal).ThenBy(c => c.Before.Type).ToList()
		);
	}

	/// <summary>
	/// One line per difference. The objects of a missing or extra schema, table or view are counted on its line, not listed.
	/// </summary>
	public override string ToString()
	{
		StringBuilder text = new();

		AppendMissingOrExtra(text, "missing", Missing);
		AppendMissingOrExtra(text, "extra", Extra);

		foreach (SchemaObjectChange change in Changed)
		{
			AppendChange(text, change);
		}

		return text.ToString().TrimEnd();
	}

	private static List<SchemaObject> Sort(IEnumerable<SchemaObject> objects)
		=> objects.OrderBy(o => o.Name, StringComparer.Ordinal).ThenBy(o => o.Type).ToList();

	private static bool IsContainer(SchemaObjectType type)
		=> type is SchemaObjectType.Schema or SchemaObjectType.Table or SchemaObjectType.View or SchemaObjectType.MaterializedView;

	private static void AppendMissingOrExtra(StringBuilder text, string label, IReadOnlyList<SchemaObject> objects)
	{
		HashSet<string> containers = objects.Where(o => IsContainer(o.Type)).Select(o => o.Name).ToHashSet(StringComparer.Ordinal);
		ILookup<string, SchemaObject> children = objects.Where(o => o.Parent is not null).ToLookup(o => o.Parent!, StringComparer.Ordinal);

		foreach (SchemaObject obj in objects)
		{
			if (obj.Parent is not null && containers.Contains(obj.Parent))
			{
				continue;
			}

			_ = text.Append($"  {label} {Describe(obj.Type)} {obj.Name}");

			string definition = ForDisplay(obj.Definition);
			if (definition.Length > 0)
			{
				_ = text.Append($": {Cut(definition)}");
			}

			if (IsContainer(obj.Type))
			{
				List<SchemaObject> content = [];
				CollectContent(obj, children, content);

				if (content.Count > 0)
				{
					IEnumerable<string> counts = content
						.GroupBy(o => o.Type)
						.OrderBy(g => g.Key)
						.Select(g => $"{g.Count()} {Describe(g.Key, plural: g.Count() > 1)}");

					_ = text.Append($" (with {string.Join(", ", counts)})");
				}
			}

			_ = text.AppendLine();
		}
	}

	private static void CollectContent(SchemaObject container, ILookup<string, SchemaObject> children, List<SchemaObject> content)
	{
		foreach (SchemaObject child in children[container.Name])
		{
			content.Add(child);

			if (IsContainer(child.Type))
			{
				CollectContent(child, children, content);
			}
		}
	}

	private static void AppendChange(StringBuilder text, SchemaObjectChange change)
	{
		string before = ForDisplay(change.Before.Definition);
		string after = ForDisplay(change.After.Definition);

		_ = text.AppendLine($"  changed {Describe(change.Before.Type)} {change.Before.Name}");

		if (before.Length <= MaxDefinitionLength && after.Length <= MaxDefinitionLength)
		{
			_ = text.AppendLine($"    was: {before}");
			_ = text.AppendLine($"    now: {after}");
			return;
		}

		int firstDifference = 0;
		while (firstDifference < before.Length && firstDifference < after.Length && before[firstDifference] == after[firstDifference])
		{
			firstDifference++;
		}

		if (firstDifference == before.Length && firstDifference == after.Length)
		{
			_ = text.AppendLine("    the definitions differ in whitespace only");
			return;
		}

		_ = text.AppendLine($"    was: {Excerpt(before, firstDifference)}");
		_ = text.AppendLine($"    now: {Excerpt(after, firstDifference)}");
	}

	private static string Excerpt(string definition, int position)
	{
		int start = Math.Max(0, position - ExcerptContextLength);
		int length = Math.Min(MaxDefinitionLength, definition.Length - start);

		string prefix = start > 0 ? "..." : "";
		string suffix = start + length < definition.Length ? "..." : "";

		return prefix + definition.Substring(start, length) + suffix;
	}

	private static string Cut(string definition)
		=> definition.Length <= MaxDefinitionLength ? definition : definition[..MaxDefinitionLength] + "...";

	private static string ForDisplay(string definition)
		=> SqlText.CollapseWhitespace(definition);

	private static string Describe(SchemaObjectType type, bool plural = false)
	{
		string name = WordBoundaryRegex().Replace(type.ToString(), " ").ToLowerInvariant();

		if (!plural)
		{
			return name;
		}

		return name.EndsWith('x') ? name + "es" : name + "s";
	}

	[GeneratedRegex("(?<=[a-z])(?=[A-Z])")]
	private static partial Regex WordBoundaryRegex();
}
