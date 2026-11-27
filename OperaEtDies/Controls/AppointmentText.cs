using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Calendar;
using OperaEtDies.Data;
using OperaEtDies.GameState;

namespace OperaEtDies.Controls;

/// <summary>
/// Describes the player's appointments: when and where they are, and what they're for.
/// </summary>
public static class AppointmentText
{
	/// <summary>
	/// Describes when something is, such as "14:30 today", "14:30 tomorrow", or "14:30 on Wednesday".
	/// </summary>
	public static string FormatWhen(GameWorld world, TimePoint time)
	{
		ArgumentNullException.ThrowIfNull(world);
		var calendar = world.GameData.Calendar;
		var clock = calendar.FormatClock(time);
		var days = calendar.GetTotalMinutes(calendar.GetStartOfDay(time) - calendar.GetStartOfDay(world.Time)) / (24 * 60);
		return days switch
		{
			0 => OurResources.AppointmentToday.FormatCurrentCulture(clock),
			1 => OurResources.AppointmentTomorrow.FormatCurrentCulture(clock),
			_ => OurResources.AppointmentOnDay.FormatCurrentCulture(clock, calendar.GetDayOfWeek(time).Title),
		};
	}

	/// <summary>
	/// Describes where an appointment is: the home of the one it's with, such as "her home" (or with their name, if their
	/// gender has no possessive), the player's home, or the place's title, along with the quarter it's off, if it's a house
	/// or business, so the player knows where to go.
	/// </summary>
	public static string FormatPlace(GameWorld world, Appointment appointment)
	{
		ArgumentNullException.ThrowIfNull(world);
		ArgumentNullException.ThrowIfNull(appointment);
		return FormatPlace(world, appointment.Place, appointment.GetOther(world.Player));
	}

	/// <summary>
	/// Describes a place the player goes for someone, such as for an appointment or a job: their home, such as "her home" (or
	/// with their name, if their gender has no possessive), the player's home, or the place's title, along with the quarter
	/// it's off, if it's a house or business.
	/// </summary>
	public static string FormatPlace(GameWorld world, Location place, Person other)
	{
		ArgumentNullException.ThrowIfNull(world);
		ArgumentNullException.ThrowIfNull(place);
		ArgumentNullException.ThrowIfNull(other);
		return place == other.Home && GetPossessive(world, other) is { } possessive ?
			AddQuarter(place, OurResources.AppointmentPlaceHome.FormatCurrentCulture(possessive)) :
			AddQuarter(place, place == other.Home ? OurResources.AppointmentPlaceNamedHome.FormatCurrentCulture(other.FullName) :
				place == world.Player.Home ? OurResources.AppointmentPlacePlayerHome :
				place.Title);
	}

	/// <summary>
	/// Describes where an appointment is, naming the one it's with if it's their home, such as "Mary Smith's home", or the
	/// player's home, or the place's title, along with the quarter it's off, if it's a house or business.
	/// </summary>
	public static string FormatPlace(Appointment appointment, Person player)
	{
		ArgumentNullException.ThrowIfNull(appointment);
		ArgumentNullException.ThrowIfNull(player);
		var other = appointment.GetOther(player);
		return AddQuarter(appointment.Place, appointment.Place == other.Home ? OurResources.AppointmentPlaceNamedHome.FormatCurrentCulture(other.FullName) :
			appointment.Place == player.Home ? OurResources.AppointmentPlacePlayerHome :
			appointment.Place.Title);
	}

	/// <summary>
	/// Adds the quarter a house or business is off to the words for it, such as "her home, off Mill Lane". A quarter is left
	/// as it is.
	/// </summary>
	private static string AddQuarter(Location place, string text) =>
		place.Template is not QuarterTemplate && place.Quarter is { } quarter ? OurResources.AppointmentPlaceOffQuarter.FormatCurrentCulture(text, quarter.Title) : text;

	/// <summary>
	/// Describes what the player does at an appointment, such as "Attending an expectant mother".
	/// </summary>
	public static string FormatActivity(GameWorld world, Appointment appointment)
	{
		ArgumentNullException.ThrowIfNull(world);
		ArgumentNullException.ThrowIfNull(appointment);
		var (_, meet, _) = appointment.Need.GetAppointmentSteps(appointment.Step)!.Value;
		return appointment.Client == world.Player ? OurResources.AppointmentActivityClient.FormatCurrentCulture(appointment.Worker.FullName) :
			ActivityText.Fill(appointment.Need.GetActivity(meet + 1, world.Player.Business?.Profession.Id), appointment.GetOther(world.Player));
	}

	/// <summary>
	/// Gets what the player can say to the other of an appointment about starting it, from conversation: as its worker,
	/// asking whether they're ready to start it, or as its client, telling them they're ready now. That's the title to
	/// choose, and what the player says, which is the title without a text of its own.
	/// </summary>
	public static (string Title, string Text) GetStartLine(GameWorld world, Appointment appointment)
	{
		ArgumentNullException.ThrowIfNull(world);
		ArgumentNullException.ThrowIfNull(appointment);
		var player = world.Player;
		var isWorker = player == appointment.Worker;
		var line = GetLine(appointment, isWorker);
		var title = line?.Title ?? (isWorker ? OurResources.AppointmentStartTitle : OurResources.AppointmentReadyTitle);
		return (title, line?.Text is { } text ? ActivityText.FillOther(text.Text, appointment.GetOther(player)) : title);
	}

	/// <summary>
	/// Gets what the player is told when the other of an appointment says something to them about starting it: as its
	/// worker asking whether they're ready to start it, or as its client telling them they're ready now.
	/// </summary>
	public static string GetStartAsked(GameWorld world, Appointment appointment)
	{
		ArgumentNullException.ThrowIfNull(world);
		ArgumentNullException.ThrowIfNull(appointment);
		var player = world.Player;
		var isWorkerAsking = player == appointment.Client;
		var other = appointment.GetOther(player);
		return GetLine(appointment, isWorkerAsking)?.Asked is { } asked ? ActivityText.FillOther(asked.Text, other) :
			(isWorkerAsking ? OurResources.AppointmentStartAsked : OurResources.AppointmentReadyAsked).FormatCurrentCulture(other.FullName);
	}

	/// <summary>
	/// Gets what the data says the worker of an appointment says to ask whether to start it, or the client says to tell them
	/// they're ready, or null if it doesn't say.
	/// </summary>
	private static AppointmentLine? GetLine(Appointment appointment, bool isWorker)
	{
		var settings = appointment.Need.Steps[appointment.Step].Appointment;
		return isWorker ? settings?.Start : settings?.Ready;
	}

	/// <summary>
	/// Gets someone's possessive, such as "her", from the text of their gender, or null if it has none.
	/// </summary>
	private static string? GetPossessive(GameWorld world, Person person)
	{
		var gameData = world.GameData;
		var genderId = gameData.Families.Roles.Properties.Gender;
		var valueId = person.GetConditionValueId(genderId);
		return gameData.CharacterProperties.Find(genderId)?.Values?.FirstOrDefault(x => x.Id == valueId)?.Text.GetValueOrDefault(PossessiveText);
	}

	/// <summary>
	/// The name of the text of a gender value that's its possessive.
	/// </summary>
	private const string PossessiveText = "possessive";
}
