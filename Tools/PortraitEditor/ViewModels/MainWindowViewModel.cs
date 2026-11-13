using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Windows;
using OperaEtDies;
using OperaEtDies.Characters;
using OperaEtDies.Data;
using PortraitEditor.Model;

namespace PortraitEditor.ViewModels;

/// <summary>
/// The portrait editor: the tree of portrait data, the selected item's editor, the canvas, and the preview
/// characters. Changes are drawn again a moment after they're made.
/// </summary>
public sealed class MainWindowViewModel : ViewModelBase, IDisposable, ITreeItemHost
{
	public MainWindowViewModel(EditorOptions options)
	{
		m_options = options;
		m_random = new Random();
		m_refreshTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromMilliseconds(200) };
		m_refreshTimer.Tick += (_, _) => Refresh();
		m_diskTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromMilliseconds(500) };
		m_diskTimer.Tick += (_, _) => OnDiskChangeSettled();
		var settings = EditorSettings.Load();
		Canvas = new CanvasViewModel(this) { ShowGrid = settings.ShowGrid };
		m_drawnOnly = settings.DrawnOnly;
		m_previewSize = settings.PreviewSize;

		// The default characters are only used the first time, so a list that was emptied stays empty.
		m_savedPreviews = (IReadOnlyList<PreviewSettings?>?) PreviewStore.Load() ?? [null, null, null];
		m_data = Load();
	}

	public string Title => $"Portrait Editor{(m_document?.IsModified == true ? " (edited)" : "")}";

	/// <summary>
	/// Where the data was loaded from.
	/// </summary>
	public string DataDescription => m_data.ModDirectories.Count == 0
		? $"Data: {m_data.DataDirectory}{(m_options.AreModsEnabled ? "" : " (mods turned off)")}"
		: $"Data: {m_data.DataDirectory}   Mods: {string.Join(", ", m_data.ModDirectories.Select(x => System.IO.Path.GetFileName(x)))}";

	public IReadOnlyList<CategoryItemViewModel> TreeRoots
	{
		get => VerifyAccess(m_treeRoots);
		private set => SetPropertyField(value, ref m_treeRoots);
	}

	public DataItemViewModel? SelectedItem
	{
		get => VerifyAccess(m_selectedItem);
		private set => SetPropertyField(value, ref m_selectedItem);
	}

	public ItemEditorViewModel? ItemEditor
	{
		get => VerifyAccess(m_itemEditor);
		private set => SetPropertyField(value, ref m_itemEditor);
	}

	public CanvasViewModel Canvas { get; }

	public ObservableCollection<PreviewCharacterViewModel> Previews { get; } = [];

	/// <summary>
	/// The preview character shown on the canvas, whose colors and drawn layers the tree shows.
	/// </summary>
	public PreviewCharacterViewModel? FocusedPreview
	{
		get => VerifyAccess(m_focusedPreview);
		set
		{
			if (SetPropertyField(value, ref m_focusedPreview))
				UpdateFocus();
		}
	}

	public double PreviewSize
	{
		get => VerifyAccess(m_previewSize);
		set => SetPropertyField(value, ref m_previewSize);
	}

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
	/// Whether the tree only shows the layers and shapes drawn for the focused character.
	/// </summary>
	public bool DrawnOnly
	{
		get => VerifyAccess(m_drawnOnly);
		set
		{
			if (SetPropertyField(value, ref m_drawnOnly))
				ApplyFilter();
		}
	}

	public IReadOnlyList<string> Problems
	{
		get => VerifyAccess(m_problems);
		private set
		{
			if (SetPropertyField(value, ref m_problems))
				RaisePropertyChanged(nameof(ProblemSummary));
		}
	}

	public string ProblemSummary => m_problems.Count switch
	{
		0 => "No problems",
		1 => "1 problem",
		var count => $"{count.ToString(CultureInfo.CurrentCulture)} problems",
	};

	/// <summary>
	/// Whether the portraits are from the last version of the data without problems, since the current one has some.
	/// </summary>
	public bool IsStale
	{
		get => VerifyAccess(m_isStale);
		private set => SetPropertyField(value, ref m_isStale);
	}

	/// <summary>
	/// Whether the data files changed on disk while there were edits, so they weren't loaded again.
	/// </summary>
	public bool IsDiskChangePending
	{
		get => VerifyAccess(m_isDiskChangePending);
		private set => SetPropertyField(value, ref m_isDiskChangePending);
	}

	public string StatusText
	{
		get => VerifyAccess(m_statusText);
		private set => SetPropertyField(value, ref m_statusText);
	}

	/// <summary>
	/// The portrait colors, as they are for the focused character.
	/// </summary>
	public IReadOnlyList<PaletteEntry> Palette { get; private set; } = [];

	public bool CanUndo() => m_document?.CanUndo ?? false;

	public void Undo() => m_document?.Undo();

	public bool CanRedo() => m_document?.CanRedo ?? false;

	public void Redo() => m_document?.Redo();

	/// <summary>
	/// Loads everything from disk again, discarding any edits.
	/// </summary>
	public void Reload() => m_data = Load();

	public void DismissDiskChange() => IsDiskChangePending = false;

	public void AddPreview()
	{
		if (m_data.GameData is { } gameData)
		{
			var preview = CreatePreview(gameData, null);
			Previews.Add(preview);
			preview.SetRenderer(m_renderer);
			FocusedPreview = preview;
		}
	}

	public bool CanDuplicateItem() => SelectedItem?.Node is not null;

	public void DuplicateItem()
	{
		if (SelectedItem is { List: { } list, Node: { } node } item)
		{
			var copy = node.DeepClone().AsObject();
			if (item.Id is { } id)
				copy.SetProperty("id", JsonValue.Create(GetUnusedId(list, id)));
			m_pendingSelection = (item.Kind, item.Parent?.Index ?? -1, item.Index + 1);
			m_document!.EditStructure(() => list.Insert(item.Index + 1, copy));
		}
	}

	public bool CanDeleteItem() => SelectedItem?.Node is not null;

	public void DeleteItem()
	{
		if (SelectedItem is { List: { } list } item)
		{
			m_pendingSelection = list.Count > 1 ? (item.Kind, item.Parent?.Index ?? -1, Math.Min(item.Index, list.Count - 2)) : null;
			m_document!.EditStructure(() => list.RemoveAt(item.Index));
		}
	}

	public bool CanMoveItemUp() => SelectedItem is { Index: > 0 };

	public void MoveItemUp() => MoveItem(-1);

	public bool CanMoveItemDown() => SelectedItem is { List: { } list } item && item.Index < list.Count - 1;

	public void MoveItemDown() => MoveItem(1);

	public bool CanAddShape() => SelectedItem?.Kind is DataItemKind.Layer or DataItemKind.Shape;

	/// <summary>
	/// Adds a square to the selected layer, after the selected shape or at the end.
	/// </summary>
	public void AddShape()
	{
		var layer = SelectedItem?.Kind == DataItemKind.Shape ? SelectedItem.Parent : SelectedItem;
		if (layer?.Node is { } layerNode)
		{
			var shapes = layerNode.GetProperty("shapes") as JsonArray;
			var index = SelectedItem?.Kind == DataItemKind.Shape ? SelectedItem.Index + 1 : shapes?.Count ?? 0;
			var shape = new JsonObject { ["path"] = "M180,220 h40 v40 h-40 Z", ["fill"] = "#FF00FF" };
			m_pendingSelection = (DataItemKind.Shape, layer.Index, index);
			m_document!.EditStructure(() =>
			{
				if (shapes is null)
				{
					shapes = [];
					layerNode.SetProperty("shapes", shapes);
				}

				shapes.Insert(index, shape);
			});
		}
	}

	/// <summary>
	/// Copies an item as JSON, to paste into a data file or mod file.
	/// </summary>
	public void CopyJson(DataItemViewModel item)
	{
		if (item.Node is { } node)
		{
			try
			{
				Clipboard.SetText(JsonFormatter.Format(node));
				StatusText = $"Copied {item.Path}";
			}
			catch (System.Runtime.InteropServices.COMException e)
			{
				StatusText = $"Couldn't copy: {e.Message}";
			}
		}
	}

	public void Dispose()
	{
		if (m_arePreviewsCreated)
			PreviewStore.Save(Previews.Select(x => x.GetSettings()).ToList());
		new EditorSettings(m_drawnOnly, Canvas.ShowGrid, m_previewSize).Save();
		DisposeWatchers();
		m_refreshTimer.Stop();
		m_diskTimer.Stop();
	}

	/// <summary>
	/// Changes an item. Changes with the same key made close together are undone together.
	/// </summary>
	internal void EditItem(DataItemViewModel item, string mergeKey, Action<JsonObject> change)
	{
		if (m_document is not null && item.Node is { } node)
		{
			m_lastEditedItem = item;
			m_document.Edit($"{item.Path}:{mergeKey}", () => change(node));
		}
	}

	/// <summary>
	/// Replaces an item with new JSON.
	/// </summary>
	internal void ReplaceItem(DataItemViewModel item, JsonObject node)
	{
		if (m_document is not null && item.List is { } list && item.Index < list.Count)
		{
			m_lastEditedItem = item;
			m_document.Edit($"{item.Path}:json", () => list[item.Index] = node);
			if (GetStructure() != m_structure)
				RebuildTree();
		}
	}

	/// <summary>
	/// Whether an item was edited and is in the data as it was loaded, so it can be put back.
	/// </summary>
	internal bool CanRevertItem(DataItemViewModel item) => m_document is { } document && item.IsModified && FindOriginal(document, item) is JsonObject;

	/// <summary>
	/// Puts an item back as it was loaded.
	/// </summary>
	internal void RevertItem(DataItemViewModel item)
	{
		if (m_document is { } document && FindOriginal(document, item) is JsonObject original)
		{
			document.BreakMerge();
			ReplaceItem(item, original.DeepClone().AsObject());
			document.BreakMerge();
			StatusText = $"Reverted {item.Path}";
		}
	}

	internal void BreakUndoMerge() => m_document?.BreakMerge();

	internal void SelectShape(int layerIndex, int shapeIndex)
	{
		if (FindItem(DataItemKind.Shape, layerIndex, shapeIndex) is { } item)
			Select(item);
	}

	internal IEnumerable<string> GetIds(string listName) =>
		m_document is not null && PortraitDocument.GetList(m_document.Root, listName) is { } list
			? list.OfType<JsonObject>().Select(x => x.GetString("id")).OfType<string>()
			: [];

	/// <summary>
	/// Gets a color for the focused character from a hex color or a portrait color's id.
	/// </summary>
	internal Color? ResolveColor(string? text) =>
		text is null ? null :
		PortraitColor.TryParseHex(text, out var color) ? color :
		m_colors.TryGetValue(text, out var paletteColor) ? paletteColor : null;

	private EditorData Load()
	{
		if (m_arePreviewsCreated)
			m_savedPreviews = Previews.Select(x => x.GetSettings()).ToList<PreviewSettings?>();

		var data = EditorData.Load(m_options.DataDirectory, m_options.AreModsEnabled);
		m_data = data;
		m_loadErrors = data.Errors.Select(x => x.ToString()).ToList();
		WatchFiles(data);

		if (m_document is not null)
		{
			m_document.Changed -= OnDocumentChanged;
			m_document.Replaced -= OnDocumentReplaced;
		}

		m_document = data.Portraits is { } portraits ? new PortraitDocument(data.PortraitsFilePath, portraits) : null;
		if (m_document is not null)
		{
			m_document.Changed += OnDocumentChanged;
			m_document.Replaced += OnDocumentReplaced;
		}

		m_renderer = null;
		RebuildTree();
		CreatePreviews(data.GameData);
		IsDiskChangePending = false;
		Refresh();

		StatusText = $"Loaded at {DateTime.Now.ToString("T", CultureInfo.CurrentCulture)}";
		RaisePropertyChanged(nameof(DataDescription), nameof(Title));
		return data;
	}

	private void CreatePreviews(GameData? gameData)
	{
		var focusedIndex = m_focusedPreview is null ? 0 : Math.Max(0, Previews.IndexOf(m_focusedPreview));
		foreach (var preview in Previews)
			DetachPreview(preview);

		Previews.Clear();
		if (gameData is not null)
		{
			foreach (var setting in m_savedPreviews)
				Previews.Add(CreatePreview(gameData, setting));
			m_arePreviewsCreated = true;
		}

		m_focusedPreview = Previews.Count == 0 ? null : Previews[Math.Min(focusedIndex, Previews.Count - 1)];
		RaisePropertyChanged(nameof(FocusedPreview));
	}

	private PreviewCharacterViewModel CreatePreview(GameData gameData, PreviewSettings? settings)
	{
		var preview = new PreviewCharacterViewModel(gameData, m_random, settings);
		preview.Changed += OnPreviewChanged;
		preview.PropertyChanged += OnPreviewPropertyChanged;
		preview.DuplicateRequested += OnPreviewDuplicateRequested;
		preview.RemoveRequested += OnPreviewRemoveRequested;
		return preview;
	}

	private void DetachPreview(PreviewCharacterViewModel preview)
	{
		preview.Changed -= OnPreviewChanged;
		preview.PropertyChanged -= OnPreviewPropertyChanged;
		preview.DuplicateRequested -= OnPreviewDuplicateRequested;
		preview.RemoveRequested -= OnPreviewRemoveRequested;
	}

	/// <summary>
	/// Only one character's properties are shown at a time, so showing one character's closes the rest.
	/// </summary>
	private void OnPreviewPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(PreviewCharacterViewModel.IsEditing) && sender is PreviewCharacterViewModel { IsEditing: true } editing)
		{
			foreach (var preview in Previews.Where(x => x != editing))
				preview.IsEditing = false;
		}
	}

	/// <summary>
	/// Adds a copy of a character right after it, in the same place, and selects the copy.
	/// </summary>
	private void OnPreviewDuplicateRequested(object? sender, EventArgs e)
	{
		if (sender is PreviewCharacterViewModel preview && m_data.GameData is { } gameData)
		{
			var copy = CreatePreview(gameData, preview.GetSettings());
			copy.IsEditing = preview.IsEditing;
			Previews.Insert(Previews.IndexOf(preview) + 1, copy);
			copy.SetRenderer(m_renderer);
			FocusedPreview = copy;
		}
	}

	private void OnPreviewChanged(object? sender, EventArgs e)
	{
		if (sender == m_focusedPreview)
			UpdateFocus();
	}

	private void OnPreviewRemoveRequested(object? sender, EventArgs e)
	{
		if (sender is PreviewCharacterViewModel preview)
		{
			DetachPreview(preview);
			var index = Previews.IndexOf(preview);
			Previews.Remove(preview);
			if (preview == m_focusedPreview)
				FocusedPreview = Previews.Count == 0 ? null : Previews[Math.Min(index, Previews.Count - 1)];
		}
	}

	/// <summary>
	/// Reads the portrait data being edited, reports its problems, and draws the portraits again if it can be read.
	/// Otherwise, the portraits from the last version that could be read are kept.
	/// </summary>
	private void Refresh()
	{
		m_refreshTimer.Stop();
		var problems = new List<string>(m_loadErrors);
		if (m_document is not null && m_data.GameData is { } gameData)
		{
			var result = JsonDataLoader.Deserialize<PortraitData>(m_document.FilePath, m_document.Root, null);
			problems.AddRange(result.Errors.Select(x => x.Message));
			if (result.Value is { } portraits)
			{
				problems.AddRange(gameData.ValidatePortraits(portraits));
				m_renderer = new PortraitRenderer(portraits, gameData.CharacterProperties);
				IsStale = false;
			}
			else
			{
				IsStale = m_renderer is not null;
			}
		}

		Problems = problems;
		foreach (var preview in Previews)
			preview.SetRenderer(m_renderer);
		UpdateFocus();
	}

	/// <summary>
	/// Shows the focused character on the canvas, and in the tree, which layers and shapes are drawn for them and
	/// their colors.
	/// </summary>
	private void UpdateFocus()
	{
		var renderer = m_renderer;
		var preview = m_focusedPreview;
		if (renderer is not null && preview is not null)
		{
			var traits = preview.GetDrawnTraits();
			var place = preview.GetPlace();
			var placements = renderer.GetShapePlacements(traits, place);
			m_colors = renderer.GetColors(traits);
			Canvas.SetPortrait(renderer.Render(traits, place, GetLayerOverride, GetShapeOverride), renderer.Size, placements, IsDrawnOnCanvas);
			UpdateDrawn(placements);
		}
		else
		{
			m_colors = new Dictionary<string, Color>();
			Canvas.SetPortrait(null, renderer?.Size ?? new Size(400, 480), [], IsDrawnOnCanvas);
			UpdateDrawn([]);
		}

		Palette = m_colors.Select(x => new PaletteEntry(x.Key, CreateBrush(x.Value))).ToList();
		RaisePropertyChanged(nameof(Palette));
		UpdateSwatches();
		ItemEditor?.RefreshColors();
		if (m_drawnOnly)
			ApplyFilter();
	}

	private void UpdateDrawn(IReadOnlyList<PortraitShapePlacement> placements)
	{
		var drawn = placements.Where(x => x.IsDrawn).Select(x => (x.LayerIndex, x.ShapeIndex)).ToHashSet();
		foreach (var layer in m_layers)
		{
			var isLayerDrawn = false;
			foreach (var shape in layer.Children.OfType<DataItemViewModel>())
			{
				shape.IsDrawn = drawn.Contains((layer.Index, shape.Index));
				isLayerDrawn |= shape.IsDrawn;
			}

			layer.IsDrawn = isLayerDrawn;
		}
	}

	private void UpdateSwatches()
	{
		foreach (var category in m_treeRoots)
		{
			foreach (var item in category.Children.OfType<DataItemViewModel>())
			{
				if (item.Kind == DataItemKind.Color)
					item.Swatch = item.Id is { } id && m_colors.TryGetValue(id, out var color) ? CreateBrush(color) : null;
				foreach (var shape in item.Children.OfType<DataItemViewModel>())
					UpdateShapeSwatch(shape);
			}
		}
	}

	private void UpdateShapeSwatch(DataItemViewModel shape) =>
		shape.Swatch = ResolveColor(shape.Node?.GetString("fill") ?? shape.Node?.GetString("stroke")) is { } color ? CreateBrush(color) : null;

	private void OnDocumentChanged(object? sender, EventArgs e)
	{
		if (m_lastEditedItem is { } item)
		{
			item.RefreshText();
			UpdateModified(item);
			if (item.Parent is { } parent)
				UpdateModified(parent);
			if (item.Kind == DataItemKind.Shape)
				UpdateShapeSwatch(item);
			m_lastEditedItem = null;
		}

		ItemEditor?.Refresh();
		Canvas.OnShapeDataChanged();
		m_refreshTimer.Restart();
		RaisePropertyChanged(nameof(Title));
		CommandManagerRequery();
	}

	/// <summary>
	/// The data was replaced or its lists changed, so the tree is built again if they don't match it anymore.
	/// </summary>
	private void OnDocumentReplaced(object? sender, EventArgs e)
	{
		if (GetStructure() != m_structure || m_pendingSelection is not null)
		{
			RebuildTree();
		}
		else
		{
			foreach (var item in GetAllItems())
			{
				item.RefreshText();
				UpdateModified(item);
			}
		}

		m_refreshTimer.Restart();
	}

	private static void CommandManagerRequery() => System.Windows.Input.CommandManager.InvalidateRequerySuggested();

	/// <summary>
	/// Builds the tree from the data, keeping the selection, the expanded layers, and the hidden layers where they
	/// still exist.
	/// </summary>
	private void RebuildTree()
	{
		var selection = m_pendingSelection ?? (m_selectedItem is { } selected ? (selected.Kind, selected.Parent?.Index ?? -1, selected.Index) : null);
		m_pendingSelection = null;
		var expanded = m_layers.Where(x => x.IsExpanded).Select(x => x.Index).ToHashSet();
		var expandedCategories = m_treeRoots.Where(x => x.IsExpanded).Select(x => x.Name).ToHashSet(StringComparer.Ordinal);

		var roots = new List<CategoryItemViewModel>();
		m_layers = [];
		if (m_document is { } document)
		{
			roots.Add(CreateCategory(document, "colors", DataItemKind.Color));
			roots.Add(CreateCategory(document, "scales", DataItemKind.Scale));
			roots.Add(CreateCategory(document, "offsets", DataItemKind.Offset));
			var layers = CreateCategory(document, "layers", DataItemKind.Layer);
			roots.Add(layers);
			m_layers = layers.Children.OfType<DataItemViewModel>().ToList();
			foreach (var layer in m_layers)
			{
				var getShapes = () => layer.Node?.GetProperty("shapes") as JsonArray;
				layer.Children = Enumerable.Range(0, getShapes()?.Count ?? 0)
					.Select(index => new DataItemViewModel(DataItemKind.Shape, "shapes", getShapes, index, layer, this))
					.ToList();
				layer.IsExpanded = expanded.Contains(layer.Index);

				// Canvas states are kept by layer id, so they last when the data is loaded again.
				var key = GetLayerKey(layer);
				layer.SetCanvasState(m_layerStates.GetValueOrDefault(key));
				foreach (var shape in layer.Children.OfType<DataItemViewModel>())
					shape.SetCanvasState(m_shapeStates.GetValueOrDefault((key, shape.Index)));
			}
		}

		foreach (var root in roots)
			root.IsExpanded = expandedCategories.Count == 0 ? root.Name == "layers" : expandedCategories.Contains(root.Name);
		TreeRoots = roots;
		m_structure = GetStructure();
		foreach (var item in GetAllItems())
			UpdateModified(item);

		m_selectedItem = null;
		if (selection is var (kind, parentIndex, index) && FindItem(kind, parentIndex, index) is { } item2)
			Select(item2);
		if (m_selectedItem is null)
		{
			SelectedItem = null;
			ItemEditor = null;
			Canvas.SetShape(null);
		}

		ApplyFilter();
		UpdateSwatches();
		CommandManagerRequery();
	}

	private CategoryItemViewModel CreateCategory(PortraitDocument document, string listName, DataItemKind kind)
	{
		JsonArray? GetList() => PortraitDocument.GetList(document.Root, listName);
		var category = new CategoryItemViewModel(listName, this);
		category.Children = Enumerable.Range(0, GetList()?.Count ?? 0)
			.Select(index => new DataItemViewModel(kind, listName, GetList, index, null, this))
			.ToList();
		return category;
	}

	/// <summary>
	/// Describes the lengths of the lists in the data, which the tree is built from.
	/// </summary>
	private string GetStructure()
	{
		if (m_document is not { } document)
			return "";

		var counts = new[] { "colors", "scales", "offsets" }.Select(x => PortraitDocument.GetList(document.Root, x)?.Count ?? 0);
		var layers = PortraitDocument.GetList(document.Root, "layers");
		var shapeCounts = layers?.Select(x => (x as JsonObject)?.GetProperty("shapes") is JsonArray shapes ? shapes.Count : -1) ?? [];
		return string.Join(",", counts.Concat(shapeCounts).Select(x => x.ToString(CultureInfo.InvariantCulture)));
	}

	private IEnumerable<DataItemViewModel> GetAllItems() =>
		m_treeRoots.SelectMany(x => x.Children.OfType<DataItemViewModel>()).SelectMany(x => x.Children.OfType<DataItemViewModel>().Prepend(x));

	private DataItemViewModel? FindItem(DataItemKind kind, int parentIndex, int index)
	{
		var items = kind == DataItemKind.Shape
			? (parentIndex >= 0 && parentIndex < m_layers.Count ? m_layers[parentIndex].Children : [])
			: m_treeRoots.FirstOrDefault(x => x.Children.OfType<DataItemViewModel>().FirstOrDefault()?.Kind == kind)?.Children ?? [];
		return index >= 0 && index < items.Count ? items[index] as DataItemViewModel : null;
	}

	private void Select(DataItemViewModel item)
	{
		if (item.Parent is { } parent)
			parent.IsExpanded = true;
		var category = m_treeRoots.FirstOrDefault(x => x.Children.Contains(item.Parent ?? item));
		if (category is not null)
			category.IsExpanded = true;
		item.IsVisible = true;
		if (item.IsSelected)
			OnItemSelected(item);
		else
			item.IsSelected = true;
	}

	private void OnItemSelected(DataItemViewModel item)
	{
		if (item != m_selectedItem)
		{
			if (m_selectedItem is { } previous && previous.IsSelected)
				previous.IsSelected = false;
			SelectedItem = item;
			ItemEditor = new ItemEditorViewModel(this, item);
			Canvas.SetShape(item.Kind == DataItemKind.Shape ? item : null);
			CommandManagerRequery();
		}
	}

	void ITreeItemHost.OnItemSelected(DataItemViewModel item) => OnItemSelected(item);

	void ITreeItemHost.OnCanvasStateChanged(DataItemViewModel item)
	{
		StoreCanvasState(item);
		OnCanvasStatesChanged();
	}

	void ITreeItemHost.SetLayerAndShapes(DataItemViewModel layer, CanvasState state)
	{
		SetCanvasState(layer, state);
		foreach (var shape in layer.Children.OfType<DataItemViewModel>())
			SetCanvasState(shape, state);
		OnCanvasStatesChanged();
	}

	void ITreeItemHost.ShowOnly(DataItemViewModel item)
	{
		// Hides every other layer, and when showing a shape, the other shapes in its layer, then forces the item.
		var showLayer = item.Parent ?? item;
		foreach (var layer in m_layers)
		{
			if (layer != showLayer)
			{
				SetCanvasState(layer, CanvasState.Hidden);
				foreach (var shape in layer.Children.OfType<DataItemViewModel>())
					SetCanvasState(shape, CanvasState.Normal);
			}
			else if (item.Parent is not null)
			{
				foreach (var shape in layer.Children.OfType<DataItemViewModel>())
					SetCanvasState(shape, shape == item ? CanvasState.Forced : CanvasState.Hidden);
			}
		}

		SetCanvasState(item, CanvasState.Forced);
		OnCanvasStatesChanged();
	}

	void ITreeItemHost.SetFiltered(CanvasState state) => SetLayerStates(m_layers.Where(x => x.IsVisible), state);

	void ITreeItemHost.SetAll(CanvasState state) => SetLayerStates(m_layers, state);

	/// <summary>
	/// The number of layers and shapes forced and hidden on the canvas, or empty if there are none.
	/// </summary>
	public string CanvasStateSummary
	{
		get
		{
			var states = m_layerStates.Values.Concat(m_shapeStates.Values).ToList();
			var parts = new List<string>();
			var forced = states.Count(x => x == CanvasState.Forced);
			var hidden = states.Count(x => x == CanvasState.Hidden);
			if (forced != 0)
				parts.Add($"📌 {forced.ToString(CultureInfo.CurrentCulture)} forced");
			if (hidden != 0)
				parts.Add($"⊘ {hidden.ToString(CultureInfo.CurrentCulture)} hidden");
			return string.Join("   ", parts);
		}
	}

	/// <summary>
	/// Puts every layer and shape back to normal on the canvas.
	/// </summary>
	public void ResetCanvasStates() => SetLayerStates(m_layers, CanvasState.Normal);

	/// <summary>
	/// Sets the canvas state of layers, and puts their shapes back to normal so they follow their layers.
	/// </summary>
	private void SetLayerStates(IEnumerable<DataItemViewModel> layers, CanvasState state)
	{
		foreach (var layer in layers)
		{
			SetCanvasState(layer, state);
			foreach (var shape in layer.Children.OfType<DataItemViewModel>())
				SetCanvasState(shape, CanvasState.Normal);
		}

		OnCanvasStatesChanged();
	}

	private void SetCanvasState(DataItemViewModel item, CanvasState state)
	{
		if (item.SetCanvasState(state))
			StoreCanvasState(item);
	}

	private void StoreCanvasState(DataItemViewModel item)
	{
		if (item.Kind == DataItemKind.Layer)
		{
			if (item.CanvasState == CanvasState.Normal)
				m_layerStates.Remove(GetLayerKey(item));
			else
				m_layerStates[GetLayerKey(item)] = item.CanvasState;
		}
		else if (item.Parent is { } layer)
		{
			var key = (GetLayerKey(layer), item.Index);
			if (item.CanvasState == CanvasState.Normal)
				m_shapeStates.Remove(key);
			else
				m_shapeStates[key] = item.CanvasState;
		}
	}

	private void OnCanvasStatesChanged()
	{
		RaisePropertyChanged(nameof(CanvasStateSummary));
		UpdateFocus();
	}

	private static string GetLayerKey(DataItemViewModel layer) => layer.Id ?? $"#{layer.Index.ToString(CultureInfo.InvariantCulture)}";

	private static bool? ToOverride(CanvasState state) => state switch
	{
		CanvasState.Forced => true,
		CanvasState.Hidden => false,
		_ => null,
	};

	private bool? GetLayerOverride(int layerIndex) => layerIndex < m_layers.Count ? ToOverride(m_layers[layerIndex].CanvasState) : null;

	private bool? GetShapeOverride(int layerIndex, int shapeIndex) =>
		layerIndex < m_layers.Count && shapeIndex < m_layers[layerIndex].Children.Count && m_layers[layerIndex].Children[shapeIndex] is DataItemViewModel shape
			? ToOverride(shape.CanvasState)
			: null;

	/// <summary>
	/// Whether a shape is drawn on the canvas, with the forced and hidden layers and shapes.
	/// </summary>
	private bool IsDrawnOnCanvas(PortraitShapePlacement placement) =>
		PortraitRenderer.IsShapeDrawn(placement.IsLayerDrawn, placement.IsShapeConditionMet, GetLayerOverride(placement.LayerIndex), GetShapeOverride(placement.LayerIndex, placement.ShapeIndex));

	private void UpdateModified(DataItemViewModel item)
	{
		if (m_document is { } document)
		{
			var original = FindOriginal(document, item);
			item.IsModified = original is null || !JsonNode.DeepEquals(item.Node, original);
		}
	}

	/// <summary>
	/// Finds an item in the data as it was loaded: by id where it has one, or else by its place in the list.
	/// </summary>
	private static JsonNode? FindOriginal(PortraitDocument document, DataItemViewModel item)
	{
		var list = item.Parent is { } parent
			? (FindOriginal(document, parent) as JsonObject)?.GetProperty(item.ListName) as JsonArray
			: PortraitDocument.GetList(document.Original, item.ListName);
		if (list is null)
			return null;

		if (item.Parent is null && item.Id is { } id)
			return list.OfType<JsonObject>().FirstOrDefault(x => x.GetString("id") == id);
		return item.Index < list.Count ? list[item.Index] : null;
	}

	private void ApplyFilter()
	{
		var filter = m_filter.Trim();
		foreach (var category in m_treeRoots)
		{
			foreach (var item in category.Children.OfType<DataItemViewModel>())
			{
				var matches = filter.Length == 0 || (item.Id ?? "").Contains(filter, StringComparison.OrdinalIgnoreCase);
				// Forced layers and shapes count as drawn, since they're drawn on the canvas.
				var shapes = item.Children.OfType<DataItemViewModel>().ToList();
				var isDrawn = !m_drawnOnly || item.Kind != DataItemKind.Layer || item.IsDrawn || item.CanvasState == CanvasState.Forced || shapes.Any(x => x.CanvasState == CanvasState.Forced);
				item.IsVisible = (matches && isDrawn) || item == m_selectedItem || item == m_selectedItem?.Parent;
				foreach (var shape in shapes)
					shape.IsVisible = !m_drawnOnly || shape.IsDrawn || shape.CanvasState == CanvasState.Forced || shape == m_selectedItem;
			}
		}
	}

	private void MoveItem(int direction)
	{
		if (SelectedItem is { List: { } list } item)
		{
			var newIndex = item.Index + direction;
			if (newIndex >= 0 && newIndex < list.Count)
			{
				m_pendingSelection = (item.Kind, item.Parent?.Index ?? -1, newIndex);
				m_document!.EditStructure(() =>
				{
					var node = list[item.Index];
					list.RemoveAt(item.Index);
					list.Insert(newIndex, node);
				});
			}
		}
	}

	private static string GetUnusedId(JsonArray list, string id)
	{
		var ids = list.OfType<JsonObject>().Select(x => x.GetString("id")).OfType<string>().ToHashSet(StringComparer.Ordinal);
		var number = 1;
		var newId = $"{id}_copy";
		while (ids.Contains(newId))
		{
			number++;
			newId = $"{id}_copy{number.ToString(CultureInfo.InvariantCulture)}";
		}

		return newId;
	}

	private static SolidColorBrush CreateBrush(Color color)
	{
		var brush = new SolidColorBrush(color);
		brush.Freeze();
		return brush;
	}

	private void WatchFiles(EditorData data)
	{
		DisposeWatchers();
		foreach (var directory in data.GetWatchedDirectories().Where(Directory.Exists))
		{
			var watcher = new FileSystemWatcher(directory, "*.json")
			{
				NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
			};
			watcher.Changed += OnFileChanged;
			watcher.Created += OnFileChanged;
			watcher.Deleted += OnFileChanged;
			watcher.Renamed += OnFileChanged;
			watcher.EnableRaisingEvents = true;
			m_watchers.Add(watcher);
		}
	}

	private void DisposeWatchers()
	{
		foreach (var watcher in m_watchers)
			watcher.Dispose();
		m_watchers.Clear();
	}

	// File system events come from other threads, and often several at once, so they're handled once they settle.
