using System.Globalization;
using System.Windows;

namespace PortraitEditor.Model;

/// <summary>
/// Describes the part of a path's markup at the caret: the command it's in, that command's numbers, and which number
/// the caret is on (or would be typed next). It reads the markup loosely, so it works on paths that are being typed and
/// can't be parsed yet.
/// </summary>
public static class PathHelp
{
	/// <summary>
	/// Describes the markup at the caret.
	/// </summary>
	public static PathHelpInfo Describe(string text, int caret)
	{
		caret = Math.Clamp(caret, 0, text.Length);
		var tokens = Tokenize(text);

		// The caret is on a token when it's inside it or just after it. When it's between two tokens that touch, the
		// later one wins, so the caret just after a command letter is on its first number.
		var token = tokens.FirstOrDefault(x => x.Start <= caret && caret < x.End) ?? tokens.LastOrDefault(x => x.End == caret);
		if (token is not null)
			return DescribeToken(token);

		// Between tokens, the caret is where the next number (or command) would be typed.
		var previous = tokens.LastOrDefault(x => x.End <= caret);
		return previous switch
		{
			null or { Kind: TokenKind.FillRule } => s_commandList,
			{ Kind: TokenKind.Command, Command: { } command } => DescribeNext(command, -1),
			{ Kind: TokenKind.Number, Command: { } command } => DescribeNext(command, previous.ArgumentIndex),
			_ => s_noCommand,
		};
	}

	/// <summary>
	/// The commands that can be inserted, with their names, in the order the insert menu lists them.
	/// </summary>
	public static IReadOnlyList<PathCommandOption> CommandOptions => s_commandOptions;

	/// <summary>
	/// Inserts a command into a path for the caret: after the command the caret is in, or at the caret when it's between
	/// commands (but never before the fill rule). The command is absolute, with numbers that draw a short segment from
	/// the point it starts at, and is set off from the rest of the path by spaces, except at the start and end of the
	/// path. Returns the new path and where the caret goes: on the command's first number.
	/// </summary>
	public static (string Text, int Caret) InsertCommand(string text, int caret, char command)
	{
		caret = Math.Clamp(caret, 0, text.Length);
		var tokens = Tokenize(text);
		var position = caret;

		// Each command runs from its letter to its last number. Numbers with no command run on their own.
		var spans = new List<(int Start, int End)>();
		var isAfterFillRule = false;
		foreach (var token in tokens)
		{
			if (token.Kind == TokenKind.Number && spans.Count != 0 && !isAfterFillRule)
				spans[^1] = (spans[^1].Start, token.End);
			else
				spans.Add((token.Start, token.End));
			isAfterFillRule = token.Kind == TokenKind.FillRule;
		}

		foreach (var (start, end) in spans)
		{
			if (start < caret && caret < end)
				position = end;
		}

		if (tokens.Count != 0 && tokens[0].Kind == TokenKind.FillRule)
			position = Math.Max(position, tokens[0].End);

		var current = SvgPath.TryParse(text[..position], out var path, out _) && path.Segments.Count != 0 ? path.Segments[^1].End : (Point?) null;
		var commandText = CreateCommandText(char.ToUpperInvariant(command), current);
		var spaceBefore = position > 0 && !char.IsWhiteSpace(text[position - 1]);
		var spaceAfter = position < text.Length && !char.IsWhiteSpace(text[position]);
		var inserted = $"{(spaceBefore ? " " : "")}{commandText}{(spaceAfter ? " " : "")}";
		var newCaret = position + (spaceBefore ? 1 : 0) + 1;
		return (text[..position] + inserted + text[position..], newCaret);
	}

	/// <summary>
	/// Writes an absolute command with numbers that draw a short segment to the right of the point it starts at (or
	/// for a move, a new figure below and to the right of it).
	/// </summary>
	private static string CreateCommandText(char command, Point? start)
	{
		var (x, y) = start is { } point ? (point.X, point.Y) : (0.0, 0.0);
		string F(double value) => Math.Round(value, SvgPath.Precision).ToString("0.#", CultureInfo.InvariantCulture);
		string P(double dx, double dy) => $"{F(x + dx)},{F(y + dy)}";
		return command switch
		{
			'M' => start is null ? "M200,240" : $"M{P(20, 20)}",
			'L' => $"L{P(20, 0)}",
			'H' => $"H{F(x + 20)}",
			'V' => $"V{F(y + 20)}",
			'C' => $"C{P(5, -10)} {P(15, -10)} {P(20, 0)}",
			'S' => $"S{P(15, 10)} {P(20, 0)}",
			'Q' => $"Q{P(10, -10)} {P(20, 0)}",
			'T' => $"T{P(20, 0)}",
			'A' => $"A10,10 0 0 1 {P(20, 0)}",
			_ => command.ToString(),
		};
	}

