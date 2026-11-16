using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModEditor.Model;
using OperaEtDies;
using OperaEtDies.Data;

namespace ModEditor.ViewModels.Form;

/// <summary>
/// Where a field is in its form: how deeply it's nested in objects, lists, and maps, and the path that finds its data
/// from a mod operation (null if it has none, such as the properties of an operation itself). A path that isn't
/// copyable (such as an operation's value, whose path is the operation's own) is only there for its fields. The help
/// explains the field: a property's row in its class's table, which its list items and map values share.
/// </summary>
public sealed record FieldPlace(int Depth, string? DataPath, bool IsCopyable = true, HelpTarget? Help = null)
{
	public static FieldPlace Top { get; } = new(0, null);

	/// <summary>
	/// The place of a property of the object at this place.
	/// </summary>
	public FieldPlace Property(string name) => new(Depth + 1, ModEditing.AppendName(DataPath, name), Help: Help);

	/// <summary>
	/// The place of an item of the list at this place, found by its id if it has one, or else by its index.
	/// </summary>
	public FieldPlace Item(JsonNode? item, int index) => new(Depth + 1, ModEditing.AppendItem(DataPath, item, index), Help: Help);
}

/// <summary>
/// A field in a form, editing the value in one slot of the JSON.
/// </summary>
public abstract class FieldViewModel : ViewModelBase
{
	protected FieldViewModel(FormViewModel form, string label, ValueSlot slot, bool isRequired, FieldPlace? place)
	{
		Form = form;
		Label = label;
		Slot = slot;
		IsRequired = isRequired;
		Place = place ?? FieldPlace.Top;
	}

	/// <summary>
	/// How far in a field's editor starts from its name's left edge at the top level. Every editor starts at the same
	/// place, so a nested field's name has less room, by <see cref="IndentWidth"/> for each level.
	/// </summary>
	public const double EditorStart = 200;

	/// <summary>
	/// How far the fields of an object, list, or map are indented from it.
	/// </summary>
	public const double IndentWidth = 14;

	public FormViewModel Form { get; }

	/// <summary>
	/// The property's name, a list item's number, or empty for a map value, which is shown beside its key.
	/// </summary>
	public string Label { get; }

	public bool HasLabel => Label.Length != 0;

	public bool IsRequired { get; }

	public bool IsEditable => !Form.IsReadOnly;

	/// <summary>
	/// Whether the property isn't one the game reads there, which is an error when the data is loaded.
	/// </summary>
	public virtual bool IsUnknown => false;

	public FieldPlace Place { get; }

	/// <summary>
	/// The width of the field's name (with its copy button), so its editor lines up with the others.
	/// </summary>
	public double LabelWidth => Math.Max(EditorStart - (IndentWidth * Place.Depth), 48);

	/// <summary>
	/// The path that finds the field's data from a mod operation, such as "houses[cottage].description.short", or null.
	/// </summary>
	public string? DataPath => Place.DataPath;

	public bool CanCopyPath() => DataPath is not null && Place.IsCopyable;

	/// <summary>
	/// Whether the field's path can be copied.
	/// </summary>
	public bool HasCopyablePath => CanCopyPath();

	/// <summary>
	/// Where the help explains the field, or null if it doesn't.
	/// </summary>
	public HelpTarget? Help => Place.Help;

	public bool CanShowHelp() => Help is not null;

	public void ShowHelp()
	{
		if (Help is { } help)
			Form.ShowHelp(help);
	}

	/// <summary>
	/// Whether the field's name has a menu (to copy its path or show its help).
	/// </summary>
	public bool HasMenu => HasCopyablePath || Help is not null || CanGoToModChange();

	/// <summary>
	/// What the mod does to the field's data, in a form showing the game data: adds or changes it (or something in it),
	/// or null if nothing (or the form isn't showing the game data).
	/// </summary>
	public ChangeKind? Change => DataPath is { } path ? Form.Changes?.GetChange(path) : null;

