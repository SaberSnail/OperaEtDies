using GoldenAnvil.Utility;
using OperaEtDies.Characters;
using OperaEtDies.Data;

namespace OperaEtDies.Controls.NewGame;

/// <summary>
/// Lets the player configure their character before starting a new game.
/// </summary>
public sealed class NewGameViewModel : ViewModelBase
{
	public NewGameViewModel(GameData gameData, Random random)
	{
		m_character = new CharacterRandomizer(gameData, random, CharacterRole.Player);
		Properties = CharacterDependencies.GetOrder(m_character.Properties)
			.Where(x => CharacterData.CanHaveValue(m_character.Properties, x, CharacterRole.Player))
			.Select(CreateEditor)
			.ToList();
	}

	public event EventHandler<GenericEventArgs<NewGameSettings>>? StartGameRequested;

	public event EventHandler? BackRequested;

	/// <summary>
	/// Editors for every property the player could have, in dependency order, so the player chooses a property's
	/// value before the values that depend on it. Properties that don't depend on each other keep the order they were
	/// defined in. Properties that never apply to the player aren't included. Properties without a value right now are
	/// unavailable, and properties with only one possible value are fixed.
	/// </summary>
	public IReadOnlyList<CharacterPropertyEditorViewModel> Properties { get; }

	/// <summary>
	/// Gets the editor for the property with the given id, or null if there is no such property or it never applies
	/// to the player.
	/// </summary>
	public CharacterPropertyEditorViewModel? FindProperty(string id) => Properties.FirstOrDefault(x => x.Id == id);

	/// <summary>
	/// Randomizes everything except string values the player customized.
	/// </summary>
	public void RandomizeAll()
	{
		VerifyAccess();
		m_character.RandomizeAll();
	}

	/// <summary>
	/// Every string property with a value must be filled in.
	/// </summary>
	public bool CanStartGame() => m_character.Properties
		.Where(x => x.Type == CharacterPropertyType.String)
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

	private CharacterPropertyEditorViewModel CreateEditor(CharacterProperty property) => property.Type switch
	{
		CharacterPropertyType.String => new StringPropertyEditorViewModel(m_character, property),
		CharacterPropertyType.Integer => new IntegerPropertyEditorViewModel(m_character, property),
		CharacterPropertyType.ValueList => new ValueListPropertyEditorViewModel(m_character, property),
		_ => throw new InvalidOperationException($"Unknown property type {property.Type}."),
	};

	private readonly CharacterRandomizer m_character;
}
