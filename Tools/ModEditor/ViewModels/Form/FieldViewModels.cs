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

	/// <summary>
	/// What the line beside the field's own fields (for an object, list, or map) says when hovered, since it can be taller
	/// than the window: the path of the property they belong to, such as "needs[need_water].steps[1]", or its name if it has
	/// no path.
	/// </summary>
	public string? IndentToolTip => DataPath ?? (HasLabel ? Label : null);

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
	/// Whether the field's name has a menu (to copy its path, add a comment, or show its help).
	/// </summary>
	public bool HasMenu => HasCopyablePath || Help is not null || CanGoToModChange() || CanAddComment();

	/// <summary>
	/// Whether comments can be added to the field's data, which only an object read into a class can have.
	/// </summary>
	public virtual bool CanAddComment() => false;

	public virtual void AddComment()
	{
	}

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

	/// <summary>
	/// A field of the game data can be replaced or removed by the mod, when there's a mod open.
	/// </summary>
	public bool CanReplace() => DataPath is not null && Place.IsCopyable && Form.Changes is { CanChangeMod: true };

	/// <summary>
	/// Adds an operation to the mod that replaces the field's data, starting from its current value.
	/// </summary>
	public void Replace()
	{
		if (CanReplace())
			Form.Changes!.Replace(DataPath!, Slot.Get());
	}

	public bool CanRemove() => CanReplace();

	/// <summary>
	/// Adds an operation to the mod that removes the field's data.
	/// </summary>
	public void Remove()
	{
		if (CanRemove())
			Form.Changes!.Remove(DataPath!);
	}

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
	public NumberFieldViewModel(FormViewModel form, string label, ValueSlot slot, bool isRequired, bool isInteger, FieldPlace? place = null, bool isDuration = false)
		: base(form, label, slot, isRequired, place)
	{
		m_isInteger = isInteger;
		m_isDuration = isDuration;
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
			else if (m_isDuration && DurationConverter.Parse(text) is not null)
			{
				Error = null;
				SetOrRemove(JsonValue.Create(text), isEmpty: false);
			}
			else
			{
				Error = m_isDuration ? "Not a whole number of minutes, or hours and minutes (\"1:30\")." : m_isInteger ? "Not a whole number." : "Not a number.";
			}
		}
	}

	public string? Error
	{
		get => VerifyAccess(m_error);
		private set => SetPropertyField(value, ref m_error);
	}

	private readonly bool m_isInteger;
	private readonly bool m_isDuration;
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
/// A place, such as where a step goes: one of the named places (such as "home"), or a quarter or a remembered place,
/// whose settings are fields below the choice.
/// </summary>
public sealed class PlaceFieldViewModel : FieldViewModel
{
	public PlaceFieldViewModel(FormViewModel form, string label, ValueSlot slot, bool isRequired, string? propertyId, FieldPlace? place = null)
		: base(form, label, slot, isRequired, place)
	{
		m_propertyId = propertyId;
		m_selected = slot.Get() switch
		{
			JsonValue value when value.GetValueKind() == JsonValueKind.String => value.GetValue<string>(),
			JsonObject obj when ModJson.FindName(obj, "remembered") is not null => RememberedChoice,
			JsonObject => QuarterChoice,
			_ => "",
		};
		var all = isRequired ? Kinds : Kinds.Prepend("");
		Choices = (m_selected.Length != 0 && !Kinds.Contains(m_selected) ? all.Append(m_selected) : all).ToList();
		m_details = CreateDetails();
	}

	/// <summary>
	/// The named places, then the kinds of place written as an object.
	/// </summary>
	public IReadOnlyList<string> Choices { get; }

	public string Selected
	{
		get => VerifyAccess(m_selected);
		set
		{
			if (!SetPropertyField(value ?? "", ref m_selected))
				return;

			// Choosing a kind of place written as an object starts it with the setting it needs.
			if (m_selected == QuarterChoice)
			{
				Slot.Set(new JsonObject { ["quarterKinds"] = new JsonArray() });
				Changed();
			}
			else if (m_selected == RememberedChoice)
			{
				Slot.Set(new JsonObject { ["remembered"] = "" });
				Changed();
			}
			else
			{
				SetOrRemove(JsonValue.Create(m_selected), m_selected.Length == 0);
			}

			Details = CreateDetails();
		}
	}

