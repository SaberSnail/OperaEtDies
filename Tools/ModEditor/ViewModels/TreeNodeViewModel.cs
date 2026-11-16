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
	/// What the mod does to the item: adds it, removes it, or changes it (or something in it), or null if nothing.
	/// </summary>
	public ChangeKind? Change
	{
		get => VerifyAccess(m_change);
		set
		{
			if (SetPropertyField(value, ref m_change))
				RaisePropertyChanged(nameof(ChangeGlyph), nameof(ChangeText));
		}
	}

	/// <summary>
	/// The mark shown before a changed item's title.
	/// </summary>
	public string ChangeGlyph => m_change switch
	{
		ChangeKind.Added => "✚",
		ChangeKind.Removed => "✖",
		ChangeKind.Changed => "✎",
		_ => "",
	};

	public string? ChangeText => m_change switch
	{
		ChangeKind.Added => "The mod adds this.",
		ChangeKind.Removed => "The mod removes this.",
		ChangeKind.Changed => "The mod changes this (see the Changes tab).",
		_ => null,
	};

	/// <summary>
	/// Something to look at about the item, such as an operation that changes nothing, or null if there's nothing.
	/// </summary>
	public string? Warning
	{
		get => VerifyAccess(m_warning);
		set => SetPropertyField(value, ref m_warning);
	}

	/// <summary>
	/// Shows the items whose titles have the text (and, if asked, that the mod changes), and the items they're in.
	/// Returns whether this item is shown.
	/// </summary>
	public bool ApplyFilter(string filter, bool onlyChanged = false)
	{
		var anyChild = false;
		foreach (var child in Children)
			anyChild |= child.ApplyFilter(filter, onlyChanged);
		var isMatch = (filter.Length == 0 || Title.Contains(filter, StringComparison.OrdinalIgnoreCase) || Detail.Contains(filter, StringComparison.OrdinalIgnoreCase)) &&
			(!onlyChanged || m_change is not null);
		IsVisible = isMatch || anyChild || Parent is null;
		if ((filter.Length != 0 || onlyChanged) && anyChild)
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
	private ChangeKind? m_change;
	private string? m_warning;
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
/// A section, item, or setting of the game data, which can be looked at and copied but not changed. The tree shows
/// the data as the mod leaves it: what the mod adds is in it, and what it removes stays in it, marked.
/// </summary>
public sealed class GameDataNodeViewModel : TreeNodeViewModel
{
	private GameDataNodeViewModel(MainWindowViewModel host, TreeNodeViewModel? parent, string? section, string? key)
		: base(host, parent)
	{
		Section = section;
		Key = key;
	}

	/// <summary>
	/// The section the item is in (or is), or null for the top of the tree.
	/// </summary>
	public string? Section { get; }

	/// <summary>
	/// The id of a list item, or the name of a setting, or null for a section.
	/// </summary>
	public string? Key { get; }

	public override string Glyph => m_piece?.Kind switch
	{
		ModPieceKind.Section => "▤",
		null => "📚",
		_ => "•",
	};

	public override bool IsContainer => m_piece is null;

	public override ModPiece? GetPiece() => m_piece is null ? null : m_piece with { Value = ModJson.Clone(m_piece.Value) };

	/// <summary>
	/// Makes the top of the game data's tree, to be filled in by <see cref="Update"/>.
	/// </summary>
	public static GameDataNodeViewModel CreateRoot(MainWindowViewModel host) =>
		new(host, null, null, null) { Title = "Game data", IsExpanded = true };

	/// <summary>
	/// Brings the tree up to date with the game data and what the mod changes in it, keeping the items that are still
	/// there (so they stay selected and expanded). Each item shows its value with the mod, or without it if the mod
	/// removes it.
	/// </summary>
	public void Update(JsonObject? gameData, ModChanges changes, string description)
	{
		Detail = description;
		var data = changes.Changes.Count == 0 ? gameData ?? [] : changes.Data;
		var names = (gameData?.Select(x => x.Key) ?? []).Concat(data.Select(x => x.Key)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
		var sections = names.Select(name =>
		{
			var before = Get(gameData, name);
			var after = Get(data, name);
			var node = GetChild(name, null);
			node.UpdateSection(name, before, after, changes);
			return node;
		}).ToList();
		SetChildren(sections);
	}

	private void UpdateSection(string name, JsonNode? before, JsonNode? after, ModChanges changes)
	{
		var value = after ?? before;
		var sectionChanges = changes.GetChanges(name, null);
		Title = name;
		m_piece = ModPiece.WholeSection(name, value);
		Change = before is null ? ChangeKind.Added : after is null ? ChangeKind.Removed : sectionChanges.Count != 0 ? ChangeKind.Changed : null;

		var children = new List<GameDataNodeViewModel>();
		if (value is JsonArray)
		{
			// The items in the order the mod leaves them, then those it removes.
			var beforeItems = (before as JsonArray ?? []).Where(x => ModJson.GetId(x) is not null).ToDictionary(x => ModJson.GetId(x)!, StringComparer.Ordinal);
			var afterItems = (after as JsonArray ?? []).Where(x => ModJson.GetId(x) is not null).ToDictionary(x => ModJson.GetId(x)!, StringComparer.Ordinal);
			foreach (var id in afterItems.Keys.Concat(beforeItems.Keys.Where(x => !afterItems.ContainsKey(x))))
			{
				var item = afterItems.GetValueOrDefault(id) ?? beforeItems[id];
				var child = GetChild(name, id);
				child.Title = id;
				child.Detail = item is JsonObject obj && obj.TryGetPropertyValue("title", out var title) && title is JsonValue ? title.ToString() : "";
				child.m_piece = ModPiece.Item(name, item);
				child.Change = GetChange(beforeItems.ContainsKey(id), afterItems.ContainsKey(id), changes.GetChanges(name, id).Count != 0);
				children.Add(child);
			}
		}
		else if (value is JsonObject)
		{
			var beforeSettings = before as JsonObject ?? [];
			var afterSettings = after as JsonObject ?? [];
			foreach (var key in afterSettings.Select(x => x.Key).Concat(beforeSettings.Select(x => x.Key).Where(x => ModJson.FindName(afterSettings, x) is null)))
			{
				var setting = Get(afterSettings, key) ?? Get(beforeSettings, key);
				var child = GetChild(name, key);
				child.Title = key;
				child.Detail = setting is JsonObject or JsonArray ? "" : Preview(setting);
				child.m_piece = ModPiece.Setting(name, key, setting);
				child.Change = GetChange(ModJson.FindName(beforeSettings, key) is not null, ModJson.FindName(afterSettings, key) is not null, changes.GetChanges(name, key).Count != 0);
				children.Add(child);
			}
		}

		var changed = children.Count(x => x.Change is not null);
		var count = value switch
		{
			JsonArray list => Count(list.Count),
			JsonObject obj => $"settings ({Count(obj.Count)})",
			_ => "",
		};
		Detail = changed == 0 ? count : $"{count} · {Count(changed)} changed";
		SetChildren(children);
	}

	private static ChangeKind? GetChange(bool wasThere, bool isThere, bool isChanged) =>
		!wasThere ? ChangeKind.Added : !isThere ? ChangeKind.Removed : isChanged ? ChangeKind.Changed : null;

	/// <summary>
	/// Gets the child for a section (key null) or for an item or setting in this section, reusing the one already there.
	/// </summary>
	private GameDataNodeViewModel GetChild(string section, string? key) =>
		Children.OfType<GameDataNodeViewModel>().FirstOrDefault(x => string.Equals(x.Section, section, StringComparison.OrdinalIgnoreCase) && x.Key == key)
		?? new GameDataNodeViewModel(Host, this, section, key);

	private void SetChildren(IReadOnlyList<GameDataNodeViewModel> children)
	{
		foreach (var removed in Children.Except(children).ToList())
			Children.Remove(removed);
		for (var index = 0; index < children.Count; index++)
		{
			var current = Children.IndexOf(children[index]);
			if (current < 0)
				Children.Insert(index, children[index]);
			else if (current != index)
				Children.Move(current, index);
		}
	}

	private static JsonNode? Get(JsonObject? obj, string name) => obj is not null && ModJson.FindName(obj, name) is { } key ? obj[key] : null;

	private ModPiece? m_piece;
}
