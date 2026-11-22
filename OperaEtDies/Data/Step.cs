using System.Text.Json.Serialization;
using GoldenAnvil.Utility;
using OperaEtDies.Utility.Random;

namespace OperaEtDies.Data;

/// <summary>
/// One step of what someone does to meet a need, such as going somewhere, spending time there, or picking up clothes.
/// The steps are carried out in order, one at a time, each worked out when it's reached. Which settings a step can have
/// depends on its kind.
/// </summary>
public sealed class Step
{
	/// <summary>
	/// What kind of step it is.
	/// </summary>
	[JsonPropertyName("step")]
	public required StepKind Kind { get; init; }

	/// <summary>
	/// A name for the step, unique among the steps of its need or step list, for an <see cref="OnFail"/> to skip to.
	/// </summary>
	public string? Id { get; init; }

	/// <summary>
	/// When the step is carried out. Without it, it always is. A step whose condition isn't met is passed by.
	/// </summary>
	public StepCondition? When { get; init; }

	/// <summary>
	/// The hours the step can be carried out in, start to end. It fails if it's reached outside them, or they end while it's
	/// being carried out. Without it, any hour.
	/// </summary>
	public Hours? Hours { get; init; }

	/// <summary>
	/// What happens when the step fails (or for a "repeat" step, any step it repeats). Causes it says nothing about are
	/// left to whatever it's in.
	/// </summary>
	public StepFailure? OnFail { get; init; }

	/// <summary>
	/// For a "goTo" step, where to go.
	/// </summary>
	[AllowedFor(nameof(Kind), StepKind.GoTo, StepKind.Wander, StepKind.Seek)]
	[RequiredFor(nameof(Kind), StepKind.GoTo, StepKind.Wander)]
	public SchedulePlace? Place { get; init; }

	/// <summary>
	/// For a "do" step, how many minutes it takes, at most the time left of the time chosen for the goal if it meets the
	/// quota. Without it, it takes the rest of the time chosen for the goal: until a level need is met (or for a nap, the
	/// nap's length), or for a quota need, what's left of the shift chosen when the goal was chosen. For a "meet" step, how
	/// long past the appointment's time to wait for the other.
	/// </summary>
	[AllowedFor(nameof(Kind), StepKind.Do, StepKind.Wait, StepKind.Wander, StepKind.Seek, StepKind.Talk, StepKind.Ask, StepKind.Lead, StepKind.Meet)]
	[RequiredFor(nameof(Kind), StepKind.Wander, StepKind.Seek, StepKind.Talk, StepKind.Ask, StepKind.Lead, StepKind.Meet)]
	public FamilyDuration? Minutes { get; init; }

	/// <summary>
	/// For a "meet" step, how long before the appointment's time to arrive. Without it, they set off to arrive just in
	/// time.
	/// </summary>
	[AllowedFor(nameof(Kind), StepKind.Meet)]
	public Duration? Early { get; init; }

	/// <summary>
	/// For a "do" step, what someone doing it is shown to be doing, such as "Washing clothes". Defaults to the need's
	/// activity.
	/// </summary>
	[AllowedFor(nameof(Kind), StepKind.Do, StepKind.Wait, StepKind.Wander)]
	public string? Activity { get; init; }

	/// <summary>
	/// For a step of a need whose quota is measured in time, whether the time spent on it counts toward the quota. Time
	/// spent talking for a "talk" or "ask" step that meets the quota doesn't meet the socializing need. For a "meet" step,
	/// the time the one asking spends waiting there for the other counts.
	/// </summary>
	[AllowedFor(nameof(Kind), StepKind.Do, StepKind.Wander, StepKind.Seek, StepKind.Talk, StepKind.Ask, StepKind.Meet)]
	public bool MeetsQuota { get; init; }

	/// <summary>
	/// For a "do", "talk", or "ask" step, the id of a need the step meets for the partner while it lasts, if they have it:
	/// for a "talk" or "ask" step, the one talked with or asked, and for a "do" step, the partner while they're following
	/// the person, such as an appointment's client.
	/// </summary>
	[AllowedFor(nameof(Kind), StepKind.Do, StepKind.Talk, StepKind.Ask)]
	public string? PartnerMeets { get; init; }

	/// <summary>
	/// For a "do" step, whether the person is at work while doing it.
	/// </summary>
	[AllowedFor(nameof(Kind), StepKind.Do, StepKind.Wander, StepKind.Seek)]
	public bool AtWork { get; init; }

	/// <summary>
	/// For a "take" step, the kind of things it takes. For a "put" step, the kind of things in the bundle it puts down;
	/// without it, the bundle was made by a "make" step, and is done with once it's put down.
	/// </summary>
	[JsonPropertyName("kind")]
	[AllowedFor(nameof(Kind), StepKind.Take, StepKind.Put)]
	[RequiredFor(nameof(Kind), StepKind.Take)]
	public StepItemKind? ItemKind { get; init; }

	/// <summary>
	/// For a "take" step, where it takes things from.
	/// </summary>
	[AllowedFor(nameof(Kind), StepKind.Take)]
	[RequiredFor(nameof(Kind), StepKind.Take)]
	public StepItemPlace? From { get; init; }

	/// <summary>
	/// For a "put" step with a kind of things, where it puts them.
	/// </summary>
	[AllowedFor(nameof(Kind), StepKind.Put)]
	public StepItemPlace? To { get; init; }

	/// <summary>
	/// For a "take" step, whose clothes it takes: the person's own, or their whole household's.
	/// </summary>
	[AllowedFor(nameof(Kind), StepKind.Take)]
	public StepOwner? Owner { get; init; }

