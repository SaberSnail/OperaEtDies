using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using ModEditor.Model;

namespace ModEditor.ViewModels.Form;

/// <summary>
/// A mod operation (see Docs/Mods.md): its op and path, and the properties that op takes. A value is edited with a
/// field for the kind of data the path leads to, when that's known, and as JSON otherwise. The fields after the path
/// change when the op or the path does.
/// </summary>
public sealed class OperationFieldViewModel : FieldViewModel
{
	public OperationFieldViewModel(FormViewModel form, ValueSlot slot)
		: base(form, "", slot, isRequired: true, place: null)
	{
		Fields = [];
		if (slot.Get() is not JsonObject)
			slot.Set(new JsonObject());
		var operation = (JsonObject) slot.Get()!;
		Fields.Add(new ChoiceFieldViewModel(form, "op", new PropertySlot(operation, "op"), isRequired: true, ModOperations.Names, OperationPlace("op")));
		// The suggestions follow the path typed so far. They're only some of what a path can be, so it's checked against
		// the shape of the data instead.
		Fields.Add(new TextFieldViewModel(form, "path", new PropertySlot(operation, "path"), isRequired: true,
			() => form.Catalog.GetPathSuggestions(ModOperations.GetString(operation, "path") ?? ""), isColor: false, isMultiline: false,
			OperationPlace("path"), check: x => DataSchema.IsKnownPath(x) ? null : $"\"{x}\" doesn't lead to data the game reads."));
		RememberValue(operation);
		BuildDetails();
	}

	public ObservableCollection<FieldViewModel> Fields { get; }

	public override void Refresh()
	{
		var operation = (JsonObject) Slot.Get()!;
		var path = ModOperations.GetString(operation, "path");
		if (m_isApplyingValue)
		{
			// The value set for a new path was reported, which comes back here; the fields are already up to date.
		}
		else if (path != m_path)
		{
			// The value is worked out again from the one the operation had before its path was changed, so it isn't lost
			// while a path is typed, and is narrowed if the new path goes further into it.
			if (ApplyRememberedValue(operation, path))
			{
				m_path = path;
				BuildDetails();
				m_isApplyingValue = true;
				try
				{
					Changed();
				}
				finally
				{
					m_isApplyingValue = false;
				}

				return;
			}

			BuildDetails();
		}
		else if (ModOperations.GetString(operation, "op") != m_op)
		{
			BuildDetails();
		}
		else
		{
			// Anything else that changed was the value (or a property beside it), which is what a new path starts from.
			RememberValue(operation);
		}

		foreach (var field in Fields)
			field.Refresh();
	}

	/// <summary>
	/// Remembers the value and the path it's the data of, which the value is worked out from when the path changes.
	/// </summary>
	private void RememberValue(JsonObject operation)
	{
		m_rememberedPath = ModOperations.GetString(operation, "path");
		m_rememberedValue = ModJson.FindName(operation, "value") is { } name ? ModJson.Clone(operation[name]) : null;
		m_hasRememberedValue = ModJson.FindName(operation, "value") is not null;
	}

	/// <summary>
	/// Sets the value for a new path: the remembered value, or the part of it the new path leads to, if the new path goes
	/// further into the data the remembered value was of (for a replace or set). Returns whether the value changed.
	/// </summary>
	private bool ApplyRememberedValue(JsonObject operation, string? path)
	{
		if (!m_hasRememberedValue)
			return false;
		var value = m_rememberedValue;
		if (ModOperations.GetString(operation, "op")?.ToLowerInvariant() is "replace" or "set" && m_rememberedPath is not null && path is not null &&
			FindWithin(m_rememberedValue, m_rememberedPath, path) is { } narrowed)
		{
			value = narrowed;
		}

		var name = ModJson.FindName(operation, "value") ?? "value";
		if (JsonNode.DeepEquals(operation[name], value))
			return false;
		operation[name] = ModJson.Clone(value);
		return true;
	}

	/// <summary>
	/// Replaces the fields after the op and path with the ones the op takes.
	/// </summary>
	private void BuildDetails()
	{
		var operation = (JsonObject) Slot.Get()!;
		m_op = ModOperations.GetString(operation, "op");
		m_path = ModOperations.GetString(operation, "path");
		while (Fields.Count > 2)
			Fields.RemoveAt(Fields.Count - 1);

		var op = m_op?.ToLowerInvariant();
		var path = m_path ?? "";
		var pathType = DataSchema.GetPathType(path);
		var (section, itemId) = DataSchema.GetPathItem(path);
		var propertyId = string.Equals(section, "properties", StringComparison.OrdinalIgnoreCase) ? itemId : null;
		var shown = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "op", "path" };

