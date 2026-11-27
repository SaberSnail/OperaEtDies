using GoldenAnvil.Utility;

namespace OperaEtDies.Data;

/// <summary>
/// Text the player is shown that can name the other person in what they're doing (the one they're asking or talking
/// with, or the one asking them): "{other}" is their name, and "{other.x}" is what a character placeholder "{x}" would
/// be for them, such as "{other.subject}" (a character template) or "{other.gender.pronoun}" (a property's text).
/// </summary>
public static class OtherText
{
	/// <summary>
	/// The id of the placeholder that names the other person.
	/// </summary>
	public const string OtherId = "other";

	/// <summary>
	/// Checks a text that can name the other person: its placeholders are well formed, all name the other person, use a
	/// known format, and name a character template or property (with text that every value has).
	/// </summary>
	internal static IEnumerable<string> Validate(string path, string text, IReadOnlyDictionary<string, CharacterProperty> properties, IReadOnlySet<string> templateIds)
	{
		if (!TextTemplate.TryParse(text, out var template))
		{
			yield return OurResources.TemplateMalformed.FormatCurrentCulture(path);
			yield break;
		}

		foreach (var placeholder in template.Placeholders)
		{
			if (placeholder.Id != OtherId)
				yield return OurResources.OtherTextPlaceholderUnknown.FormatCurrentCulture(path, placeholder.Id);
			else if (placeholder.Format is { } format && !TextTemplate.IsKnownFormat(format))
				yield return OurResources.TemplateFormatUnknown.FormatCurrentCulture(path, format);
			else if (placeholder.Field is { } field)
			{
				// The field is what a character placeholder would be, such as "gender.pronoun" for "{gender.pronoun}".
				if (!TextTemplate.TryParse($"{{{field}}}", out var inner))
					yield return OurResources.TemplateMalformed.FormatCurrentCulture(path);
				else
				{
					foreach (var error in CharacterData.ValidatePlaceholders(path, inner, null, properties, templateIds, allowsClothing: false))
						yield return error;
				}
			}
		}
	}
}
