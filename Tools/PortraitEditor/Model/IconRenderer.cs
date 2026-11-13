using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OperaEtDies.Characters;
using OperaEtDies.Data;
using PortraitEditor.ViewModels;

namespace PortraitEditor.Model;

/// <summary>
/// Draws the editor's icon from the portrait data: a close-up of one character's face, cropped around the head layer,
/// at each of the sizes Windows uses.
/// </summary>
public static class IconRenderer
{
	/// <summary>
	/// The sizes of the images in the icon, in pixels.
	/// </summary>
	public static IReadOnlyList<int> Sizes { get; } = [16, 20, 24, 32, 40, 48, 64, 128, 256];

	/// <summary>
	/// Draws the icon from the data in the folder (without mods) and saves it, and each size as a PNG file in the
	/// preview folder, if given. Returns the problems, which are empty if it worked.
	/// </summary>
	public static IReadOnlyList<string> Save(string dataDirectory, string iconPath, string? previewDirectory)
	{
		var data = EditorData.Load(dataDirectory, areModsEnabled: false);
		var portraits = data.Portraits is { } json ? JsonDataLoader.Deserialize<PortraitData>(data.PortraitsFilePath, json, null) : null;
		var errors = data.Errors.Select(x => x.ToString()).Concat(portraits?.Errors.Select(x => x.ToString()) ?? []).ToList();
		if (data.GameData is { } gameData && portraits?.Value is { } portraitData)
		{
			var renderer = new PortraitRenderer(portraitData, gameData.CharacterProperties);
			var traits = new PreviewCharacterViewModel(gameData, new Random(1), new PreviewSettings(s_character, "none")).GetDrawnTraits();
			var crop = GetFaceCrop(renderer, portraitData, traits);
			if (crop is { } faceCrop)
			{
				var portrait = renderer.Render(traits, null);
				var images = Sizes.Select(x => (Size: x, Png: RenderPng(portrait, faceCrop, x))).ToList();
				try
				{
					File.WriteAllBytes(iconPath, CreateIcon(images));
					if (previewDirectory is not null)
					{
						Directory.CreateDirectory(previewDirectory);
						foreach (var (size, png) in images)
							File.WriteAllBytes(Path.Combine(previewDirectory, $"Icon{size}.png"), png);
					}
				}
				catch (Exception e) when (e is IOException or UnauthorizedAccessException)
				{
					errors.Add($"Couldn't save the icon: {e.Message}");
				}
			}
			else
			{
				errors.Add($"The portrait has no \"{HeadLayerId}\" layer to crop the icon around.");
			}
		}
		else if (errors.Count == 0)
		{
			errors.Add("The data has no portraits.");
		}

		return errors;
	}

	/// <summary>
	/// Gets the square around the character's face: the drawn shapes of the head layer, with some room around them for
	/// the hair.
	/// </summary>
	private static Rect? GetFaceCrop(PortraitRenderer renderer, PortraitData data, CharacterTraits traits)
	{
		var bounds = Rect.Empty;
		foreach (var placement in renderer.GetShapePlacements(traits, null).Where(x => x.IsDrawn && data.Layers[x.LayerIndex].Id == HeadLayerId))
		{
			var geometry = placement.Geometry.Clone();
			geometry.Transform = new MatrixTransform(placement.Transform);
			bounds.Union(geometry.Bounds);
		}

		Rect? crop = null;
		if (!bounds.IsEmpty)
		{
			var side = Math.Max(bounds.Width, bounds.Height) * 1.3;
			var center = new Point(bounds.Left + (bounds.Width / 2), bounds.Top + (bounds.Height / 2));
			crop = new Rect(center.X - (side / 2), center.Y - (side / 2), side, side);
		}

		return crop;
	}

	/// <summary>
	/// Draws the face in a rounded square with a thin frame, as a PNG file.
	/// </summary>
	private static byte[] RenderPng(ImageSource portrait, Rect crop, int size)
	{
		var scale = size / crop.Width;
		var tile = new Rect(0, 0, size, size);
		var radius = size * 0.14;
		var frameWidth = Math.Max(1, size / 32.0);

		var visual = new DrawingVisual();
		using (var context = visual.RenderOpen())
		{
			context.PushClip(new RectangleGeometry(tile, radius, radius));
			context.PushTransform(new MatrixTransform(scale, 0, 0, scale, -crop.Left * scale, -crop.Top * scale));
			context.DrawImage(portrait, new Rect(0, 0, portrait.Width, portrait.Height));
			context.Pop();
			context.Pop();
			var frameRect = tile;
			frameRect.Inflate(-frameWidth / 2, -frameWidth / 2);
			context.DrawRoundedRectangle(null, new Pen(s_frameBrush, frameWidth), frameRect, radius - (frameWidth / 2), radius - (frameWidth / 2));
		}

		var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
		bitmap.Render(visual);
		var encoder = new PngBitmapEncoder();
		encoder.Frames.Add(BitmapFrame.Create(bitmap));
		using var stream = new MemoryStream();
		encoder.Save(stream);
		return stream.ToArray();
	}

	/// <summary>
	/// Writes an icon file holding the PNG images.
	/// </summary>
	private static byte[] CreateIcon(IReadOnlyList<(int Size, byte[] Png)> images)
	{
		using var stream = new MemoryStream();
		using (var writer = new BinaryWriter(stream))
		{
			writer.Write((short) 0);
			writer.Write((short) 1);
			writer.Write((short) images.Count);

			var offset = 6 + (16 * images.Count);
			foreach (var (size, png) in images)
			{
				// A size of 256 is written as 0.
				writer.Write((byte) (size % 256));
				writer.Write((byte) (size % 256));
				writer.Write((byte) 0);
				writer.Write((byte) 0);
				writer.Write((short) 1);
				writer.Write((short) 32);
				writer.Write(png.Length);
				writer.Write(offset);
				offset += png.Length;
			}

			foreach (var (_, png) in images)
				writer.Write(png);
		}

		return stream.ToArray();
	}

	private const string HeadLayerId = "head";

	/// <summary>
	/// The character in the icon, by property id and value id. Anything not given is chosen at random, the same way
	/// each time.
	/// </summary>
	private static readonly Dictionary<string, string> s_character = new(StringComparer.Ordinal)
	{
		["gender"] = "gender_female",
		["age"] = "26",
		["build"] = "build_slender",
		["skinTone"] = "skinTone_fair",
		["complexion"] = "complexion_freckled",
		["hairColor"] = "hairColor_red",
		["hairLength"] = "hairLength_shoulderLength",
		["hairStyle"] = "hairStyle_wavy",
		["eyeColor"] = "eyeColor_green",
		["distinguishingFeature"] = "distinguishingFeature_none",
	};

	private static readonly Brush s_frameBrush = CreateFrameBrush();

	private static SolidColorBrush CreateFrameBrush()
	{
		var brush = new SolidColorBrush(Color.FromRgb(0x4A, 0x34, 0x24));
		brush.Freeze();
		return brush;
	}
}