	/// <summary>
	/// The mark shown before a changed field's name.
	/// </summary>
	public string ChangeGlyph => Change switch
	{
		ChangeKind.Added => "✚",
		ChangeKind.Changed => "✎",
		_ => "",
	};

	public string? ChangeText => Change switch
	{
		ChangeKind.Added => "The mod adds this.",
		ChangeKind.Changed => "The mod changes this, or something in it (see the Changes tab).",
		_ => null,
	};

	public bool CanGoToModChange() => DataPath is { } path && Form.Changes?.GetSource(path) is not null;

	/// <summary>
	/// Shows the part of the mod that last changed the field's data (or something in it) in the Mod tab.
	/// </summary>
	public void GoToModChange()
	{
		if (DataPath is { } path && Form.Changes is { } changes && changes.GetSource(path) is { } source)
			changes.GoTo(source);
	}

	/// <summary>
	/// The tooltip of the field's name: what the mod does to it, what the help says about it (its type and notes),
	/// and its path.
	/// </summary>
	public string? ToolTipText =>
		string.Join(Environment.NewLine + Environment.NewLine, new[] { ChangeText, HelpLibrary.GetRowText(Help), HasCopyablePath ? $"Path: {DataPath}" : null }.OfType<string>()) is { Length: > 0 } text ? text : null;

	public void CopyPath()
	{
		if (DataPath is null)
			return;
		try
		{
			System.Windows.Clipboard.SetText(DataPath);
		}
		catch (System.Runtime.InteropServices.COMException)
		{
			// The clipboard can be held by another program for a moment.
		}
	}

	protected ValueSlot Slot { get; }

	/// <summary>
	/// Updates anything that depends on other fields, such as suggestions.
	/// </summary>
	public virtual void Refresh()
	{
	}

	protected void Changed() => Form.NotifyChanged();

	/// <summary>
	/// Sets the value, or removes it if it's empty and the field is optional.
	/// </summary>
	protected void SetOrRemove(JsonNode? value, bool isEmpty)
	{
		if (isEmpty && !IsRequired && Slot is PropertySlot)
			Slot.Remove();
		else
			Slot.Set(value);
		Changed();
	}
}

/// <summary>
/// Text, with suggestions when it refers to something by id (or has a known set of values, such as a frequency).
/// </summary>
public sealed class TextFieldViewModel : FieldViewModel
{
	public TextFieldViewModel(FormViewModel form, string label, ValueSlot slot, bool isRequired, Func<IReadOnlyList<string>>? getSuggestions, bool isColor, bool isMultiline, FieldPlace? place = null, Func<string, string?>? check = null)
		: base(form, label, slot, isRequired, place)
	{
		m_check = check;
		m_getSuggestions = getSuggestions;
		m_isColor = isColor;
		IsMultiline = isMultiline;
		m_text = slot.Get() switch
		{
			JsonValue value when value.GetValueKind() == JsonValueKind.String => value.GetValue<string>(),
			null => "",
			var other => other.ToJsonString(),
		};
		m_suggestions = getSuggestions?.Invoke() ?? [];
	}

	public string Text
	{
		get => VerifyAccess(m_text);
		set
		{
			if (SetPropertyField(value, ref m_text))
			{
				SetOrRemove(JsonValue.Create(value), value.Length == 0);
				RaisePropertyChanged(nameof(Warning));
			}
		}
	}

	public bool HasSuggestions => m_getSuggestions is not null;

	public IReadOnlyList<string> Suggestions
	{
		get => VerifyAccess(m_suggestions);
		private set => SetPropertyField(value, ref m_suggestions);
	}

	public bool IsMultiline { get; }

	/// <summary>
	/// Says what's wrong with the text, or null if it's fine: what the field's own check finds, if it has one, or else
	/// that the text isn't one of the suggestions.
	/// </summary>
	public string? Warning =>
		m_check is not null ? (m_text.Length == 0 ? null : m_check(m_text)) :
		HasSuggestions && m_text.Length != 0 && m_suggestions.Count != 0 && !m_suggestions.Contains(m_text) && !(m_isColor && m_text.StartsWith('#'))
			? $"\"{m_text}\" isn't one of the known values."
			: null;

