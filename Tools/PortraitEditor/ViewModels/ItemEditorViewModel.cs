using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Input;
using System.Windows.Threading;
using GoldenAnvil.Utility.Windows;
using OperaEtDies;
using OperaEtDies.Data;
using PortraitEditor.Model;

namespace PortraitEditor.ViewModels;

/// <summary>
/// Edits the selected item of the portrait data: any item as JSON, and shapes and colors with fields.
/// </summary>
public sealed class ItemEditorViewModel : ViewModelBase
{
	public ItemEditorViewModel(MainWindowViewModel owner, DataItemViewModel item)
	{
		m_owner = owner;
		m_item = item;
		m_jsonText = FormatNode();
		m_jsonTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromMilliseconds(400) };
		m_jsonTimer.Tick += (_, _) => ApplyJson();
		m_selectedTab = item.Kind switch { DataItemKind.Shape => 0, DataItemKind.Color => 1, _ => 2 };

		if (item.Kind == DataItemKind.Shape)
		{
			Fill = new ColorFieldViewModel(owner, item, "fill", "Fill");
			FillTo = new ColorFieldViewModel(owner, item, "fillTo", "Fill to");
			Stroke = new ColorFieldViewModel(owner, item, "stroke", "Stroke");
		}
		else if (item.Kind == DataItemKind.Color)
		{
			BaseColor = new ColorFieldViewModel(owner, item, "color", "Color");
			Mix = new ColorFieldViewModel(owner, item, "mix", "Mix");
		}
	}

	public DataItemViewModel Item => m_item;

	public string Path => m_item.Path;

	public string KindTitle => m_item.Kind.ToString();

	public bool IsShape => m_item.Kind == DataItemKind.Shape;

	public bool IsColor => m_item.Kind == DataItemKind.Color;

	/// <summary>
	/// The tab shown: the fields for shapes and colors, or else the JSON.
	/// </summary>
	public int SelectedTab
	{
		get => VerifyAccess(m_selectedTab);
		set => SetPropertyField(value, ref m_selectedTab);
	}

	/// <summary>
	/// The item as JSON. Changes are applied a moment after typing stops, if they're valid.
	/// </summary>
	public string JsonText
	{
		get => VerifyAccess(m_jsonText);
		set
		{
			if (SetPropertyField(value, ref m_jsonText))
				m_jsonTimer.Restart();
		}
	}

	public string? JsonError
	{
		get => VerifyAccess(m_jsonError);
		private set
		{
			if (SetPropertyField(value, ref m_jsonError))
				CommandManager.InvalidateRequerySuggested();
		}
	}

	public ColorFieldViewModel? Fill { get; }

	public ColorFieldViewModel? FillTo { get; }

	public ColorFieldViewModel? Stroke { get; }

	public ColorFieldViewModel? BaseColor { get; }

	public ColorFieldViewModel? Mix { get; }

	public string PathText
	{
		get => GetText("path");
		set => m_owner.EditItem(m_item, "path", node => node.SetProperty("path", JsonValue.Create(value)));
	}

	public string? PathError => PortraitShape.TryParsePath(PathText, out _, out var error) ? null : error;

	public IReadOnlyList<string> GradientOptions { get; } = ["", "vertical", "horizontal", "radial"];

	public string Gradient
	{
		get => GetText("gradient");
		set => SetString("gradient", value);
	}

	public IReadOnlyList<string> ScaleOptions => ["", .. m_owner.GetIds("scales")];

	public IReadOnlyList<string> OffsetOptions => ["", .. m_owner.GetIds("offsets")];

	public string Scale
	{
		get => GetText("scale");
		set => SetString("scale", value);
	}

	public string StrokeWidth
	{
		get => GetFieldText("strokeWidth");
		set => SetNumber("strokeWidth", value);
	}

	public string Opacity
	{
		get => GetFieldText("opacity");
		set => SetNumber("opacity", value);
	}

	public string Amount
	{
		get => GetFieldText("amount");
		set => SetNumber("amount", value);
	}

	/// <summary>
	/// The shape's condition, as JSON. Empty removes it.
	/// </summary>
	public string When
	{
		get => GetFieldText("when");
		set => SetJson("when", value);
	}

	/// <summary>
	/// The problems with the text typed into fields that couldn't be applied, or null if there are none.
	/// </summary>
	public string? FieldError => m_fieldErrors.Count == 0 ? null : string.Join(Environment.NewLine, m_fieldErrors.Values);

	/// <summary>
	/// Updates everything shown from the data, after it changes. The JSON isn't replaced while it's being edited, and
	/// fields keep text that couldn't be applied.
	/// </summary>
	public void Refresh()
	{
		if (!m_isApplyingJson && !m_jsonTimer.IsEnabled)
		{
			m_jsonText = FormatNode();
			JsonError = null;
			RaisePropertyChanged(nameof(JsonText));
		}

		RaisePropertyChanged(nameof(Path), nameof(PathText), nameof(PathError), nameof(Gradient), nameof(Scale), nameof(StrokeWidth), nameof(Opacity), nameof(Amount), nameof(When), nameof(ScaleOptions));
		RefreshColors();
	}

	/// <summary>
	/// Updates the colors, after the focused character changes.
	/// </summary>
	public void RefreshColors()
	{
		Fill?.Refresh();
		FillTo?.Refresh();
		Stroke?.Refresh();
		BaseColor?.Refresh();
		Mix?.Refresh();
	}

	public void CopyJson() => m_owner.CopyJson(m_item);

	/// <summary>
	/// Whether the item was edited, or there's text in a field or the JSON that couldn't be applied.
	/// </summary>
	public bool CanRevert() => m_pendingTexts.Count != 0 || m_jsonError is not null || m_owner.CanRevertItem(m_item);

	/// <summary>
	/// Puts the item back as it was loaded, discarding any text that couldn't be applied.
	/// </summary>
	public void Revert()
	{
		m_jsonTimer.Stop();
		ClearPendingTexts();
		if (m_owner.CanRevertItem(m_item))
			m_owner.RevertItem(m_item);
		Refresh();
	}

	/// <summary>
	/// Applies the JSON as typed, if it's a valid object.
	/// </summary>
	public void ApplyJson()
	{
		m_jsonTimer.Stop();
		JsonNode? node;
		try
		{
			node = JsonNode.Parse(m_jsonText, documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
		}
		catch (JsonException e)
		{
			JsonError = e.Message;
			return;
		}

		if (node is not JsonObject obj)
		{
			JsonError = "The item must be a JSON object.";
		}
		else
		{
			JsonError = null;
			ClearPendingTexts();
			m_isApplyingJson = true;
			try
			{
				m_owner.ReplaceItem(m_item, obj);
			}
			finally
			{
				m_isApplyingJson = false;
			}
		}
	}

	private string FormatNode() => m_item.Node is { } node ? JsonFormatter.Format(node) : "";

	private string GetText(string name) => m_item.Node?.GetText(name) ?? "";

	/// <summary>
	/// Gets a field's text: the text typed into it if that couldn't be applied, or else the data.
	/// </summary>
	private string GetFieldText(string name) => m_pendingTexts.TryGetValue(name, out var text) ? text : GetText(name);

	private void SetString(string name, string? value) =>
		m_owner.EditItem(m_item, name, node => node.SetProperty(name, string.IsNullOrWhiteSpace(value) ? null : JsonValue.Create(value.Trim())));

	/// <summary>
	/// Sets a number, or removes it if the text is empty. Text that isn't a number is kept but not applied.
	/// </summary>
	private void SetNumber(string name, string value)
	{
		if (string.IsNullOrWhiteSpace(value))
			ApplyField(name, null);
		else if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
			ApplyField(name, JsonValue.Create(number));
		else
			KeepField(name, value, $"{name} must be a number.");
	}

	/// <summary>
	/// Sets a property to JSON, or removes it if the text is empty. Text that isn't valid JSON is kept but not applied.
	/// </summary>
	private void SetJson(string name, string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			ApplyField(name, null);
		}
		else
		{
			try
			{
				ApplyField(name, JsonNode.Parse(value, documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true }));
			}
			catch (JsonException e)
			{
				KeepField(name, value, $"{name}: {e.Message}");
			}
		}
	}

	/// <summary>
	/// Sets a property from a field, or removes it if the value is null, discarding any text the field kept.
	/// </summary>
	private void ApplyField(string name, JsonNode? value)
	{
		var hadPendingText = m_pendingTexts.Remove(name);
		if (m_fieldErrors.Remove(name) || hadPendingText)
			RaiseFieldsChanged();
		m_owner.EditItem(m_item, name, node => node.SetProperty(name, value));
	}

	/// <summary>
	/// Keeps text typed into a field that couldn't be applied, with the problem.
	/// </summary>
	private void KeepField(string name, string text, string error)
	{
		m_pendingTexts[name] = text;
		m_fieldErrors[name] = error;
		RaiseFieldsChanged();
	}

	private void ClearPendingTexts()
	{
		if (m_pendingTexts.Count != 0 || m_fieldErrors.Count != 0)
		{
			m_pendingTexts.Clear();
			m_fieldErrors.Clear();
			RaiseFieldsChanged();
		}
	}

	private void RaiseFieldsChanged()
	{
		RaisePropertyChanged(nameof(FieldError));
		CommandManager.InvalidateRequerySuggested();
	}

	private readonly MainWindowViewModel m_owner;
	private readonly DataItemViewModel m_item;
	private readonly DispatcherTimer m_jsonTimer;
	private readonly Dictionary<string, string> m_pendingTexts = new(StringComparer.Ordinal);
	private readonly Dictionary<string, string> m_fieldErrors = new(StringComparer.Ordinal);
	private string m_jsonText;
	private string? m_jsonError;
	private bool m_isApplyingJson;
	private int m_selectedTab;
}
