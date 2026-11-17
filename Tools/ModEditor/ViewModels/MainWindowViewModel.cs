using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Threading;
using GoldenAnvil.Utility;
using ModEditor.Model;
using ModEditor.ViewModels.Form;
using OperaEtDies;
using OperaEtDies.Data;

namespace ModEditor.ViewModels;

/// <summary>
/// The editor: the mod being edited (once a folder is opened), its tree, the game data's tree, the JSON of the
/// selected item, and the problems found in the mod. Changes are kept in memory until they're saved.
/// </summary>
public sealed class MainWindowViewModel : ViewModelBase, IDisposable
{
	/// <summary>
	/// Creates the editor. Changed settings are saved with <paramref name="saveSettings"/>, or to the registry if it's null.
	/// </summary>
	public MainWindowViewModel(IEditorDialogs dialogs, EditorSettings settings, Action<EditorSettings>? saveSettings = null)
	{
		m_dialogs = dialogs;
		m_settings = settings;
		m_saveSettings = saveSettings ?? (x => x.Save());
		m_editorText = "";
		m_editorHeading = "";
		m_filter = "";
		m_statusText = "";
		m_toastText = "";
		ModRoots = [];
		GameDataRoots = [];
		Problems = [];
		RecentFolders = new ObservableCollection<string>(settings.RecentFolders);
		m_editTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromMilliseconds(300) };
		m_editTimer.Tick += (_, _) => ApplyEditorText();
		m_validateTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromMilliseconds(600) };
		m_validateTimer.Tick += (_, _) => Validate();
		m_diskTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromMilliseconds(500) };
		m_diskTimer.Tick += (_, _) => OnDiskChangeSettled();
		m_toastTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromSeconds(4) };
		m_toastTimer.Tick += (_, _) =>
		{
			m_toastTimer.Stop();
			ToastText = "";
		};
	}

	/// <summary>
	/// The format of a piece of a mod file on the clipboard.
	/// </summary>
	public const string PieceClipboardFormat = "OperaEtDies.ModEditor.Piece";

	/// <summary>
	/// Raised to show a place in the help.
	/// </summary>
	public event EventHandler<GenericEventArgs<HelpTarget>>? HelpRequested;

	/// <summary>
	/// Shows the help, at the start of the editor's own documentation.
	/// </summary>
	public void ShowHelp()
	{
		if (HelpLibrary.Find(HelpLibrary.ModEditor)?.Headings is [var first, ..])
			RequestHelp(new HelpTarget(HelpLibrary.ModEditor, first.Text));
	}

	/// <summary>
	/// Shows the help about the selected item: what the data files say about a piece, how operations work, or how the
	/// editor organizes files and folders. Without a selection, it's the editor's own documentation.
	/// </summary>
	public void ShowSelectionHelp()
	{
		var target = m_selectedNode switch
		{
			ModNodeViewModel { Location: FolderLocation or FileLocation } => new HelpTarget(HelpLibrary.ModEditor, "Organizing"),
			GameDataNodeViewModel { Parent: null } => HelpTopics.ForSection(""),
			{ } node when node.GetPiece() is { } piece => HelpTopics.ForPiece(piece),
			_ => null,
		};
		if (target is null)
			ShowHelp();
		else
			RequestHelp(target);
	}

	/// <summary>
	/// Shows a place in the help.
	/// </summary>
	public void RequestHelp(HelpTarget target) => HelpRequested.Raise(this, new GenericEventArgs<HelpTarget>(target));

	public string Title => m_project is null ? "Mod Editor" : $"{(m_project.IsModified ? "● " : "")}{m_project.Name} - Mod Editor";

	public EditorSettings Settings => m_settings;

	/// <summary>
	/// The mod being edited, or null until a folder is opened.
	/// </summary>
	public ModProject? Project => m_project;

	public bool IsProjectOpen => m_project is not null;

	public ObservableCollection<string> RecentFolders { get; }

	/// <summary>
	/// The mod's folder, holding its files and subfolders.
	/// </summary>
	public ObservableCollection<TreeNodeViewModel> ModRoots { get; }

	public ObservableCollection<TreeNodeViewModel> GameDataRoots { get; }

	public ObservableCollection<ModProblem> Problems { get; }

	public string ProblemSummary => m_isValidating ? "Checking…" : Problems.Count switch
	{
		0 => "No problems",
		1 => "1 problem",
		var count => $"{count} problems",
	};

	public TreeNodeViewModel? SelectedNode
	{
		get => VerifyAccess(m_selectedNode);
		private set => SetPropertyField(value, ref m_selectedNode);
	}

	/// <summary>
	/// The JSON of the selected item. Typing in it changes the item once the JSON is valid (a whole file's text is
	/// kept as typed, even when it isn't).
	/// </summary>
	public string EditorText
	{
		get => VerifyAccess(m_editorText);
		set
		{
			if (SetPropertyField(value, ref m_editorText) && !m_isLoadingEditor && !IsEditorReadOnly)
			{
				m_editTimer.Stop();
				m_editTimer.Start();
			}
		}
	}

	public string EditorHeading
	{
		get => VerifyAccess(m_editorHeading);
		private set => SetPropertyField(value, ref m_editorHeading);
	}

	/// <summary>
	/// Why the editor's text hasn't been applied, or null if it has.
	/// </summary>
	public string? EditorError
	{
		get => VerifyAccess(m_editorError);
		private set => SetPropertyField(value, ref m_editorError);
	}

	/// <summary>
	/// The form for the selected piece, or null if it can only be edited as JSON.
	/// </summary>
	public FormViewModel? Form
	{
		get => VerifyAccess(m_form);
		private set => SetPropertyField(value, ref m_form);
	}

	/// <summary>
	/// Why there's no form for the selected item, or null if there is one.
	/// </summary>
	public string? FormMessage
	{
		get => VerifyAccess(m_formMessage);
		private set => SetPropertyField(value, ref m_formMessage);
	}

	/// <summary>
	/// The comments of the selected data file or group of operations, for the people editing the mod. Typing in them
	/// changes the file or group.
	/// </summary>
	public string Comments
	{
		get => VerifyAccess(m_comments);
		set
		{
			if (SetPropertyField(value, ref m_comments) && !m_isLoadingEditor)
				ApplyComments();
		}
	}

	/// <summary>
	/// What the comments are for.
	/// </summary>
	public string CommentsHint
	{
		get => VerifyAccess(m_commentsHint);
		private set => SetPropertyField(value, ref m_commentsHint);
	}

	/// <summary>
	/// Whether a data file (or a file that can't be read) or a group of operations is selected, so its comments are
	/// shown.
	/// </summary>
	public bool IsCommentsShown
	{
		get => VerifyAccess(m_isCommentsShown);
		private set => SetPropertyField(value, ref m_isCommentsShown);
	}

	/// <summary>
	/// Whether the comments shown can be edited (they can't while the file can't be read).
	/// </summary>
	public bool IsCommentsEnabled
	{
		get => VerifyAccess(m_isCommentsEnabled);
		private set => SetPropertyField(value, ref m_isCommentsEnabled);
	}

	/// <summary>
	/// Which way the selected item is edited: 0 for the form, 1 for JSON. It's kept between sessions.
	/// </summary>
	public int EditorTab
	{
		get => VerifyAccess(m_settings.EditorTab);
		set
		{
			if (value != m_settings.EditorTab)
			{
				FlushEditorText();
				UpdateSettings(m_settings with { EditorTab = value });
				RaisePropertyChanged(nameof(EditorTab));
			}
		}
	}

	public bool IsEditorReadOnly
	{
		get => VerifyAccess(m_isEditorReadOnly);
		private set => SetPropertyField(value, ref m_isEditorReadOnly);
	}

	/// <summary>
	/// Shows only the items in both trees whose titles have this text, and the items they're in.
	/// </summary>
	public string Filter
	{
		get => VerifyAccess(m_filter);
		set
		{
			if (SetPropertyField(value, ref m_filter))
				ApplyFilter();
		}
	}

	/// <summary>
	/// The result of the last action that couldn't be done, or empty.
	/// </summary>
	public string StatusText
	{
		get => VerifyAccess(m_statusText);
		private set => SetPropertyField(value, ref m_statusText);
	}

	public bool RunAsDeveloper
	{
		get => VerifyAccess(m_settings.RunAsDeveloper);
		set
		{
			if (value != m_settings.RunAsDeveloper)
			{
				UpdateSettings(m_settings with { RunAsDeveloper = value });
				RaisePropertyChanged(nameof(RunAsDeveloper));
			}
		}
	}

	/// <summary>
	/// The files the selected piece can be moved to: data files for items, settings, and sections, and operations files
	/// for operations.
	/// </summary>
	public IReadOnlyList<MoveTarget> MoveTargets
	{
		get
		{
			if (m_project is null || m_selectedNode is not ModNodeViewModel { Location: var location } || location is FolderLocation or FileLocation)
				return [];
			var kind = location is OperationLocation or GroupLocation ? ModFileKind.Operations : ModFileKind.Data;
			return m_project.Files.Where(x => x.Kind == kind && x.Path != location.FilePath).Select(x => new MoveTarget(x.Path, new DelegateCommand(() => MoveTo(x.Path)))).ToList();
		}
	}

	public void UpdateSettings(EditorSettings settings)
	{
		m_settings = settings;
		m_saveSettings(m_settings);
	}

	public void OpenFolder()
	{
		var initial = m_project?.Directory ?? RecentFolders.FirstOrDefault();
		if (m_dialogs.PickFolder(initial is null ? null : Path.GetDirectoryName(initial)) is { } folder)
			OpenFolderAt(folder);
	}

	public void OpenRecentFolder(string folder) => OpenFolderAt(folder);

	public void RemoveRecentFolder(string folder)
	{
		RecentFolders.Remove(folder);
		UpdateSettings(m_settings.WithoutRecentFolder(folder));
	}

	/// <summary>
	/// Opens a mod folder, after asking whether to save the changes to the one that's open.
	/// </summary>
	private void OpenFolderAt(string folder)
	{
		if (!ConfirmDiscard())
			return;

		if (!Directory.Exists(folder))
		{
			m_dialogs.ShowError($"The folder \"{folder}\" doesn't exist.");
			RemoveRecentFolder(folder);
			return;
		}

		ModProject project;
		try
		{
			project = ModProject.Load(folder);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			m_dialogs.ShowError($"The mod couldn't be loaded: {e.Message}");
			return;
		}

		UpdateSettings(m_settings.WithRecentFolder(folder));
		RecentFolders.Clear();
		foreach (var recent in m_settings.RecentFolders)
			RecentFolders.Add(recent);

		// The game data comes first, since checking the mod compares it with the game data.
		if (GameDataRoots.Count == 0)
			LoadGameData();
		SetProject(project);
	}

	public bool CanSave() => m_project is not null;

	public void Save() => TrySave();

	public bool CanReload() => m_project is not null;

	/// <summary>
	/// Loads the mod folder again, after asking whether to save the changes.
	/// </summary>
	public void Reload()
	{
		if (m_project is null || !ConfirmDiscard())
			return;
		LoadGameData();
		LoadAgain();
	}

	/// <summary>
	/// Whether the mod's files changed on disk while there are changes here that loading them again would discard.
	/// </summary>
	public bool IsDiskChangePending
	{
		get => VerifyAccess(m_isDiskChangePending);
		private set => SetPropertyField(value, ref m_isDiskChangePending);
	}

	/// <summary>
	/// Loads the mod's files again after they changed on disk, discarding the changes here.
	/// </summary>
	public void ReloadFromDisk() => LoadAgain();

	/// <summary>
	/// Keeps the changes here after the mod's files changed on disk. Saving only writes the files changed here.
	/// </summary>
	public void DismissDiskChange() => IsDiskChangePending = false;

	/// <summary>
	/// Whether the window can close: there are no changes, or they were saved or discarded.
	/// </summary>
	public bool CanClose() => ConfirmDiscard();

	public bool CanUndo() => m_undo.Count != 0;

	public void Undo() => Restore(m_undo, m_redo);

	public bool CanRedo() => m_redo.Count != 0;

	public void Redo() => Restore(m_redo, m_undo);

	public bool CanRunGame() => m_project is not null;

	/// <summary>
	/// Saves the mod and starts the game with only it loaded.
	/// </summary>
	public void RunGame()
	{
		if (m_project is null || !TrySave())
			return;
		if (GameLauncher.Launch(m_project.Directory, RunAsDeveloper) is { } error)
			m_dialogs.ShowError(error);
	}

	public bool CanNewDataFile() => m_project is not null;

	public void NewDataFile() => NewFile("Data.json", "{}\r\n");

	public bool CanNewOperationsFile() => m_project is not null;

	public void NewOperationsFile() => NewFile("Operations.json", "[]\r\n");

	public bool CanNewFolder() => m_project is not null;

	public void NewFolder()
	{
		if (m_project is not { } project)
			return;
		var parent = GetSelectedFolder();
		var name = PromptName("New Folder", "Name of the new folder:", MakeUniqueName(parent, "New Folder", ""), parent, isFile: false);
		if (name is not null)
		{
			var path = ModFilePath.Combine(parent, name);
			Change(() => project.AddFolder(path), new FolderLocation(path));
		}
	}

	public bool CanRename() => m_selectedNode is ModNodeViewModel { Location: FileLocation or FolderLocation { Path.Length: > 0 } };

	/// <summary>
	/// Renames the selected file or folder.
	/// </summary>
	public void Rename()
	{
		if (m_project is not { } project || !CanRename())
			return;
		var location = ((ModNodeViewModel) m_selectedNode!).Location;
		var path = location is FileLocation file ? file.Path : ((FolderLocation) location).Path;
		var isFile = location is FileLocation;
		var parent = ModFilePath.GetParent(path);
		var name = PromptName(isFile ? "Rename File" : "Rename Folder", "New name:", ModFilePath.GetName(path), parent, isFile, path);
		if (name is null || name == ModFilePath.GetName(path))
			return;

		var newPath = ModFilePath.Combine(parent, name);
		if (isFile)
			Change(() => project.MoveFile(project.FindFile(path)!, newPath), new FileLocation(newPath));
		else
			Change(() => project.MoveFolder(path, newPath), new FolderLocation(newPath));
	}

	public bool CanDelete() => m_selectedNode is ModNodeViewModel { Location: not FolderLocation { Path.Length: 0 } };

	public void Delete()
	{
		if (m_project is not { } project || !CanDelete())
			return;
		var node = (ModNodeViewModel) m_selectedNode!;
		var next = GetSelectionAfterRemoving(node);
		Change(() => ModEditing.Remove(project, node.Location), next);
	}

	public bool CanDuplicate() => m_selectedNode is ModNodeViewModel { Location: FileLocation or ItemLocation or OperationLocation or GroupLocation };

	/// <summary>
	/// Copies the selected file, item, operation, or group to right after it (an item with a new id).
	/// </summary>
	public void Duplicate()
	{
		if (m_project is not { } project || !CanDuplicate())
			return;
		var location = ((ModNodeViewModel) m_selectedNode!).Location;
		if (location is FileLocation fileLocation)
		{
			var file = project.FindFile(fileLocation.Path)!;
			var parent = ModFilePath.GetParent(file.Path);
			var path = ModFilePath.Combine(parent, MakeUniqueName(parent, Path.GetFileNameWithoutExtension(file.Name) + " Copy", ".json"));
			Change(() => project.AddFile(path, file.Text), new FileLocation(path));
		}
		else
		{
			ModLocation? result = null;
			ChangeOrReport(() => (result = ModEditing.Duplicate(project, location)) is null ? "It can't be duplicated." : null, () => result);
		}
	}

	public bool CanCopy() => m_selectedNode?.GetPiece() is not null;

	/// <summary>
	/// Copies the selected piece, so it can be pasted into the mod or another program.
	/// </summary>
	public void Copy()
	{
		if (m_selectedNode?.GetPiece() is { } piece)
			SetClipboard(piece);
	}

	public bool CanCut() => m_selectedNode is ModNodeViewModel && CanCopy();

	public void Cut()
	{
		if (CanCut())
		{
			Copy();
			Delete();
		}
	}

	public bool CanPaste() => m_selectedNode is ModNodeViewModel { Location: not FolderLocation };

	/// <summary>
	/// Pastes after the selected piece, or into the selected file or section. A piece copied in the editor goes where it
	/// belongs. Other JSON is pasted as operations into an operations file, as items or settings into a section, or as
	/// sections into a data file.
	/// </summary>
	public void Paste()
	{
		if (m_project is not { } project || m_selectedNode is not ModNodeViewModel { Location: not FolderLocation } node)
			return;

		var pieces = GetClipboardPieces(node.Location, out var error);
		if (error is not null)
		{
			StatusText = error;
			return;
		}

		ModLocation? last = null;
		ChangeOrReport(() =>
		{
			var target = node.Location;
			foreach (var piece in pieces)
			{
				var (location, insertError) = ModEditing.Insert(project, ModEditing.MakeIdUnique(project, piece, target), target, after: true);
				if (location is null)
					return insertError;
				last = location;
				target = location is SectionLocation ? target : location;
			}

			return null;
		}, () => last);
	}

	public bool CanCopyPath() => m_selectedNode?.GetPiece() is { } piece && ModEditing.GetDataPath(piece) is not null;

	/// <summary>
	/// Copies the path that finds the selected piece from a mod operation.
	/// </summary>
	public void CopyPath()
	{
		if (m_selectedNode?.GetPiece() is { } piece && ModEditing.GetDataPath(piece) is { } path)
			SetClipboardText(path);
	}

	public bool CanReplace() => m_project is not null && m_selectedNode is GameDataNodeViewModel && CanCopyPath();

	/// <summary>
	/// Adds an operation to the mod that replaces the selected piece of the game data, with its current value so it's
	/// easy to see what it was, and selects the operation in the mod's tree to be changed.
	/// </summary>
	public void Replace() => AddOperationFor("replace", includeValue: true);

	public bool CanRemove() => CanReplace();

	/// <summary>
	/// Adds an operation to the mod that removes the selected piece of the game data. The selection stays where it is.
	/// </summary>
	public void Remove() => AddOperationFor("remove", includeValue: false);

	/// <summary>
	/// A short message that goes away on its own, such as what was added to the mod.
	/// </summary>
	public string ToastText
	{
		get => VerifyAccess(m_toastText);
		private set => SetPropertyField(value, ref m_toastText);
	}

	/// <summary>
	/// Which tree is shown: 0 for the mod, 1 for the game data.
	/// </summary>
	public int TreeTab
	{
		get => VerifyAccess(m_treeTab);
		set => SetPropertyField(value, ref m_treeTab);
	}

	public bool CanAddSection() => GetSelectedFile() is { Kind: ModFileKind.Data };

	/// <summary>
	/// Adds a section to the selected data file, with the kind (list or settings) it has in the game data.
	/// </summary>
	public void AddSection()
	{
		if (m_project is not { } project || GetSelectedFile() is not { Root: JsonObject root } file)
			return;
		var suggestions = m_gameData?.Select(x => x.Key).Where(x => ModJson.FindName(root, x) is null).ToList() ?? [];
		var name = m_dialogs.PromptText("Add Section", "Name of the section:", suggestions.FirstOrDefault() ?? "", suggestions)?.Trim();
		if (string.IsNullOrEmpty(name))
			return;
		if (ModJson.FindName(root, name) is { } existing)
		{
			Select(new SectionLocation(file.Path, existing));
			return;
		}

		JsonNode value = m_gameData?[name] is JsonObject ? new JsonObject() : new JsonArray();
		ChangeOrReport(() => ModEditing.Insert(project, ModPiece.WholeSection(name, value), new FileLocation(file.Path), after: true).Error, () => new SectionLocation(file.Path, name));
	}

	public bool CanAddItem() => m_selectedNode is ModNodeViewModel { Location: SectionLocation or ItemLocation or SettingLocation or OperationLocation or GroupLocation } ||
		GetSelectedFile() is { Kind: ModFileKind.Operations } && m_selectedNode is ModNodeViewModel { Location: FileLocation };

	/// <summary>
	/// Adds an item (with a new id) to the selected list section, a setting (named by the user) to the selected
	/// settings section, or an operation to the selected operations file or group, after the selected piece.
	/// </summary>
	public void AddItem()
	{
		if (m_project is not { } project || !CanAddItem())
			return;
		var location = ((ModNodeViewModel) m_selectedNode!).Location;
		ModPiece piece;
		if (location is FileLocation or OperationLocation or GroupLocation)
		{
			piece = ModPiece.Operation(new JsonObject { ["op"] = "set", ["path"] = "", ["value"] = null });
		}
		else
		{
			var section = location switch
			{
				SectionLocation x => x.Section,
				ItemLocation x => x.Section,
				_ => ((SettingLocation) location).Section,
			};
			var sectionNode = ModEditing.GetSection(project.FindFile(location.FilePath!)?.Root, section);
			if (sectionNode is JsonObject)
			{
				var name = m_dialogs.PromptText("Add Setting", $"Name of the setting to add to \"{section}\":", "")?.Trim();
				if (string.IsNullOrEmpty(name))
					return;
				piece = ModPiece.Setting(section, name, null);
			}
			else
			{
				var ids = (sectionNode as JsonArray)?.Select(ModJson.GetId).OfType<string>().ToHashSet() ?? [];
				piece = ModPiece.Item(section, new JsonObject { ["id"] = ModEditing.MakeUnique("newItem", ids.Contains) });
			}
		}

		ModLocation? result = null;
		ChangeOrReport(() =>
		{
			(result, var error) = ModEditing.Insert(project, piece, location, after: true);
			return error;
		}, () => result);
	}

	public bool CanAddGroup() => GetSelectedFile() is { Kind: ModFileKind.Operations } && m_selectedNode is ModNodeViewModel { Location: FileLocation or OperationLocation or GroupLocation };

	/// <summary>
	/// Adds an empty group of operations to the selected operations file, after the selected operation or group (or the
	/// group the selected operation is in).
	/// </summary>
	public void AddGroup()
	{
		if (m_project is not { } project || !CanAddGroup())
			return;
		var location = ((ModNodeViewModel) m_selectedNode!).Location;
		var group = new JsonObject { [DataFileParts.CommentsName] = "", [ModPatcher.GroupOperationsName] = new JsonArray() };
		ModLocation? result = null;
		ChangeOrReport(() =>
		{
			(result, var error) = ModEditing.Insert(project, ModPiece.Group(group), location, after: true);
			return error;
		}, () => result);
	}

	public bool CanMoveUp() => GetSibling(-1) is not null;

	public void MoveUp() => MoveToSibling(-1);

	public bool CanMoveDown() => GetSibling(1) is not null;

	public void MoveDown() => MoveToSibling(1);

	/// <summary>
	/// Moves the selected piece to the end of another file.
	/// </summary>
	public void MoveTo(string filePath)
	{
		if (m_selectedNode is ModNodeViewModel node)
			Drop(node, new FileLocation(filePath), after: true, isCopy: false);
	}

	/// <summary>
	/// Selects the file a problem was found in.
	/// </summary>
	public void ShowProblem(ModProblem problem)
	{
		if (problem.FilePath is { } path)
			Select(new FileLocation(path));
	}

	/// <summary>
	/// Whether one item could be dropped on another: pieces on files and pieces of the mod, and files and folders on
	/// folders.
	/// </summary>
	public static bool CanDrop(TreeNodeViewModel source, TreeNodeViewModel target)
	{
		if (target is not ModNodeViewModel { Location: var location } || source == target)
			return false;
		return source switch
		{
			ModNodeViewModel { Location: FolderLocation folder } => location is FolderLocation { Path: var path } && !ModFilePath.IsWithin(path, folder.Path),
			ModNodeViewModel { Location: FileLocation } => location is FolderLocation,
			_ => source.GetPiece() is not null && location is not FolderLocation,
		};
	}

	/// <summary>
	/// Drops an item on another: a piece goes before or after the target (or into a file or section), a file or folder
	/// goes into the target folder. Pieces of the game data are always copied; the others are moved unless copying.
	/// </summary>
	public void Drop(TreeNodeViewModel source, TreeNodeViewModel target, bool after, bool isCopy)
	{
		if (target is ModNodeViewModel { Location: var location } && CanDrop(source, target))
			Drop(source, location, after, isCopy);
	}

	internal void OnNodeSelected(TreeNodeViewModel node)
	{
		if (m_selectedNode == node)
			return;
		FlushEditorText();
		if (m_selectedNode is not null && m_selectedNode != node)
			m_selectedNode.IsSelected = false;
		SelectedNode = node;
		if (node is ModNodeViewModel modNode)
			m_lastModLocation = modNode.Location;
		m_editSession = null;
		StatusText = "";
		RaisePropertyChanged(nameof(MoveTargets));
		LoadEditorText();
		RefreshChanges();
	}

	private void Drop(TreeNodeViewModel source, ModLocation target, bool after, bool isCopy, bool intoGroup = true)
	{
		if (m_project is not { } project)
			return;

		switch (source)
		{
		case ModNodeViewModel { Location: FolderLocation folder }:
			var folderPath = ModFilePath.Combine(((FolderLocation) target).Path, ModFilePath.GetName(folder.Path));
			if (ModFilePath.AreSame(folderPath, folder.Path))
				return;
			if (project.Exists(folderPath))
				StatusText = $"\"{folderPath}\" already exists.";
			else
				Change(() => project.MoveFolder(folder.Path, folderPath), new FolderLocation(folderPath));
			break;

		case ModNodeViewModel { Location: FileLocation fileLocation }:
			var file = project.FindFile(fileLocation.Path)!;
			var folderTarget = ((FolderLocation) target).Path;
			var newPath = ModFilePath.Combine(folderTarget, isCopy ? MakeUniqueName(folderTarget, Path.GetFileNameWithoutExtension(file.Name), ".json") : file.Name);
			if (!isCopy && ModFilePath.AreSame(newPath, file.Path))
				return;
			if (!isCopy && project.Exists(newPath))
				StatusText = $"\"{newPath}\" already exists.";
			else if (isCopy)
				Change(() => project.AddFile(newPath, file.Text), new FileLocation(newPath));
			else
				Change(() => project.MoveFile(file, newPath), new FileLocation(newPath));
			break;

		case ModNodeViewModel { Location: var sourceLocation }:
			ModLocation? moved = null;
			ChangeOrReport(() =>
			{
				(moved, var error) = ModEditing.Move(project, sourceLocation, target, after, isCopy, intoGroup);
				return error;
			}, () => moved);
			break;

		default:
			if (source.GetPiece() is { } piece)
			{
				ModLocation? inserted = null;
				ChangeOrReport(() =>
				{
					(inserted, var error) = ModEditing.Insert(project, ModEditing.MakeIdUnique(project, piece, target), target, after);
					return error;
				}, () => inserted);
			}

			break;
		}
	}

	/// <summary>
	/// Stops watching the mod folder.
	/// </summary>
	public void Dispose() => StopWatching();

	/// <summary>
	/// Loads the mod's files again, keeping the selection where it can.
	/// </summary>
	private void LoadAgain()
	{
		if (m_project is null)
			return;
		try
		{
			var location = (m_selectedNode as ModNodeViewModel)?.Location;
			SetProject(ModProject.Load(m_project.Directory));
			Select(location);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			m_dialogs.ShowError($"The mod couldn't be loaded: {e.Message}");
		}
	}

	/// <summary>
	/// Watches the mod folder (and its subfolders) for changes made by other programs.
	/// </summary>
	private void WatchFolder(string directory)
	{
		StopWatching();
		try
		{
			m_watcher = new FileSystemWatcher(directory)
			{
				IncludeSubdirectories = true,
				NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size,
			};
			m_watcher.Changed += OnFileChanged;
			m_watcher.Created += OnFileChanged;
			m_watcher.Deleted += OnFileChanged;
			m_watcher.Renamed += OnFileChanged;
			m_watcher.EnableRaisingEvents = true;
		}
		catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException)
		{
			// Without a watcher, changes on disk are only seen when the mod is loaded again.
			StopWatching();
		}
	}

	private void StopWatching()
	{
		m_diskTimer.Stop();
		m_watcher?.Dispose();
		m_watcher = null;
	}

	// File system events come from other threads, and often several at once, so they're handled once they settle.