	public override void Refresh()
	{
		if (m_getSuggestions is not null)
		{
			var suggestions = m_getSuggestions();
			if (!suggestions.SequenceEqual(m_suggestions))
			{
				Suggestions = suggestions;
				RaisePropertyChanged(nameof(Warning));
			}
		}
	}

	private readonly Func<IReadOnlyList<string>>? m_getSuggestions;
	private readonly bool m_isColor;
	private readonly Func<string, string?>? m_check;
	private string m_text;
	private IReadOnlyList<string> m_suggestions;
}

/// <summary>
/// A whole number or a number.
/// </summary>
public sealed class NumberFieldViewModel : FieldViewModel
{
	public NumberFieldViewModel(FormViewModel form, string label, ValueSlot slot, bool isRequired, bool isInteger, FieldPlace? place = null)
		: base(form, label, slot, isRequired, place)
	{
		m_isInteger = isInteger;
		m_text = slot.Get() is JsonValue value ? value.ToJsonString().Trim('"') : "";
	}

	public string Text
	{
		get => VerifyAccess(m_text);
		set
		{
			if (!SetPropertyField(value, ref m_text))
				return;
			var text = value.Trim();
			if (text.Length == 0)
			{
				Error = IsRequired ? "A number is required." : null;
				if (!IsRequired)
					SetOrRemove(null, isEmpty: true);
			}
			else if (m_isInteger && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
			{
				Error = null;
				SetOrRemove(JsonValue.Create(integer), isEmpty: false);
			}
			else if (!m_isInteger && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number))
			{
				Error = null;
				SetOrRemove(JsonValue.Create(number), isEmpty: false);
			}
			else
			{
				Error = m_isInteger ? "Not a whole number." : "Not a number.";
			}
		}
	}

	public string? Error
	{
		get => VerifyAccess(m_error);
		private set => SetPropertyField(value, ref m_error);
	}

	private readonly bool m_isInteger;
	private string m_text;
	private string? m_error;
}

/// <summary>
/// True or false, or left out when it's optional.
/// </summary>
public sealed class BooleanFieldViewModel(FormViewModel form, string label, ValueSlot slot, bool isRequired, FieldPlace? place = null)
	: FieldViewModel(form, label, slot, isRequired, place)
{
	public bool IsThreeState => !IsRequired;

	public bool? Value
	{
		get => VerifyAccess(m_value);
		set
		{
			if (SetPropertyField(value, ref m_value))
				SetOrRemove(value is { } flag ? JsonValue.Create(flag) : null, value is null);
		}
	}

	private bool? m_value = slot.Get() is JsonValue value && value.GetValueKind() is JsonValueKind.True or JsonValueKind.False ? value.GetValue<bool>() : null;
}

/// <summary>
/// One of a fixed set of names. An optional one can be left out (the empty choice).
/// </summary>
public sealed class ChoiceFieldViewModel : FieldViewModel
{
	public ChoiceFieldViewModel(FormViewModel form, string label, ValueSlot slot, bool isRequired, IReadOnlyList<string> choices, FieldPlace? place = null)
		: base(form, label, slot, isRequired, place)
	{
		var current = slot.Get() is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : "";
		m_selected = choices.FirstOrDefault(x => string.Equals(x, current, StringComparison.OrdinalIgnoreCase)) ?? current;
		var all = isRequired ? choices : choices.Prepend("");
		Choices = (m_selected.Length != 0 && !choices.Contains(m_selected) ? all.Append(m_selected) : all).ToList();
	}

	public IReadOnlyList<string> Choices { get; }

	public string Selected
	{
		get => VerifyAccess(m_selected);
		set
		{
			if (SetPropertyField(value ?? "", ref m_selected))
				SetOrRemove(JsonValue.Create(m_selected), m_selected.Length == 0);
		}
	}

	private string m_selected;
}