	/// <summary>
	/// For a "take" step, the kind of outfit whose pieces it takes (the person's own outfits of that kind), or without it,
	/// any clothing. For a "dress" or "undress" step, the kind of outfit to change into or out of.
	/// </summary>
	[AllowedFor(nameof(Kind), StepKind.Take, StepKind.Dress, StepKind.Undress)]
	[RequiredFor(nameof(Kind), StepKind.Dress, StepKind.Undress)]
	public OutfitKind? Outfit { get; init; }

	/// <summary>
	/// For a "take" step, whether it takes accessories too. Defaults to true.
	/// </summary>
	[AllowedFor(nameof(Kind), StepKind.Take)]
	public bool Accessories { get; init; } = true;

	/// <summary>
	/// For a "take" or "make" step, the bundle it takes things into or makes, with its id and title, which the person
	/// carries until a later "put" step puts it down. For a "put" step, the id of the bundle it puts down.
	/// </summary>
	[AllowedFor(nameof(Kind), StepKind.Take, StepKind.Put, StepKind.Make)]
	[RequiredFor(nameof(Kind), StepKind.Take, StepKind.Put, StepKind.Make)]
	public StepBundle? Bundle { get; init; }

	/// <summary>
	/// For a "make" step, the id of a bundle the one it makes replaces, such as a full bucket replacing an empty one.
	/// </summary>
	[AllowedFor(nameof(Kind), StepKind.Make)]
	public string? Replaces { get; init; }

	/// <summary>
	/// For a "put" step with a kind of things, how long after they're put down they can be used again, such as washing
	/// hung out to dry. Until then they're out of sight. Without it, they can be used straight away.
	/// </summary>
	[AllowedFor(nameof(Kind), StepKind.Put)]
	public Duration? AfterDelay { get; init; }

	/// <summary>
	/// For a "put" step with <see cref="AfterDelay"/>, what its things are shown to be doing until they can be used again,
	/// such as "Drying".
	/// </summary>
	[AllowedFor(nameof(Kind), StepKind.Put)]
	public string? DelayActivity { get; init; }

	/// <summary>
	/// For a "repeat" step, the steps it repeats.
	/// </summary>
	[AllowedFor(nameof(Kind), StepKind.Repeat)]
	[RequiredFor(nameof(Kind), StepKind.Repeat)]
	public IReadOnlyList<Step>? Steps { get; init; }

	/// <summary>
	/// For a "repeat" step, whether someone other than the player stops repeating after a pass when another of their needs
	/// is more urgent than the goal.
	/// </summary>
	[AllowedFor(nameof(Kind), StepKind.Repeat)]
	public bool Reconsider { get; init; }

	/// <summary>
	/// For a "choose" step, the options it chooses from.
	/// </summary>
	[AllowedFor(nameof(Kind), StepKind.Choose)]
	[RequiredFor(nameof(Kind), StepKind.Choose)]
	public IReadOnlyList<StepOption>? Options { get; init; }

	/// <summary>
	/// For a "choose" step, whether it takes the first option whose condition is met, rather than choosing by frequency.
	/// </summary>
	[AllowedFor(nameof(Kind), StepKind.Choose)]
	public bool First { get; init; }

	/// <summary>
	/// For a "call" step, the id of the step list it carries out.
	/// </summary>
	[AllowedFor(nameof(Kind), StepKind.Call)]
	[RequiredFor(nameof(Kind), StepKind.Call)]
	public string? List { get; init; }

	/// <summary>
	/// For a "remember" step, the name it remembers the person's location by, for a later "goTo" step.
	/// </summary>
	[AllowedFor(nameof(Kind), StepKind.Remember, StepKind.Ask)]
	[RequiredFor(nameof(Kind), StepKind.Remember, StepKind.Ask)]
	public string? Name { get; init; }

	/// <summary>
	/// For a "wait" step, waits until the next time it's this hour, rather than for a number of minutes.
	/// </summary>
	[AllowedFor(nameof(Kind), StepKind.Wait)]
	public int? UntilHour { get; init; }

	/// <summary>
	/// For a "wander" step, how long the person stays at each quarter they wander to.
	/// </summary>
	[AllowedFor(nameof(Kind), StepKind.Wander)]
	[RequiredFor(nameof(Kind), StepKind.Wander)]
	public DurationRange? StopMinutes { get; init; }

	/// <summary>
	/// For a "talk" or "ask" step, who the person talks with.
	/// </summary>
	[AllowedFor(nameof(Kind), StepKind.Talk, StepKind.Ask)]
	[RequiredFor(nameof(Kind), StepKind.Talk, StepKind.Ask)]
	public StepTarget? With { get; init; }

	/// <summary>
	/// For a "talk", "ask", or "seek" step, who can be talked with or looked for: only people who meet this condition (and
	/// with <see cref="StepWho.Friends"/>, only the person's friends). Without it, anyone can.
	/// </summary>
	[AllowedFor(nameof(Kind), StepKind.Talk, StepKind.Ask, StepKind.Seek)]
	public StepWho? Who { get; init; }

	/// <summary>
	/// For an "ask" step, the chance that the answer is yes.
	/// </summary>
	[AllowedFor(nameof(Kind), StepKind.Ask)]
	[RequiredFor(nameof(Kind), StepKind.Ask)]
	public StepChance? Chance { get; init; }

