using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
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
            backgroundWorkerConnect.RunWorkerAsync();
        }

        public SshClient Connection { get; private set; }

        private void backgroundWorkerConnect_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                Connection = new SshClient(_ip.ToString(), Settings.Default.DefaultPort, "pi", "raspberry");

                Connection.Connect();
                var c = Connection.RunCommand("cat /home/pi/mono/matriculas/settings.ini");
                c.Execute();
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
            DialogResult = Connection != null && Connection.IsConnected ? DialogResult.OK : DialogResult.Cancel;
            
            if (Connection != null && Connection.IsConnected)
            {
                Connection.Disconnect();
                Connection.Dispose();
                Connection = null;
            }
        }
    }
}
    