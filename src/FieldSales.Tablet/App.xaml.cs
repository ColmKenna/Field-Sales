namespace FieldSales.Tablet;

public partial class App : Application
{
    private readonly TabletApp tablet;

    public App(TabletApp tablet)
    {
        InitializeComponent();
        this.tablet = tablet;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        Window window = new(new MainPage()) { Title = "Field Sales" };
        // Reopening the app re-checks the 24-hour rule (I3.1); it never extends the window.
        window.Resumed += (_, _) => tablet.NotifyResumed();
        return window;
    }
}
