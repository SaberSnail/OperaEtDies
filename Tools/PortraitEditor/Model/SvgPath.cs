using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Windows;

namespace PortraitEditor.Model;

/// <summary>
/// Path markup (the same as SVG path data, with WPF's optional F0 or F1 fill rule at the start), parsed so its points
/// can be moved. Every point is kept in absolute coordinates, and only the segments whose numbers change are written
/// again, so the rest of the markup keeps its original text, including whether each segment is relative.
/// </summary>
public sealed class SvgPath
{
	/// <summary>
	/// The number of decimal places points are rounded to when they're moved.
	/// </summary>
	public const int Precision = 1;

	public static bool TryParse(string text, [NotNullWhen(true)] out SvgPath? path, out string error)
	{
		path = null;
		var parser = new Parser(text);
		var isValid = parser.TryParse(out var segments, out var prefixLength, out var trailing, out error);
		if (isValid)
			path = new SvgPath(text[..prefixLength], segments, trailing);
		return isValid;
	}

	public IReadOnlyList<SvgPathSegment> Segments => m_segments;

	/// <summary>
	/// Gets the points that can be moved: the end of each segment, and the control points of curves.
	/// </summary>
	public IReadOnlyList<SvgPathPoint> GetPoints()
	{
		var points = new List<SvgPathPoint>();
		for (var index = 0; index < m_segments.Count; index++)
		{
			var segment = m_segments[index];
			var end = segment.End;
			switch (segment.Kind)
			{
			case 'C':
				points.Add(new SvgPathPoint(index, SvgPointRole.Control1, segment.Points[0], [segment.Start]));
				points.Add(new SvgPathPoint(index, SvgPointRole.Control2, segment.Points[1], [end]));
				break;
			case 'S':
				points.Add(new SvgPathPoint(index, SvgPointRole.Control2, segment.Points[0], [end]));
				break;
			case 'Q':
				points.Add(new SvgPathPoint(index, SvgPointRole.Control1, segment.Points[0], [segment.Start, end]));
				break;
			}

			if (segment.Kind != 'Z')
				points.Add(new SvgPathPoint(index, SvgPointRole.End, end, []));
		}

		return points;
	}

	/// <summary>
	/// Moves a point to a position. Moving the end of a segment also moves the control points attached to it, and
	/// the start of a closed figure and the end of its last segment move together if they were at the same place.
	/// </summary>
	public void MovePoint(SvgPathPoint point, Point position)
	{
		position = Round(position);
		var segment = m_segments[point.SegmentIndex];
		if (point.Role != SvgPointRole.End)
		{
			segment.Points[GetPointIndex(segment, point.Role)] = position;
		}
		else
		{
			var oldEnd = segment.End;
			var delta = position - oldEnd;
			if (segment.Kind == 'H')
				delta.Y = 0;
			else if (segment.Kind == 'V')
				delta.X = 0;

			// The start of a closed figure and the end of its last segment move together when they're at the same place.
			var moved = new List<int> { point.SegmentIndex };
			if (FindJoinedSegment(point.SegmentIndex) is { } joined && AreSame(m_segments[joined].End, oldEnd))
				moved.Add(joined);

			foreach (var index in moved)
			{
				MoveEnd(m_segments[index], delta);
				if (index + 1 < m_segments.Count && m_segments[index + 1] is { Kind: 'C' } next)
					next.Points[0] = Round(next.Points[0] + delta);
			}
		}

		UpdateStarts();
	}

	/// <summary>
	/// Stretches every point by the scale around the origin, then moves it by the offset. The scale must be positive.
	/// </summary>
	public void Transform(Point origin, double scaleX, double scaleY, Vector offset)
	{
		double MapX(double x) => Math.Round(origin.X + ((x - origin.X) * scaleX) + offset.X, Precision);
		double MapY(double y) => Math.Round(origin.Y + ((y - origin.Y) * scaleY) + offset.Y, Precision);

		foreach (var segment in m_segments)
		{
			for (var index = 0; index < segment.Points.Length; index++)
				segment.Points[index] = new Point(MapX(segment.Points[index].X), MapY(segment.Points[index].Y));
			if (segment.Kind == 'A')
			{
				segment.Arc[0] = Math.Round(segment.Arc[0] * scaleX, Precision);
				segment.Arc[1] = Math.Round(segment.Arc[1] * scaleY, Precision);
			}
		}

		UpdateStarts();
	}

