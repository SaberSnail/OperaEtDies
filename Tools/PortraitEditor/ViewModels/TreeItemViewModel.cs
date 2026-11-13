using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows.Media;
using OperaEtDies;
using PortraitEditor.Model;

namespace PortraitEditor.ViewModels;

/// <summary>
/// An item in the tree of portrait data.
/// </summary>
public abstract class TreeItemViewModel : ViewModelBase
{
	public abstract string Title { get; }

	public IReadOnlyList<TreeItemViewModel> Children
	{
		get => VerifyAccess(m_children);
		set => SetPropertyField(value, ref m_children);
	}

	public bool IsExpanded
	{
		get => VerifyAccess(m_isExpanded);
		set => SetPropertyField(value, ref m_isExpanded);
	}

	public bool IsSelected
	{
		get => VerifyAccess(m_isSelected);
		set
		{
			if (SetPropertyField(value, ref m_isSelected) && value)
				OnSelected();
		}
	}

	/// <summary>
	/// Whether the item passes the tree's filter.
	/// </summary>
	public bool IsVisible
	{
		get => VerifyAccess(m_isVisible);
		set => SetPropertyField(value, ref m_isVisible);
	}

	protected virtual void OnSelected()
	{
	}

	private IReadOnlyList<TreeItemViewModel> m_children = [];
	private bool m_isExpanded;
	private bool m_isSelected;
	private bool m_isVisible = true;
}

/// <summary>
/// One of the top level lists of the portrait data: colors, scales, offsets, or layers.
/// </summary>
public sealed class CategoryItemViewModel(string name, ITreeItemHost host) : TreeItemViewModel
{
	public string Name { get; } = name;

	public override string Title => $"{Name} ({Children.Count.ToString(CultureInfo.CurrentCulture)})";

	/// <summary>
	/// Whether the category's items have canvas states (only the layers do).
	/// </summary>
	public bool HasCanvasState => Name == "layers";

	public void SetFiltered(string state) => host.SetFiltered(Enum.Parse<CanvasState>(state));

	public void SetAll(string state) => host.SetAll(Enum.Parse<CanvasState>(state));
}

/// <summary>
/// Whether a layer or shape is drawn on the canvas. It doesn't affect the previews or the data.
/// </summary>
public enum CanvasState
{
	/// <summary>
	/// Drawn when the character meets its conditions.
	/// </summary>
	Normal,

	/// <summary>
	/// Drawn whether or not the character meets its conditions. A forced layer's shapes still need their own
	/// conditions.
	/// </summary>
	Forced,

	/// <summary>
	/// Never drawn.
	/// </summary>
	Hidden,
}

/// <summary>
/// What the items in the tree tell the editor about, and ask it to do.
/// </summary>
public interface ITreeItemHost
{
	void OnItemSelected(DataItemViewModel item);

	void OnCanvasStateChanged(DataItemViewModel item);

	/// <summary>
	/// Sets the canvas state of a layer and every shape in it.
	/// </summary>
	void SetLayerAndShapes(DataItemViewModel layer, CanvasState state);

	/// <summary>
	/// Forces a layer or shape and hides everything else on the canvas.
	/// </summary>
	void ShowOnly(DataItemViewModel item);

	/// <summary>
	/// Sets the canvas state of the layers the tree is showing, and puts their shapes back to normal.
	/// </summary>
	void SetFiltered(CanvasState state);

	/// <summary>
	/// Sets the canvas state of every layer, and puts every shape back to normal.
	/// </summary>
	void SetAll(CanvasState state);

	/// <summary>
	/// Adds a copy of an item right after it, and selects the copy.
	/// </summary>
	void Duplicate(DataItemViewModel item);

	/// <summary>
	/// Removes an item from the data.
	/// </summary>
	void Delete(DataItemViewModel item);
}

public enum DataItemKind
{
	Color,
	Scale,
	Offset,
	Layer,
	Shape,
}

/// <summary>
/// An item of one of the lists in the portrait data. The item is found in the data by its place each time it's used,
/// so it keeps working when the data is replaced (such as by undo) as long as the lists are the same length.
/// </summary>
public sealed class DataItemViewModel : TreeItemViewModel
{
	public DataItemViewModel(DataItemKind kind, string listName, Func<JsonArray?> getList, int index, DataItemViewModel? parent, ITreeItemHost host)
	{
		Kind = kind;
		ListName = listName;
		m_getList = getList;
		Index = index;
		Parent = parent;
		m_host = host;
	}

	public DataItemKind Kind { get; }

	/// <summary>
	/// The name of the list the item is in, such as "layers" or "shapes".
	/// </summary>
	public string ListName { get; }

	public int Index { get; }

	/// <summary>
	/// The layer a shape is in, or null for anything else.
	/// </summary>
	public DataItemViewModel? Parent { get; }

	public JsonArray? List => m_getList();

	public JsonObject? Node => List is { } list && Index < list.Count ? list[Index] as JsonObject : null;