	/// <summary>
	/// For an "ask" step, how long saying yes would take the one asked, such as going with the one asking to be helped at
	/// home. They only say yes if it fits around their own needs: none of them would be too urgent by the end of it (see
	/// <see cref="TalkingSettings.AgreeUrgency"/>).
	/// </summary>
	[AllowedFor(nameof(Kind), StepKind.Ask)]
	public FamilyDuration? ForMinutes { get; init; }

	/// <summary>
	/// For an "ask" step, an appointment made when the answer is yes: the two meet later, at a time that fits the one
	/// asked, where the one asking carries out the appointment's steps.
	/// </summary>
	[AllowedFor(nameof(Kind), StepKind.Ask)]
	public StepAppointment? Appointment { get; init; }

	/// <summary>
	/// For an "ask" step, a job taken on when the answer is yes: work for the one asked, at a place worked out when it's
	/// taken on, for a number of hours in all, spread over as many days as it takes.
	/// </summary>
	[AllowedFor(nameof(Kind), StepKind.Ask)]
	public StepJob? Job { get; init; }

	/// <summary>
	/// For a "dress" or "undress" step, whether the partner (the one the steps last talked with or asked) changes too, at
	/// the same time, if they're there.
	/// </summary>
	[AllowedFor(nameof(Kind), StepKind.Dress, StepKind.Undress)]
	public bool WithPartner { get; init; }

	/// <summary>
	/// How the player takes the step, or null if they don't take it themselves.
	/// </summary>
	[AllowedFor(nameof(Kind), StepKind.GoTo, StepKind.Do, StepKind.Take, StepKind.Put, StepKind.Dress, StepKind.Undress, StepKind.Wait, StepKind.Ask)]
	[RequiredFor(nameof(Kind), StepKind.Ask)]
	public StepPlayer? Player { get; init; }

	/// <summary>
	/// Whether the player has a button for the step, named by its title, which carries it out. The player asks people in
	/// conversation instead, so an "ask" step's title is in conversation rather than a button of its own.
	/// </summary>
	public bool IsPlayerAction => Player?.Title is not null && Kind != StepKind.Ask;