	public override string ToString()
	{
		var text = new StringBuilder(m_prefix);
		foreach (var segment in m_segments)
			text.Append(segment.GetText());
		return text.Append(m_trailing).ToString();
	}

	private SvgPath(string prefix, IReadOnlyList<SvgPathSegment> segments, string trailing)
	{
		m_prefix = prefix;
		m_segments = segments;
		m_trailing = trailing;
	}

	private static void MoveEnd(SvgPathSegment segment, Vector delta)
	{
		var endIndex = segment.Points.Length - 1;
		segment.Points[endIndex] = Round(segment.Points[endIndex] + delta);
		if (segment.Kind is 'C' or 'S')
			segment.Points[endIndex - 1] = Round(segment.Points[endIndex - 1] + delta);
	}

	/// <summary>
	/// Gets the last drawing segment of the figure started by the move at the index, if the figure is closed.
	/// </summary>
	private int? FindClosingSegment(int moveIndex)
	{
		for (var index = moveIndex + 1; index < m_segments.Count; index++)
		{
			var kind = m_segments[index].Kind;
			if (kind == 'M')
				return null;
			if (kind == 'Z')
				return index - 1 > moveIndex ? index - 1 : null;
		}

		return null;
	}

	/// <summary>
	/// Gets the segment whose end joins the end of the segment at the index in a closed figure: the last segment of the
	/// figure for its start, or the start of the figure for its last segment.
	/// </summary>
	private int? FindJoinedSegment(int segmentIndex)
	{
		if (m_segments[segmentIndex].Kind == 'M')
			return FindClosingSegment(segmentIndex);

		var isLast = segmentIndex + 1 < m_segments.Count && m_segments[segmentIndex + 1].Kind == 'Z';
		var start = segmentIndex - 1;
		while (isLast && start >= 0 && m_segments[start].Kind is not ('M' or 'Z'))
			start--;
		return isLast && start >= 0 && m_segments[start].Kind == 'M' ? start : null;
	}

	private void UpdateStarts()
	{
		var current = new Point();
		var figureStart = new Point();
		foreach (var segment in m_segments)
		{
			segment.Start = current;
			segment.FigureStart = figureStart;
			if (segment.Kind == 'M')
				figureStart = segment.End;
			current = segment.Kind == 'Z' ? figureStart : segment.End;
		}
	}

	private static int GetPointIndex(SvgPathSegment segment, SvgPointRole role) => (segment.Kind, role) switch
	{
		(_, SvgPointRole.End) => segment.Points.Length - 1,
		('S', SvgPointRole.Control2) => 0,
		(_, SvgPointRole.Control1) => 0,
		_ => 1,
	};

	private static bool AreSame(Point a, Point b) => Math.Abs(a.X - b.X) < 1e-6 && Math.Abs(a.Y - b.Y) < 1e-6;

	private static Point Round(Point point) => new(Math.Round(point.X, Precision), Math.Round(point.Y, Precision));

