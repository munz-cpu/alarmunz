namespace ToolInertia;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var context = new TrayManager();
        Application.Run(context);
    }
}
