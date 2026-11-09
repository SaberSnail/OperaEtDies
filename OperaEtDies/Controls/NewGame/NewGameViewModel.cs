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
		Properties = m_character.Properties.Select(CreateEditor).Where(x => x.IsEditable).ToList();
	}

	public event EventHandler<GenericEventArgs<NewGameSettings>>? StartGameRequested;

	public event EventHandler? BackRequested;

	/// <summary>
	/// Editors for the properties the player can choose, in the order they were defined.
	/// </summary>
	public IReadOnlyList<CharacterPropertyEditorViewModel> Properties { get; }

	/// <summary>
	/// Gets the editor for the property with the given id, or null if the player can't edit it.
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
	/// Every string property must be filled in.
	/// </summary>
	public bool CanStartGame() => m_character.Properties
		.Where(x => x.Type == CharacterPropertyType.String)
		.All(x => !string.IsNullOrWhiteSpace((string) m_character[x.Id].Value));

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
