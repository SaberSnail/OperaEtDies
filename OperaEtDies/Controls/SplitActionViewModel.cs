using GoldenAnvil.Utility;

namespace OperaEtDies.Controls;

/// <summary>
/// An action with a reasonable default and other ways of doing it, shown as a split button: the main part does the
/// default, such as "Wait", and a chevron opens a menu of the others, such as "Wait an hour". The menu is only offered
/// when there are other ways, and for an action whose text changes (such as the step the player is on), only while one
/// of them can be chosen now, so it doesn't come and go with a text that's already changing. While the default can't be
/// done, the menu can't be opened either, since the other ways are never available when the obvious one isn't.
/// </summary>
public sealed class SplitActionViewModel : ViewModelBase
{
	/// <summary>
	/// Creates the action, which does the given default while it can be done, with the other ways to choose from.
	/// </summary>
	public SplitActionViewModel(string text, Action doAction, Func<bool>? canDo = null, IReadOnlyList<MenuActionViewModel>? alternatives = null, string? toolTip = null,
		bool isTextStable = true)
	{
		Text = text;
		ToolTip = toolTip;
		Alternatives = alternatives ?? [];
		m_doAction = doAction;
		m_canDo = canDo ?? (() => true);
		m_isTextStable = isTextStable;
		for (var index = 0; index < Alternatives.Count; index++)
			Alternatives[index].Badge = Hotkeys.GetChoiceKeyText(index + 1);
	}

	/// <summary>
	/// Creates an action for a menu button (see <see cref="MenuButton"/>), such as "Go to", whose default opens the menu of
	/// the given choices.
	/// </summary>
	public static SplitActionViewModel CreateMenu(string text, IReadOnlyList<MenuActionViewModel> choices, string? toolTip = null)
	{
		SplitActionViewModel? menu = null;
		menu = new SplitActionViewModel(text, () => menu!.OpenMenu(), alternatives: choices, toolTip: toolTip);
		return menu;
	}

	/// <summary>
	/// What the default does, such as "Wait" or "Work until 15:00 (1 hour)".
	/// </summary>
	public string Text { get; }

	/// <summary>
	/// The main part's tooltip, such as why it can't be done now, or null for none.
	/// </summary>
	public string? ToolTip { get; }

	/// <summary>
	/// The other ways, in the order they're listed in the menu, each numbered from 1 for the keys that choose them while
	/// it's open.
	/// </summary>
	public IReadOnlyList<MenuActionViewModel> Alternatives { get; }

	/// <summary>
	/// The key that does the default, such as "1", shown on the main part, or null if it has none. Shift with it opens the
	/// menu.
	/// </summary>
	public string? Badge
	{
		get => VerifyAccess(m_badge);
		set
		{
			if (SetPropertyField(value, ref m_badge))
				RaisePropertyChanged(nameof(MenuToolTip));
		}
	}

	/// <summary>
	/// Whether the chevron is shown: there are other ways, and either the text doesn't change or one of them can be chosen
	/// now.
	/// </summary>
	public bool HasMenu => Alternatives.Count != 0 && (m_isTextStable || Alternatives.Any(x => x.CanDo()));

	/// <summary>
	/// The chevron's tooltip, with the key that opens the menu, if the main part has a number.
	/// </summary>
	public string MenuToolTip => Badge is { } badge ? OurResources.SplitButtonMenuToolTipWithKey.FormatCurrentCulture(Hotkeys.FormatShiftChoiceKey(badge)) : OurResources.SplitButtonMenuToolTip;

	/// <summary>
	/// Whether the menu of the other ways is open.
	/// </summary>
	public bool IsMenuOpen
	{
		get => VerifyAccess(m_isMenuOpen);
		set => SetPropertyField(value && HasMenu && CanDo(), ref m_isMenuOpen);
	}

	public bool CanDo() => m_canDo();

	/// <summary>
	/// Does the default, if it can be done now.
	/// </summary>
	public void Do()
	{
		VerifyAccess();
		IsMenuOpen = false;
		if (CanDo())
			m_doAction();
	}

	/// <summary>
	/// Opens the menu of the other ways, if there is one and the default can be done. Returns whether it opened.
	/// </summary>
	public bool OpenMenu()
	{
		VerifyAccess();
		IsMenuOpen = true;
		return IsMenuOpen;
	}

	/// <summary>
	/// Chooses the other way with the given number in the menu, from 1, if there is one and it can be chosen now, closing
	/// the menu. Returns whether it was chosen.
	/// </summary>
	public bool ChooseAlternative(int number)
	{
		VerifyAccess();
		var isChosen = HasMenu && CanDo() && number >= 1 && number <= Alternatives.Count && Alternatives[number - 1].CanDo();
		if (isChosen)
		{
			IsMenuOpen = false;
			Alternatives[number - 1].Do();
		}

		return isChosen;
	}

	private readonly Action m_doAction;
	private readonly Func<bool> m_canDo;
	private readonly bool m_isTextStable;
	private string? m_badge;
	private bool m_isMenuOpen;
}

/// <summary>
/// One of the other ways of a split button, such as "Wait an hour", in its menu. It can only be chosen while it's
/// enabled, and its tooltip may say why not.
/// </summary>
public sealed class MenuActionViewModel : ViewModelBase
{
	public MenuActionViewModel(string text, Action doAction, Func<bool>? canDo = null, string? toolTip = null)
	{
		Text = text;
		ToolTip = toolTip;
		m_doAction = doAction;
		m_canDo = canDo ?? (() => true);
	}

	public string Text { get; }

	/// <summary>
	/// More about it, such as why it can't be chosen now, or null.
	/// </summary>
	public string? ToolTip { get; }

	/// <summary>
	/// The key that chooses it while the menu is open, such as "1", or null if it has none.
	/// </summary>
	public string? Badge { get; internal set; }

	public bool CanDo() => m_canDo();

	public void Do()
	{
		VerifyAccess();
		if (CanDo())
			m_doAction();
	}

	private readonly Action m_doAction;
	private readonly Func<bool> m_canDo;
}
