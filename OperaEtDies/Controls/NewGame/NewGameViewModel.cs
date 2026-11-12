using System.Windows.Media;
using GoldenAnvil.Utility;
using OperaEtDies.Characters;
using OperaEtDies.Data;

namespace OperaEtDies.Controls.NewGame;

/// <summary>
/// Lets the player configure their character before starting a new game.
/// </summary>
public sealed class NewGameViewModel : ViewModelBase
{
	/// <summary>
	/// Creates the screen with a random player character. Developers can also choose a gender the player isn't
	/// usually allowed, and edit the properties that only apply with that gender.
	/// </summary>
	public NewGameViewModel(GameData gameData, Random random, bool isDeveloper = false)
	{
		m_character = new CharacterRandomizer(gameData, random, CharacterRole.Player);
		m_portraitRenderer = gameData.PortraitRenderer;
		m_atWorkPropertyId = gameData.Families.Properties.AtWork;
		m_notAtWork = gameData.CharacterProperties.Find(m_atWorkPropertyId)?.Values?.FirstOrDefault(x => x.Id == gameData.Families.NotAtWork);
		m_developerPropertyId = isDeveloper ? gameData.Families.Properties.Gender : null;

		// For developers, the properties are also checked as if the player could have any gender.
		var developerProperties = m_developerPropertyId is { } developerPropertyId
			? m_character.Properties.Select(x => x.Id == developerPropertyId ? x.WithAllValuesFor(CharacterRole.Player) : x).ToList()
			: null;
		Properties = CharacterDependencies.GetOrder(m_character.Properties)
			.Where(x => !x.SetByGame)
			.Select(x => (Property: x, CanHaveValue: CharacterData.CanHaveValue(m_character.Properties, x, CharacterRole.Player)))
			.Where(x => x.CanHaveValue || (developerProperties is not null && CharacterData.CanHaveValue(developerProperties, x.Property, CharacterRole.Player)))
			.Select(x => CreateEditor(x.Property, isDeveloperOnly: !x.CanHaveValue))
			.ToList();

		// Any change can change the portrait. It's drawn again the next time it's needed.
		foreach (var property in m_character.Properties)
		{
			m_character[property.Id].ValueChanged += (_, _) =>
			{
				m_portrait = null;
				RaisePropertyChanged(nameof(Portrait));
			};
		}
	}

	public event EventHandler<GenericEventArgs<NewGameSettings>>? StartGameRequested;

	public event EventHandler? BackRequested;

	/// <summary>
	/// Editors for every property the player could have, in dependency order, so the player chooses a property's
	/// value before the values that depend on it. Properties that don't depend on each other keep the order they were
	/// defined in. Properties that never apply to the player, or that the game sets itself, aren't included. Properties
	/// without a value right now are unavailable, and properties with only one possible value are fixed. For developers,
	/// properties that only apply with a gender the player isn't usually allowed are included too, marked as developer only.
	/// </summary>
	public IReadOnlyList<CharacterPropertyEditorViewModel> Properties { get; }

	/// <summary>
	/// Gets the editor for the property with the given id, or null if there is no such property or it never applies
	/// to the player.
	/// </summary>
	public CharacterPropertyEditorViewModel? FindProperty(string id) => Properties.FirstOrDefault(x => x.Id == id);

	/// <summary>
	/// A picture of the player character as currently configured, at home and not at work, or null if there's no
	/// portrait data.
	/// </summary>
	public ImageSource? Portrait => VerifyAccess(m_portrait ??= m_portraitRenderer?.Render(GetPortraitTraits(), PortraitPlace.Home));

	/// <summary>
	/// Randomizes everything except string values the player customized.
	/// </summary>
	public void RandomizeAll()
	{
		VerifyAccess();
		m_character.RandomizeAll();
	}

	/// <summary>
	/// Every string property with a value must be filled in, except those the game sets.
	/// </summary>
	public bool CanStartGame() => m_character.Properties
		.Where(x => x.Type == CharacterPropertyType.String && !x.SetByGame)
		.All(x => m_character[x.Id].Value is not string text || !string.IsNullOrWhiteSpace(text));

	public void StartGame()
	{
		VerifyAccess();
		if (!CanStartGame())
			return;

		var values = m_character.GetTraits().Values.ToDictionary(x => x.Key, x => x.Value is string text ? text.Trim() : x.Value, StringComparer.Ordinal);
		StartGameRequested.Raise(this, new GenericEventArgs<NewGameSettings>(new NewGameSettings(new CharacterTraits(values))));
	}

	public void Back()
	{
		VerifyAccess();
		BackRequested.Raise(this);
	}

	/// <summary>
	/// Gets the character's traits with the at work property cleared, since the game sets it.
	/// </summary>
	private CharacterTraits GetPortraitTraits()
	{
		var traits = m_character.GetTraits();
		return m_notAtWork is null ? traits : traits.With(m_atWorkPropertyId, m_notAtWork);
	}

	private CharacterPropertyEditorViewModel CreateEditor(CharacterProperty property, bool isDeveloperOnly) => property.Type switch
	{
		CharacterPropertyType.String => new StringPropertyEditorViewModel(m_character, property) { IsDeveloperOnly = isDeveloperOnly },
		CharacterPropertyType.Integer => new IntegerPropertyEditorViewModel(m_character, property) { IsDeveloperOnly = isDeveloperOnly },
		CharacterPropertyType.ValueList => new ValueListPropertyEditorViewModel(m_character, property, hasDeveloperOptions: property.Id == m_developerPropertyId) { IsDeveloperOnly = isDeveloperOnly },
		_ => throw new InvalidOperationException($"Unknown property type {property.Type}."),
	};

	private readonly CharacterRandomizer m_character;
	private readonly PortraitRenderer? m_portraitRenderer;
	private readonly string m_atWorkPropertyId;
	private readonly CharacterPropertyOption? m_notAtWork;
	private readonly string? m_developerPropertyId;
	private ImageSource? m_portrait;
}
