using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows.Forms;
using CarPlateMonitor;
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

            Utilities.GenerateGui(tabControlMain, new IniParser(@"C:\Users\Erwin\SkyDrive\Personales\vWorker\Current\Raspberry Jon\bin\matriculas\settings.ini"));
        }
    }

    public class Utilities
    {
        public static void GenerateGui(TabControl tabControlMain, IniParser ini)
        {
            var s = ini.GetSections().ToList();
            s.Sort();

            tabControlMain.Controls.Add(new TabPage(GetPrettyName("Main")));
            foreach (var section in s)
            {
                tabControlMain.Controls.Add(new TabPage(GetPrettyName(section)));
            }
        }

        private static string GetPrettyName(string p)
        {
            return p;
        }
    }
}
