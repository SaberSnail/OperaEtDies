using System.ComponentModel;
using System.Windows.Input;
using GoldenAnvil.Utility;
using OperaEtDies.Controls.Game;

namespace OperaEtDies.Controls.MainMenu;

/// <summary>
/// Asks whether to save the game before leaving it (quitting, loading another game, or exiting), since anything since
/// the last save would be lost. Saving can't start until time stops, and if it fails, the prompt stays with the reason.
/// </summary>
public sealed class SavePromptViewModel : ViewModelBase, IDisposable
{
	public SavePromptViewModel(GameScreenViewModel game)
	{
		m_game = game;
		m_game.TimeRunner.PropertyChanged += OnTimeRunnerPropertyChanged;
	}

	/// <summary>
	/// Raised when the player has answered: true to go on (saved or not), false to stay in the game.
	/// </summary>
	public event EventHandler<GenericEventArgs<bool>>? Answered;

	public string Question { get; } = OurResources.SavePromptQuestion;

	public string Details { get; } = OurResources.SavePromptDetails;

	/// <summary>
	/// Why the last try at saving failed, or null.
	/// </summary>
	public string? Error
	{
		get => VerifyAccess(m_error);
		private set => SetPropertyField(value, ref m_error);
	}

	public bool CanSave() => m_game.CanSaveGame();

	/// <summary>
	/// Saves the game, then goes on, unless saving failed.
	/// </summary>
	public void Save()
	{
		VerifyAccess();
		if (CanSave())
		{
			if (m_game.TrySaveGame())
				Answer(true);
			else
				Error = m_game.SaveStatus;
		}
	}

	/// <summary>
	/// Goes on without saving.
	/// </summary>
	public void DontSave()
	{
		VerifyAccess();
		Answer(true);
	}

	/// <summary>
	/// Stays in the game.
	/// </summary>
	public void Cancel()
	{
		VerifyAccess();
		Answer(false);
	}

	public void Dispose() => m_game.TimeRunner.PropertyChanged -= OnTimeRunnerPropertyChanged;

	private void Answer(bool isGoingOn) => Answered.Raise(this, new GenericEventArgs<bool>(isGoingOn));

	// Commands only check whether they can run after input, so make sure Save sees time stop.
	private void OnTimeRunnerPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(TimeRunner.IsRunning))
			CommandManager.InvalidateRequerySuggested();
	}

	private readonly GameScreenViewModel m_game;
	private string? m_error;
}