#pragma warning disable VSTHRD001 // Nothing waits for this, so it can't deadlock.
	private void OnFileChanged(object sender, FileSystemEventArgs e) => _ = Dispatcher.BeginInvoke(() =>
	{
		m_diskTimer.Stop();
		m_diskTimer.Start();
	});
#pragma warning restore VSTHRD001

	/// <summary>
	/// Once changes on disk settle, compares the mod folder with the files as they were last loaded or saved. The
	/// editor's own saves, and changes that leave the files as they were, are passed over. Otherwise the files are
	/// loaded again, unless there are changes here, in which case the user is asked first.
	/// </summary>
	private void OnDiskChangeSettled()
	{
		m_diskTimer.Stop();
		if (m_project is not { } project)
			return;

		ModSnapshot onDisk;
		try
		{
			onDisk = ModProject.Load(project.Directory).TakeSnapshot();
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			// A file that's still being written is tried again a moment later.
			m_diskTimer.Start();
			return;
		}

		if (onDisk.IsSameAs(project.SavedSnapshot))
			return;
		FlushEditorText();
		if (project.IsModified)
		{
			IsDiskChangePending = true;
		}
		else
		{
			LoadAgain();
			ShowToast("The mod's files changed on disk, so they were loaded again.");
		}
	}

	private void SetProject(ModProject project)
	{
		m_editTimer.Stop();
		IsDiskChangePending = false;
		WatchFolder(project.Directory);
		m_project = project;
		m_undo.Clear();
		m_redo.Clear();
		m_lastModLocation = null;
		m_editSession = null;
		m_modNodes.Clear();
		ModRoots.Clear();
		SelectedNode = null;
		var root = new ModNodeViewModel(this, null, new FolderLocation("")) { IsExpanded = true };
		m_modNodes[root.Location] = root;
		ModRoots.Add(root);
		RefreshModTree();
		LoadEditorText();
		Validate();
		RaisePropertyChanged(nameof(Project), nameof(IsProjectOpen), nameof(Title));
	}

	/// <summary>
	/// What the mod changes in the game data, as of the last check.
	/// </summary>
	internal ModChanges CurrentChanges => m_changes;

	/// <summary>
	/// Whether the Game Data tab only shows what the mod changes (and the sections it's in).
	/// </summary>
	public bool IsOnlyChangedShown
	{
		get => VerifyAccess(m_isOnlyChangedShown);
		set
		{
			if (SetPropertyField(value, ref m_isOnlyChangedShown))
				ApplyFilter();
		}
	}

	/// <summary>
	/// The Changes tab: what the mod changes in the selected piece of the game data, or what the selected piece of the
	/// mod changes.
	/// </summary>
	public IReadOnlyList<ChangeRowViewModel> ChangeRows
	{
		get => VerifyAccess(m_changeRows);
		private set => SetPropertyField(value, ref m_changeRows);
	}

	/// <summary>
	/// What the Changes tab says about the selection, such as that the mod doesn't change it.
	/// </summary>
	public string ChangesMessage
	{
		get => VerifyAccess(m_changesMessage);
		private set => SetPropertyField(value, ref m_changesMessage);
	}

	public bool CanGoToModChange() => m_selectedNode is GameDataNodeViewModel { Change: not null, Section: not null } node && GetLastSource(node) is not null;

	/// <summary>
	/// Goes from a piece of the game data to the part of the mod that last changed it, in the Mod tab.
	/// </summary>
	public void GoToModChange()
	{
		if (m_selectedNode is GameDataNodeViewModel node && GetLastSource(node) is { } source)
			GoToSource(source);
	}

	/// <summary>
	/// Shows a part of the mod in the Mod tab.
	/// </summary>
	public void GoToSource(ModLocation location)
	{
		TreeTab = 0;
		Select(location);
	}

	/// <summary>
	/// Works out what a mod changes in the game data, from the game data's JSON. Can be run on any thread.
	/// </summary>
	private static ModChanges ComputeChanges(string? gameDataJson, ModSnapshot snapshot)
	{
		if (gameDataJson is null || JsonNode.Parse(gameDataJson, new JsonNodeOptions { PropertyNameCaseInsensitive = true }) is not JsonObject gameData)
			return ModChanges.None;
		return ModChanges.Compute(gameData, snapshot.Files);
	}

	/// <summary>
	/// Shows what the mod changes: in the Game Data tab, on the mod's operations, and in the Changes tab.
	/// </summary>
	private void ApplyChanges(ModChanges changes)
	{
		m_changes = changes;
		if (GameDataRoots.FirstOrDefault() is GameDataNodeViewModel root)
			root.Update(m_gameData, changes, m_gameDataDescription);
		UpdateChangeMarks();
		ApplyFilter();
		RefreshChanges();

		// A piece of the game data shows its value with the mod, which may have changed.
		if (m_selectedNode is GameDataNodeViewModel)
			LoadEditorText();
	}

	/// <summary>
	/// Marks the operations whose changes don't last, or that change nothing.
	/// </summary>
	private void UpdateChangeMarks()
	{
		foreach (var node in m_modNodes.Values)
			node.Warning = node.Location is OperationLocation operation ? m_changes.GetOperationWarning(operation) : null;
	}

	/// <summary>
	/// Fills in the Changes tab for the selection.
	/// </summary>
	private void RefreshChanges()
	{
		IReadOnlyList<DataChange> changes;
		var showSources = true;
		switch (m_selectedNode)
		{
		case GameDataNodeViewModel { Section: { } section } node:
			changes = m_changes.GetChanges(section, node.Key);
			ChangesMessage = changes.Count == 0 ? "The mod doesn't change this." : "";
			break;
		case ModNodeViewModel { Location: var location } when GetSourceChanges(location) is { } sourceChanges:
			changes = sourceChanges;
			showSources = false;
			ChangesMessage = changes.Count != 0 ? "What this changes when it's applied:"
				: location is OperationLocation operation && m_changes.GetOperationWarning(operation) is { } warning ? warning
				: "This doesn't change the game data.";
			break;
		default:
			changes = [];
			ChangesMessage = "Select a piece of the game data to see what the mod changes in it, or a piece of the mod to see what it changes.";
			break;
		}

		ChangeRows = changes.Select(change => new ChangeRowViewModel(change,
			showSources ? m_changes.GetSources(change.Path).Select(x => RefineSource(x, change)).Distinct().Select(x => new ChangeSourceViewModel(DescribeSource(x), () => GoToSource(x))).ToList() : [])).ToList();
	}

	/// <summary>
	/// Gets what a piece of the mod changes when it's applied, or null if it isn't a piece that's applied on its own (a
	/// data file, or something in one, or an operation or group of them).
	/// </summary>
	private IReadOnlyList<DataChange>? GetSourceChanges(ModLocation location) => location switch
	{
		OperationLocation x => m_changes.BySource.GetValueOrDefault(x) ?? [],
		GroupLocation x => m_changes.BySource.Where(c => c.Key is OperationLocation o && o.File == x.File && o.Group == x.Index).SelectMany(c => c.Value).ToList(),
		FileLocation x when m_project?.FindFile(x.Path)?.Kind == ModFileKind.Data => m_changes.BySource.GetValueOrDefault(x) ?? [],
		SectionLocation x => FileChanges(x.File).Where(c => string.Equals(c.Section, x.Section, StringComparison.OrdinalIgnoreCase)).ToList(),
		ItemLocation x => FileChanges(x.File).Where(c => string.Equals(c.Section, x.Section, StringComparison.OrdinalIgnoreCase) && c.Item == ModJson.GetId(ModEditing.Get(m_project!, x))).ToList(),
		SettingLocation x => FileChanges(x.File).Where(c => string.Equals(c.Section, x.Section, StringComparison.OrdinalIgnoreCase) && string.Equals(c.Item, x.Name, StringComparison.OrdinalIgnoreCase)).ToList(),
		_ => null,
	};

	private IReadOnlyList<DataChange> FileChanges(string path) => m_changes.BySource.GetValueOrDefault(new FileLocation(path)) ?? [];

	/// <summary>
	/// Gets the part of the mod that last changed the data at a path (or in or around it).
	/// </summary>
	internal ModLocation? GetLastSourceAt(string path)
	{
		if (m_changes.GetLastChangeAt(path) is not { } change || m_changes.GetSources(change.Path) is not [.., var source])
			return null;
		return RefineSource(source, change);
	}

	/// <summary>
	/// Gets the part of the mod that last changed a piece of the game data.
	/// </summary>
	private ModLocation? GetLastSource(GameDataNodeViewModel node)
	{
		if (m_changes.GetChanges(node.Section!, node.Key) is not [.., var change] || m_changes.GetSources(change.Path) is not [.., var source])
			return null;
		return RefineSource(source, change);
	}

	/// <summary>
	/// Narrows a data file to the item or setting in it that made a change, if it has one, so going to it selects that.
	/// </summary>
	private ModLocation RefineSource(ModLocation source, DataChange change)
	{
		if (source is not FileLocation file || change.Item is null || m_project?.FindFile(file.Path)?.Root is not JsonObject root ||
			ModEditing.GetSection(root, change.Section) is not { } section || ModJson.FindName(root, change.Section) is not { } sectionName)
		{
			return source;
		}

		return section switch
		{
			JsonArray items when items.Select(ModJson.GetId).ToList().IndexOf(change.Item) is >= 0 and var index => new ItemLocation(file.Path, sectionName, index),
			JsonObject settings when ModJson.FindName(settings, change.Item) is { } name => new SettingLocation(file.Path, sectionName, name),
			_ => source,
		};
	}

	/// <summary>
	/// Describes a part of the mod for the Changes tab: where it is in the Mod tab's tree.
	/// </summary>
	private string DescribeSource(ModLocation location) =>
		m_modNodes.TryGetValue(location, out var node) ? string.Join(" › ", node.GetAncestors().Reverse().Skip(1).Select(x => x.Title).Append(node.Title + (node.Detail.Length == 0 ? "" : " " + node.Detail))) : location.FilePath ?? "";

	/// <summary>
	/// Loads the game data without mods, for its tree and the names of its sections.
	/// </summary>
	private void LoadGameData()
	{
		var document = DataDocument.Read(ModValidator.DataDirectory, []);
		m_gameData = document.Value?.Root;
		m_gameDataJson = m_gameData?.ToJsonString();
		m_gameDataDescription = document.Value is null ? "couldn't be read" : "";
		GameDataRoots.Clear();
		GameDataRoots.Add(GameDataNodeViewModel.CreateRoot(this));
		ApplyChanges(m_changes);

		// What the mod changes is worked out again from the game data as it is now.
		if (m_project is not null)
			Validate();
	}

	private bool TrySave()
	{
		if (m_project is null)
			return false;
		FlushEditorText();
		try
		{
			m_project.Save();
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException or OperationCanceledException)
		{
			m_dialogs.ShowError($"The mod couldn't be saved: {e.Message}");
			return false;
		}

		RaisePropertyChanged(nameof(Title));
		return true;
	}

	/// <summary>
	/// Asks whether to save the changes, if there are any. Returns false if the user cancelled or saving failed.
	/// </summary>
	private bool ConfirmDiscard()
	{
		FlushEditorText();
		if (m_project is not { IsModified: true } project)
			return true;
		return m_dialogs.AskToSave(project.Name) switch
		{
			true => TrySave(),
			false => true,
			null => false,
		};
	}

	private void NewFile(string defaultName, string text)
	{
		if (m_project is not { } project)
			return;
		var parent = GetSelectedFolder();
		var name = PromptName("New File", "Name of the new file:", MakeUniqueName(parent, Path.GetFileNameWithoutExtension(defaultName), ".json"), parent, isFile: true);
		if (name is not null)
		{
			var path = ModFilePath.Combine(parent, name);
			Change(() => project.AddFile(path, text), new FileLocation(path));
		}
	}

	/// <summary>
	/// Asks for a file or folder name, until it's one that can be used in the folder. A file name gets ".json" added if
	/// it doesn't have it.
	/// </summary>
	private string? PromptName(string title, string message, string initial, string parent, bool isFile, string? currentPath = null)
	{
		var name = initial;
		while (true)
		{
			name = m_dialogs.PromptText(title, message, name)?.Trim();
			if (name is null || name.Length == 0)
				return null;
			if (isFile)
				name = ModFilePath.EnsureJsonExtension(name);

			var path = ModFilePath.Combine(parent, name);
			var problem = ModFilePath.CheckName(name) ?? (m_project!.Exists(path) && !(currentPath is not null && ModFilePath.AreSame(path, currentPath)) ? $"\"{path}\" already exists." : null);
			if (problem is null)
				return name;
			m_dialogs.ShowError(problem);
		}
	}

	private string MakeUniqueName(string folder, string name, string extension) =>
		ModEditing.MakeUnique(name, x => m_project!.Exists(ModFilePath.Combine(folder, x + extension))) + extension;

	private string GetSelectedFolder() => (m_selectedNode as ModNodeViewModel)?.Location switch
	{
		FolderLocation x => x.Path,
		{ FilePath: { } path } => ModFilePath.GetParent(path),
		_ => "",
	};

	private ModFile? GetSelectedFile() =>
		(m_selectedNode as ModNodeViewModel)?.Location.FilePath is { } path ? m_project?.FindFile(path) : null;

	/// <summary>
	/// Gets the piece next to the selected one in the same list or section, or null if there isn't one.
	/// </summary>
	private ModNodeViewModel? GetSibling(int offset)
	{
		if (m_selectedNode is not ModNodeViewModel { Location: ItemLocation or SettingLocation or OperationLocation or GroupLocation, Parent: { } parent } node)
			return null;
		var index = parent.Children.IndexOf(node) + offset;
		return index >= 0 && index < parent.Children.Count ? parent.Children[index] as ModNodeViewModel : null;
	}

	/// <summary>
	/// Moves the selected piece past the one next to it, in the same list (an operation moves past a group, rather than
	/// into it).
	/// </summary>
	private void MoveToSibling(int offset)
	{
		if (m_selectedNode is ModNodeViewModel node && GetSibling(offset) is { } sibling)
			Drop(node, sibling.Location, after: offset > 0, isCopy: false, intoGroup: false);
	}

	private static ModLocation? GetSelectionAfterRemoving(ModNodeViewModel node)
	{
		if (node.Parent is not { } parent)
			return null;
		var index = parent.Children.IndexOf(node);
		// List items after the removed one move up, so the one that takes its place has its location, with one less
		// for its index.
		if (node.Location is ItemLocation or OperationLocation or GroupLocation && index + 1 < parent.Children.Count && parent.Children[index + 1] is ModNodeViewModel following)
		{
			return following.Location switch
			{
				ItemLocation x => x with { Index = x.Index - 1 },
				OperationLocation x => x with { Index = x.Index - 1 },
				GroupLocation x => x with { Index = x.Index - 1 },
				var x => x,
			};
		}

		if (index + 1 < parent.Children.Count && parent.Children[index + 1] is ModNodeViewModel next)
			return next.Location;
		return index > 0 && parent.Children[index - 1] is ModNodeViewModel previous ? previous.Location : (parent as ModNodeViewModel)?.Location;
	}

	/// <summary>
	/// Adds an operation on the selected piece of the game data to the mod. It goes after the operation last selected in
	/// the mod (in its group, if it's in one), or at the end of the group or operations file last selected, or else at
	/// the end of the first operations file (which is made if there isn't one). A replace is selected in the mod's tree;
	/// a remove is only announced.
	/// </summary>
	private void AddOperationFor(string op, bool includeValue)
	{
		if (m_selectedNode?.GetPiece() is { } piece && ModEditing.GetDataPath(piece) is { } path)
			AddOperation(op, path, includeValue, piece.Value);
	}

	/// <summary>
	/// Whether a mod is open, so operations can be added to it.
	/// </summary>
	internal bool CanAddOperation => m_project is not null;

	/// <summary>
	/// Adds an operation to the mod for a path in the game data: a replace (with the current value, so it's easy to see what
	/// it was), which is then selected in the mod's tree to be changed, or a remove, after which the selection stays where it
	/// is.
	/// </summary>
	internal void AddOperation(string op, string path, bool includeValue, JsonNode? value)
	{
		if (m_project is not { } project)
			return;
		var operation = new JsonObject { ["op"] = op, ["path"] = path };
		if (includeValue)
			operation["value"] = ModJson.Clone(value);

		ModLocation? added = null;
		ChangeOrReport(() =>
		{
			var target = m_lastModLocation is { FilePath: { } lastPath } last && project.FindFile(lastPath) is { Kind: ModFileKind.Operations }
				? last is OperationLocation or GroupLocation ? last : new FileLocation(lastPath)
				: null;
			if (target is null)
			{
				var file = project.Files.FirstOrDefault(x => x.Kind == ModFileKind.Operations)
					?? project.AddFile(MakeUniqueName("", "Operations", ".json"), "[]\r\n");
				target = new FileLocation(file.Path);
			}

			(added, var error) = ModEditing.Insert(project, ModPiece.Operation(operation), target, after: true);
			return error;
		}, () => includeValue ? added : null);

		if (added is null)
			return;
		if (includeValue)
		{
			TreeTab = 0;
		}
		else
		{
			m_lastModLocation = added;
			ShowToast($"Added \"remove {path}\" to {added.FilePath}");
		}
	}

	private void ShowToast(string text)
	{
		ToastText = text;
		m_toastTimer.Stop();
		m_toastTimer.Start();
	}

	private static void SetClipboard(ModPiece piece)
	{
		var text = piece.Kind switch
		{
			ModPieceKind.Setting => ModJson.Format(new JsonObject { [piece.Name!] = ModJson.Clone(piece.Value) }),
			ModPieceKind.Section => ModJson.Format(new JsonObject { [piece.Section!] = ModJson.Clone(piece.Value) }),
			_ => ModJson.Format(piece.Value),
		};
		var data = new DataObject();
		data.SetText(text);
		data.SetData(PieceClipboardFormat, piece.ToJson().ToJsonString());
		TrySetClipboard(() => Clipboard.SetDataObject(data, copy: true));
	}

	private static void SetClipboardText(string text) => TrySetClipboard(() => Clipboard.SetText(text));

	// The clipboard can be held by another program for a moment.
	private static void TrySetClipboard(Action set)
	{
		try
		{
			set();
		}
		catch (System.Runtime.InteropServices.COMException)
		{
		}
	}

	/// <summary>
	/// Reads the pieces to paste at a location from the clipboard.
	/// </summary>
	private IReadOnlyList<ModPiece> GetClipboardPieces(ModLocation target, out string? error)
	{
		error = null;
		IDataObject? data;
		try
		{
			data = Clipboard.GetDataObject();
		}
		catch (System.Runtime.InteropServices.COMException)
		{
			data = null;
		}

		if (data?.GetData(PieceClipboardFormat) is string pieceText && ModPiece.FromJson(ModJson.Parse(pieceText).Node) is { } piece)
			return [piece];

		if (data?.GetData(DataFormats.UnicodeText) is not string text)
		{
			error = "There's nothing to paste.";
			return [];
		}

		var (json, parseError) = ModJson.Parse(text);
		if (parseError is not null)
		{
			error = $"The clipboard doesn't hold valid JSON: {parseError}";
			return [];
		}

		var file = target.FilePath is { } path ? m_project?.FindFile(path) : null;
		var section = target switch
		{
			SectionLocation x => x.Section,
			ItemLocation x => x.Section,
			SettingLocation x => x.Section,
			_ => null,
		};
		IEnumerable<JsonNode?> AsList(JsonNode? node) => node is JsonArray array ? array.ToList() : [node];

		if (file?.Kind == ModFileKind.Operations)
			return AsList(json).Select(x => ModPatcher.IsGroup(x) ? ModPiece.Group(x) : ModPiece.Operation(x)).ToList();
		if (section is not null && ModEditing.GetSection(file?.Root, section) is JsonObject)
			return json is JsonObject settings ? settings.Select(x => ModPiece.Setting(section, x.Key, x.Value)).ToList() : Fail("Settings are pasted as an object of settings.", out error);
		if (section is not null)
			return AsList(json).Select(x => ModPiece.Item(section, x)).ToList();
		if (file?.Kind == ModFileKind.Data && json is JsonObject sections)
			return sections.Select(x => ModPiece.WholeSection(x.Key, x.Value)).ToList();
		return Fail(file?.Kind == ModFileKind.Data ? "Only an object of sections can be pasted into a data file." : "The file can't be read.", out error);

		static IReadOnlyList<ModPiece> Fail(string message, out string? error)
		{
			error = message;
			return [];
		}
	}

	/// <summary>
	/// Makes a change that can't fail, which can be undone, and selects the location afterwards.
	/// </summary>
	private void Change(Action change, ModLocation? select)
	{
		ChangeOrReport(() =>
		{
			change();
			return null;
		}, () => select);
	}

	/// <summary>
	/// Makes a change that can be undone. If it fails, the mod is put back as it was and the reason is shown.
	/// </summary>
	private void ChangeOrReport(Func<string?> change, Func<ModLocation?> getSelection)
	{
		if (m_project is not { } project)
			return;
		FlushEditorText();
		var snapshot = project.TakeSnapshot();
		if (change() is { } error)
		{
			project.Restore(snapshot);
			StatusText = error;
			return;
		}

		PushUndo(snapshot);
		m_editSession = null;
		OnProjectChanged(getSelection(), reloadEditor: true);
		StatusText = "";
	}

	private void PushUndo(ModSnapshot snapshot)
	{
		m_undo.Push(snapshot);
		m_redo.Clear();
	}

	private void Restore(Stack<ModSnapshot> from, Stack<ModSnapshot> to)
	{
		FlushEditorText();
		if (m_project is not { } project || from.Count == 0)
			return;
		to.Push(project.TakeSnapshot());
		project.Restore(from.Pop());
		m_editSession = null;
		OnProjectChanged((m_selectedNode as ModNodeViewModel)?.Location, reloadEditor: true);
	}

	private void OnProjectChanged(ModLocation? select, bool reloadEditor)
	{
		RefreshModTree();
		var selected = m_selectedNode as ModNodeViewModel;
		var target = select ?? selected?.Location;
		if (target is not null && !m_modNodes.ContainsKey(target))
			target = FindNearestExisting(target);
		if (target is not null && target != selected?.Location)
			Select(target);
		else if (reloadEditor)
			LoadEditorText();
		ScheduleValidation();
		RaisePropertyChanged(nameof(Title), nameof(MoveTargets));
	}

	/// <summary>
	/// Selects the item at a location, showing it in the tree.
	/// </summary>
	private void Select(ModLocation? location)
	{
		if (location is null || !m_modNodes.TryGetValue(location, out var node))
		{
			LoadEditorText();
			return;
		}

		foreach (var ancestor in node.GetAncestors())
			ancestor.IsExpanded = true;
		if (node.IsSelected)
			LoadEditorText();
		else
			node.IsSelected = true;
	}

	private ModLocation? FindNearestExisting(ModLocation location)
	{
		for (var current = GetParentLocation(location); current is not null; current = GetParentLocation(current))
		{
			if (m_modNodes.ContainsKey(current))
				return current;
		}

		return null;
	}

	private static ModLocation? GetParentLocation(ModLocation location) => location switch
	{
		ItemLocation x => new SectionLocation(x.File, x.Section),
		SettingLocation x => new SectionLocation(x.File, x.Section),
		SectionLocation x => new FileLocation(x.File),
		OperationLocation { Group: { } group } x => new GroupLocation(x.File, group),
		OperationLocation x => new FileLocation(x.File),
		GroupLocation x => new FileLocation(x.File),
		FileLocation x => new FolderLocation(ModFilePath.GetParent(x.Path)),
		FolderLocation { Path.Length: > 0 } x => new FolderLocation(ModFilePath.GetParent(x.Path)),
		_ => null,
	};

	/// <summary>
	/// Brings the mod's tree up to date, keeping the items that are still there (so they stay selected and expanded).
	/// </summary>
	private void RefreshModTree()
	{
		if (m_project is not { } project || ModRoots.FirstOrDefault() is not ModNodeViewModel root)
			return;
		var seen = new HashSet<ModLocation>();
		RefreshNode(project, root, seen);
		foreach (var location in m_modNodes.Keys.Where(x => !seen.Contains(x)).ToList())
			m_modNodes.Remove(location);
		UpdateProblemMarks();
		UpdateChangeMarks();
		ApplyFilter();
	}

	private void RefreshNode(ModProject project, ModNodeViewModel node, HashSet<ModLocation> seen)
	{
		seen.Add(node.Location);
		node.Update(project);
		var desired = GetChildLocations(project, node.Location);
		var existing = node.Children.OfType<ModNodeViewModel>().ToDictionary(x => x.Location);

		var children = desired.Select(x => existing.TryGetValue(x, out var child) ? child : new ModNodeViewModel(this, node, x)).ToList();
		foreach (var removed in node.Children.Except(children).ToList())
			node.Children.Remove(removed);
		for (var index = 0; index < children.Count; index++)
		{
			var child = children[index];
			var current = node.Children.IndexOf(child);
			if (current < 0)
				node.Children.Insert(index, child);
			else if (current != index)
				node.Children.Move(current, index);
			m_modNodes[child.Location] = child;
			RefreshNode(project, child, seen);
		}
	}

	/// <summary>
	/// Gets the locations of the items within a folder, file, or section, in order. A folder's subfolders and files are
	/// in the order the game applies the files.
	/// </summary>
	private static IReadOnlyList<ModLocation> GetChildLocations(ModProject project, ModLocation location)
	{
		switch (location)
		{
		case FolderLocation folder:
			var folders = project.Folders.Where(x => ModFilePath.AreSame(ModFilePath.GetParent(x), folder.Path)).Select(x => (Key: ModFilePath.GetSortKey(ModFilePath.GetName(x), true), Location: (ModLocation) new FolderLocation(x)));
			var files = project.Files.Where(x => ModFilePath.AreSame(ModFilePath.GetParent(x.Path), folder.Path)).Select(x => (Key: ModFilePath.GetSortKey(x.Name, false), Location: (ModLocation) new FileLocation(x.Path)));
			return folders.Concat(files).OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Location).ToList();
		case FileLocation file:
			return project.FindFile(file.Path)?.Root switch
			{
				JsonObject sections => sections.Select(x => (ModLocation) new SectionLocation(file.Path, x.Key)).ToList(),
				JsonArray operations => Enumerable.Range(0, operations.Count)
					.Select(x => ModPatcher.IsGroup(operations[x]) ? (ModLocation) new GroupLocation(file.Path, x) : new OperationLocation(file.Path, x)).ToList(),
				_ => [],
			};
		case GroupLocation group:
			var groupOperations = ModEditing.GetOperationList(project.FindFile(group.File)?.Root, group.Index);
			return groupOperations is null ? [] : Enumerable.Range(0, groupOperations.Count).Select(x => (ModLocation) new OperationLocation(group.File, x, group.Index)).ToList();
		case SectionLocation section:
			return ModEditing.Get(project, section) switch
			{
				JsonArray items => Enumerable.Range(0, items.Count).Select(x => (ModLocation) new ItemLocation(section.File, section.Section, x)).ToList(),
				// A settings section's comments are edited in its form, not as a setting.
				JsonObject settings => settings.Where(x => !ModJson.IsComments(x.Key, x.Value)).Select(x => (ModLocation) new SettingLocation(section.File, section.Section, x.Key)).ToList(),
				_ => [],
			};
		default:
			return [];
		}
	}

	private void ApplyFilter()
	{
		var filter = m_filter.Trim();
		foreach (var root in ModRoots)
			root.ApplyFilter(filter);
		foreach (var root in GameDataRoots)
			root.ApplyFilter(filter, m_isOnlyChangedShown);
	}

	private void LoadEditorText()
	{
		m_editTimer.Stop();
		m_isLoadingEditor = true;
		try
		{
			EditorError = null;
			switch (m_selectedNode)
			{
			case ModNodeViewModel { Location: FolderLocation } node:
				EditorHeading = node.GetBreadcrumb();
				IsEditorReadOnly = true;
				EditorText = "";
				break;
			case ModNodeViewModel { Location: FileLocation file } node when m_project?.FindFile(file.Path) is { } modFile:
				EditorHeading = node.GetBreadcrumb();
				IsEditorReadOnly = false;
				EditorText = modFile.Text;
				EditorError = modFile.ParseError;
				break;
			case ModNodeViewModel node when m_project is not null:
				EditorHeading = node.GetBreadcrumb();
				IsEditorReadOnly = false;
				EditorText = ModJson.Format(ModEditing.Get(m_project, node.Location));
				break;
			case GameDataNodeViewModel node:
				EditorHeading = node.GetBreadcrumb() + " (read only)";
				IsEditorReadOnly = true;
				EditorText = node.GetPiece() is { } piece ? ModJson.Format(piece.Value) : "";
				break;
			default:
				EditorHeading = "";
				IsEditorReadOnly = true;
				EditorText = "";
				break;
			}
		}
		finally
		{
			m_isLoadingEditor = false;
		}

		LoadComments();
		BuildForm();
	}

	/// <summary>
	/// Shows the comments of the selected data file (which can only be edited if the file can be read) or group of
	/// operations. A file that can't be read might be a data file, so its comments are shown, turned off.
	/// </summary>
	private void LoadComments()
	{
		var location = (m_selectedNode as ModNodeViewModel)?.Location;
		var file = location is FileLocation fileLocation ? m_project?.FindFile(fileLocation.Path) : null;
		var group = location is GroupLocation && m_project is { } project ? ModEditing.Get(project, location) as JsonObject : null;
		var wasLoading = m_isLoadingEditor;
		m_isLoadingEditor = true;
		try
		{
			IsCommentsShown = file is { Kind: not ModFileKind.Operations } || group is not null;
			IsCommentsEnabled = file is { Kind: ModFileKind.Data } || group is not null;
			CommentsHint = group is not null ? "What the group's operations are for, for the people editing the mod. The game ignores them."
				: "What the file is for, for the people editing the mod. The game ignores them.";
			Comments = (group is not null ? ModOperations.GetComments(group) : file?.Comments) ?? "";
		}
		finally
		{
			m_isLoadingEditor = wasLoading;
		}
	}

	/// <summary>
	/// Applies the comments typed for the selected data file or group. Typing in the same file or group is undone all
	/// at once. Empty comments are left out.
	/// </summary>
	private void ApplyComments()
	{
		if (m_project is not { } project || m_selectedNode is not ModNodeViewModel { Location: FileLocation or GroupLocation } node)
			return;

		// Captured first, since applying JSON typed for the file or group loads its comments again.
		var comments = string.IsNullOrWhiteSpace(m_comments) ? null : m_comments;
		FlushEditorText();
		var location = node.Location;
		var file = location is FileLocation fileLocation ? project.FindFile(fileLocation.Path) : null;
		var group = location is GroupLocation ? ModEditing.Get(project, location) as JsonObject : null;
		var existing = group is not null ? ModOperations.GetComments(group) : file?.Comments;
		if ((file is { Kind: ModFileKind.Data } || group is not null) && comments != (string.IsNullOrWhiteSpace(existing) ? null : existing))
		{
			var snapshot = project.TakeSnapshot();
			if (group is not null)
				ModEditing.Replace(project, location, WithComments(group, comments));
			else
				file!.SetComments(comments);
			if (m_editSession != location)
			{
				PushUndo(snapshot);
				m_editSession = location;
			}

			m_isLoadingEditor = true;
			try
			{
				EditorText = file?.Text ?? ModJson.Format(ModEditing.Get(project, location));
				EditorError = null;
			}
			finally
			{
				m_isLoadingEditor = false;
			}

			OnProjectChanged(null, reloadEditor: false);
			EditorHeading = node.GetBreadcrumb();
		}

		// A copy of a group with its comments changed. It keeps "operations" (adding it if it's missing), so it's still a
		// group without comments.
		static JsonObject WithComments(JsonObject group, string? comments)
		{
			var copy = (JsonObject) group.DeepClone();
			var name = ModJson.FindName(copy, DataFileParts.CommentsName);
			if (comments is null && name is not null)
				copy.Remove(name);
			else if (comments is not null)
				copy[name ?? DataFileParts.CommentsName] = comments;
			if (ModJson.FindName(copy, ModPatcher.GroupOperationsName) is null)
				copy[ModPatcher.GroupOperationsName] = new JsonArray();
			return copy;
		}
	}

	/// <summary>
	/// Builds the form for the selected piece, or says why there isn't one.
	/// </summary>
	private void BuildForm()
	{
		var piece = m_selectedNode?.GetPiece();
		var form = piece is null ? null : FormViewModel.Create(piece, new IdCatalog(m_gameData, m_project?.Files ?? []), m_selectedNode is GameDataNodeViewModel, OnFormChanged, RequestHelp,
			m_selectedNode is GameDataNodeViewModel ? new FormChanges(this) : null);
		Form = form;
		FormMessage = form is not null ? null : m_selectedNode switch
		{
			null => "",
			ModNodeViewModel { Location: FolderLocation } => "Select a piece of a file to edit it here.",
			ModNodeViewModel { Location: FileLocation } => "Select a piece of the file to edit it here, or edit the whole file as JSON.",
			ModNodeViewModel { Location: GroupLocation } => "Select an operation in the group to edit it here, or edit the whole group as JSON.",
			_ when piece?.Kind == ModPieceKind.Section => "Select an item in the section to edit it here, or edit the whole section as JSON.",
			_ => "This can only be edited as JSON.",
		};
	}

	/// <summary>
	/// Applies a change made in the form to the selected piece. Changes made in the same piece are undone all at once,
	/// as typing in the JSON is.
	/// </summary>
	private void OnFormChanged(FormViewModel form)
	{
		if (form != m_form || m_project is not { } project || m_selectedNode is not ModNodeViewModel { Location: not (FolderLocation or FileLocation) } node)
			return;
		FlushEditorText();
		var snapshot = project.TakeSnapshot();
		if (ModEditing.Replace(project, node.Location, ModJson.Clone(form.Value)) is { } error)
		{
			StatusText = error;
			return;
		}

		if (m_editSession != node.Location)
		{
			PushUndo(snapshot);
			m_editSession = node.Location;
		}

		m_isLoadingEditor = true;
		try
		{
			EditorText = ModJson.Format(ModEditing.Get(project, node.Location));
			EditorError = null;
		}
		finally
		{
			m_isLoadingEditor = false;
		}

		OnProjectChanged(null, reloadEditor: false);
		EditorHeading = node.GetBreadcrumb();
	}

	private void FlushEditorText()
	{
		if (m_editTimer.IsEnabled)
			ApplyEditorText();
	}

	/// <summary>
	/// Applies the editor's text to the selected item. Typing in the same item is undone all at once.
	/// </summary>
	private void ApplyEditorText()
	{
		m_editTimer.Stop();
		if (m_project is not { } project || m_selectedNode is not ModNodeViewModel { Location: not FolderLocation } node || IsEditorReadOnly)
			return;

		var snapshot = project.TakeSnapshot();
		if (node.Location is FileLocation fileLocation)
		{
			var file = project.FindFile(fileLocation.Path);
			if (file is null || file.Text == m_editorText)
				return;
			file.SetText(m_editorText);
			EditorError = file.ParseError;
		}
		else
		{
			var (json, error) = ModJson.Parse(m_editorText);
			if (error is not null)
			{
				EditorError = error;
				return;
			}

			if (JsonNode.DeepEquals(json, ModEditing.Get(project, node.Location)))
			{
				EditorError = null;
				return;
			}

			EditorError = ModEditing.Replace(project, node.Location, json);
			if (EditorError is not null)
				return;
		}

		if (m_editSession != node.Location)
		{
			PushUndo(snapshot);
			m_editSession = node.Location;
		}

		OnProjectChanged(null, reloadEditor: false);
		EditorHeading = node.GetBreadcrumb();
		LoadComments();
		BuildForm();
	}

	private void ScheduleValidation()
	{
		m_validateTimer.Stop();
		m_validateTimer.Start();
	}

	/// <summary>
	/// Checks the mod as it is in the editor, on the thread pool. Only the latest check's problems are shown.
	/// </summary>
	private void Validate()
	{
		m_validateTimer.Stop();
		if (m_project is not { } project)
			return;
		m_isValidating = true;
		RaisePropertyChanged(nameof(ProblemSummary));
		_ = ValidateAsync(project, ++m_validationVersion);
	}

	private async Task ValidateAsync(ModProject project, int version)
	{
		var snapshot = project.TakeSnapshot();
		var name = project.Name;
		var gameDataJson = m_gameDataJson;
		IReadOnlyList<ModProblem> problems;
		ModChanges? changes = null;
		try
		{
			// What the mod changes is worked out alongside the check, from a copy of the game data of its own.
			(problems, changes) = await Task.Run(() => (ModValidator.Validate(name, snapshot), ComputeChanges(gameDataJson, snapshot)));
		}
#pragma warning disable CA1031 // Do not catch general exception types (a failed check is shown as a problem, not a crash)
		catch (Exception e)
#pragma warning restore CA1031 // Do not catch general exception types
		{
			problems = [new ModProblem(null, "", $"The mod couldn't be checked: {e.Message}")];
		}

		// The await comes back to the window's thread. A newer check, or another mod, makes these problems out of date.
		if (version != m_validationVersion || m_project != project)
			return;
		m_isValidating = false;
		Problems.Clear();
		foreach (var problem in problems)
			Problems.Add(problem);
		UpdateProblemMarks();
		RaisePropertyChanged(nameof(ProblemSummary));
		if (changes is not null)
			ApplyChanges(changes);
	}

	/// <summary>
	/// Marks the files that can't be read or that problems were found in, and the folders they're in.
	/// </summary>
	private void UpdateProblemMarks()
	{
		if (m_project is not { } project)
			return;
		var paths = new HashSet<string>(Problems.Select(x => x.FilePath).OfType<string>(), ModFilePath.Comparer);
		paths.UnionWith(project.Files.Where(x => x.Kind == ModFileKind.Invalid).Select(x => x.Path));
		foreach (var node in m_modNodes.Values)
		{
			node.HasProblems = node.Location switch
			{
				FileLocation x => paths.Contains(x.Path),
				FolderLocation x => paths.Any(path => ModFilePath.IsWithin(path, x.Path)),
				_ => false,
			};
		}
	}

	private readonly IEditorDialogs m_dialogs;
	private readonly Action<EditorSettings> m_saveSettings;
	private readonly DispatcherTimer m_editTimer;
	private readonly DispatcherTimer m_validateTimer;
	private readonly DispatcherTimer m_toastTimer;
	private readonly DispatcherTimer m_diskTimer;
	private FileSystemWatcher? m_watcher;
	private bool m_isDiskChangePending;
	private readonly Stack<ModSnapshot> m_undo = new();
	private readonly Stack<ModSnapshot> m_redo = new();
	private readonly Dictionary<ModLocation, ModNodeViewModel> m_modNodes = [];
	private EditorSettings m_settings;
	private ModProject? m_project;
	private JsonObject? m_gameData;
	private TreeNodeViewModel? m_selectedNode;
	private ModLocation? m_editSession;
	private string m_editorText;
	private string m_editorHeading;
	private string? m_editorError;
	private bool m_isEditorReadOnly;
	private bool m_isLoadingEditor;
	private bool m_isValidating;
	private string m_filter;
	private string m_statusText;
	private string m_toastText;
	private int m_treeTab;
	private FormViewModel? m_form;
	private string? m_formMessage;
	private string m_comments = "";
	private string m_commentsHint = "";
	private bool m_isCommentsShown;
	private bool m_isCommentsEnabled;
	private ModLocation? m_lastModLocation;
	private string? m_gameDataJson;
	private string m_gameDataDescription = "";
	private ModChanges m_changes = ModChanges.None;
	private bool m_isOnlyChangedShown;
	private IReadOnlyList<ChangeRowViewModel> m_changeRows = [];
	private string m_changesMessage = "";
	private int m_validationVersion;
}

/// <summary>
/// A file the selected piece can be moved to, for the Move To menu.
/// </summary>
public sealed record MoveTarget(string Path, System.Windows.Input.ICommand Command);

/// <summary>
/// What the mod changes, for the fields of a form showing a piece of the game data.
/// </summary>
internal sealed class FormChanges(MainWindowViewModel host) : IFormChanges
{
	public ChangeKind? GetChange(string path) => host.CurrentChanges.GetChangeAt(path);

	public ModLocation? GetSource(string path) => host.GetLastSourceAt(path);

	public void GoTo(ModLocation location) => host.GoToSource(location);

	public bool CanChangeMod => host.CanAddOperation;

	public void Replace(string path, JsonNode? value) => host.AddOperation("replace", path, includeValue: true, value);

	public void Remove(string path) => host.AddOperation("remove", path, includeValue: false, null);
}
