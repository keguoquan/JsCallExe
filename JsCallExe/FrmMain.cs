using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WebSocketSharp.Server;
using JsCallExeClient.Helper;
using JsCallExeClient.Handler;

namespace JsCallExeClient
{
    public partial class FrmMain : Form
    {
        public FrmMain()
        {
            InitializeComponent();
            this.Text = string.Format("{0}-版本V{1}", this.Text, CommonInfo.Version);
            this.notifyIcon1.Text = this.Text;
        }

        #region 托盘事件

        /// <summary>
        /// 窗体关闭事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FrmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 注意判断关闭事件reason来源于窗体按钮，否则用菜单退出时无法退出!
            if (e.CloseReason == CloseReason.UserClosing)
            {
                //取消"关闭窗口"事件
                e.Cancel = true;
                //使关闭时窗口向右下角缩小的效果
                this.WindowState = FormWindowState.Minimized;
                this.notifyIcon1.Visible = true;
                this.Hide();
                return;
            }
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (this.Visible)
            {
                this.WindowState = FormWindowState.Minimized;
                this.notifyIcon1.Visible = true;
                this.Hide();
            }
            else
            {
                this.Visible = true;
                this.WindowState = FormWindowState.Normal;
                this.Activate();
            }
        }

        private void 退出ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("你确定要退出吗？", "退出提示", MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.Yes)
            {
                Application.Exit();
            }
        }

        #endregion

        /// <summary>
        /// 窗体加载
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FrmMain_Load(object sender, EventArgs e)
        {
            try
            {
                CommonInfo.FrmMain = this;
                WSMain.Instance.Start();
                Output(string.Format("启动成功，监听端口：{0}", CommonInfo.WsPort));
                this.WindowState = FormWindowState.Minimized;
                this.notifyIcon1.Visible = true;
                this.Hide();
            }
            catch (Exception ex)
            {
                Output(string.Format("窗体加载异常：{0}", ex.Message));
            }
        }
        

        #region 日志

        /// <summary>
        /// 日志输出
        /// </summary>
        /// <param name="log"></param>
        public void Output(string log)
        {
            try
            {
                if (this.InvokeRequired)
                {
                    Action<string> action = new Action<string>(Output);
                    this.Invoke(action, log);
                }
                else
                {
                    log = string.Format("{0}: {1}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), log);
                    List<ListViewItem> list = CreateListViewItem(new StringBuilder(log));
                    for (int i = 0; i < list.Count; i++)
                    {
                        listView1.Items.Insert(0, list[i]);
                        if (listView1.Items.Count > 200)
                        {
                            listView1.Items.RemoveAt(listView1.Items.Count - 1);
                        }
                    }
                }
            }
            catch (Exception ex)
            { }
        }

        int colorTemp;
        /// <summary>
        /// 日志行字符
        /// </summary>
        private const int _logRowLength = 100;
        private List<ListViewItem> CreateListViewItem(StringBuilder text)
        {
            System.Drawing.Color color = System.Drawing.Color.Black;
            switch (colorTemp)
            {
                case 0: color = System.Drawing.Color.Blue; break;
                case 1: color = System.Drawing.Color.Black; break;
            }
            colorTemp = colorTemp == 1 ? 0 : colorTemp + 1;
            int round = (int)Math.Ceiling((double)text.Length / _logRowLength);
            List<ListViewItem> list = new List<ListViewItem>();

            for (int i = 0; i < round; i++)
            {
                int len = Math.Min(text.Length, _logRowLength);

                string rowLog = text.ToString(0, len);

                list.Insert(0, new ListViewItem(rowLog) { ForeColor = color });
                text.Remove(0, len);
            }
            return list;
        }

        private void 清空消息ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listView1.Items.Clear();
        }

        #endregion
    }
}
