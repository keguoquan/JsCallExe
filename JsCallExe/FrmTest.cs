using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using WebSocketSharp;
using JsCallExeClient.Helper;

namespace JsCallExeClient
{
    public partial class FrmTest : Form
    {

        public FrmTest()
        {
            InitializeComponent();
        }

        private void FrmTest_Load(object sender, EventArgs e)
        {
        }

        private void label3_Click(object sender, EventArgs e)
        {
            MessageBox.Show("这是winform弹出消息框");
        }
    }
}
