using System.Text.RegularExpressions;
using OperaEtDies.Controls.Locations;
using OperaEtDies.Data;
using OperaEtDies.GameState;

namespace OperaEtDies.Controls.Log;

/// <summary>
/// A piece of the text of an entry in the event log: plain text, or the name of a person or place.
/// </summary>
public sealed record LogTextPart(string Text, Person? Person = null, Location? Place = null);

/// <summary>
/// Builds the text of entries in the event log from the data, as pieces, so the people and places in it can be told apart.
/// </summary>
public static partial class LogEntryText
{
	/// <summary>
	/// Gets the text of an entry, such as "Mary Smith arrived from Mill Lane.", in pieces.
	/// </summary>
	public static IReadOnlyList<LogTextPart> Format(LogEntry entry)
	{
		ArgumentNullException.ThrowIfNull(entry);
		var subject = GetName(entry.Person, isSubject: true);
		return entry.Kind switch
		{
			LogEntryKind.Arrived when entry.Person.IsPlayer => Fill(OurResources.LogArrivedPlayer, subject, GetPlace(entry.Location)),
			LogEntryKind.Arrived => Fill(OurResources.LogArrived, subject, GetPlace(entry.Place)),
			LogEntryKind.Left => Fill(OurResources.LogLeft, subject, GetPlace(entry.Place)),
			LogEntryKind.Busy => Fill(OurResources.LogBusy, subject, GetActivity(entry)),
			LogEntryKind.Idle => Fill(OurResources.LogIdle, subject),
			LogEntryKind.ChangingClothes => Fill(OurResources.LogChangingClothes, subject),
			LogEntryKind.Talking => Fill(OurResources.LogTalking, subject, GetName(entry.Other, isSubject: false)),
			LogEntryKind.Waiting => Fill(OurResources.LogWaiting, subject, GetName(entry.Other, isSubject: false)),
			LogEntryKind.Meeting => Fill(OurResources.LogMeeting, subject, GetName(entry.Other, isSubject: false)),
			LogEntryKind.Following => Fill(OurResources.LogFollowing, subject, GetName(entry.Other, isSubject: false)),
			LogEntryKind.Failed => Fill(OurResources.LogFailed, subject, GetActivity(entry), GetCauseParts(entry.Cause ?? FailCause.NoOption, entry.Person.IsPlayer, entry.Other)),
			LogEntryKind.GaveUpWaiting => Fill(OurResources.AppointmentGaveUp, subject),
			LogEntryKind.NeverCame => Fill(OurResources.AppointmentNeverCame, subject),
			LogEntryKind.CoveringUp => Fill(OurResources.LogBusy, subject, new LogTextPart(PlayerStepsViewModel.LowerFirst(entry.Person.GameData.ModestySettings.CoverUp.Activity))),
			LogEntryKind.Retreating => Fill(OurResources.LogBusy, subject, new LogTextPart(PlayerStepsViewModel.LowerFirst(entry.Person.GameData.ModestySettings.CoverUp.RetreatActivity))),
			LogEntryKind.Embarrassed => Fill(OurResources.LogBusy, subject, new LogTextPart(PlayerStepsViewModel.LowerFirst(entry.Person.GameData.ModestySettings.CoverUp.EmbarrassedActivity))),
			LogEntryKind.WaitingForPrivacy => Fill(OurResources.LogBusy, subject, new LogTextPart(PlayerStepsViewModel.LowerFirst(entry.Person.GameData.ModestySettings.CoverUp.WaitActivity))),
			LogEntryKind.Exposed => [new LogTextPart(entry.Person.GameData.ModestySettings.CoverUp.Exposed?.Text ?? OurResources.LogExposed)],
			_ => [new LogTextPart(entry.Kind.ToString())],
		};
	}

	/// <summary>
	/// Joins the pieces of an entry's text into plain text.
	/// </summary>
	public static string ToPlainText(IEnumerable<LogTextPart> parts) => string.Concat(parts.Select(x => x.Text));

	/// <summary>
	/// Describes why a step failed, to go in a sentence, naming who it was down to, if it's known (see
	/// <see cref="GetCauseParts"/>).
	/// </summary>
	public static string GetCauseText(FailCause cause, bool isPlayer = true, Person? who = null) => ToPlainText(GetCauseParts(cause, isPlayer, who));

