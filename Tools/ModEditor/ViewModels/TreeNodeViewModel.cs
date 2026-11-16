using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModEditor.Model;
using OperaEtDies;

namespace ModEditor.ViewModels;

/// <summary>
/// An item in one of the editor's trees: the mod's folders, files, and their pieces, or the game data.
/// </summary>
public abstract class TreeNodeViewModel : ViewModelBase
{
	protected TreeNodeViewModel(MainWindowViewModel host, TreeNodeViewModel? parent)
	{
		Host = host;
		Parent = parent;
		Children = [];
		m_title = "";
		m_detail = "";
	}

	/// <summary>
	/// The editor, for the commands in each item's menu.
	/// </summary>
	public MainWindowViewModel Host { get; }

	public TreeNodeViewModel? Parent { get; }

	public ObservableCollection<TreeNodeViewModel> Children { get; }

	public string Title
	{
		get => VerifyAccess(m_title);
		protected set => SetPropertyField(value, ref m_title);
	}

	/// <summary>
	/// Extra text shown after the title in grey, such as an item's title or how many items a section has.
	/// </summary>
	public string Detail
	{
		get => VerifyAccess(m_detail);
		protected set => SetPropertyField(value, ref m_detail);
	}

	/// <summary>
	/// A symbol for the kind of item.
	/// </summary>
	public abstract string Glyph { get; }

	/// <summary>
	/// Whether the item is a folder or file, which are shown in bold.
	/// </summary>
	public virtual bool IsContainer => false;

	/// <summary>
	/// Whether the item has problems (a file that can't be read, or that problems were found in).
	/// </summary>
	public bool HasProblems
	{
		get => VerifyAccess(m_hasProblems);
		set => SetPropertyField(value, ref m_hasProblems);
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
				Host.OnNodeSelected(this);
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

	/// <summary>
	/// The piece of a file the item is, as a copy, or null if it isn't one.
	/// </summary>
	public abstract ModPiece? GetPiece();

	/// <summary>
	/// The path of the item from the top of its tree, for the editor's heading.
	/// </summary>
	public string GetBreadcrumb() => Parent is null ? Title : $"{Parent.GetBreadcrumb()} › {Title}";

	public IEnumerable<TreeNodeViewModel> GetAncestors()
	{
		for (var node = Parent; node is not null; node = node.Parent)
			yield return node;
	}

	public IEnumerable<TreeNodeViewModel> GetDescendants() => Children.SelectMany(x => x.GetDescendants().Prepend(x));

	/// <summary>
	/// Shows the items whose titles have the text, and the items they're in. Returns whether this item is shown.
	/// </summary>
	public bool ApplyFilter(string filter)
	{
		var anyChild = false;
		foreach (var child in Children)
			anyChild |= child.ApplyFilter(filter);
		var isMatch = filter.Length == 0 || Title.Contains(filter, StringComparison.OrdinalIgnoreCase) || Detail.Contains(filter, StringComparison.OrdinalIgnoreCase);
		IsVisible = isMatch || anyChild || Parent is null;
		if (filter.Length != 0 && anyChild)
			IsExpanded = true;
		return IsVisible;
	}

	/// <summary>
	/// Gets a short preview of a value, for an item's detail.
	/// </summary>
	protected static string Preview(JsonNode? node)
	{
		var text = node switch
		{
			null => "null",
			JsonValue value when value.GetValueKind() == JsonValueKind.String => $"\"{value.GetValue<string>()}\"",
			JsonValue value => value.ToJsonString(),
			JsonArray array => $"[{array.Count}]",
			JsonObject obj when obj.TryGetPropertyValue("title", out var title) && title is JsonValue => title.ToString(),
			JsonObject => "{…}",
			_ => "",
		};
		return text.Length > 40 ? text[..39] + "…" : text;
	}

	protected static string Count(int count) => count.ToString(CultureInfo.CurrentCulture);

	private string m_title;
	private string m_detail;
	private bool m_hasProblems;
	private bool m_isExpanded;
	private bool m_isSelected;
	private bool m_isVisible = true;
}

/// <summary>
/// A folder, file, or piece of a file in the mod.
/// </summary>
public sealed class ModNodeViewModel : TreeNodeViewModel
{
	public ModNodeViewModel(MainWindowViewModel host, TreeNodeViewModel? parent, ModLocation location)
		: base(host, parent)
	{
		Location = location;
	}

	public ModLocation Location { get; }

	public override string Glyph => Location switch
	{
		FolderLocation => "📁",
		FileLocation => m_fileKind == ModFileKind.Operations ? "🔧" : "📄",
		SectionLocation => "▤",
		OperationLocation => "⚙",
		_ => "•",
	};

