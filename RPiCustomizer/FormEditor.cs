using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using RPICustomizer.Properties;
using Renci.SshNet;
using ConfigurationParser;

namespace RPICustomizer
{
    public partial class FormEditor : Form
    {
        private readonly SshClient _connection;
        private readonly IPAddress _ip;
        private FormConnector _loader;
        private IniFile commands;
        private IniFile config;
        private bool _dirty;

        public FormEditor(String configuration, SshClient connection)
        {
            _connection = connection;
            InitializeComponent();
            Text = connection.ConnectionInfo.Host + " - "+ Text;
            
            backgroundWorkerConnect.RunWorkerAsync(configuration);
        }

        private void openWithSystemEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start(config.ConfigurationFile);
        }

        private void backgroundWorkerConnect_DoWork(object sender, DoWorkEventArgs e)
        {
            commands = new IniFile((String)e.Argument,true);

            var c = _connection.RunCommand(commands.GetSetting("config", "get"));
            c.Execute();
            config = new IniFile(c.Result,true);
        }

        private void backgroundWorkerConnect_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (config != null)
            {
                GenerateGui();
                //labelWait.Visible = false;
                tabControlMain.Visible = true;
            }
        }

        private void GenerateGui()
        {
            var s = config.GetSections().ToList();
            s.Sort();

            foreach (var section in s)
            {
                var page = new TabPage(Utilities.GetPrettyName(section));
                var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
                var lastComment = "";
                var i = 0;

                foreach (var option in config.EnumSection(section))
                {

                    if (!new[] { "#", ";", "//" }.Any(c => option.StartsWith(c)))
                    {
                        var l = new Label
                        {
                            Text = Utilities.GetPrettyName(option) + ":",
                            TextAlign = ContentAlignment.MiddleLeft,
                            //Width = tabControlMain.Width/4,
                            AutoEllipsis = true
                        };
                        panel.SetColumn(l, 0);
                        panel.SetRow(l, i);
                        panel.Controls.Add(l);

                        Control t = null;
                        var defaultValue = config.GetSetting(section, option);
                        var tooltip = "";

                        // Check previous comment
                        if (!String.IsNullOrEmpty(lastComment))
                        {
                            var tmp = lastComment.Trim().Split(new[] { ']' }, 2);

                            if (tmp.Length > 1)
                                tooltip = tmp[1].Trim();

                            var m = Regex.Match(lastComment, @"\[(?<min>[-]?[\d.]{1,8}):(?<max>[-]?[\d.]{1,8})\]");
                            if (m.Success)
                            {
                                t = new NumericUpDown
                                {
                                    Minimum = decimal.Parse(m.Groups["min"].Value),
                                    Maximum = decimal.Parse(m.Groups["max"].Value),
                                    Value = decimal.Parse(defaultValue)
                                };

                                ((NumericUpDown)t).ValueChanged += (o, args) => { config.AddSetting(section, option, ((NumericUpDown)o).Value.ToString()); };
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
                                            var parts = v.Split(new[] { ':' }, 2);
                                            name = parts[1];
                                            value = parts[0];
                                            description.Add(String.Format(" {0} ({1})", name, value));
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
                                            ((ComboBox)t).SelectedIndex = ((ComboBox)t).Items.Count - 1;
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
                                    if (((ComboBox)t).SelectedIndex < 0)
                                    {
                                        ((ComboBox)t).DropDownStyle = ComboBoxStyle.DropDown;
                                        t.Text = defaultValue;
                                    }

                                    ((ComboBox)t).SelectedValueChanged += (o, args) =>
                                    {
                                        var c = o as ComboBox;
                                        if (c != null)
                                        {
                                            string v = null;
                                            if (c.Tag != null)
                                            {
                                                var list = c.Tag as List<string>;
                                                if (list != null)
                                                    v = list[c.SelectedIndex];
                                            }
                                            
                                            if(v == null)
                                                v = c.SelectedValue.ToString();

                                            config.AddSetting(section, option, v);
                                        }
                                    };
                                }
                            }
                        }

                        if (t == null)
                        {
                            t = new TextBox
                            {
                                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                                Text = defaultValue /*, Width = (int) (tabControlMain.Width / 1.5)*/
                            };

                            t.TextChanged += (o, args) => { config.AddSetting(section, option, ((TextBox) o).Text); };
                        }

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
                    panel.AutoScroll = true;
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

        private void quitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void exportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var s = new SaveFileDialog();

            if (s.ShowDialog() == DialogResult.OK)
            {
                config.SaveSettings(s.FileName);
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var cmd = commands.GetSetting("config", "set");
            if (!String.IsNullOrEmpty(cmd))
            {
                try
                {
                    config.SaveSettings();
                    var t = cmd.Replace("{config}", File.ReadAllText(config.ConfigurationFile).Replace("\"", "\\\""));

                    _connection.RunCommand(t).Execute();
                    _dirty = false;
                }
                catch
                {
                }
            }

            if (_dirty)
                MessageBox.Show("Can't save in the remote device");
        }
    }

    public class Utilities
    {
        internal static string GetPrettyName(string p)
        {
            if (p.Equals(Resources.IniParser_RootSection)) return "Main";
            var tmp = p.Trim().Replace("_", " ");

            if (tmp.Length > 0)
                return tmp.Substring(0, 1).ToUpper() + tmp.Substring(1);
            return p;
        }

        internal static string AppendIp(string Text, string ip, int port=-1)
        {
            return Text + " (" + ip + (port!=-1?":" + port:"") +")";
        }
    }
}
