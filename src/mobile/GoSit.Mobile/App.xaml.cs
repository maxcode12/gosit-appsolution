namespace GoSit.Mobile;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		// Sized like a phone on desktop, where the window is resizable and would otherwise
		// open wide enough to make a one-hand layout look like a mistake.
		return new Window(new MainPage())
		{
			Title = "GoSit",
			Width = 420,
			Height = 880,
		};
	}
}
