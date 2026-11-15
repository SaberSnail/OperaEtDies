using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Calendar;
using OperaEtDies.Controls.Locations;
using OperaEtDies.GameState;

namespace OperaEtDies.Controls.Conversation;

/// <summary>
/// Asks the player whether they'll chat with someone who asked them to, shown on top of everything else until they answer.
/// </summary>
public sealed class TalkPromptViewModel : ViewModelBase
{
	public TalkPromptViewModel(Person asker, GameWorld world)
	{
		var gameData = world.GameData;
		var calendar = gameData.Calendar;
		var talking = gameData.Schedules.Talking;
		Asker = new LocationPersonViewModel(asker, gameData.PortraitRenderer, gameData.Schedules, gameData.Families);
		Text = OurResources.TalkPromptText.FormatCurrentCulture(asker.FullName);
		AcceptText = OurResources.TalkPromptAccept.FormatCurrentCulture(calendar.FormatOffsetFrom(world.Time, calendar.CreateTimeOffset(0, 0, talking.SocializeMinutes), TimeFormat.Long));
	}

	/// <summary>
	/// Raised when the player agrees to chat.
	/// </summary>
	public event EventHandler? Accepted;

	/// <summary>
	/// Raised when the player turns them down.
	/// </summary>
	public event EventHandler? Declined;

	/// <summary>
	/// Who asked, with their name and portrait.
	/// </summary>
	public LocationPersonViewModel Asker { get; }

	/// <summary>
	/// The question, such as "Anna Smith would like to chat."
	/// </summary>
	public string Text { get; }

	/// <summary>
	/// The text of the button that agrees, with how long chatting takes.
	/// </summary>
	public string AcceptText { get; }

	public void Accept()
	{
		VerifyAccess();
		Accepted.Raise(this);
	}

	public void Decline()
	{
		VerifyAccess();
		Declined.Raise(this);
	}
}
