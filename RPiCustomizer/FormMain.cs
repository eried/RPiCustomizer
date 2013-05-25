using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using RPICustomizer.Properties;

namespace RPICustomizer
{
    public partial class FormMain : Form
    {
        public FormMain()
        {
            InitializeComponent();
            backgroundWorkerScanner.RunWorkerAsync();
        }

        private void FormMain_Load(object sender, EventArgs e)
        {

           /* var r = new Renci.SshNet.SshClient("192.168.1.143","pi","raspberry");

            r.Connect();
            var c = r.RunCommand("ls");
            c.Execute();
            MessageBox.Show(c.Result);
            r.Disconnect();*/
        }

        private void backgroundWorkerScanner_DoWork(object sender, DoWorkEventArgs e)
        {
            var lanIp = LocalIPAddress();
            if (lanIp != null)
            {
                var result = Parallel.ForEach(getIps(lanIp),
                                              new ParallelOptions { MaxDegreeOfParallelism = Settings.Default.MaxDegreeOfParallelism },
                                              ip => {
                                                  if (IsPortOpen(ip, Settings.Default.DefaultPort))
                                                        {
                                                            backgroundWorkerScanner.ReportProgress(-1, ip);
                                                        }
                                              });
            }
        }

        private static bool TryPing(IPAddress strIpAddress, int intPort, int nTimeoutMsec)
        {
            Socket socket = null;
            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, false);


                var result = socket.BeginConnect(strIpAddress, intPort, null, null);
                result.AsyncWaitHandle.WaitOne(nTimeoutMsec, true);

                return socket.Connected;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (null != socket)
                    socket.Close();
            }
        }

        private static bool IsPortOpen(IPAddress ip, int port)
        {
            try
            {
                return TryPing(ip, port, Settings.Default.PortScanTimeout);
            }
            catch
            {
            }
            return false;
        }

        private IEnumerable<IPAddress> getIps(IPAddress lanIp)
        {
            for (int i = 1; i < 256; i++)
            {
                var ip = lanIp.GetAddressBytes();
                ip[3] = (byte) i;
                yield return new IPAddress(ip);
            }
        }


        public IPAddress LocalIPAddress()
        {
            if (NetworkInterface.GetIsNetworkAvailable())
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        var f = ip.GetAddressBytes()[0];

                        if(f!=127 && f!=169)
                            return ip;
                    }
                }
            }
            return null;
        }

        private void backgroundWorkerScanner_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            var ip = (IPAddress) e.UserState;
            listViewDevices.Items.Add(new ListViewItem((ip).ToString()) {Tag = ip});
        }

        private void listViewDevices_DoubleClick(object sender, EventArgs e)
        {
            var c = listViewDevices.SelectedItems;
            if (c.Count > 0)
            {
                var f = new FormConnector((IPAddress) c[0].Tag);
                if (f.ShowDialog() == DialogResult.OK)
                    new FormEditor(f.Connection).ShowDialog();
            }
        }

        private void backgroundWorkerScanner_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            toolStripStatusLabelMain.Text = "Ready";
            toolStripProgressBarMain.Visible = false;
        }
    }
}