	/// <summary>
	/// Validates the step on its own, for the need it's carried out for, if it's known. Without one, as for a step list no
	/// need calls, checks that depend on the need are left out.
	/// </summary>
	internal IEnumerable<string> Validate(string path, Need? need)
	{
		var kind = Kind.ToString();
		kind = char.ToLowerInvariant(kind[0]) + kind[1..];
		// Which kinds of step allow or require each setting is marked on the setting, and some settings are also required in
		// other cases. A setting that's required is only required for the kinds of step it's allowed for.
		IEnumerable<string> Check(string property, bool isSet, bool isRequired = false)
		{
			var name = DataRules.GetDataName(typeof(Step), property);
			return DataRules.Check(this, property, isSet, isRequired) switch
			{
				DataRuleViolation.NotAllowed => [OurResources.StepSettingNotAllowed.FormatCurrentCulture($"{path}.{name}", kind)],
				DataRuleViolation.Missing => [OurResources.StepSettingMissing.FormatCurrentCulture($"{path}.{name}", kind)],
				_ => [],
			};
		}

		var errors = new List<string>();
		if (Id is not null && string.IsNullOrWhiteSpace(Id))
			errors.Add(OurResources.DataValueMissing.FormatCurrentCulture($"{path}.id"));

		// A "meet" step that fails always calls off the appointment.
		if (Kind == StepKind.Meet && OnFail is not null)
			errors.Add(OurResources.StepSettingNotAllowed.FormatCurrentCulture($"{path}.onFail", kind));
		if (Hours is not null)
			errors.AddRange(Hours.Validate($"{path}.hours", this));
		errors.AddRange(Check(nameof(ItemKind), ItemKind is not null));
		errors.AddRange(Check(nameof(From), From is not null));
		errors.AddRange(Check(nameof(To), To is not null, isRequired: Kind == StepKind.Put && ItemKind is not null));
		if (Kind == StepKind.Put && ItemKind is null && To is not null)
			errors.Add(OurResources.StepSettingNotAllowed.FormatCurrentCulture($"{path}.to", kind));
		errors.AddRange(Check(nameof(Replaces), Replaces is not null));
		errors.AddRange(Check(nameof(AfterDelay), AfterDelay is not null));
		errors.AddRange(Check(nameof(DelayActivity), DelayActivity is not null, isRequired: AfterDelay is not null && ItemKind is not null));
		errors.AddRange(Check(nameof(Early), Early is not null));
		if (Kind == StepKind.Put && ItemKind is null && AfterDelay is not null)
			errors.Add(OurResources.StepSettingNotAllowed.FormatCurrentCulture($"{path}.afterDelay", kind));
		if (Kind == StepKind.Put && AfterDelay is null && DelayActivity is not null)
			errors.Add(OurResources.StepSettingNotAllowed.FormatCurrentCulture($"{path}.delayActivity", kind));
		if (Bundle is { } stepBundle)
		{
			if (string.IsNullOrWhiteSpace(stepBundle.Id))
				errors.Add(OurResources.DataValueMissing.FormatCurrentCulture($"{path}.bundle.id"));
			if (Kind is StepKind.Take or StepKind.Make && string.IsNullOrWhiteSpace(stepBundle.Title))
				errors.Add(OurResources.DataValueMissing.FormatCurrentCulture($"{path}.bundle.title"));
			if (Kind == StepKind.Put && stepBundle.Title is not null)
				errors.Add(OurResources.StepSettingNotAllowed.FormatCurrentCulture($"{path}.bundle.title", kind));
		}

		errors.AddRange(Check(nameof(Place), Place is not null));
		if (Kind == StepKind.Seek && Who is { Friends: true } && Place is not null)
			errors.Add(OurResources.StepSeekFriendsWithPlace.FormatCurrentCulture($"{path}.place"));
		if (Place is not null)
			errors.AddRange(Place.Validate($"{path}.place"));
		if (Kind == StepKind.Wander && Place is not null && Place.Kind != NeedPlace.Quarter)
			errors.Add(OurResources.StepWanderPlaceInvalid.FormatCurrentCulture($"{path}.place"));
		errors.AddRange(Check(nameof(Minutes), Minutes is not null, isRequired: (Kind == StepKind.Do && (need?.IsRuns ?? false)) || (Kind == StepKind.Wait && UntilHour is null)));
		errors.AddRange(Check(nameof(With), With is not null));
		errors.AddRange(Check(nameof(Who), Who is not null));
		errors.AddRange(Check(nameof(Chance), Chance is not null));
		if (Chance is not null)
			errors.AddRange(Chance.Validate($"{path}.chance"));
		errors.AddRange(Check(nameof(ForMinutes), ForMinutes is not null));
		if (ForMinutes is not null)
			errors.AddRange(ForMinutes.Validate($"{path}.forMinutes", isWhole: true));
		errors.AddRange(Check(nameof(Appointment), Appointment is not null));
		errors.AddRange(Check(nameof(WithPartner), WithPartner));
		if (Appointment is not null)
			errors.AddRange(Appointment.Validate($"{path}.appointment"));
		errors.AddRange(Check(nameof(Job), Job is not null));
		if (Job is not null)
		{
			if (Appointment is not null)
				errors.Add(OurResources.StepJobWithAppointment.FormatCurrentCulture($"{path}.job"));
			errors.AddRange(Job.Validate($"{path}.job"));
		}

		errors.AddRange(Check(nameof(PartnerMeets), PartnerMeets is not null));
		if (PartnerMeets is not null && string.IsNullOrWhiteSpace(PartnerMeets))
			errors.Add(OurResources.DataValueMissing.FormatCurrentCulture($"{path}.partnerMeets"));
		if (DelayActivity is not null && string.IsNullOrWhiteSpace(DelayActivity))
			errors.Add(OurResources.DataValueMissing.FormatCurrentCulture($"{path}.delayActivity"));
		errors.AddRange(Check(nameof(StopMinutes), StopMinutes is not null));
		if (StopMinutes is not null)
		{
			errors.AddRange(CharacterData.ValidateRange($"{path}.stopMinutes", StopMinutes));
			if (StopMinutes.Minimum < 1)
				errors.Add(OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.stopMinutes.minimum", StopMinutes.Minimum.Minutes, 1));
		}
		errors.AddRange(Check(nameof(UntilHour), UntilHour is not null));
		if (Kind == StepKind.Wait && Minutes is not null && UntilHour is not null)
			errors.Add(OurResources.StepWaitInvalid.FormatCurrentCulture(path));
		if (UntilHour is { } hour && hour is < 0 or > 23)
			errors.Add(OurResources.ScheduleHourInvalid.FormatCurrentCulture($"{path}.untilHour", hour, 23));
		if (Minutes is not null)
			errors.AddRange(Minutes.Validate($"{path}.minutes", isWhole: true));
		errors.AddRange(Check(nameof(Activity), Activity is not null));
		if (Activity is not null && string.IsNullOrWhiteSpace(Activity))
			errors.Add(OurResources.DataValueMissing.FormatCurrentCulture($"{path}.activity"));
		errors.AddRange(Check(nameof(MeetsQuota), MeetsQuota));
		if (MeetsQuota && DataRules.IsAllowed(this, nameof(MeetsQuota)) && need is not null && need.Quota?.Time is null)
			errors.Add(OurResources.StepMeetsQuotaNotAllowed.FormatCurrentCulture($"{path}.meetsQuota"));
		errors.AddRange(Check(nameof(AtWork), AtWork));
		errors.AddRange(Check(nameof(Owner), Owner is not null));
		errors.AddRange(Check(nameof(Outfit), Outfit is not null));
		errors.AddRange(Check(nameof(Accessories), !Accessories));
		errors.AddRange(Check(nameof(Bundle), Bundle is not null));
		errors.AddRange(Check(nameof(Steps), Steps is not null));
		errors.AddRange(Check(nameof(Reconsider), Reconsider));
		errors.AddRange(Check(nameof(Options), Options is not null));
		errors.AddRange(Check(nameof(First), First));
		errors.AddRange(Check(nameof(List), List is not null));
		errors.AddRange(Check(nameof(Name), Name is not null));
		errors.AddRange(Check(nameof(Player), Player is not null));
		foreach (var (name, value) in new[] { ("replaces", Replaces), ("list", List), ("name", Name) })
		{
			if (value is not null && string.IsNullOrWhiteSpace(value))
				errors.Add(OurResources.DataValueMissing.FormatCurrentCulture($"{path}.{name}"));
		}

		if (When is { } when)
			errors.AddRange(when.Validate($"{path}.when", need));
		if (Who is { } who)
			errors.AddRange(who.Validate($"{path}.who"));

		if (Player is { } player)
			errors.AddRange(player.Validate($"{path}.player", this));
		return errors;
	}
}

/// <summary>
/// What kind of thing a step does.
/// </summary>
public enum StepKind
{
	/// <summary>
	/// Walks to a place, the quickest way.
	/// </summary>
	GoTo,

	/// <summary>
	/// Spends time where the person is, meeting their need.
	/// </summary>
	Do,