/// <summary>
/// Any value, edited as JSON, for values the form doesn't know the shape of.
/// </summary>
public sealed class JsonFieldViewModel : FieldViewModel
{
	public JsonFieldViewModel(FormViewModel form, string label, ValueSlot slot, bool isRequired, bool isUnknown = false, FieldPlace? place = null)
		: base(form, label, slot, isRequired, place)
	{
		IsUnknown = isUnknown;
		m_text = slot.Exists ? ModJson.Format(slot.Get()) : "";
	}

	public override bool IsUnknown { get; }

	public string Text
	{
		get => VerifyAccess(m_text);
		set
		{
			if (!SetPropertyField(value, ref m_text))
				return;
			if (value.Trim().Length == 0 && !IsRequired)
			{
				Error = null;
				SetOrRemove(null, isEmpty: true);
				return;
			}

			var (json, error) = ModJson.Parse(value);
			Error = error;
			if (error is null)
				SetOrRemove(json, isEmpty: false);
		}
	}

	public string? Error
	{
		get => VerifyAccess(m_error);
		private set => SetPropertyField(value, ref m_error);
	}

	private string m_text;
	private string? m_error;
}

/// <summary>
/// An object with a field for each property the game reads, followed by any it doesn't know (as JSON). An optional
/// object that's left out can be added.
/// </summary>
public sealed class ObjectFieldViewModel : FieldViewModel
{
	public ObjectFieldViewModel(FormViewModel form, string label, DataType type, ValueSlot slot, bool isRequired, bool isRoot, string? propertyId, FieldPlace? place = null)
		: base(form, label, slot, isRequired, place)
	{
		m_type = type;
		m_propertyId = propertyId;
		IsRoot = isRoot;
		Fields = [];
		BuildFields();
	}

	/// <summary>
	/// Whether the object is the whole form, which is shown without a heading or frame.
	/// </summary>
	public bool IsRoot { get; }

	public ObservableCollection<FieldViewModel> Fields { get; }

	public bool IsPresent => Slot.Get() is JsonObject;

	/// <summary>
	/// Whether the value is there but isn't an object, so it can only be fixed as JSON.
	/// </summary>
	public bool IsInvalid => Slot.Exists && Slot.Get() is not null and not JsonObject;

	public bool CanAdd() => IsEditable && !IsPresent;

	public void Add()
	{
		if (!CanAdd())
			return;
		Slot.Set(new JsonObject());
		BuildFields();
		Changed();
	}

	public bool CanRemoveValue() => IsEditable && IsPresent && IsOptional;

	/// <summary>
	/// Whether the object can be left out, which only a property that isn't required can be (not a list item or map value).
	/// </summary>
	public bool IsOptional => !IsRequired && !IsRoot && Slot is PropertySlot;

	public void RemoveValue()
	{
		if (!CanRemoveValue())
			return;
		Slot.Remove();
		BuildFields();
		Changed();
	}

	public override void Refresh()
	{
		foreach (var field in Fields)
			field.Refresh();
	}

	private void BuildFields()
	{
		Fields.Clear();
		if (Slot.Get() is JsonObject obj)
		{
			foreach (var field in m_type.Fields)
				Fields.Add(FieldFactory.Create(Form, field.Name, field.Type, new PropertySlot(obj, field.Name), field.IsRequired, DataReferences.Find(field), m_propertyId, place: Place.Property(field.Name) with { Help = HelpTopics.ForProperty(m_type.ClrType, field.Name) }));

			var known = m_type.Fields.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
			foreach (var name in obj.Select(x => x.Key).Where(x => !known.Contains(x)).ToList())
				Fields.Add(new JsonFieldViewModel(Form, name, new PropertySlot(obj, name), isRequired: false, isUnknown: true, Place.Property(name) with { Help = null }));
		}

		RaisePropertyChanged(nameof(IsPresent), nameof(IsInvalid));
	}

	private readonly DataType m_type;
	private readonly string? m_propertyId;
}