		// A value's fields can copy the paths of the data they'll change.
		void Add(string name, DataType? type, bool isRequired, string? dataPath)
		{
			shown.Add(name);
			var slot = new PropertySlot(operation, name);
			var place = new FieldPlace(0, dataPath, IsCopyable: false, HelpTopics.ForOperation(name));
			Fields.Add(type is null
				? new JsonFieldViewModel(Form, name, slot, isRequired, place: place)
				: FieldFactory.Create(Form, name, type, slot, isRequired, reference: null, propertyId, place: place));
		}

		void AddPlacement()
		{
			var ids = new Func<IReadOnlyList<string>>(() => Form.Catalog.GetListIds(op == "insert" ? path : ParentPath(path)));
			Fields.Add(new NumberFieldViewModel(Form, "index", new PropertySlot(operation, "index"), isRequired: false, isInteger: true, OperationPlace("index")));
			// The ids only come from the game data, so another id (such as one the mod adds) isn't marked.
			Fields.Add(new TextFieldViewModel(Form, "before", new PropertySlot(operation, "before"), isRequired: false, ids, isColor: false, isMultiline: false, OperationPlace("before"), check: _ => null));
			Fields.Add(new TextFieldViewModel(Form, "after", new PropertySlot(operation, "after"), isRequired: false, ids, isColor: false, isMultiline: false, OperationPlace("after"), check: _ => null));
			shown.UnionWith(["index", "before", "after"]);
		}

		switch (op)
		{
		case "insert":
			var element = pathType is { Kind: DataKind.List } ? pathType.Element : null;
			// An inserted item is found by its id once it's there.
			var inserted = ModJson.FindName(operation, "value") is { } valueName ? operation[valueName] : null;
			Add("value", element, isRequired: false, ModJson.GetId(inserted) is null ? null : ModEditing.AppendItem(path, inserted, 0));
			Add("values", element is null ? null : new DataType(DataKind.List, typeof(List<>).MakeGenericType(element.ClrType), element), isRequired: false, path);
			AddPlacement();
			break;
		case "replace" or "set":
			Add("value", pathType, isRequired: true, path);
			break;
		case "move":
			AddPlacement();
			break;
		case "copy":
			Add("changes", null, isRequired: false, null);
			AddPlacement();
			break;
		}

		// Anything else the operation has is shown as JSON, so it can be seen and removed.
		foreach (var name in operation.Select(x => x.Key).Where(x => !shown.Contains(x)).ToList())
			Fields.Add(new JsonFieldViewModel(Form, name, new PropertySlot(operation, name), isRequired: false, isUnknown: true));
	}

	/// <summary>
	/// Finds the part of a value that a longer path leads to, such as its "description" when the value is the data of
	/// "houses[cottage]" and the path is "houses[cottage].description". Returns null if the path doesn't go further into
	/// the value's data, or the value doesn't have that part.
	/// </summary>
	private static JsonNode? FindWithin(JsonNode? value, string valuePath, string path)
	{
		var valueSteps = DataSchema.ParsePath(valuePath);
		var steps = DataSchema.ParsePath(path);
		if (steps.Count <= valueSteps.Count || !valueSteps.SequenceEqual(steps.Take(valueSteps.Count)))
			return null;

		var node = value;
		foreach (var (text, isIndex) in steps.Skip(valueSteps.Count))
		{
			node = node switch
			{
				JsonArray list when isIndex && int.TryParse(text, out var index) => index >= 0 && index < list.Count ? list[index] : null,
				JsonArray list when isIndex => list.FirstOrDefault(x => ModJson.GetId(x) == text),
				JsonObject obj => ModJson.FindName(obj, text) is { } name ? obj[name] : null,
				_ => null,
			};
			if (node is null)
				return null;
		}

		return node;
	}

	// The place of one of the operation's own properties, which has no path of its own but has help.
	private static FieldPlace OperationPlace(string name) => new(0, null, Help: HelpTopics.ForOperation(name));

	// The list an item's path is in, such as "properties[hair].values" for "properties[hair].values[red]".
	private static string ParentPath(string path)
	{
		var index = path.LastIndexOf('[');
		return index > 0 && path.EndsWith(']') ? path[..index] : path;
	}

	private string? m_op;
	private string? m_path;

	// The value the operation had before its path was last changed, and the path it was the data of.
	private JsonNode? m_rememberedValue;
	private string? m_rememberedPath;
	private bool m_hasRememberedValue;
	private bool m_isApplyingValue;
}