	public string? Id => Node?.GetString("id");

	/// <summary>
	/// The path to the item, in the form mods use, such as layers[hair].shapes[3].
	/// </summary>
	public string Path => Parent is { } parent ? $"{parent.Path}.{ListName}[{FormatIndex(Index)}]" : $"{ListName}[{Id ?? FormatIndex(Index)}]";

	public override string Title => Kind switch
	{
		DataItemKind.Shape => GetShapeTitle(),
		DataItemKind.Layer => $"{Id ?? FormatIndex(Index)}{GetLayerDetails()}",
		_ => Id ?? FormatIndex(Index),
	};

	public bool IsModified
	{
		get => VerifyAccess(m_isModified);
		set => SetPropertyField(value, ref m_isModified);
	}

	/// <summary>
	/// Whether the item is drawn in the focused character's portrait. For a color, whether it's used.
	/// </summary>
	public bool IsDrawn
	{
		get => VerifyAccess(m_isDrawn);
		set => SetPropertyField(value, ref m_isDrawn);
	}

	/// <summary>
	/// The color of the item for the focused character: a color's value, or a shape's fill or outline.
	/// </summary>
	public Brush? Swatch
	{
		get => VerifyAccess(m_swatch);
		set => SetPropertyField(value, ref m_swatch);
	}

	/// <summary>
	/// Whether a layer or shape is drawn on the canvas as the data says, always, or never. It only affects the canvas.
	/// </summary>
	public CanvasState CanvasState
	{
		get => VerifyAccess(m_canvasState);
		set
		{
			if (SetCanvasState(value))
				m_host.OnCanvasStateChanged(this);
		}
	}

	/// <summary>
	/// Whether the item is a layer or a shape, which have a canvas state.
	/// </summary>
	public bool HasCanvasState => Kind is DataItemKind.Layer or DataItemKind.Shape;

	/// <summary>
	/// Goes to the next canvas state: normal, forced, hidden, and back to normal.
	/// </summary>
	public void CycleCanvasState() => CanvasState = m_canvasState switch
	{
		CanvasState.Normal => CanvasState.Forced,
		CanvasState.Forced => CanvasState.Hidden,
		_ => CanvasState.Normal,
	};

	/// <summary>
	/// Sets the canvas state, from the name of a state.
	/// </summary>
	public void SetState(string state) => CanvasState = Enum.Parse<CanvasState>(state);

	/// <summary>
	/// Sets the canvas state of the item's layer (the item itself, or a shape's layer) and every shape in it.
	/// </summary>
	public void SetLayerAndShapes(string state) => m_host.SetLayerAndShapes(Parent ?? this, Enum.Parse<CanvasState>(state));

	/// <summary>
	/// Draws only this layer or shape on the canvas, forcing it and hiding the rest.
	/// </summary>
	public void ShowOnly() => m_host.ShowOnly(this);

	public void SetFiltered(string state) => m_host.SetFiltered(Enum.Parse<CanvasState>(state));

	public void SetAll(string state) => m_host.SetAll(Enum.Parse<CanvasState>(state));

	public bool CanDuplicate() => Node is not null;

	/// <summary>
	/// Adds a copy of the item right after it, and selects the copy.
	/// </summary>
	public void Duplicate() => m_host.Duplicate(this);

	public bool CanDelete() => Node is not null;

	public void Delete() => m_host.Delete(this);

	/// <summary>
	/// Sets the canvas state without telling the host, for changes the host makes itself. Returns whether it changed.
	/// </summary>
	internal bool SetCanvasState(CanvasState state) => SetPropertyField(state, ref m_canvasState, nameof(CanvasState));

	/// <summary>
	/// Raises change notifications for the text worked out from the data.
	/// </summary>
	public void RefreshText() => RaisePropertyChanged(nameof(Title), nameof(Path), nameof(Id));

	protected override void OnSelected() => m_host.OnItemSelected(this);

	private string GetShapeTitle()
	{
		var node = Node;
		var parts = new List<string> { FormatIndex(Index) };
		if (node?.GetString("fill") is { } fill)
			parts.Add($"fill {fill}");
		if (node?.GetString("stroke") is { } stroke)
			parts.Add($"stroke {stroke}");
		if (node?.GetProperty("when") is not null)
			parts.Add("(when)");
		return string.Join("  ", parts);
	}

	private string GetLayerDetails()
	{
		var node = Node;
		var details = new List<string>();
		if (node?.GetProperty("when") is not null)
			details.Add("when");
		if (node?.GetProperty("at") is not null)
			details.Add("at");
		return details.Count == 0 ? "" : $"  ({string.Join(", ", details)})";
	}

	private static string FormatIndex(int index) => index.ToString(CultureInfo.InvariantCulture);

	private readonly Func<JsonArray?> m_getList;
	private readonly ITreeItemHost m_host;
	private bool m_isModified;
	private bool m_isDrawn = true;
	private CanvasState m_canvasState;
	private Brush? m_swatch;
}