/// <summary>
/// A list, with a field for each item.
/// </summary>
public sealed class ListFieldViewModel : FieldViewModel
{
	public ListFieldViewModel(FormViewModel form, string label, DataType type, ValueSlot slot, bool isRequired, FieldReference? reference, string? propertyId, Func<string?>? getPropertyId, FieldPlace? place = null)
		: base(form, label, slot, isRequired, place)
	{
		m_element = type.Element!;
		m_reference = reference;
		m_propertyId = propertyId;
		m_getPropertyId = getPropertyId;
		Items = [];

		// A single string can stand for a list of one, and is edited as one.
		if (slot.Get() is JsonValue single)
			slot.Set(new JsonArray(single.DeepClone()));
		BuildItems();
	}

	public ObservableCollection<ListItemViewModel> Items { get; }

	public bool IsPresent => Slot.Get() is JsonArray;

	public bool CanAdd() => IsEditable;

	public void Add()
	{
		if (!CanAdd())
			return;
		if (Slot.Get() is not JsonArray list)
			Slot.Set(list = []);
		list.Add(FieldFactory.CreateDefault(m_element));
		BuildItems();
		Changed();
	}

	public void RemoveItem(ListItemViewModel item)
	{
		if (!IsEditable || Slot.Get() is not JsonArray list || item.Index >= list.Count)
			return;
		list.RemoveAt(item.Index);
		if (list.Count == 0 && !IsRequired)
			Slot.Remove();
		BuildItems();
		Changed();
	}

	/// <summary>
	/// Whether an item can move by the offset (-1 for up, 1 for down) and stay in the list.
	/// </summary>
	public bool CanMoveItem(ListItemViewModel item, int offset) => IsEditable && item.Index + offset >= 0 && item.Index + offset < Items.Count;

	/// <summary>
	/// Moves an item up (-1) or down (1) in the list, swapping it with the one beside it.
	/// </summary>
	public void MoveItem(ListItemViewModel item, int offset)
	{
		if (!CanMoveItem(item, offset) || Slot.Get() is not JsonArray list)
			return;
		var value = list[item.Index];
		list.RemoveAt(item.Index);
		list.Insert(item.Index + offset, value);
		BuildItems();
		Changed();
	}

	public override void Refresh()
	{
		foreach (var item in Items)
			item.Field.Refresh();
	}

	private void BuildItems()
	{
		Items.Clear();
		if (Slot.Get() is JsonArray list)
		{
			for (var index = 0; index < list.Count; index++)
			{
				// Each item is labeled with its number, so its editor lines up with the others.
				var label = (index + 1).ToString(CultureInfo.CurrentCulture);
				var field = FieldFactory.Create(Form, label, m_element, new ItemSlot(list, index), isRequired: true, m_reference, m_propertyId, getPropertyId: m_getPropertyId, place: Place.Item(list[index], index));
				Items.Add(new ListItemViewModel(this, index, field));
			}
		}

		RaisePropertyChanged(nameof(IsPresent));
	}

	private readonly DataType m_element;
	private readonly FieldReference? m_reference;
	private readonly string? m_propertyId;
	private readonly Func<string?>? m_getPropertyId;
}

public sealed class ListItemViewModel(ListFieldViewModel list, int index, FieldViewModel field)
{
	public int Index { get; } = index;

	public FieldViewModel Field { get; } = field;

	public bool IsEditable => Field.IsEditable;

	public void Remove() => list.RemoveItem(this);

	public bool CanMoveUp() => list.CanMoveItem(this, -1);

	public void MoveUp() => list.MoveItem(this, -1);

	public bool CanMoveDown() => list.CanMoveItem(this, 1);

	public void MoveDown() => list.MoveItem(this, 1);
}

/// <summary>
/// An object whose property names are keys, with a field for each value.
/// </summary>
public sealed class MapFieldViewModel : FieldViewModel
{
	public MapFieldViewModel(FormViewModel form, string label, DataType type, ValueSlot slot, bool isRequired, FieldReference? reference, string? propertyId, FieldPlace? place = null)
		: base(form, label, slot, isRequired, place)
	{
		m_element = type.Element!;
		m_reference = reference;
		m_propertyId = propertyId;
		m_parent = slot.ParentObject;
		Entries = [];
		BuildEntries();
	}

