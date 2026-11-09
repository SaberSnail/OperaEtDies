using OperaEtDies.Characters;

namespace OperaEtDies.Controls.NewGame;

/// <summary>
/// The choices the player made when configuring a new game.
/// </summary>
public sealed record NewGameSettings(CharacterTraits Player);