	/// <summary>
	/// Picks up things of a kind from where they're kept, such as clothes from the wardrobe at home, to carry them in a
	/// bundle. It only takes clothes that can be worn now. It takes no time.
	/// </summary>
	Take,

	/// <summary>
	/// Puts down a bundle the person is carrying: its things where they're kept, or a bundle a "make" step made, which is
	/// then done with. It takes no time.
	/// </summary>
	Put,

	/// <summary>
	/// Makes a bundle for the person to carry, such as an empty bucket, or a full one in place of the empty one. It takes no
	/// time.
	/// </summary>
	Make,

	/// <summary>
	/// Changes into an outfit of a kind, the first of the person's that's all at hand, unless they're already wearing one.
	/// It takes the time changing clothes takes.
	/// </summary>
	Dress,

	/// <summary>
	/// Changes out of an outfit of a kind the person is wearing, if they are. It takes the time changing clothes takes.
	/// </summary>
	Undress,

	/// <summary>
	/// Counts one run of a need whose quota is measured in runs. It takes no time.
	/// </summary>
	CountRun,

	/// <summary>
	/// Carries out its steps, then carries them out again for as long as there's time left of the time chosen for the goal
	/// and the need can still be met. Only for a need whose quota is measured in time.
	/// </summary>
	Repeat,

	/// <summary>
	/// Carries out the steps of one of its options: one chosen by frequency among those whose condition is met and whose
	/// places can be reached, or with "first", the first whose condition is met. It does nothing if there's none.
	/// </summary>
	Choose,

	/// <summary>
	/// Carries out the steps of a step list.
	/// </summary>
	Call,

	/// <summary>
	/// Stops carrying out the goal's steps, skipping the rest of them.
	/// </summary>
	Stop,

	/// <summary>
	/// Remembers where the person is by a name, for a later "goTo" step. It takes no time.
	/// </summary>
	Remember,

	/// <summary>
	/// Waits where the person is, without meeting the need, for a number of minutes or until an hour.
	/// </summary>
	Wait,

	/// <summary>
	/// Wanders between quarters of some kinds for a number of minutes, staying a while at each one.
	/// </summary>
	Wander,

	/// <summary>
	/// Looks for a friend: chooses a friend and somewhere they might be from clues, walks there, and looks around for a
	/// number of minutes, which doesn't meet the need, asking someone there what they've seen of their friends. A friend
	/// they meet may stop to chat with them. It does nothing if there's nowhere to look.
	/// </summary>
	Seek,

	/// <summary>
	/// Talks with someone where the person is for a number of minutes, as a chat, which meets the socializing need of each
	/// of them who counts the other as a friend. The one talked with becomes the person's partner for later steps. It does
	/// nothing if there's no one to talk with.
	/// </summary>
	Talk,

	/// <summary>
	/// Asks someone where the person is a question, which takes a number of minutes, and remembers the answer by a name for
	/// later steps' conditions. The answer is yes by chance. The one asked becomes the person's partner. The answer is no
	/// if there's no one to ask.
	/// </summary>
	Ask,

	/// <summary>
	/// Has the person's partner follow them wherever they go, for at most a number of minutes, until a "release" step or
	/// the end of the steps lets them go.
	/// </summary>
	Lead,

	/// <summary>
	/// Lets go of anyone following the person, who then choose what to do next.
	/// </summary>
	Release,

	/// <summary>
	/// Goes to an appointment's place and waits there for the other, until the appointment starts once both are there, or
	/// for a number of minutes past its time. Only directly among an appointment's steps, which have exactly one: the steps
	/// before it are carried out once the person sets off, and those after it once the appointment starts.
	/// </summary>
	Meet,
}

/// <summary>
/// Who a "talk" or "ask" step talks with.
/// </summary>
public enum StepTarget
{
	/// <summary>
	/// The friend a "seek" step chose to look for, if they're there, or for a "seek" step that looks for anyone who meets a
	/// condition, anyone there.
	/// </summary>
	Sought,

	/// <summary>
	/// The person last talked with or asked by the steps, if they're there.
	/// </summary>
	Partner,

	/// <summary>
	/// Anyone there, chosen at random.
	/// </summary>
	Anyone,
}

/// <summary>
/// Who a step talks with, asks, or looks for: people who meet a condition on their character, and if it says so, only
/// the person's friends. Its values and not values can also list "family", with "member" for those in the same household
/// as the person taking the step, or "other" for those who aren't.
/// </summary>
public sealed class StepWho : CharacterCondition
{
	/// <summary>
	/// The key for whether someone is in the household of the person taking the step.
	/// </summary>
	public const string FamilyKey = "family";

	/// <summary>
	/// The family value for someone in the household of the person taking the step.
	/// </summary>
	public const string FamilyMember = "member";

	/// <summary>
	/// The family value for someone not in the household of the person taking the step.
	/// </summary>
	public const string FamilyOther = "other";

	/// <summary>
	/// Only the person's friends, among those who meet the rest of the condition. A "seek" step looking for friends picks
	/// a friend and where they might be from clues, rather than looking in a place.
	/// </summary>
	public bool Friends { get; init; }

	/// <summary>
	/// Whether it says anything about family.
	/// </summary>
	public bool HasFamily => (Values?.ContainsKey(FamilyKey) ?? false) || (NotValues?.ContainsKey(FamilyKey) ?? false);

