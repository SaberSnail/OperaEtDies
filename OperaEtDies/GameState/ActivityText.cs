using OperaEtDies.Data;

namespace OperaEtDies.GameState;

/// <summary>
/// Fills in the activities from the data, which can name the partner of the one doing them with
/// <see cref="PartnerPlaceholder"/>, such as "Teaching {partner} to swim", and the text the player is shown that can name
/// the other person in what they're doing (see <see cref="OtherText"/>).
/// </summary>
public static class ActivityText
{
	/// <summary>
	/// Where an activity names the partner of the one doing it: the one they last talked with or asked, such as the client
	/// of an appointment.
	/// </summary>
	public const string PartnerPlaceholder = "{partner}";

	/// <summary>
	/// Whether an activity names the partner.
	/// </summary>
	public static bool HasPartner(string activity) => activity.Contains(PartnerPlaceholder, StringComparison.Ordinal);

	/// <summary>
	/// Fills in the partner an activity names: their name, "you" for the player, or "someone" if who it is isn't known.
	/// </summary>
	public static string Fill(string activity, Person? partner) =>
		HasPartner(activity) ? activity.Replace(PartnerPlaceholder, GetPartnerName(partner), StringComparison.Ordinal) : activity;

	/// <summary>
	/// Fills in the partner an activity names, with the partner of a sequence of steps.
	/// </summary>
	public static string Fill(string activity, StepSequence? sequence) => Fill(activity, sequence?.Partner);

	/// <summary>
	/// Gets how an activity names a partner: their name, "you" for the player, or "someone" if who it is isn't known.
	/// </summary>
	public static string GetPartnerName(Person? partner) =>
		partner is null ? OurResources.ActivityPartnerUnknown : partner.IsPlayer ? OurResources.LogYouObject : partner.FullName;

	/// <summary>
	/// Fills in text from the data that can name the other person in what the player is doing (see <see cref="OtherText"/>):
	/// their name, or what a character placeholder would be for them, as far as can be seen, such as their pronoun, or
	/// "someone" if who it is isn't known. Text that isn't a valid template, which the data's validation reports, is left
	/// as it is.
	/// </summary>
	public static string FillOther(string text, Person? other)
	{
		ArgumentNullException.ThrowIfNull(text);
		return TextTemplate.TryParse(text, out var template)
			? template.Format(x => x.Field is not { } field ? GetPartnerName(other) : other?.FormatPlaceholder(field) ?? OurResources.ActivityPartnerUnknown)
			: text;
	}
}
