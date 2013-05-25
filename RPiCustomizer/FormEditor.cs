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
    public partial class FormEditor : Form
    {
        private readonly IPAddress _ip;
        private FormConnector _loader;

        public FormEditor(SshClient connection)
        {
            InitializeComponent();
            backgroundWorkerConnect.RunWorkerAsync();

            //GenerateGui(tabControlMain, "");
        }
    }
}