	public override bool IsContainer => Location is FolderLocation or FileLocation;

	public override ModPiece? GetPiece() => Host.Project is { } project ? ModEditing.GetPiece(project, Location) : null;

	/// <summary>
	/// Updates the title and detail from the mod.
	/// </summary>
	public void Update(ModProject project)
	{
		var node = ModEditing.Get(project, Location);
		switch (Location)
		{
		case FolderLocation x:
			Title = x.Path.Length == 0 ? project.Name : ModFilePath.GetName(x.Path);
			Detail = "";
			break;
		case FileLocation x:
			var file = project.FindFile(x.Path);
			Title = ModFilePath.GetName(x.Path);
			if (file?.Kind != m_fileKind)
			{
				m_fileKind = file?.Kind ?? ModFileKind.Invalid;
				RaisePropertyChanged(nameof(Glyph));
			}

			Detail = file?.Kind switch
			{
				ModFileKind.Operations => "operations",
				ModFileKind.Invalid => "can't be read",
				_ => "",
			};
			break;
		case SectionLocation x:
			Title = x.Section;
			Detail = node switch
			{
				JsonArray list => Count(list.Count),
				JsonObject obj => $"settings ({Count(obj.Count)})",
				_ => "",
			};
			break;
		case ItemLocation x:
			Title = ModJson.GetId(node) is { } id ? id : $"[{Count(x.Index)}]";
			Detail = node is JsonObject obj2 && obj2.TryGetPropertyValue("title", out var title) && title is JsonValue ? title.ToString() : "";
			break;
		case SettingLocation x:
			Title = x.Name;
			Detail = node is JsonObject or JsonArray ? "" : Preview(node);
			break;
		case OperationLocation:
			// The ids of the items an operation works on tell apart operations on the same list.
			var operation = node as JsonObject;
			var op = operation is null ? null : ModOperations.GetString(operation, "op");
			var ids = operation is null ? [] : ModOperations.GetItemIds(operation);
			Title = ids.Count == 0 ? op ?? "?" : $"{op ?? "?"} {string.Join(", ", ids)}";
			Detail = operation is null ? "" : ModOperations.GetString(operation, "path") ?? "";
			break;
		}
	}

	private ModFileKind m_fileKind = ModFileKind.Data;
}

/// <summary>
/// A section, item, or setting of the game data, which can be looked at and copied but not changed.
/// </summary>
public sealed class GameDataNodeViewModel : TreeNodeViewModel
{
	public GameDataNodeViewModel(MainWindowViewModel host, TreeNodeViewModel? parent, string title, string detail, ModPiece? piece)
		: base(host, parent)
	{
		Title = title;
		Detail = detail;
		m_piece = piece;
	}

	public override string Glyph => m_piece?.Kind switch
	{
		ModPieceKind.Section => "▤",
		null => "📚",
		_ => "•",
	};

	public override bool IsContainer => m_piece is null;

	public override ModPiece? GetPiece() => m_piece is null ? null : m_piece with { Value = ModJson.Clone(m_piece.Value) };

	/// <summary>
	/// Builds the tree of the game data: its sections, with their items or settings.
	/// </summary>
	public static GameDataNodeViewModel Create(MainWindowViewModel host, JsonObject? data, string description)
	{
		var root = new GameDataNodeViewModel(host, null, "Game data", description, null) { IsExpanded = true };
		foreach (var (name, value) in data ?? [])
		{
			var section = new GameDataNodeViewModel(host, root, name, value switch
			{
				JsonArray list => Count(list.Count),
				JsonObject obj => $"settings ({Count(obj.Count)})",
				_ => "",
			}, ModPiece.WholeSection(name, value));
			root.Children.Add(section);

			if (value is JsonArray items)
			{
				var index = 0;
				foreach (var item in items)
				{
					var detail = item is JsonObject obj && obj.TryGetPropertyValue("title", out var title) && title is JsonValue ? title.ToString() : "";
					section.Children.Add(new GameDataNodeViewModel(host, section, ModJson.GetId(item) ?? $"[{Count(index)}]", detail, ModPiece.Item(name, item)));
					index++;
				}
			}
			else if (value is JsonObject settings)
			{
				foreach (var (key, setting) in settings)
					section.Children.Add(new GameDataNodeViewModel(host, section, key, setting is JsonObject or JsonArray ? "" : Preview(setting), ModPiece.Setting(name, key, setting)));
			}
		}

		return root;
	}

	private readonly ModPiece? m_piece;
}