	/// <summary>
	/// The settings of a quarter or remembered place, or null for a named place.
	/// </summary>
	public ObjectFieldViewModel? Details
	{
		get => VerifyAccess(m_details);
		private set => SetPropertyField(value, ref m_details);
	}

	public override void Refresh() => m_details?.Refresh();

	private ObjectFieldViewModel? CreateDetails() => Slot.Get() is JsonObject
		? new ObjectFieldViewModel(Form, Label, new DataType(DataKind.Object, typeof(PlaceObject)), Slot, isRequired: true, isRoot: false, m_propertyId, Place)
		: null;

	/// <summary>
	/// The choice for a quarter of some kinds.
	/// </summary>
	public const string QuarterChoice = "(quarter)";

	/// <summary>
	/// The choice for a place a "remember" step remembered.
	/// </summary>
	public const string RememberedChoice = "(remembered)";

	private static IReadOnlyList<string> Kinds { get; } = ["home", "workplace", "otherHome", "partnerHome", "jobSite", QuarterChoice, RememberedChoice];

	private readonly string? m_propertyId;
	private string m_selected;
	private ObjectFieldViewModel? m_details;
}

/// <summary>
/// An entry in an outfit's clothing: a clothing id, or a choice among options, whose options are edited below it.
/// </summary>
public sealed class OutfitClothingFieldViewModel : FieldViewModel
{
	public OutfitClothingFieldViewModel(FormViewModel form, string label, ValueSlot slot, bool isRequired, string? propertyId, FieldPlace? place = null)
		: base(form, label, slot, isRequired, place)
	{
		m_propertyId = propertyId;
		m_selected = slot.Get() switch
		{
			JsonValue value when value.GetValueKind() == JsonValueKind.String => value.GetValue<string>(),
			JsonObject => OptionsChoice,
			_ => "",
		};
		var clothing = form.Catalog.GetIds("clothing");
		var all = (isRequired ? clothing : clothing.Prepend("")).Append(OptionsChoice);
		Choices = (m_selected.Length != 0 && !all.Contains(m_selected, StringComparer.Ordinal) ? all.Append(m_selected) : all).ToList();
		m_details = CreateDetails();
	}

	/// <summary>
	/// The clothing ids, then the choice for options.
	/// </summary>
	public IReadOnlyList<string> Choices { get; }

	public string Selected
	{
		get => VerifyAccess(m_selected);
		set
		{
			if (!SetPropertyField(value ?? "", ref m_selected))
				return;

			// Choosing options starts with none.
			if (m_selected == OptionsChoice)
			{
				Slot.Set(new JsonObject { ["options"] = new JsonArray() });
				Changed();
			}
			else
			{
				SetOrRemove(JsonValue.Create(m_selected), m_selected.Length == 0);
			}

			Details = CreateDetails();
		}
	}

	/// <summary>
	/// The options, or null for a clothing id.
	/// </summary>
	public ObjectFieldViewModel? Details
	{
		get => VerifyAccess(m_details);
		private set => SetPropertyField(value, ref m_details);
	}

	public override void Refresh() => m_details?.Refresh();

	private ObjectFieldViewModel? CreateDetails() => Slot.Get() is JsonObject
		? new ObjectFieldViewModel(Form, Label, new DataType(DataKind.Object, typeof(OutfitClothingChoice)), Slot, isRequired: true, isRoot: false, m_propertyId, Place)
		: null;

	/// <summary>
	/// The choice for an entry that's a choice among options.
	/// </summary>
	public const string OptionsChoice = "(options)";

