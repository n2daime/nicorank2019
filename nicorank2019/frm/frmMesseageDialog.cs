using nicorankLib.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static nicorank2019.Program;

namespace nicorank2019.frm
{
    public partial class frmMesseageDialog : Form
    {
        const int WM_GETTEXTLENGTH = 0x000E;
        const int EM_SETSEL = 0x00B1;
        const int EM_REPLACESEL = 0x00C2;

        [DllImport("User32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int uMsg, int wParam, string lParam);

        [DllImport("User32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int uMsg, int wParam, int lParam);

        public delegate void RunFunction();

        RunFunction runFunction;

        public frmMesseageDialog(RunFunction runFunction)
        {
            this.runFunction = runFunction;
            InitializeComponent();
        }

        private void frmMesseageDialog_FormClosing(object sender, FormClosingEventArgs e)
        {
            StatusLog.SetLogWriter(new ConsolWriter());
        }


        private void frmMesseageDialog_Load(object sender, EventArgs e)
        {
            StatusLog.SetLogWriter(new TextBoxWriter(this.tbMessage));

            //            runFunction();
            backgroundWorker1.RunWorkerAsync();
        }

        public class TextBoxWriter : IStatusLogWriter
        {
            TextBox textBox;
            public TextBoxWriter(TextBox textBox)
            {
                this.textBox = textBox;
            }

            void IStatusLogWriter.Write(string log)
            {
                Console.Write(log);
            }
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            runFunction();
        }
    }
}
