using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows.Forms;
using RPICustomizer.Properties;
using Renci.SshNet;

namespace RPICustomizer
{
    public partial class FormConnector : Form
    {
        private readonly IPAddress _ip;

        public FormConnector(IPAddress ip)
        {
            _ip = ip;
            InitializeComponent();
            labelStatus.Text = labelStatus.Text.Replace("%d",String.Join(".", ip.GetAddressBytes()) + ":" + Settings.Default.DefaultPort);

            backgroundWorkerConnect.RunWorkerAsync();
        }

        public SshClient Connection { get; private set; }

        private void backgroundWorkerConnect_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                Connection = new SshClient(_ip.ToString(), Settings.Default.DefaultPort, "pi", "raspberry");
                Connection.Connect();

                var c = Connection.RunCommand("cat /boot/customizer.txt");
                c.Execute();

                if (String.IsNullOrEmpty(c.Error))
                {
                    Configuration = c.Result;
                }
                else
                {
                    e.Result = c.Error;
                    Connection.Disconnect();
                }
            }
            catch
            {
            }
        }

        private void backgroundWorkerConnect_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Close();
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void FormConnector_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (Connection != null && Connection.IsConnected)
            {
                DialogResult = DialogResult.OK;
            }
            else
            {
                try
                {
                    MessageBox.Show("Can't connect to the device", "Error",MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Connection.Disconnect();
                    Connection.Dispose();
                    Connection = null;
                }
                catch
                {
                }

                DialogResult = DialogResult.Cancel;
            }
        }

        public string Configuration { get; set; }
    }
}
    