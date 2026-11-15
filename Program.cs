namespace NFSU1ProfileMaster;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Strings.Current = LanguageDialog.Ask();      // a ogni avvio
        Application.Run(new MainForm());
    }
}