	private static PathHelpInfo DescribeToken(Token token)
	{
		if (token.Kind == TokenKind.FillRule)
			return s_fillRule;
		if (token.Command is not { } command)
			return token.Kind == TokenKind.Number ? s_noCommand : s_unknownCommand;
		if (!s_commands.TryGetValue(char.ToUpperInvariant(command), out var info))
			return s_unknownCommand;
		return token.Kind == TokenKind.Command ? Create(command, info, -1, null) : Create(command, info, token.ArgumentIndex, null);
	}

	/// <summary>
	/// Describes the number that would come after the argument at the index (-1 for the command letter itself).
	/// </summary>
	private static PathHelpInfo DescribeNext(char command, int argumentIndex)
	{
		if (!s_commands.TryGetValue(char.ToUpperInvariant(command), out var info))
			return s_unknownCommand;
		if (info.Parameters.Count == 0)
			return s_commandList;
		if (argumentIndex + 1 < info.Parameters.Count)
			return Create(command, info, argumentIndex + 1, null);

		// The command is complete, so more numbers repeat it (a move's are lines).
		var repeated = command switch { 'M' => 'L', 'm' => 'l', _ => command };
		return Create(repeated, s_commands[char.ToUpperInvariant(repeated)], 0, $"More numbers repeat the command ({repeated}), or type another command.");
	}

	private static PathHelpInfo Create(char command, CommandInfo info, int parameterIndex, string? note)
	{
		var isRelative = char.IsLower(command);
		var title = $"{command}  {info.Name}{(info.Parameters.Count == 0 ? "" : isRelative ? ", relative to the current point" : ", absolute")}";
		var parameters = info.Parameters.Select((x, index) => new PathHelpParameter(x.Name, index == parameterIndex)).ToList();
		var description = parameterIndex >= 0 && parameterIndex < info.Parameters.Count
			? $"{info.Parameters[parameterIndex].Name}: {info.Parameters[parameterIndex].Description}"
			: info.Description;
		return new PathHelpInfo(title, parameters, note is null ? description : $"{description} {note}");
	}

	private static IReadOnlyList<Token> Tokenize(string text)
	{
		var tokens = new List<Token>();
		var index = 0;
		char? command = null;
		var argumentIndex = -1;
		while (index < text.Length)
		{
			var c = text[index];
			if (char.IsWhiteSpace(c) || c == ',')
			{
				index++;
			}
			else if (tokens.Count == 0 && c == 'F' && index + 1 < text.Length && text[index + 1] is '0' or '1')
			{
				tokens.Add(new Token(TokenKind.FillRule, index, index + 2, null, -1));
				index += 2;
			}
			else if (char.IsLetter(c))
			{
				command = c;
				argumentIndex = -1;
				tokens.Add(new Token(TokenKind.Command, index, index + 1, command, -1));
				index++;
			}
			else
			{
				var end = ReadNumber(text, index);
				if (end == index)
				{
					// Anything else is skipped, so one stray character doesn't hide the rest.
					index++;
				}
				else if (command is { } current && s_commands.TryGetValue(char.ToUpperInvariant(current), out var info) && info.Parameters.Count != 0)
				{
					argumentIndex++;
					if (argumentIndex >= info.Parameters.Count)
					{
						// Numbers after a complete command repeat it, and the numbers after a move are lines.
						command = current switch { 'M' => 'L', 'm' => 'l', _ => current };
						argumentIndex = 0;
					}

					tokens.Add(new Token(TokenKind.Number, index, end, command, argumentIndex));
					index = end;
				}
				else
				{
					// A number with no command to take it (at the start, or after a close or an unknown command).
					tokens.Add(new Token(TokenKind.Number, index, end, null, -1));
					index = end;
				}
			}
		}

		return tokens;
	}

	/// <summary>
	/// Reads a number the way paths do, returning where it ends (where it started if there's no number there).
	/// </summary>
	private static int ReadNumber(string text, int start)
	{
		var index = start;
		if (index < text.Length && text[index] is '+' or '-')
			index++;
		var digitsStart = index;
		while (index < text.Length && char.IsAsciiDigit(text[index]))
			index++;
		var hasDigits = index > digitsStart;
		if (index < text.Length && text[index] == '.')
		{
			index++;
			var fractionStart = index;
			while (index < text.Length && char.IsAsciiDigit(text[index]))
				index++;
			hasDigits |= index > fractionStart;
		}

		if (!hasDigits)
			return text.Length > start && text[start] is '+' or '-' or '.' ? start + 1 : start;

		if (index < text.Length && text[index] is 'e' or 'E')
		{
			var exponent = index + 1;
			if (exponent < text.Length && text[exponent] is '+' or '-')
				exponent++;
			if (exponent < text.Length && char.IsAsciiDigit(text[exponent]))
			{
				index = exponent;
				while (index < text.Length && char.IsAsciiDigit(text[index]))
					index++;
			}
		}

		return index;
	}

