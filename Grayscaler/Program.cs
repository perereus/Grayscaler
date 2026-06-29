using System.Windows.Forms;

namespace Grayscaler
{
    internal static class Program
    {
        [System.STAThread]
        private static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.Run(new AppController());
        }
    }
}