	public ObservableCollection<MapEntryViewModel> Entries { get; }

	public bool HasKeySuggestions => m_reference?.Key is not null;

	public bool CanAdd() => IsEditable;

	/// <summary>
	/// Adds an entry, with the first suggested key that isn't used yet.
	/// </summary>
	public void Add()
	{
		if (!CanAdd())
			return;
		if (Slot.Get() is not JsonObject map)
			Slot.Set(map = []);
		var key = GetKeySuggestions().FirstOrDefault(x => ModJson.FindName(map, x) is null) ?? ModEditing.MakeUnique("key", x => ModJson.FindName(map, x) is not null);
		map[key] = FieldFactory.CreateDefault(m_element);
		BuildEntries();
		Changed();
	}

	public void RemoveEntry(MapEntryViewModel entry)
	{
		if (!IsEditable || Slot.Get() is not JsonObject map)
			return;
		map.Remove(entry.Key);
		if (map.Count == 0 && !IsRequired)
			Slot.Remove();
		BuildEntries();
		Changed();
	}

	/// <summary>
	/// Renames an entry's key, keeping its place. Returns false if the key is empty or already used.
	/// </summary>
	public bool RenameEntry(MapEntryViewModel entry, string key)
	{
		if (!IsEditable || Slot.Get() is not JsonObject map || key.Length == 0 || (ModJson.FindName(map, key) is not null && !string.Equals(key, entry.Key, StringComparison.OrdinalIgnoreCase)))
			return false;
		var entries = map.ToList();
		map.Clear();
		foreach (var (name, value) in entries)
			map[name == entry.Key ? key : name] = value;
		BuildEntries();
		Changed();
		return true;
	}

	public IReadOnlyList<string> GetKeySuggestions()
	{
		if (m_reference?.Key is not { } source)
			return [];
		var propertyId = source is PropertyValueIds { SiblingField: { } sibling } && m_parent is { } parent && ModJson.FindName(parent, sibling) is { } name && parent[name] is JsonValue value && value.TryGetValue<string>(out var id) ? id : m_propertyId;
		var suggestions = Form.Catalog.GetIds(source, propertyId);
		return source is SectionIds { Section: "properties" } && m_reference.Value is null ? suggestions.Append("*").ToList() : suggestions;
	}

	public override void Refresh()
	{
		foreach (var entry in Entries)
			entry.Refresh();
	}

	private void BuildEntries()
	{
		Entries.Clear();
		if (Slot.Get() is JsonObject map)
		{
			foreach (var key in map.Select(x => x.Key).ToList())
			{
				var valueReference = m_reference?.Value is { } source ? new FieldReference(source) : null;
				var field = FieldFactory.Create(Form, "", m_element, new PropertySlot(map, key), isRequired: true, valueReference, m_propertyId, getPropertyId: () => key, place: Place.Property(key));
				Entries.Add(new MapEntryViewModel(this, key, field));
			}
		}
	}

	private readonly DataType m_element;
	private readonly FieldReference? m_reference;
	private readonly string? m_propertyId;
	private readonly JsonObject? m_parent;
}

public sealed class MapEntryViewModel : ViewModelBase
{
	public MapEntryViewModel(MapFieldViewModel map, string key, FieldViewModel field)
	{
		m_map = map;
		Key = key;
		m_keyText = key;
		Field = field;
		m_keySuggestions = map.GetKeySuggestions();
	}

	public string Key { get; }

	/// <summary>
	/// The key as typed. It's applied when the box loses focus, and goes back if it can't be used.
	/// </summary>
	public string KeyText
	{
		get => VerifyAccess(m_keyText);
		set
		{
			if (SetPropertyField(value ?? "", ref m_keyText) && m_keyText != Key && !m_map.RenameEntry(this, m_keyText.Trim()))
			{
				m_keyText = Key;
				RaisePropertyChanged(nameof(KeyText));
			}
		}
	}