	private readonly string? m_propertyId;
	private string m_selected;
	private ObjectFieldViewModel? m_details;
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
		VisibleFields = [];
		m_addableProperties = [];
		BuildFields();
	}

	/// <summary>
	/// The fields that are shown: those that are required (always, or by the object's other settings, such as a step's
	/// kind), set, or added since the object was shown, and properties the game doesn't read there. With
	/// <see cref="FormViewModel.ShowAllProperties"/>, every allowed property is shown. A property the object's other
	/// settings don't allow is only shown while it's set, so it can be fixed.
	/// </summary>
	public ObservableCollection<FieldViewModel> VisibleFields { get; }

	/// <summary>
	/// The properties that aren't shown and can be added, in the order the class declares them.
	/// </summary>
	public IReadOnlyList<AddablePropertyViewModel> AddableProperties
	{
		get => VerifyAccess(m_addableProperties);
		private set => SetPropertyField([nameof(AddableProperties), nameof(HasAddableProperties)], value, ref m_addableProperties);
	}

	public bool HasAddableProperties => AddableProperties.Count != 0;

	/// <summary>
	/// Adds the property chosen from the list of those that can be added. It's never anything for long, since the property
	/// added leaves the list.
	/// </summary>
	public AddablePropertyViewModel? ChosenProperty
	{
		get
		{
			VerifyAccess();
			return null;
		}
		set => value?.Add();
	}

	/// <summary>
	/// Shows a property that isn't set, so it can be filled in. It stays shown while the object is, even if it's left
	/// unset.
	/// </summary>
	public void AddProperty(string name)
	{
		VerifyAccess();
		if (m_revealed.Add(name))
		{
			m_added.Add(name);
			UpdateVisibleFields();
		}
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

	/// <summary>
	/// Whether the object is the one the form is for (an item, a settings section, or a setting), whose comments are
	/// always shown. A nested object only shows comments it has.
	/// </summary>
	public bool IsMain
	{
		get => m_isMain;
		set
		{
			m_isMain = value;
			RaisePropertyChanged(nameof(IsMain), nameof(IsCommentsShown), nameof(IsCommentsRemovable));
		}
	}

	/// <summary>
	/// The object's comments, for the people editing the data, or empty if it has none. The main object's comments are
	/// left out when they're emptied; a nested object's are left out with <see cref="RemoveComment"/>.
	/// </summary>
	public string Comments
	{
		get => GetCommentsName() is { } name && Slot.Get() is JsonObject obj && obj[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";
		set
		{
			if (IsEditable && Slot.Get() is JsonObject obj && value != Comments)
			{
				var name = GetCommentsName();
				if (IsMain && string.IsNullOrWhiteSpace(value))
				{
					if (name is not null)
						obj.Remove(name);
				}
				else
				{
					SetComments(obj, name, value);
				}

				RaiseCommentsChanged();
				Changed();
			}
		}
	}

	/// <summary>
	/// Whether the comments box is shown: always for the main object, and for a nested object that has comments.
	/// </summary>
	public bool IsCommentsShown => IsPresent && (IsMain || GetCommentsName() is not null);

	public bool IsCommentsRemovable => !IsMain && IsEditable && GetCommentsName() is not null;

	/// <summary>
	/// The width of the comments' name, which lines their box up with the object's fields.
	/// </summary>
	public double CommentsLabelWidth => Math.Max(EditorStart - (IndentWidth * (Place.Depth + 1)), 48);

	public override bool CanAddComment() => !IsMain && IsEditable && Slot.Get() is JsonObject && GetCommentsName() is null;

	public override void AddComment()
	{
		if (CanAddComment() && Slot.Get() is JsonObject obj)
		{
			SetComments(obj, null, "");
			RaiseCommentsChanged();
			Changed();
		}
	}

	public bool CanRemoveComment() => IsCommentsRemovable;

	public void RemoveComment()
	{
		if (CanRemoveComment() && Slot.Get() is JsonObject obj)
		{
			obj.Remove(GetCommentsName()!);
			RaiseCommentsChanged();
			Changed();
		}
	}

	public override void Refresh()
	{
		foreach (var field in Fields)
			field.Refresh();
		UpdateVisibleFields();
	}

	/// <summary>
	/// Works out which fields are shown and which properties can be added, from what's set, what the object's other
	/// settings allow and require, and what's been added. When only one property could be added, it's shown instead,
	/// since it takes about as much room as the list would. Properties that were added (or are shown in place of the list)
	/// come last, in the order they were added, where the list is, so they don't appear somewhere else in the object.
	/// </summary>
	private void UpdateVisibleFields()
	{
		var obj = Slot.Get() as JsonObject;

		// The object that holds this one, for rules on it. A form's main object could be part of anything, so rules on its
		// parent are taken as met for what it allows, and not met for what it requires.
		var parent = IsRoot ? null : Slot.ParentObject;
		bool IsMet(DataRule rule, bool isUnknownMet)
		{
			var target = rule.IsOnParent ? parent : obj;
			if (rule.IsOnParent && parent is null)
				return isUnknownMet;
			var found = target is not null ? ModJson.FindName(target, rule.Property) : null;
			var text = found is not null && target![found] is JsonValue value && value.TryGetValue<string>(out var valueText) ? valueText : null;
			return rule.IsMetBy(found is not null && target![found] is not null, text);
		}

		var shown = new List<(FieldViewModel Field, bool IsShown, AddablePropertyViewModel? Addable)>();
		foreach (var (info, field) in m_fieldInfos)
		{
			AddablePropertyViewModel? canAdd = null;
			var isShown = info is null;
			if (info is not null)
			{
				var isSet = obj is not null && ModJson.FindName(obj, info.Name) is not null;
				var isAllowed = info.AllowedFor is not { } allowed || IsMet(allowed, isUnknownMet: true);
				var isRequired = info.IsRequired || (info.RequiredFor is { } required && IsMet(required, isUnknownMet: false));

				// Once a field is shown, it stays shown while it's allowed, even if it's emptied.
				isShown = isSet || (isAllowed && (isRequired || Form.ShowAllProperties || m_revealed.Contains(info.Name)));
				if (isShown && isAllowed)
					m_revealed.Add(info.Name);
				if (!isShown && isAllowed && IsEditable && obj is not null)
				{
					var name = info.Name;
					canAdd = new AddablePropertyViewModel(name, HelpLibrary.GetRowText(HelpTopics.ForProperty(m_type.ClrType, name)), () => AddProperty(name));
				}
			}

			shown.Add((field, isShown, canAdd));
		}

		var addable = shown.Select(x => x.Addable).OfType<AddablePropertyViewModel>().ToList();
		var isLone = addable.Count == 1;
		var lone = isLone ? shown.First(x => x.Addable is not null).Field : null;
		var added = m_added.Select(name => shown.FirstOrDefault(x => x.IsShown && x.Field.Label == name).Field).OfType<FieldViewModel>().ToList();
		var visible = shown.Where(x => x.IsShown && !added.Contains(x.Field)).Select(x => x.Field).Concat(added).ToList();
		if (lone is not null)
			visible.Add(lone);
		if (isLone)
			addable = [];

		if (!visible.SequenceEqual(VisibleFields))
		{
			VisibleFields.Clear();
			foreach (var field in visible)
				VisibleFields.Add(field);
		}

		if (!addable.Select(x => x.Name).SequenceEqual(m_addableProperties.Select(x => x.Name)))
			AddableProperties = addable;
	}

	/// <summary>
	/// Gets the name of the object's comments, if it has them as text. Comments that aren't text are shown as an unknown
	/// property instead, so they can be fixed.
	/// </summary>
	private string? GetCommentsName() =>
		Slot.Get() is JsonObject obj && ModJson.FindName(obj, DataFileParts.CommentsName) is { } name && obj[name] is JsonValue value && value.GetValueKind() == JsonValueKind.String ? name : null;

	/// <summary>
	/// Sets the comments, adding them at the start of the object if it doesn't have them.
	/// </summary>
	private static void SetComments(JsonObject obj, string? name, string comments)
	{
		if (name is not null)
			obj[name] = comments;
		else
			obj.Insert(0, DataFileParts.CommentsName, comments);
	}

	private void RaiseCommentsChanged() => RaisePropertyChanged(nameof(Comments), nameof(IsCommentsShown), nameof(IsCommentsRemovable));

	private void BuildFields()
	{
		Fields.Clear();
		m_fieldInfos.Clear();
		if (Slot.Get() is JsonObject obj)
		{
			foreach (var info in m_type.Fields)
			{
				var field = FieldFactory.Create(Form, info.Name, info.Type, new PropertySlot(obj, info.Name), info.IsRequired, DataReferences.Find(info), m_propertyId, place: Place.Property(info.Name) with { Help = HelpTopics.ForProperty(m_type.ClrType, info.Name) });
				Fields.Add(field);
				m_fieldInfos.Add((info, field));
			}

			var known = m_type.Fields.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
			if (GetCommentsName() is { } commentsName)
				known.Add(commentsName);
			foreach (var name in obj.Select(x => x.Key).Where(x => !known.Contains(x)).ToList())
			{
				var field = new JsonFieldViewModel(Form, name, new PropertySlot(obj, name), isRequired: false, isUnknown: true, Place.Property(name) with { Help = null });
				Fields.Add(field);
				m_fieldInfos.Add((null, field));
			}
		}

		UpdateVisibleFields();
		RaisePropertyChanged(nameof(IsPresent), nameof(IsInvalid));
		RaiseCommentsChanged();
	}

	private readonly DataType m_type;
	private readonly string? m_propertyId;
	private readonly Collection<(DataField? Info, FieldViewModel Field)> m_fieldInfos = [];
	private readonly HashSet<string> m_revealed = new(StringComparer.OrdinalIgnoreCase);
	private readonly Collection<string> m_added = [];
	private IReadOnlyList<AddablePropertyViewModel> m_addableProperties;
	private bool m_isMain;
}

/// <summary>
/// A property that can be added to an object in the form, with what the help says about it.
/// </summary>
public sealed class AddablePropertyViewModel(string name, string? toolTip, Action add)
{
	public string Name { get; } = name;

	public string? ToolTip { get; } = toolTip;

	public void Add() => add();

	public override string ToString() => Name;
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

	/// <summary>
	/// Adds a new item right after an item, so it can be added without going back to the top of the list.
	/// </summary>
	public void AddItemAfter(ListItemViewModel item)
	{
		if (!IsEditable || Slot.Get() is not JsonArray list || item.Index >= list.Count)
			return;
		list.Insert(item.Index + 1, FieldFactory.CreateDefault(m_element));
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
				// Each item is labeled with its index (from 0, as mod paths count), so its editor lines up with the others.
				var label = index.ToString(CultureInfo.CurrentCulture);
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

	public bool CanAddAfter() => IsEditable;

	public void AddAfter() => list.AddItemAfter(this);

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
				var valueReference = m_reference?.Entries ?? (m_reference?.Value is { } source ? new FieldReference(source) : null);
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

		// Places are named or written as an object, which the game reads with a converter of its own.
		if (type.ClrType == typeof(SchedulePlace))
			return new PlaceFieldViewModel(form, label, slot, isRequired, propertyId, place);

		// Outfit clothing is a clothing id or written as an object of options, which the game reads with a converter of its own.
		if (type.ClrType == typeof(OutfitClothing))
			return new OutfitClothingFieldViewModel(form, label, slot, isRequired, propertyId, place);

		return type.Kind switch
		{
			DataKind.Text => new TextFieldViewModel(form, label, slot, isRequired,
				source is ColorIds ? () => form.Catalog.GetColorIds(slot.ParentObject, form.Section, form.ItemId) : source is not null ? () => form.Catalog.GetIds(source, resolve()) : DataReferences.GetTextChoices(type.ClrType) is { } choices ? () => choices : null,
				isColor: source is ColorIds,
				isMultiline: slot.ParentObject is not null && IsLongText(slot, label), place),
			DataKind.Integer => new NumberFieldViewModel(form, label, slot, isRequired, isInteger: true, place, isDuration: type.ClrType == typeof(Duration)),
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
