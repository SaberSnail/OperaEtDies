using System.Globalization;
using GoldenAnvil.Utility;
using OperaEtDies.Data;
using OperaEtDies.GameState;

namespace OperaEtDies.Controls.Locations;

/// <summary>
/// Asks the player to answer a question someone asked them as one of their steps, such as whether they'd like a
/// midwife to look in on them, shown on top of everything else until they answer.
/// </summary>
public sealed class AskPromptViewModel : ViewModelBase, INumberedChoices
{
	public AskPromptViewModel(Person asker, GameWorld world)
	{
		ArgumentNullException.ThrowIfNull(asker);
		ArgumentNullException.ThrowIfNull(world);
		var gameData = world.GameData;
		Asker = new LocationPersonViewModel(asker, gameData.PortraitRenderer, gameData.Schedules, gameData.Relationships);
		var player = asker.Current?.CurrentStep?.Player;
		Text = player?.Asked is { } asked ? OurResources.AskRequestText.FormatCurrentCulture(asker.FullName, ActivityText.FillOther(asked.Text, asker)) :
			OurResources.AskRequestTextGeneric.FormatCurrentCulture(asker.FullName, player?.Title ?? "");
		IsTooShy = world.IsPlayerTooShyToAccept(asker);
		var conflicts = world.GetPlayerAskConflicts(asker);
		Warning = IsTooShy ? OurResources.AskRequestShy : conflicts.Count == 0 ? null : OurResources.AskRequestWarning.FormatCurrentCulture(TextList.Join(conflicts.Select(x => x.Title.ToLower(CultureInfo.CurrentCulture)).ToList()));
	}

	/// <summary>
	/// Raised when the player answers, with whether they said yes.
	/// </summary>
	public event EventHandler<GenericEventArgs<bool>>? Answered;

	/// <summary>
	/// Who's asking, with their name and portrait.
	/// </summary>
	public LocationPersonViewModel Asker { get; }

	/// <summary>
	/// The question, such as "Mary Wise asks: "Would you like me to look in on you?"".
	/// </summary>
	public string Text { get; }

	/// <summary>
	/// That the player would be too shy to say yes, or which of their needs saying yes would keep them from seeing to in
	/// time, or null if neither.
	/// </summary>
	public string? Warning { get; }

	/// <summary>
	/// Whether the player would be too shy to say yes, so they can only say no.
	/// </summary>
	public bool IsTooShy { get; }

	public bool CanYes() => !IsTooShy;

	public void Yes()
	{
		VerifyAccess();
		if (CanYes())
			Answered.Raise(this, new GenericEventArgs<bool>(true));
	}

	public void No()
	{
		VerifyAccess();
		Answered.Raise(this, new GenericEventArgs<bool>(false));
	}

	/// <summary>
	/// Presses the numbered button: 1 for yes, 2 for no.
	/// </summary>
	public bool ChooseNumber(int number)
	{
		VerifyAccess();
		var choice = number switch
		{
			1 when CanYes() => (Action) Yes,
			2 => No,
			_ => null,
		};
		choice?.Invoke();
		return choice is not null;
	}
}