	private sealed class Parser(string text)
	{
		public bool TryParse(out IReadOnlyList<SvgPathSegment> segments, out int prefixLength, out string trailing, out string error)
		{
			var list = new List<SvgPathSegment>();
			segments = list;
			trailing = "";
			error = "";

			SkipSeparators();
			if (m_index + 1 < text.Length && text[m_index] == 'F' && text[m_index + 1] is '0' or '1')
				m_index += 2;
			prefixLength = m_index;

			var current = new Point();
			var figureStart = new Point();
			char? lastCommand = null;
			while (true)
			{
				var leadingStart = m_index;
				SkipSeparators();
				if (m_index >= text.Length)
				{
					trailing = text[leadingStart..];
					return true;
				}

				var segmentStart = m_index;
				char command;
				var hasCommand = char.IsLetter(text[m_index]);
				if (hasCommand)
				{
					command = text[m_index];
					m_index++;
				}
				else if (lastCommand is { } last && char.ToUpperInvariant(last) != 'Z')
				{
					// Numbers after a move are lines.
					command = last switch { 'M' => 'L', 'm' => 'l', _ => last };
				}
				else
				{
					error = $"Expected a command at position {m_index}.";
					return false;
				}

				var kind = char.ToUpperInvariant(command);
				var argumentCount = kind switch
				{
					'M' or 'L' or 'T' => 2,
					'H' or 'V' => 1,
					'C' => 6,
					'S' or 'Q' => 4,
					'A' => 7,
					'Z' => 0,
					_ => -1,
				};
				if (argumentCount < 0)
				{
					error = $"Unknown command '{command}' at position {segmentStart}.";
					return false;
				}

				var arguments = new double[argumentCount];
				for (var index = 0; index < argumentCount; index++)
				{
					SkipSeparators();
					if (!TryReadNumber(out arguments[index]))
					{
						error = $"Expected a number at position {m_index}.";
						return false;
					}
				}

				var segment = new SvgPathSegment(kind, char.IsLower(command), hasCommand, text[leadingStart..segmentStart], text[segmentStart..m_index], arguments)
				{
					Start = current,
					FigureStart = figureStart,
				};
				segment.SetArguments(arguments);
				list.Add(segment);

				if (kind == 'M')
					figureStart = segment.End;
				current = kind == 'Z' ? figureStart : segment.End;
				lastCommand = command;
			}
		}

		private void SkipSeparators()
		{
			while (m_index < text.Length && (char.IsWhiteSpace(text[m_index]) || text[m_index] == ','))
				m_index++;
		}

		private bool TryReadNumber(out double value)
		{
			var start = m_index;
			if (m_index < text.Length && text[m_index] is '+' or '-')
				m_index++;
			var hasDigits = SkipDigits();
			if (m_index < text.Length && text[m_index] == '.')
			{
				m_index++;
				hasDigits |= SkipDigits();
			}

			if (hasDigits && m_index < text.Length && text[m_index] is 'e' or 'E')
			{
				var exponent = m_index + 1;
				if (exponent < text.Length && text[exponent] is '+' or '-')
					exponent++;
				if (exponent < text.Length && char.IsAsciiDigit(text[exponent]))
				{
					m_index = exponent;
					SkipDigits();
				}
			}

			value = 0;
			if (!hasDigits)
			{
				m_index = start;
				return false;
			}

			return double.TryParse(text.AsSpan(start, m_index - start), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
		}

		private bool SkipDigits()
		{
			var start = m_index;
			while (m_index < text.Length && char.IsAsciiDigit(text[m_index]))
				m_index++;
			return m_index > start;
		}

		private int m_index;
	}

	private readonly string m_prefix;
	private readonly IReadOnlyList<SvgPathSegment> m_segments;
	private readonly string m_trailing;
}

/// <summary>
/// One segment of a path: a command and its numbers, with its points in absolute coordinates.
/// </summary>
public sealed class SvgPathSegment
{
	internal SvgPathSegment(char kind, bool isRelative, bool hasCommand, string leading, string text, double[] arguments)
	{
		Kind = kind;
		IsRelative = isRelative;
		m_hasCommand = hasCommand;
		m_leading = leading;
		m_text = text;
		m_arguments = arguments;
		Points = new Point[kind switch { 'C' => 3, 'S' or 'Q' => 2, 'Z' => 0, _ => 1 }];
		Arc = new double[kind == 'A' ? 5 : 0];
	}

	/// <summary>
	/// The command, in upper case: M, L, H, V, C, S, Q, T, A, or Z.
	/// </summary>
	public char Kind { get; }

