using System;
using System.Threading;
using System.Windows.Forms;

namespace AzureaProxy
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            var mt = new Mutex(false, "23dtsh13m1hgd,alevytl", out bool createdNew);
            if (!createdNew)
                return;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}
