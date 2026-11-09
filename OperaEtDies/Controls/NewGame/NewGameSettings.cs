using OperaEtDies.Data;

namespace OperaEtDies.Controls.NewGame;

/// <summary>
/// The choices the player made when configuring a new game.
/// </summary>
public sealed record NewGameSettings(
	Profession Profession,
	string FirstName,
	string LastName,
	CharacterOption Height,
	CharacterOption Weight,
	int Age);