	public bool IsRelative { get; }

	/// <summary>
	/// Where the segment starts: the end of the segment before it.
	/// </summary>
	public Point Start { get; internal set; }

	/// <summary>
	/// The start of the figure the segment is in, which a Z segment ends at.
	/// </summary>
	public Point FigureStart { get; internal set; }

	/// <summary>
	/// The points of the segment, in absolute coordinates, ending with its end. A horizontal line only uses the x of
	/// its point, and a vertical line only the y.
	/// </summary>
	internal readonly Point[] Points;

	/// <summary>
	/// The radii, rotation, large arc flag, and sweep flag of an arc.
	/// </summary>
	internal readonly double[] Arc;

	public Point End => Kind switch
	{
		'Z' => FigureStart,
		'H' => new Point(Points[0].X, Start.Y),
		'V' => new Point(Start.X, Points[0].Y),
		_ => Points[^1],
	};

	internal void SetArguments(double[] arguments)
	{
		var origin = IsRelative ? (Vector) Start : default;
		Point ReadPoint(int index) => new Point(arguments[index], arguments[index + 1]) + origin;

		switch (Kind)
		{
		case 'H':
			Points[0] = new Point(arguments[0] + origin.X, 0);
			break;
		case 'V':
			Points[0] = new Point(0, arguments[0] + origin.Y);
			break;
		case 'A':
			Array.Copy(arguments, Arc, 5);
			Points[0] = ReadPoint(5);
			break;
		default:
			for (var index = 0; index < Points.Length; index++)
				Points[index] = ReadPoint(index * 2);
			break;
		}
	}

	/// <summary>
	/// Gets the segment's markup: the original text if its numbers haven't changed, or else new text.
	/// </summary>
	internal string GetText()
	{
		var arguments = GetArguments();
		var isChanged = arguments.Where((x, index) => Math.Abs(x - m_arguments[index]) > 1e-7).Any();
		if (!isChanged)
			return m_leading + m_text;

		var text = new StringBuilder();
		if (m_hasCommand)
			text.Append(IsRelative ? char.ToLowerInvariant(Kind) : Kind);
		else
			text.Append(m_leading.Length == 0 ? " " : "");

		string[] parts = Kind switch
		{
			'H' or 'V' => [Format(arguments[0])],
			'A' => [$"{Format(arguments[0])},{Format(arguments[1])}", Format(arguments[2]), Format(arguments[3]), Format(arguments[4]), $"{Format(arguments[5])},{Format(arguments[6])}"],
			_ => Enumerable.Range(0, arguments.Length / 2).Select(x => $"{Format(arguments[x * 2])},{Format(arguments[(x * 2) + 1])}").ToArray(),
		};
		text.AppendJoin(' ', parts);
		return (m_hasCommand || m_leading.Length != 0 ? m_leading : "") + text;
	}

	private double[] GetArguments()
	{
		var origin = IsRelative ? (Vector) Start : default;
		return Kind switch
		{
			'Z' => [],
			'H' => [Points[0].X - origin.X],
			'V' => [Points[0].Y - origin.Y],
			'A' => [.. Arc, Points[0].X - origin.X, Points[0].Y - origin.Y],
			_ => Points.SelectMany(x => new[] { x.X - origin.X, x.Y - origin.Y }).ToArray(),
		};
	}

	private static string Format(double value)
	{
		var text = Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture);
		return text == "-0" ? "0" : text;
	}

	private readonly bool m_hasCommand;
	private readonly string m_leading;
	private readonly string m_text;
	private readonly double[] m_arguments;
}

/// <summary>
/// The kinds of point in a path segment that can be moved.
/// </summary>
public enum SvgPointRole
{
	End,
	Control1,
	Control2,
}

/// <summary>
/// A point of a path that can be moved, with the points its handle is drawn to (for control points).
/// </summary>
public sealed record SvgPathPoint(int SegmentIndex, SvgPointRole Role, Point Position, IReadOnlyList<Point> Anchors);
