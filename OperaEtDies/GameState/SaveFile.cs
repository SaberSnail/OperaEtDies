using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using GoldenAnvil.Utility;
using OperaEtDies.Data;

namespace OperaEtDies.GameState;

/// <summary>
/// A saved game: what it was saved with, and the world objects. The player's name and the time it was saved are only
/// for showing the save; loading doesn't use them.
/// </summary>
public sealed record SaveFile(int Version, string GameTitle, string DataHash, IReadOnlyList<string> Mods, DateTimeOffset SavedAt, string PlayerName, int NextId, IReadOnlyList<WorldObjectRecord> Objects)
{
	/// <summary>
	/// The version of the save format this version of the game writes and reads. Increase it when old saves can no
	/// longer load.
	/// </summary>
	public const int CurrentVersion = 1;

	public const string Extension = ".json";

	/// <summary>
	/// The hash of the mod folders the game was saved with, or empty if it was saved without mods.
	/// </summary>
	public string ModsHash { get; init; } = "";

	/// <summary>
	/// Creates the save for a world, with what the game is being saved with.
	/// </summary>
	public static SaveFile Create(GameWorld world, SaveSettings settings, DateTimeOffset savedAt) =>
		new(CurrentVersion, settings.GameTitle, settings.DataHash, settings.Mods, savedAt, world.Player.FullName, world.Objects.NextId, world.Objects.All.Select(x => x.CreateRecord()).ToList())
		{
			ModsHash = settings.ModsHash,
		};

	/// <summary>
	/// Whether the data or mods have changed since the game was saved, going by their hashes.
	/// </summary>
	public bool IsDataChanged(SaveSettings settings) => DataHash != settings.DataHash || ModsHash != settings.ModsHash;

	/// <summary>
	/// The folder saves are kept in: <c>%LocalAppData%\Golden Anvil\&lt;game title&gt;\Saves</c>.
	/// </summary>
	public static string GetSaveDirectory(string gameTitle)
	{
		var invalid = Path.GetInvalidFileNameChars();
		var folderName = new string(gameTitle.Trim().Select(x => invalid.Contains(x) ? '_' : x).ToArray());
		return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Golden Anvil", folderName, "Saves");
	}

	/// <summary>
	/// Gets a file name for a new save, from the player's name and the time it was saved.
	/// </summary>
	public string GetFileName()
	{
		var invalid = Path.GetInvalidFileNameChars();
		var name = new string(PlayerName.Select(x => invalid.Contains(x) ? '_' : x).ToArray()).Trim();
		return $"{name} {SavedAt.ToLocalTime().ToString("yyyy-MM-dd HH-mm-ss", CultureInfo.InvariantCulture)}{Extension}";
	}

	public string Serialize() => JsonSerializer.Serialize(this, s_options);

	/// <summary>
	/// Writes the save to a file, creating its folder if needed.
	/// </summary>
	public void Write(string filePath)
	{
		if (Path.GetDirectoryName(filePath) is { Length: not 0 } directory)
			Directory.CreateDirectory(directory);
		File.WriteAllText(filePath, Serialize());
	}

	/// <summary>
	/// Reads a save from its JSON. The version and game title are checked when it's loaded, not here.
	/// </summary>
	public static SaveFile Deserialize(string json)
	{
		try
		{
			return JsonSerializer.Deserialize<SaveFile>(json, s_options) ?? throw new SaveLoadException(OurResources.SaveFileInvalid.FormatCurrentCulture("null"));
		}
		catch (JsonException e)
		{
			throw new SaveLoadException(OurResources.SaveFileInvalid.FormatCurrentCulture(e.Message), e);
		}
		catch (NotSupportedException e)
		{
			throw new SaveLoadException(OurResources.SaveFileInvalid.FormatCurrentCulture(e.Message), e);
		}
	}

	/// <summary>
	/// Reads a save from a file.
	/// </summary>
	public static SaveFile Read(string filePath)
	{
		string json;
		try
		{
			json = File.ReadAllText(filePath);
		}
		catch (IOException e)
		{
			throw new SaveLoadException(OurResources.SaveFileInvalid.FormatCurrentCulture(e.Message), e);
		}
		catch (UnauthorizedAccessException e)
		{
			throw new SaveLoadException(OurResources.SaveFileInvalid.FormatCurrentCulture(e.Message), e);
		}

		return Deserialize(json);
	}

	/// <summary>
	/// Creates the world from the save. Fails if the save is from another version of the save format or another game, or
	/// doesn't match the game data. Whether the data has changed since the save is checked separately, using
	/// <see cref="DataHash"/>.
	/// </summary>
	public GameWorld Load(GameData gameData, string gameTitle, Random random)
	{
		if (Version != CurrentVersion)
			throw new SaveLoadException(OurResources.SaveVersionUnsupported.FormatCurrentCulture(Version, CurrentVersion));
		if (!string.Equals(GameTitle, gameTitle, StringComparison.Ordinal))
			throw new SaveLoadException(OurResources.SaveGameMismatch.FormatCurrentCulture(GameTitle, gameTitle));

		return GameWorld.Load(gameData, NextId, Objects ?? [], random);
	}

	private static readonly JsonSerializerOptions s_options = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = true,
		Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
		RespectNullableAnnotations = true,
		RespectRequiredConstructorParameters = true,
	};
}
