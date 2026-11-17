using System.Text.Json.Nodes;
using ModEditor.Model;
using OperaEtDies;

namespace ModEditor.ViewModels.Form;

/// <summary>
/// A form for editing a piece of a mod file (or looking at a piece of the game data), with a field for each of its
/// properties. Its fields change a copy of the piece's JSON, and each change is reported so the editor can apply it.
/// </summary>
public sealed class FormViewModel : ViewModelBase
{
	/// <summary>
	/// Builds a form for a piece, or returns null if its shape isn't known (such as a whole list section).
	/// </summary>
	public static FormViewModel? Create(ModPiece piece, IdCatalog catalog, bool isReadOnly, Action<FormViewModel> changed, Action<HelpTarget>? showHelp = null, IFormChanges? changes = null)
	{
		var form = new FormViewModel(ModJson.Clone(piece.Value), catalog, isReadOnly, changed, showHelp) { Changes = changes, Section = piece.Section, ItemId = piece.Kind == ModPieceKind.Item ? ModJson.GetId(piece.Value) : null };
		var slot = new RootSlot(form);
		FieldViewModel? root = piece.Kind switch
		{
			ModPieceKind.Operation => new OperationFieldViewModel(form, slot),
			ModPieceKind.Item or ModPieceKind.Section when DataSchema.GetPieceType(piece) is { Kind: DataKind.Object } type =>
				new ObjectFieldViewModel(form, "", type, slot, isRequired: true, isRoot: true, propertyId: GetPropertyId(piece), place: new FieldPlace(-1, ModEditing.GetDataPath(piece), Help: HelpTopics.ForPiece(piece))),
			ModPieceKind.Setting when DataSchema.GetPieceType(piece) is { } type =>
				FieldFactory.Create(form, piece.Name!, type, slot, isRequired: true, reference: null, propertyId: null, place: new FieldPlace(0, ModEditing.GetDataPath(piece), Help: HelpTopics.ForPiece(piece))),
			_ => null,
		};
		if (root is null)
			return null;

		// The object the form is for always shows its comments.
		if (root is ObjectFieldViewModel main)
			main.IsMain = true;
		form.Root = root;
		return form;
	}

	private FormViewModel(JsonNode? value, IdCatalog catalog, bool isReadOnly, Action<FormViewModel> changed, Action<HelpTarget>? showHelp)
	{
		Value = value;
		Catalog = catalog;
		IsReadOnly = isReadOnly;
		m_changed = changed;
		m_showHelp = showHelp;
		Root = null!;
	}

	/// <summary>
	/// The JSON being edited, which the fields change.
	/// </summary>
	public JsonNode? Value { get; set; }

	public FieldViewModel Root { get; private set; }

	public IdCatalog Catalog { get; }

	/// <summary>
	/// The section of the data the item or setting the form shows is in, or for an operation, that its path leads into, or
	/// null if neither is known.
	/// </summary>
	public string? Section { get; internal set; }

	/// <summary>
	/// The id of the item the form shows, or for an operation, of the item its path leads into, or null if neither is known.
	/// </summary>
	public string? ItemId { get; internal set; }

	/// <summary>
	/// What the mod changes in the data the form shows, or null if it isn't showing the game data.
	/// </summary>
	public IFormChanges? Changes { get; private init; }

	public bool IsReadOnly { get; }

	public bool IsEditable => !IsReadOnly;

	/// <summary>
	/// Asks the editor to show a place in the help.
	/// </summary>
	public void ShowHelp(HelpTarget target) => m_showHelp?.Invoke(target);

	/// <summary>
	/// Reports that a field changed the value, and updates the suggestions that depend on other fields.
	/// </summary>
	public void NotifyChanged()
	{
		if (IsReadOnly)
			return;
		m_changed(this);
		Root.Refresh();
	}

	// The values of a property item belong to that property.
	private static string? GetPropertyId(ModPiece piece) =>
		piece.Kind == ModPieceKind.Item && string.Equals(piece.Section, "properties", StringComparison.OrdinalIgnoreCase) ? ModJson.GetId(piece.Value) : null;

	private readonly Action<FormViewModel> m_changed;
	private readonly Action<HelpTarget>? m_showHelp;
}