	/// <summary>
	/// Gets the condition to check against the character properties: itself without family, which isn't a property, or
	/// null if it's only about friends or family, without any condition on their character.
	/// </summary>
	internal CharacterCondition? GetPropertyCondition()
	{
		var values = WithoutFamily(Values);
		var notValues = WithoutFamily(NotValues);
		return values is not null || notValues is not null || Hidden is { Count: > 0 } ? new CharacterCondition { Values = values, NotValues = notValues, Hidden = Hidden } :
			Friends || HasFamily ? null : this;
	}

	/// <summary>
	/// Checks that its family values are "member" or "other".
	/// </summary>
	internal IEnumerable<string> Validate(string path)
	{
		foreach (var (name, values) in new[] { ("values", Values), ("notValues", NotValues) })
		{
			if (values?.GetValueOrDefault(FamilyKey) is not { } family)
				continue;
			var listName = $"{path}.{name}.{FamilyKey}";
			if (family.Count == 0)
				yield return OurResources.DataListEmpty.FormatCurrentCulture(listName);
			foreach (var value in family.Where(x => x is not FamilyMember and not FamilyOther))
				yield return OurResources.StepWhoFamilyInvalid.FormatCurrentCulture(listName, value);
		}
	}

	private static IReadOnlyDictionary<string, IReadOnlyList<string>>? WithoutFamily(IReadOnlyDictionary<string, IReadOnlyList<string>>? values) =>
		values?.Where(x => x.Key != FamilyKey).ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal) is { Count: > 0 } rest ? rest : null;
}

/// <summary>
/// An appointment an "ask" step makes when the answer is yes: where and within what hours the two meet, and what the
/// one asking does there.
/// </summary>
public sealed class StepAppointment
{
	/// <summary>
	/// Where the two meet, worked out for the one asking when it's made, with the one asked as their partner (so
	/// "partnerHome" is the home of the one asked).
	/// </summary>
	public SchedulePlace? Place { get; init; }

	/// <summary>
	/// How many hours ahead to look for a time.
	/// </summary>
	public int WithinHours { get; init; } = 24;

	/// <summary>
	/// The hours an appointment can be within, start to end. Without it, any hour.
	/// </summary>
	public Hours? Hours { get; init; }

	/// <summary>
	/// How far apart, beyond how long they take, someone's appointments must be.
	/// </summary>
	public Duration? Gap { get; init; }

	/// <summary>
	/// Once it starts, how much longer than its steps take the one asked stays with the one asking, in case they never
	/// finish, such as a player who goes off to do something else.
	/// </summary>
	public Duration? Patience { get; init; }

	/// <summary>
	/// What each does once they set off (the steps before its "meet" step, for the one asking), going there and meeting
	/// (its "meet" step), and what the one asking does once both have arrived, while the other stays with them (the steps
	/// after it).
	/// </summary>
	public IReadOnlyList<Step?>? Steps { get; init; }

	internal IEnumerable<string> Validate(string path)
	{
		if (Place is null)
			yield return OurResources.DataValueMissing.FormatCurrentCulture($"{path}.place");
		else
		{
			foreach (var error in Place.Validate($"{path}.place"))
				yield return error;
		}

		if (WithinHours < 1)
			yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.withinHours", WithinHours, 1);
		if (Gap is null)
			yield return OurResources.DataValueMissing.FormatCurrentCulture($"{path}.gap");
		if (Patience is null)
			yield return OurResources.DataValueMissing.FormatCurrentCulture($"{path}.patience");
		if (Hours is not null)
		{
			foreach (var error in Hours.Validate($"{path}.hours", this))
				yield return error;
		}

		if (Steps is null)
			yield return OurResources.DataValueMissing.FormatCurrentCulture($"{path}.steps");
		else if (Steps.Count == 0)
			yield return OurResources.DataListEmpty.FormatCurrentCulture($"{path}.steps");
	}
}

/// <summary>
/// A job an "ask" step takes on when the answer is yes: work for the one asked, at a place, for a number of hours in all.
/// Until they're done, a "goTo" step to the job site goes there, and the time spent there on steps that meet the need's
/// quota counts toward the job.
/// </summary>
public sealed class StepJob
{
	/// <summary>
	/// Where the work is done, worked out for the one asking when it's taken on, with the one asked as their partner (so
	/// "partnerHome" is the home of the one asked).
	/// </summary>
	public SchedulePlace? Place { get; init; }

	/// <summary>
	/// How long the work takes in all.
	/// </summary>
	public Duration? Minutes { get; init; }

	internal IEnumerable<string> Validate(string path)
	{
		if (Place is null)
		{
			yield return OurResources.DataValueMissing.FormatCurrentCulture($"{path}.place");
		}
		else
		{
			foreach (var error in Place.Validate($"{path}.place"))
				yield return error;
			if (Place.Kind == NeedPlace.JobSite)
				yield return OurResources.StepJobPlaceInvalid.FormatCurrentCulture($"{path}.place");
		}

		if (Minutes is not { } minutes)
			yield return OurResources.DataValueMissing.FormatCurrentCulture($"{path}.minutes");
		else if (minutes < 1)
			yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.minutes", minutes.Minutes, 1);
	}
}

/// <summary>
/// The chance, from 0 to 100, that someone says yes when an "ask" step asks them: a base chance, plus more the better
/// they think of the one asking, multiplied by every factor whose condition they meet, less some for how urgent their own
/// goal is.
/// </summary>
public sealed class StepChance
{
	/// <summary>
	/// The chance with no relationship, factors, or goal.
	/// </summary>
	public required double Base { get; init; }

	/// <summary>
	/// Added for a relationship score of the maximum, in proportion for less.
	/// </summary>
	public double PerScore { get; init; }

