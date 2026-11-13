using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows.Media;
using OperaEtDies;
using PortraitEditor.Model;

namespace PortraitEditor.ViewModels;

/// <summary>
/// Edits a property that holds a color: a hex color or the id of a portrait color. Leaving it empty removes the
/// property.
/// </summary>
public sealed class ColorFieldViewModel : ViewModelBase
{
	public ColorFieldViewModel(MainWindowViewModel owner, DataItemViewModel item, string propertyName, string label)
	{
		m_owner = owner;
		m_item = item;
		PropertyName = propertyName;
		Label = label;
	}

	public string PropertyName { get; }

	public string Label { get; }

	public string Text
	{
		get => m_item.Node?.GetString(PropertyName) ?? "";
		set => m_owner.EditItem(m_item, PropertyName, node => node.SetProperty(PropertyName, string.IsNullOrWhiteSpace(value) ? null : JsonValue.Create(value.Trim())));
	}

	/// <summary>
	/// The color for the focused character, or null if there's no color or it isn't known.
	/// </summary>
	public Brush? Swatch => m_owner.ResolveColor(Text) is { } color ? new SolidColorBrush(color) : null;

	/// <summary>
	/// The portrait colors, as they are for the focused character.
	/// </summary>
	public IReadOnlyList<PaletteEntry> Palette => m_owner.Palette;

	/// <summary>
	/// Choosing a portrait color uses its id.
	/// </summary>
	public PaletteEntry? SelectedPaletteEntry
	{
		get => Palette.FirstOrDefault(x => x.Id == Text);
		set
		{
			if (value is not null && value.Id != Text)
				Text = value.Id;
		}
	}

	/// <summary>
	/// The color as a color, for the color picker. Picking a color writes it as hex.
	/// </summary>
	public Color PickerColor
	{
		get => m_owner.ResolveColor(Text) ?? Colors.Black;
		set => Text = ToHex(value);
	}

	public bool IsPopupOpen
	{
		get => VerifyAccess(m_isPopupOpen);
		set => SetPropertyField(value, ref m_isPopupOpen);
	}

	public void Clear() => Text = "";

	public void Refresh() => RaisePropertyChanged(nameof(Text), nameof(Swatch), nameof(Palette), nameof(SelectedPaletteEntry), nameof(PickerColor));

	public static string ToHex(Color color)
	{
		static string Hex(byte value) => value.ToString("X2", CultureInfo.InvariantCulture);
		return color.A == 255 ? $"#{Hex(color.R)}{Hex(color.G)}{Hex(color.B)}" : $"#{Hex(color.A)}{Hex(color.R)}{Hex(color.G)}{Hex(color.B)}";
	}

	private readonly MainWindowViewModel m_owner;
	private readonly DataItemViewModel m_item;
	private bool m_isPopupOpen;
}

/// <summary>
/// A portrait color, as it is for the focused character.
/// </summary>
public sealed record PaletteEntry(string Id, Brush Brush);