	private enum TokenKind
	{
		FillRule,
		Command,
		Number,
	}

	/// <summary>
	/// A command letter, a number, or the fill rule at the start, with the command it belongs to and, for a number,
	/// which of the command's numbers it is.
	/// </summary>
	private sealed record Token(TokenKind Kind, int Start, int End, char? Command, int ArgumentIndex);

	private sealed record CommandInfo(string Name, string Description, IReadOnlyList<ParameterInfo> Parameters);

	private sealed record ParameterInfo(string Name, string Description);

	private static readonly Dictionary<char, CommandInfo> s_commands = new()
	{
		['M'] = new("Move", "Starts a new figure at a point, without drawing.",
		[
			new("x", "x of the point the figure starts at"),
			new("y", "y of the point the figure starts at"),
		]),
		['L'] = new("Line", "Draws a straight line to a point.",
		[
			new("x", "x of the end point"),
			new("y", "y of the end point"),
		]),
		['H'] = new("Horizontal line", "Draws a straight line across to an x, keeping the same y.",
		[
			new("x", "x of the end point"),
		]),
		['V'] = new("Vertical line", "Draws a straight line up or down to a y, keeping the same x.",
		[
			new("y", "y of the end point"),
		]),
		['C'] = new("Curve", "Draws a curve to a point, bent by two control points.",
		[
			new("x1", "x of the first control point, which the curve leaves the start toward"),
			new("y1", "y of the first control point, which the curve leaves the start toward"),
			new("x2", "x of the second control point, which the curve arrives at the end from"),
			new("y2", "y of the second control point, which the curve arrives at the end from"),
			new("x", "x of the end point"),
			new("y", "y of the end point"),
		]),
		['S'] = new("Smooth curve", "Draws a curve that carries on smoothly from the curve before it: its first control point is the previous curve's second one, mirrored.",
		[
			new("x2", "x of the second control point, which the curve arrives at the end from"),
			new("y2", "y of the second control point, which the curve arrives at the end from"),
			new("x", "x of the end point"),
			new("y", "y of the end point"),
		]),
		['Q'] = new("Quadratic curve", "Draws a curve to a point, bent by one control point.",
		[
			new("x1", "x of the control point, which the curve bends toward"),
			new("y1", "y of the control point, which the curve bends toward"),
			new("x", "x of the end point"),
			new("y", "y of the end point"),
		]),
		['T'] = new("Smooth quadratic curve", "Draws a quadratic curve that carries on smoothly from the one before it: its control point is the previous one, mirrored.",
		[
			new("x", "x of the end point"),
			new("y", "y of the end point"),
		]),
		['A'] = new("Arc", "Draws part of an ellipse to a point.",
		[
			new("rx", "the ellipse's horizontal radius (never relative)"),
			new("ry", "the ellipse's vertical radius (never relative)"),
			new("angle", "how many degrees the ellipse is turned, clockwise"),
			new("large", "1 to go the long way around the ellipse (more than half of it), 0 for the short way"),
			new("sweep", "1 to go clockwise, 0 to go counterclockwise"),
			new("x", "x of the end point"),
			new("y", "y of the end point"),
		]),
		['Z'] = new("Close", "Draws a straight line back to the start of the figure and closes it. It has no numbers.", []),
	};

	private static readonly IReadOnlyList<PathCommandOption> s_commandOptions =
		s_commands.Select(x => new PathCommandOption(x.Key.ToString(), $"{x.Key}   {x.Value.Name}")).ToList();

	private static readonly PathHelpInfo s_commandList = new(
		"Commands",
		[],
		"M move · L line · H horizontal line · V vertical line · C curve · S smooth curve · Q quadratic curve · T smooth quadratic curve · A arc · Z close. Upper case is absolute, lower case is relative to the current point.");

	private static readonly PathHelpInfo s_fillRule = new(
		"Fill rule",
		[],
		"F0 (the default): where the path crosses itself, the overlaps alternate between filled and empty. F1: everything inside the outline is filled, unless a figure goes the other way around.");

	private static readonly PathHelpInfo s_noCommand = new("Expected a command", [], "Numbers must come after a command. " + s_commandList.Description);

	private static readonly PathHelpInfo s_unknownCommand = new("Unknown command", [], s_commandList.Description);
}

/// <summary>
/// What the path help shows: a title (the command), the command's numbers with the one at the caret marked, and a
/// description of that number or the command.
/// </summary>
public sealed record PathHelpInfo(string Title, IReadOnlyList<PathHelpParameter> Parameters, string Description);

public sealed record PathHelpParameter(string Name, bool IsCurrent);

/// <summary>
/// A command the insert menu offers: its letter, and the menu's text for it.
/// </summary>
public sealed record PathCommandOption(string Command, string Title);