#pragma warning disable VSTHRD001 // Nothing waits for this, so it can't deadlock.
	private void OnFileChanged(object sender, FileSystemEventArgs e) => _ = Dispatcher.BeginInvoke(() => m_diskTimer.Restart());
#pragma warning restore VSTHRD001

	private void OnDiskChangeSettled()
	{
		m_diskTimer.Stop();
		if (m_document?.IsModified == true)
			IsDiskChangePending = true;
		else
			m_data = Load();
	}

	private readonly EditorOptions m_options;
	private readonly Random m_random;
	private readonly DispatcherTimer m_refreshTimer;
	private readonly DispatcherTimer m_diskTimer;
	private readonly Collection<FileSystemWatcher> m_watchers = [];
	private readonly Dictionary<string, CanvasState> m_layerStates = new(StringComparer.Ordinal);
	private readonly Dictionary<(string Layer, int Shape), CanvasState> m_shapeStates = [];
	private EditorData m_data;
	private IReadOnlyList<string> m_loadErrors = [];
	private IReadOnlyList<PreviewSettings?> m_savedPreviews;
	private bool m_arePreviewsCreated;
	private PortraitDocument? m_document;
	private PortraitRenderer? m_renderer;
	private IReadOnlyDictionary<string, Color> m_colors = new Dictionary<string, Color>();
	private IReadOnlyList<CategoryItemViewModel> m_treeRoots = [];
	private IReadOnlyList<DataItemViewModel> m_layers = [];
	private string m_structure = "";
	private (DataItemKind Kind, int ParentIndex, int Index)? m_pendingSelection;
	private DataItemViewModel? m_selectedItem;
	private DataItemViewModel? m_lastEditedItem;
	private ItemEditorViewModel? m_itemEditor;
	private PreviewCharacterViewModel? m_focusedPreview;
	private double m_previewSize = 200;
	private string m_filter = "";
	private bool m_drawnOnly;
	private IReadOnlyList<string> m_problems = [];
	private bool m_isStale;
	private bool m_isDiskChangePending;
	private string m_statusText = "";
}