	public IReadOnlyList<string> KeySuggestions
	{
		get => VerifyAccess(m_keySuggestions);
		private set => SetPropertyField(value, ref m_keySuggestions);
	}

	public bool HasKeySuggestions => m_map.HasKeySuggestions;

	public FieldViewModel Field { get; }

	public bool IsEditable => Field.IsEditable;

	public void Remove() => m_map.RemoveEntry(this);

	public void Refresh()
	{
		var suggestions = m_map.GetKeySuggestions();
		if (!suggestions.SequenceEqual(m_keySuggestions))
			KeySuggestions = suggestions;
		Field.Refresh();
	}

	private readonly MapFieldViewModel m_map;
	private string m_keyText;
	private IReadOnlyList<string> m_keySuggestions;
}

/// <summary>
/// Makes the field for a value of a given shape.
/// </summary>
public static class FieldFactory
{
	/// <summary>
	/// Creates a field. The property a value belongs to (for suggesting a property's values) is the one given by
	/// <paramref name="getPropertyId"/> (such as a map's key), the one named by another field of the same object, or else
	/// <paramref name="propertyId"/> (the property item the form is editing).
	/// </summary>
	public static FieldViewModel Create(FormViewModel form, string label, DataType type, ValueSlot slot, bool isRequired, FieldReference? reference, string? propertyId, Func<string?>? getPropertyId = null, FieldPlace? place = null)
	{
		var source = reference?.Value;
		if (source is PropertyValueIds { SiblingField: { } sibling } && slot.ParentObject is { } parent)
			getPropertyId = () => ModJson.FindName(parent, sibling) is { } name && parent[name] is JsonValue value && value.TryGetValue<string>(out var id) ? id : null;
		else if (source is PropertyValueIds { FromMapKey: false, SiblingField: null })
			getPropertyId = () => propertyId;
		var resolve = getPropertyId ?? (() => propertyId);

		return type.Kind switch
		{
			DataKind.Text => new TextFieldViewModel(form, label, slot, isRequired,
				source is not null ? () => form.Catalog.GetIds(source, resolve()) : DataReferences.GetTextChoices(type.ClrType) is { } choices ? () => choices : null,
				isColor: source is ColorIds,
				isMultiline: slot.ParentObject is not null && IsLongText(slot, label), place),
			DataKind.Integer => new NumberFieldViewModel(form, label, slot, isRequired, isInteger: true, place),
			DataKind.Number => new NumberFieldViewModel(form, label, slot, isRequired, isInteger: false, place),
			DataKind.Boolean => new BooleanFieldViewModel(form, label, slot, isRequired, place),
			DataKind.Choice => new ChoiceFieldViewModel(form, label, slot, isRequired, type.Choices!, place),
			DataKind.Object => new ObjectFieldViewModel(form, label, type, slot, isRequired, isRoot: false, propertyId, place),
			DataKind.List => new ListFieldViewModel(form, label, type, slot, isRequired, reference, propertyId, getPropertyId, place),
			DataKind.Map => new MapFieldViewModel(form, label, type, slot, isRequired, reference, propertyId, place),
			_ => new JsonFieldViewModel(form, label, slot, isRequired, place: place),
		};
	}

	/// <summary>
	/// Creates the value a new list item or map entry starts with.
	/// </summary>
	public static JsonNode? CreateDefault(DataType type) => type.Kind switch
	{
		DataKind.Text => JsonValue.Create(""),
		DataKind.Integer or DataKind.Number => JsonValue.Create(0),
		DataKind.Boolean => JsonValue.Create(false),
		DataKind.Choice => JsonValue.Create(type.Choices is [var first, ..] ? first : ""),
		DataKind.Object or DataKind.Map => new JsonObject(),
		DataKind.List => new JsonArray(),
		_ => null,
	};

	// The text of a long text (a sentence or more) gets a box with room for it.
	private static bool IsLongText(ValueSlot slot, string label) =>
		label == "text" && slot.ParentObject is { } parent && ModJson.FindName(parent, "ai") is not null;
}