	/// <summary>
	/// Multiplies the chance for people who meet a condition.
	/// </summary>
	public IReadOnlyList<ChanceFactor> Factors { get; init; } = [];

	/// <summary>
	/// Subtracted for each point of urgency of the goal the one asked is busy with.
	/// </summary>
	public double Busy { get; init; }

	internal IEnumerable<string> Validate(string path)
	{
		if (!(Base >= 0) || Base > 100)
			yield return OurResources.StepChanceOutOfRange.FormatCurrentCulture($"{path}.base");
		if (!double.IsFinite(PerScore))
			yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.perScore", PerScore, -100);
		if (!(Busy >= 0) || !double.IsFinite(Busy))
			yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.busy", Busy, 0);
		for (var index = 0; index < Factors.Count; index++)
		{
			if (Factors[index] is not { } factor)
				yield return OurResources.DataListItemEmpty.FormatCurrentCulture($"{path}.factors");
			else if (!(factor.Factor >= 0) || !double.IsFinite(factor.Factor))
				yield return OurResources.DataNumberTooSmall.FormatCurrentCulture($"{path}.factors[{index}].factor", factor.Factor, 0);
		}
	}
}

/// <summary>
/// Multiplies a chance for people who meet a condition.
/// </summary>
public sealed class ChanceFactor
{
	public required CharacterCondition When { get; init; }

	public required double Factor { get; init; }
}

/// <summary>
/// Whose clothes a step takes.
/// </summary>
public enum StepOwner
{
	/// <summary>
	/// The person's own.
	/// </summary>
	Self,

	/// <summary>
	/// Anyone's in the person's household.
	/// </summary>
	Household,
}

/// <summary>
/// When a step is carried out. Every setting it has must be met.
/// </summary>
public sealed class StepCondition
{
	/// <summary>
	/// Met when the person has an outfit of this kind that they aren't dressed in, with every piece of it worn, carried, or
	/// kept at home ready to wear, and some of it at home.
	/// </summary>
	public OutfitKind? OutfitAtHome { get; init; }

	/// <summary>
	/// For a quota need, met when this is whether its quota for the day or week is done, or its hours for the day are over.
	/// </summary>
	public bool? DoneForToday { get; init; }

	/// <summary>
	/// Met when the person is at a place like this one.
	/// </summary>
	public SchedulePlace? At { get; init; }

	/// <summary>
	/// Met within these hours of the day. It can only have a start and an end.
	/// </summary>
	public Hours? Hours { get; init; }

	/// <summary>
	/// Met when at least this many minutes are left of the time chosen for the goal.
	/// </summary>
	public int? ShiftLeft { get; init; }

	/// <summary>
	/// Met when the person owns or helps in a business in the profession with this id.
	/// </summary>
	public string? BusinessProfession { get; init; }

	/// <summary>
	/// Met when the answer an "ask" step remembered by this name was yes.
	/// </summary>
	public string? AnsweredYes { get; init; }

	/// <summary>
	/// Met when the answer an "ask" step remembered by this name was no, or nobody was asked.
	/// </summary>
	public string? AnsweredNo { get; init; }

	/// <summary>
	/// Met when this is whether the person has a job for the need (taken on by an "ask" step) that isn't done yet.
	/// </summary>
	public bool? HasJob { get; init; }

	/// <summary>
	/// Met when this condition isn't met.
	/// </summary>
	public StepCondition? Not { get; init; }

	/// <summary>
	/// Met when at least one of these conditions is met.
	/// </summary>
	public IReadOnlyList<StepCondition>? Any { get; init; }

	/// <summary>
	/// Gets the conditions it's made of (its "not" and "any"), each with its path, and theirs in turn.
	/// </summary>
	internal IEnumerable<(string Path, StepCondition Condition)> GetNested(string path)
	{
		if (Not is { } not)
		{
			yield return ($"{path}.not", not);
			foreach (var nested in not.GetNested($"{path}.not"))
				yield return nested;
		}

		var any = Any ?? [];
		for (var index = 0; index < any.Count; index++)
		{
			if (any[index] is not { } item)
				continue;
			yield return ($"{path}.any[{index}]", item);
			foreach (var nested in item.GetNested($"{path}.any[{index}]"))
				yield return nested;
		}
	}

	/// <summary>
	/// Validates the condition, for the need whose steps it's in, if it's known. Without one, as for a step list no need
	/// calls, checks that depend on the need are left out.
	/// </summary>
	internal IEnumerable<string> Validate(string path, Need? need)
	{
		if (DoneForToday is not null && need is not null && need.Kind != NeedKind.Quota)
			yield return OurResources.StepConditionQuotaOnly.FormatCurrentCulture($"{path}.doneForToday");
		if (OutfitAtHome is null && DoneForToday is null && At is null && Hours is null && ShiftLeft is null && BusinessProfession is null && AnsweredYes is null && AnsweredNo is null &&
			HasJob is null && Not is null && Any is null)
		{
			yield return OurResources.DataValueMissing.FormatCurrentCulture(path);
		}

		if (Not is { } not)
		{
			foreach (var error in not.Validate($"{path}.not", need))
				yield return error;
		}

		if (Any is { } any)
		{
			if (any.Count == 0)
				yield return OurResources.DataListEmpty.FormatCurrentCulture($"{path}.any");
			for (var index = 0; index < any.Count; index++)
			{
				if (any[index] is not { } item)
				{
					yield return OurResources.DataListItemEmpty.FormatCurrentCulture($"{path}.any");
					continue;
				}

				foreach (var error in item.Validate($"{path}.any[{index}]", need))
					yield return error;
			}
		}
		if (At is { } at)
		{
			foreach (var error in at.Validate($"{path}.at"))
				yield return error;
		}

		if (Hours is { } hours)
		{
			foreach (var error in hours.Validate($"{path}.hours", this))
				yield return error;
		}

		if (ShiftLeft < 1)
			yield return OurResources.DataNumberNotPositive.FormatCurrentCulture($"{path}.shiftLeft", ShiftLeft);
	}
}