	/// <summary>
	/// Describes why a step failed, to go in a sentence, in pieces: who said no, didn't come, or was too shy, by name (or as
	/// "you", for the player), if it's known. Someone other than the player who left is described as such.
	/// </summary>
	public static IReadOnlyList<LogTextPart> GetCauseParts(FailCause cause, bool isPlayer = true, Person? who = null) => (cause, who) switch
	{
		(FailCause.No, { IsPlayer: true }) => [new LogTextPart(OurResources.FailCauseNoYou)],
		(FailCause.No, { } other) => Fill(OurResources.FailCauseNoNamed, GetName(other, isSubject: false)),
		(FailCause.NoShow, { IsPlayer: true }) => [new LogTextPart(OurResources.FailCauseNoShowYou)],
		(FailCause.NoShow, { } other) => Fill(OurResources.FailCauseNoShowNamed, GetName(other, isSubject: false)),
		(FailCause.Shy, { IsPlayer: true }) => [new LogTextPart(OurResources.FailCauseShyYou)],
		(FailCause.Shy, { } other) => Fill(OurResources.FailCauseShyNamed, GetName(other, isSubject: false)),
		_ => [new LogTextPart(GetUnnamedCauseText(cause, isPlayer))],
	};

	/// <summary>
	/// Describes why a step failed, without naming anyone.
	/// </summary>
	private static string GetUnnamedCauseText(FailCause cause, bool isPlayer) => cause switch
	{
		FailCause.No => OurResources.FailCauseNo,
		FailCause.Nobody => OurResources.FailCauseNobody,
		FailCause.Unreachable => OurResources.FailCauseUnreachable,
		FailCause.Missing => OurResources.FailCauseMissing,
		FailCause.Left => isPlayer ? OurResources.FailCauseLeft : OurResources.FailCauseLeftOther,
		FailCause.Interrupted => OurResources.FailCauseInterrupted,
		FailCause.Hours => OurResources.FailCauseHours,
		FailCause.NoShow => OurResources.FailCauseNoShow,
		FailCause.Shy => OurResources.FailCauseShy,
		_ => OurResources.FailCauseNoOption,
	};

	/// <summary>
	/// Gets what an entry's person was doing for its need, from the data, such as "washing clothes", in pieces, with the
	/// partner it names, if any, being the entry's other person.
	/// </summary>
	private static IReadOnlyList<LogTextPart> GetActivity(LogEntry entry)
	{
		var activity = entry.Need is { } need ? PlayerStepsViewModel.LowerFirst(need.GetActivity(entry.Step, entry.Person.Business?.Profession.Id)) : "";
		var pieces = activity.Split(ActivityText.PartnerPlaceholder);
		var parts = new List<LogTextPart>();
		for (var index = 0; index < pieces.Length; index++)
		{
			if (index != 0)
				parts.Add(entry.Other is { } other ? GetName(other, isSubject: false) : new LogTextPart(ActivityText.GetPartnerName(null)));
			if (pieces[index].Length != 0)
				parts.Add(new LogTextPart(pieces[index]));
		}

		return parts;
	}

	private static LogTextPart GetName(Person? person, bool isSubject) =>
		person is null ? new LogTextPart("") :
		person.IsPlayer ? new LogTextPart(isSubject ? OurResources.LogYou : OurResources.LogYouObject) :
		new LogTextPart(person.FullName, Person: person);

	private static LogTextPart GetPlace(Location? location) => location is null ? new LogTextPart("") : new LogTextPart(location.Title, Place: location);

	/// <summary>
	/// Fills in a format with the given pieces, keeping them apart from the text around them. Each argument is a piece, or
	/// a list of pieces.
	/// </summary>
	private static IReadOnlyList<LogTextPart> Fill(string format, params object[] args)
	{
		var parts = new List<LogTextPart>();
		var start = 0;
		foreach (Match match in PlaceholderRegex().Matches(format))
		{
			if (match.Index > start)
				parts.Add(new LogTextPart(format[start..match.Index]));
			var index = int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
			if (index < args.Length && args[index] is LogTextPart part)
				parts.Add(part);
			else if (index < args.Length && args[index] is IEnumerable<LogTextPart> list)
				parts.AddRange(list);
			start = match.Index + match.Length;
		}

		if (start < format.Length)
			parts.Add(new LogTextPart(format[start..]));
		return parts;
	}

	[GeneratedRegex(@"\{(\d+)\}")]
	private static partial Regex PlaceholderRegex();
}
