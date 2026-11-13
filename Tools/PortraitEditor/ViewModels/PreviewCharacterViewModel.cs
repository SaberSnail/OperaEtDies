using System.Globalization;
using System.Windows.Media;
using GoldenAnvil.Utility;
using OperaEtDies;
using OperaEtDies.Characters;
using OperaEtDies.Controls.NewGame;
using OperaEtDies.Data;

namespace PortraitEditor.ViewModels;

/// <summary>
/// A character whose portrait is shown while editing, with editors for their properties like the new game screen's,
/// and the place they're drawn in.
/// </summary>
public sealed class PreviewCharacterViewModel : ViewModelBase
{
	public PreviewCharacterViewModel(GameData gameData, Random random, PreviewSettings? settings)
	{
		m_character = new CharacterRandomizer(gameData, random, CharacterRole.Npc);
		var families = gameData.Families;
		m_atWorkPropertyId = families.Properties.AtWork;
		m_professionPropertyId = families.Properties.Profession;
		var atWorkValues = gameData.CharacterProperties.Find(m_atWorkPropertyId)?.Values;
		m_atWork = atWorkValues?.FirstOrDefault(x => x.Id == families.AtWork);
		m_notAtWork = atWorkValues?.FirstOrDefault(x => x.Id == families.NotAtWork);

		// The game sets whether a character is at work from where they are, so it's set from the place instead.
		var properties = CharacterDependencies.GetOrder(m_character.Properties);
		Properties = properties
			.Where(x => x.Id != m_atWorkPropertyId && CharacterData.CanHaveValue(m_character.Properties, x, CharacterRole.Npc))
			.Select(CreateEditor)
			.ToList();

		Places = [
			new PlaceOption("Anywhere", NoPlaceKey),
			new PlaceOption("Home", HomeKey),
			new PlaceOption("At work", WorkKey),
			.. gameData.QuarterKinds.Select(x => new PlaceOption($"Quarter: {x.Id}", QuarterKeyPrefix + x.Id)),
		];
		m_place = Places.FirstOrDefault(x => x.Key == settings?.Place) ?? Places[1];

		if (settings is not null)
			Restore(properties, settings.Values);

		foreach (var property in m_character.Properties)
			m_character[property.Id].ValueChanged += (_, _) => OnTraitsChanged();
	}

	/// <summary>
	/// Raised when the character or their place changes.
	/// </summary>
	public event EventHandler? Changed;

	public event EventHandler? DuplicateRequested;

	public event EventHandler? RemoveRequested;

	public IReadOnlyList<CharacterPropertyEditorViewModel> Properties { get; }

	public IReadOnlyList<PlaceOption> Places { get; }

	public PlaceOption SelectedPlace
	{
		get => VerifyAccess(m_place);
		set
		{
			if (value is not null && SetPropertyField(value, ref m_place))
				OnTraitsChanged();
		}
	}

	public ImageSource? Portrait
	{
		get => VerifyAccess(m_portrait);
		private set => SetPropertyField(value, ref m_portrait);
	}

	public bool IsEditing
	{
		get => VerifyAccess(m_isEditing);
		set => SetPropertyField(value, ref m_isEditing);
	}

	/// <summary>
	/// The character's traits as they're drawn, with the at work property set from the place.
	/// </summary>
	public CharacterTraits GetDrawnTraits()
	{
		var traits = m_character.GetTraits();
		var atWork = m_place.Key == WorkKey ? m_atWork : m_notAtWork;
		return atWork is null ? traits : traits.With(m_atWorkPropertyId, atWork);
	}

	/// <summary>
	/// The place the character is drawn in. At work, that's the workplace of their profession.
	/// </summary>
	public PortraitPlace? GetPlace() => m_place.Key switch
	{
		NoPlaceKey => null,
		HomeKey => PortraitPlace.Home,
		WorkKey => m_character.GetTraits().GetValueId(m_professionPropertyId) is { } profession ? PortraitPlace.InWorkplace(profession) : PortraitPlace.Home,
		var key => PortraitPlace.InQuarter(key[QuarterKeyPrefix.Length..]),
	};

	/// <summary>
	/// Draws the portrait again with a renderer, as the game would draw it.
	/// </summary>
	public void SetRenderer(PortraitRenderer? renderer)
	{
		m_renderer = renderer;
		UpdatePortrait();
	}

	public void RandomizeAll() => m_character.RandomizeAll();

	public void ToggleEditing() => IsEditing = !IsEditing;

	public void Remove() => RemoveRequested.Raise(this);

	public void Duplicate() => DuplicateRequested.Raise(this);

	public PreviewSettings GetSettings()
	{
		var values = m_character.GetTraits().Values.ToDictionary(x => x.Key, x => x.Value switch
		{
			CharacterPropertyOption option => option.Id,
			int number => number.ToString(CultureInfo.InvariantCulture),
			var value => value.ToString() ?? "",
		}, StringComparer.Ordinal);
		return new PreviewSettings(values, m_place.Key);
	}

	private void Restore(IReadOnlyList<CharacterProperty> properties, IReadOnlyDictionary<string, string> values)
	{
		// Values are set in dependency order, so each value's options depend on the values already set.
		foreach (var property in properties)
		{
			if (values.TryGetValue(property.Id, out var text) && m_character.HasValue(property))
			{
				object? value = property.Type switch
				{
					CharacterPropertyType.ValueList => m_character.GetOptions(property).FirstOrDefault(x => x.Id == text),
					CharacterPropertyType.Integer => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : null,
					_ => text,
				};
				if (value is not null)
					m_character[property.Id].Value = value;
			}
		}
	}

	private void OnTraitsChanged()
	{
		UpdatePortrait();
		Changed.Raise(this);
	}

	private void UpdatePortrait() => Portrait = m_renderer?.Render(GetDrawnTraits(), GetPlace());

	private CharacterPropertyEditorViewModel CreateEditor(CharacterProperty property) => property.Type switch
	{
		CharacterPropertyType.String => new StringPropertyEditorViewModel(m_character, property),
		CharacterPropertyType.Integer => new IntegerPropertyEditorViewModel(m_character, property),
		CharacterPropertyType.ValueList => new ValueListPropertyEditorViewModel(m_character, property),
		_ => throw new InvalidOperationException($"Unknown property type {property.Type}."),
	};

	private const string NoPlaceKey = "none";
	private const string HomeKey = "home";
	private const string WorkKey = "work";
	private const string QuarterKeyPrefix = "quarter:";

	private readonly CharacterRandomizer m_character;
	private readonly string m_atWorkPropertyId;
	private readonly string m_professionPropertyId;
	private readonly CharacterPropertyOption? m_atWork;
	private readonly CharacterPropertyOption? m_notAtWork;
	private PlaceOption m_place;
	private PortraitRenderer? m_renderer;
	private ImageSource? m_portrait;
	private bool m_isEditing;
}

/// <summary>
/// A place a preview character can be drawn in.
/// </summary>
public sealed record PlaceOption(string Title, string Key);

/// <summary>
/// A preview character's values (by property id: a value id, a number, or text) and place, kept between sessions.
/// </summary>
public sealed record PreviewSettings(IReadOnlyDictionary<string, string> Values, string Place);