/// <summary>
/// One of the options of a "choose" step.
/// </summary>
public sealed class StepOption : IHasFrequency
{
	/// <summary>
	/// How often it's chosen.
	/// </summary>
	public FrequencyLevel Frequency { get; init; } = new(Utility.Random.Frequency.Common);

	/// <summary>
	/// When it can be chosen. Without it, it always can.
	/// </summary>
	public StepCondition? When { get; init; }

	/// <summary>
	/// How the player sees the option when they choose: required for the options of a "choose" step that doesn't take the
	/// first.
	/// </summary>
	public StepOptionPlayer? Player { get; init; }

	/// <summary>
	/// What happens when one of its steps fails, for causes the step says nothing about.
	/// </summary>
	public StepFailure? OnFail { get; init; }

	/// <summary>
	/// The steps carried out when it's chosen.
	/// </summary>
	public IReadOnlyList<Step> Steps { get; init; } = [];

	/// <summary>
	/// Whether the ids of its steps are its own, so an "onFail" in them skips to a step among them, as for a profession's
	/// steps, which are checked on their own.
	/// </summary>
	internal bool HasOwnIds { get; init; }
}

/// <summary>
/// How the player sees one of the options of a "choose" step.
/// </summary>
public sealed class StepOptionPlayer
{
	/// <summary>
	/// What the option is called, such as "Gather herbs".
	/// </summary>
	public string? Title { get; init; }
}

/// <summary>
/// Steps that "call" steps in any need can carry out, so they can be shared.
/// </summary>
public sealed class StepList : IIdentified
{
	public required string Id { get; init; }

	/// <summary>
	/// What happens when one of its steps fails, for causes the step says nothing about.
	/// </summary>
	public StepFailure? OnFail { get; init; }

	public IReadOnlyList<Step> Steps { get; init; } = [];
}

/// <summary>
/// The kinds of things "take" and "put" steps handle.
/// </summary>
public enum StepItemKind
{
	Clothing,
}

/// <summary>
/// Where "take" steps take things from and "put" steps put them.
/// </summary>
public enum StepItemPlace
{
	/// <summary>
	/// Where the household keeps its clothes at home.
	/// </summary>
	Wardrobe,
}

/// <summary>
/// How the player takes a step. A step with a title has a button with that title that carries it out, while it's the
/// player's current step. The player takes other steps themselves, or has them done for them.
/// </summary>
public sealed class StepPlayer
{
	/// <summary>
	/// The name of the action, such as "Sleep", or for an "ask" step, what the player can choose to ask in conversation, such
	/// as "Offer to help with the birth".
	/// </summary>
	public string? Title { get; init; }

	/// <summary>
	/// The start of the sentence telling the player what they did, followed by how long it took, such as "You slept".
	/// </summary>
	public string? Result { get; init; }

	/// <summary>
	/// For a "goTo" step, what to tell the player to do when it's next, such as "Take the washing home to hang it out." For an
	/// "ask" step, what the player says when they ask, if anything.
	/// </summary>
	public LongText? Text { get; init; }

	/// <summary>
	/// For an "ask" step, what the player is told when someone else asks them its question, such as "She mentions that she's a
	/// midwife, and asks whether you'd like her to look in on you." Without it, they're told the title.
	/// </summary>
	public LongText? Asked { get; init; }

	internal IEnumerable<string> Validate(string path, Step step)
	{
		var isGoTo = step.Kind == StepKind.GoTo;
		var isAsk = step.Kind == StepKind.Ask;
		if (isGoTo ? Title is not null : Title is null || string.IsNullOrWhiteSpace(Title))
			yield return isGoTo ? OurResources.PlayerActionSettingNotAllowed.FormatCurrentCulture($"{path}.title") : OurResources.DataValueMissing.FormatCurrentCulture($"{path}.title");
		if (isGoTo || isAsk ? Result is not null : Result is null || string.IsNullOrWhiteSpace(Result))
			yield return isGoTo || isAsk ? OurResources.PlayerActionSettingNotAllowed.FormatCurrentCulture($"{path}.result") : OurResources.DataValueMissing.FormatCurrentCulture($"{path}.result");
		if (!isGoTo && !isAsk && Text is not null)
			yield return OurResources.PlayerActionSettingNotAllowed.FormatCurrentCulture($"{path}.text");
		if (isGoTo && string.IsNullOrWhiteSpace(Text?.Text))
			yield return OurResources.DataValueMissing.FormatCurrentCulture($"{path}.text");
		if (isAsk && Text is not null && string.IsNullOrWhiteSpace(Text.Text))
			yield return OurResources.DataValueMissing.FormatCurrentCulture($"{path}.text.text");
		if (!isAsk && Asked is not null)
			yield return OurResources.PlayerActionSettingNotAllowed.FormatCurrentCulture($"{path}.asked");
		if (isAsk && Asked is not null && string.IsNullOrWhiteSpace(Asked.Text))
			yield return OurResources.DataValueMissing.FormatCurrentCulture($"{path}.asked.text");
	}
}
