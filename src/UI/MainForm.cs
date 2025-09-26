using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Stay_Awake_2.UI
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
            // DPI is already set by the Designer; rest of our window policy:
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            // Optional niceties:
            this.StartPosition = FormStartPosition.CenterScreen;
            // this.KeyPreview = true; // if you’ll handle ESC / shortcuts globally
        }

        private void MainForm_Load(object sender, EventArgs e)
        {

        }
    }
}
