using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
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
        private string ini;

        public FormEditor(SshClient connection)
        {
            InitializeComponent();
            backgroundWorkerConnect.RunWorkerAsync();

            ini = @"C:\Users\Erwin\SkyDrive\Personales\vWorker\Current\Raspberry Jon\bin\matriculas\settings.ini";
            Utilities.GenerateGui(tabControlMain,toolTipInfo, new IniParser(ini));
        }

        private void openWithSystemEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start(ini);
        }
    }

    public class Utilities
    {
        public static void GenerateGui(TabControl tabControlMain, ToolTip toolTipInfo, IniParser ini)
        {
            var s = ini.GetSections().ToList();
            s.Sort();

            foreach (var section in s)
            {
                var page = new TabPage(GetPrettyName(section));
                var panel = new TableLayoutPanel { Dock = DockStyle.Fill,ColumnCount = 2 };
                var lastComment = "";
                var i = 0;

                foreach (var option in ini.EnumSection(section))
                {

                    if (!new[] { "#", ";", "//" }.Any(c => option.StartsWith(c)))
                    {
                        var l = new Label
                                    {
                                        Text = GetPrettyName(option) + ":",
                                        TextAlign = ContentAlignment.MiddleLeft,
                                        //Width = tabControlMain.Width/4,
                                        AutoEllipsis = true
                                    };
                        panel.SetColumn(l, 0);
                        panel.SetRow(l, i);
                        panel.Controls.Add(l);

                        Control t = null;
                        var defaultValue = ini.GetSetting(section, option);
                        var tooltip = "";

                        // Check previous comment
                        if (!String.IsNullOrEmpty(lastComment))
                        {
                            var tmp = lastComment.Trim().Split(new[] {']'}, 2);

                            if (tmp.Length > 1)
                                tooltip = tmp[1].Trim();

                            var m = Regex.Match(lastComment,@"\[(?<min>[-]?[\d.]{1,8}):(?<max>[-]?[\d.]{1,8})\]");
                            if (m.Success)
                            {
                                t = new NumericUpDown
                                        {
                                            Minimum = decimal.Parse(m.Groups["min"].Value),
                                            Maximum = decimal.Parse(m.Groups["max"].Value),
                                            Value = decimal.Parse(defaultValue)
                                        };
                            }
                            else
                            {
                                m = Regex.Match(lastComment, @"\[(?<value>.+,.+)\]");
                                if (m.Success)
                                {
                                    t = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

                                    var items = new List<String>();
                                    var description = new List<string>();
                                    foreach (var v in m.Groups["value"].Value.Split(new[] { ',' }))
                                    {
                                        String name, value;
                                        if (v.Contains(':'))
                                        {
                                            var parts = v.Split(new[] {':'}, 2);
                                            name = parts[1];
                                            value = parts[0];
                                            description.Add(String.Format(" {0} ({1})",name,value));
                                        }
                                        else
                                        {
                                            name = v;
                                            value = v;
                                        }

                                        items.Add(value);
                                        ((ComboBox)t).Items.Add(name);

                                        if (defaultValue.Equals(value))
                                        {
                                            ((ComboBox) t).SelectedIndex = ((ComboBox) t).Items.Count-1;
                                        }
                                    }

                                    if (description.Count > 0)
                                    {
                                        if (!String.IsNullOrEmpty(tooltip))
                                            tooltip += Environment.NewLine + Environment.NewLine;
                                        tooltip += "Available options:" + Environment.NewLine +
                                                   String.Join(Environment.NewLine, description);
                                    }
                                    t.Tag = items;

                                    // No default value match
                                    if (((ComboBox) t).SelectedIndex < 0)
                                    {
                                        ((ComboBox) t).DropDownStyle = ComboBoxStyle.DropDown;
                                        t.Text = defaultValue;
                                    }
                                }
                            }
                        }

                        if(t==null)
                            t = new TextBox { Anchor = AnchorStyles.Top| AnchorStyles.Left| AnchorStyles.Right,
                                Text = defaultValue/*, Width = (int) (tabControlMain.Width / 1.5)*/ };
                        
                        panel.SetColumn(t, 1);
                        panel.SetRow(l, i++);
                        panel.Controls.Add(t);

                        if (!String.IsNullOrEmpty(tooltip) && toolTipInfo != null)
                            toolTipInfo.SetToolTip(t, tooltip);

                        lastComment = "";
                    }
                    else
                        lastComment = option;
                }

                if (panel.Controls.Count > 0)
                {
                    page.Controls.Add(panel);
                    tabControlMain.Controls.Add(page);
                }
                else
                {
                    page.Dispose();
                    panel.Dispose();
                }
            }
        }

        private static string GetPrettyName(string p)
        {
            if (p.Equals(Resources.IniParser_RootSection)) return "Main";
            var tmp = p.Trim().Replace("_", " ");

            if (tmp.Length > 0)
                return tmp.Substring(0, 1).ToUpper() + tmp.Substring(1);
            return p;
        }
    }
}
