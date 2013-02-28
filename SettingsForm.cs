/*
Copyright (c) 2011 Ben Barron

Permission is hereby granted, free of charge, to any person obtaining a copy 
of this software and associated documentation files (the "Software"), to deal 
in the Software without restriction, including without limitation the rights 
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell 
copies of the Software, and to permit persons to whom the Software is furnished 
to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all 
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, 
INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A 
PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT 
HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION 
OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE 
SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

Hibernation Code provided and integrated by Miljbee (miljbee@gmail.com)
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Principal;
using System.Windows.Forms;
using System.Xml;

using WindowsAPI;
using Microsoft.Win32;

using BDRemoteSleep;

namespace PS3BluMote
{
    public partial class SettingsForm : Form
    {
        private readonly String SETTINGS_DIRECTORY = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData) + "\\PS3BluMote\\";
        private readonly String SETTINGS_FILE = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData) + "\\PS3BluMote\\settings.ini";
        private const String SETTINGS_VERSION = "2.0";

        private ButtonMapping[] buttonMappings = new ButtonMapping[56];
        private PS3Remote remote = null;
        private SendInputAPI.Keyboard keyboard = null;
        private System.Timers.Timer timerRepeat = null;

        private string insertSound = "";
        private string removeSound = "";

        public SettingsForm()
        {
            for (int i = 0; i < buttonMappings.Length; i++)
            {
                buttonMappings[i] = new ButtonMapping();
            }

            InitializeComponent();

            ListViewItem lvItem;
            foreach (PS3Remote.Button button in Enum.GetValues(typeof(PS3Remote.Button)))
            {
                lvItem = new ListViewItem();
                lvItem.SubItems.Add(button.ToString());
                lvItem.SubItems.Add("");
                lvButtons.Items.Add(lvItem);
            }

            foreach (SendInputAPI.Keyboard.KeyCode key in Enum.GetValues(typeof(SendInputAPI.Keyboard.KeyCode)))
            {
                lvKeys.Items.Add(new ListViewItem(key.ToString()));
            }

            if (!loadSettings())
            {
                saveSettings();
            }

            timerRepeat = new System.Timers.Timer();
            timerRepeat.Interval = int.Parse(txtRepeatInterval.Text);
            timerRepeat.Elapsed += new System.Timers.ElapsedEventHandler(timerRepeat_Elapsed);

            buttonDump.Visible = DebugLog.isLogging;

            // Finding BT Address of the remote for Hibernation
            if (txtBtAddr.Text.Length != 12 && txtBtAddr.Text.Length != 17)
            {
                try
                {
                    RegistryKey bthEnum = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\BTHENUM");
                    string vid = txtVendorId.Text.Substring(2);
                    string pid = txtProductId.Text.Substring(2);
                    string filter = vid + "_PID&" + pid;
                    SearchKey sk = new SearchKey(bthEnum, filter, "Bluetooth_UniqueID");
                    if (sk.Result != null)
                    {
                        string addr = (string)sk.Result.GetValue("Bluetooth_UniqueID");
                        if (addr.Length != 0 && addr.Contains("#") && addr.Contains("_") && (addr.IndexOf("_") - addr.IndexOf("#")) == 13)
                        {
                            addr = addr.Substring(addr.IndexOf("#") + 1, 12);
                            txtBtAddr.Text = addr.Substring(0, 2) + ":" + addr.Substring(2, 2) + ":" + addr.Substring(4, 2) + ":" + addr.Substring(6, 2) + ":" + addr.Substring(8, 2) + ":" + addr.Substring(10, 2);
                        }
                    }
                }
                catch
                {
                    if (DebugLog.isLogging) DebugLog.write("Unexpected error while trying to retrieve BT Address from registry");
                }
            }
            // Saving Device Insertion sounds
            try
            {
                string s;
                bool save=false;
                s = (string)Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\DeviceConnect\.Current", "", "");
                if (insertSound.Length == 0 || (insertSound != s && s.Length > 0))
                {
                    insertSound = s;
                    save = true;
                }
                s = (string)Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\DeviceDisconnect\.Current", "", "");
                if (removeSound.Length == 0 || (removeSound != s && s.Length > 0))
                {
                    removeSound = s;
                    save = true;
                }
                if (save) saveSettings();
            }
            catch
            {
                if (DebugLog.isLogging) DebugLog.write("Unexpected error while trying to save Devices insertion/remove sounds.");
            }

            // Restoring Device Insertion sounds in case they have been left blank
            try
            {
                string s;
                s = (string)Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\DeviceConnect\.Current", "", "");
                if (s.Length == 0 && insertSound.Length > 0) Microsoft.Win32.Registry.SetValue(@"HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\DeviceConnect\.Current", "", insertSound);
                s = (string)Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\DeviceDisconnect\.Current", "", "");
                if (s.Length == 0 && removeSound.Length > 0) Microsoft.Win32.Registry.SetValue(@"HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\DeviceDisconnect\.Current", "", removeSound);
            }
            catch 
            {
                if (DebugLog.isLogging) DebugLog.write("Unexpected error while trying to restore Devices insertion/remove sounds.");
            }

            try
            {
                int hibMs;
                try
                {
                    hibMs = System.Convert.ToInt32(txtMinutes.Text) * 60 * 1000;
                }
                catch
                {
                    if (DebugLog.isLogging) DebugLog.write("Error while parsing Hibernation Interval, taking Default 3 Minutes");
                    txtMinutes.Text = "3";
                    hibMs = 180000;
                }
                remote = new PS3Remote(int.Parse(txtVendorId.Text.Remove(0, 2), System.Globalization.NumberStyles.HexNumber), int.Parse(txtProductId.Text.Remove(0, 2), System.Globalization.NumberStyles.HexNumber));

                remote.BatteryLifeChanged += new EventHandler<EventArgs>(remote_BatteryLifeChanged);
                remote.ButtonDown += new EventHandler<PS3Remote.ButtonData>(remote_ButtonDown);
                remote.ButtonReleased += new EventHandler<PS3Remote.ButtonData>(remote_ButtonReleased);

                remote.Connected += new EventHandler<EventArgs>(remote_Connected);
                remote.Disconnected += new EventHandler<EventArgs>(remote_Disconnected);
                remote.Hibernated += new EventHandler<EventArgs>(remote_Hibernated);
                remote.Awake += new EventHandler<EventArgs>(remote_Connected);

                remote.connect();

                remote.hibernationEnabled = cbHibernation.Enabled && cbHibernation.Checked;
                remote.hibernationInterval = hibMs;
                remote.btAddress = txtBtAddr.Text.Replace(":", "").Replace("-", "").Replace(".", "").Replace(" ", "");
            }
            catch
            {
                MessageBox.Show("An error occured whilst attempting to load the remote.", "PS3BluMote: Remote error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            keyboard = new SendInputAPI.Keyboard(cbSms.Checked);
        }

        private void cbDebugMode_CheckedChanged(object sender, EventArgs e)
        {
            DebugLog.isLogging = cbDebugMode.Checked;
            buttonDump.Visible = DebugLog.isLogging;
        }

        private void cbHibernation_CheckedChanged(object sender, EventArgs e)
        {
            if (txtBtAddr.Text.Length==12 || txtBtAddr.Text.Length==17 && remote!=null) remote.hibernationEnabled = cbHibernation.Checked;
            else if (cbHibernation.Checked && remote!=null)
            {
                MessageBox.Show("Fill in the BT Address field with a correct value before attempting to enable hibernation.", "PS3BluMote: Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                cbHibernation.Checked = false;
                remote.hibernationEnabled = false;
            }
        }

        private void cbSms_CheckedChanged(object sender, EventArgs e)
        {
            if (keyboard != null) keyboard.isSmsEnabled = cbSms.Checked;
        }

        private void llblOpenFolder_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process prc = new System.Diagnostics.Process();
            prc.StartInfo.FileName = "explorer.exe";
            prc.StartInfo.Arguments = SETTINGS_DIRECTORY;
            prc.Start();
        }

        private bool loadSettings()
        {
            String errorMessage;

            if (File.Exists(SETTINGS_FILE))
            {
                XmlDocument rssDoc = new XmlDocument();

                try
                {
                    rssDoc.Load(SETTINGS_FILE);

                    XmlNode rssNode = rssDoc.SelectSingleNode("PS3BluMote");

                    if (rssNode.Attributes["version"].InnerText == SETTINGS_VERSION)
                    {
                        cbSms.Checked = rssNode.SelectSingleNode("settings/smsinput").InnerText.ToLower() == "true";
                        txtVendorId.Text = rssNode.SelectSingleNode("settings/vendorid").InnerText;
                        txtProductId.Text = rssNode.SelectSingleNode("settings/productid").InnerText;
                        try
                        {
                            txtBtAddr.Text = rssNode.SelectSingleNode("settings/btaddress").InnerText;
                            cbHibernation.Checked = rssNode.SelectSingleNode("settings/hibernation").InnerText.ToLower() == "true";
                            cbDebugMode.Checked = rssNode.SelectSingleNode("settings/debug").InnerText.ToLower() == "true";
                            txtMinutes.Text = rssNode.SelectSingleNode("settings/minutes").InnerText;
                        }
                        catch
                        {
                        }

                        try
                        {
                            txtRepeatInterval.Text = rssNode.SelectSingleNode("settings/repeatinterval").InnerText;
                        }
                        catch
                        { }
                        
                        /*if (cbHibernation.Checked &&
                            !(new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)))
                        {
                            MessageBox.Show("Admin/UAC elevated rights are required to use the hibernation feature! Please enable them!", "PS3BluMote: No admin rights found!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                        }*/

                        foreach (XmlNode buttonNode in rssNode.SelectNodes("mappings/button"))
                        {
                            int index = (int)Enum.Parse(typeof(PS3Remote.Button), buttonNode.Attributes["name"].InnerText, true);
                            buttonMappings[index].repeat = (buttonNode.Attributes["repeat"].InnerText.ToLower() == "true") ? true : false;
                            lvButtons.Items[index].Checked = buttonMappings[index].repeat;
                            List<SendInputAPI.Keyboard.KeyCode> mappedKeys = buttonMappings[index].keysMapped;

                            if (buttonNode.InnerText.Length > 0)
                            {
                                foreach (string keyCode in buttonNode.InnerText.Split(','))
                                {
                                    mappedKeys.Add((SendInputAPI.Keyboard.KeyCode)Enum.Parse(typeof(SendInputAPI.Keyboard.KeyCode), keyCode, true));
                                }

                                lvButtons.Items[index].SubItems[2].Text = buttonNode.InnerText.Replace(",", " + ");
                            }
                        }

                        return true;
                    }

                    errorMessage = "Incorrect settings file version.";
                }
                catch
                {
                    errorMessage = "An error occured whilst attempting to load settings.";
                }
            }
            else
            {
                errorMessage = "Unable to locate the settings file.";
            }

            MessageBox.Show(errorMessage + " A fresh settings file has been created.", "PS3BluMote: Settings load error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            
            return false;
        }

        private void lvButtons_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            buttonMappings[e.Item.Index].repeat = e.Item.Checked;
        }

        private void lvButtons_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lvButtons.SelectedItems.Count == 0) return;

            lvButtons.Tag = true;

            int index = (int)Enum.Parse(typeof(PS3Remote.Button), lvButtons.SelectedItems[0].SubItems[1].Text, true);
            List<SendInputAPI.Keyboard.KeyCode> mappedKeys = buttonMappings[index].keysMapped;

            foreach (ListViewItem lvItem in lvKeys.Items)
            {
                if (mappedKeys.Contains((SendInputAPI.Keyboard.KeyCode)Enum.Parse(typeof(SendInputAPI.Keyboard.KeyCode), lvItem.Text, true)))
                {
                    lvItem.Checked = true;
                }
                else
                {
                    lvItem.Checked = false;
                }
            }

            lvButtons.Tag = false;
        }

        private void lvKeys_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if ((bool)lvButtons.Tag) return;

            int index = (int)Enum.Parse(typeof(PS3Remote.Button), lvButtons.SelectedItems[0].SubItems[1].Text, true);
            List<SendInputAPI.Keyboard.KeyCode> mappedKeys = buttonMappings[index].keysMapped;
            SendInputAPI.Keyboard.KeyCode code = (SendInputAPI.Keyboard.KeyCode)Enum.Parse(typeof(SendInputAPI.Keyboard.KeyCode), lvKeys.Items[e.Index].Text, true);

            if (e.NewValue == CheckState.Checked && !mappedKeys.Contains(code))
            {
                mappedKeys.Add(code);
            }
            else
            {
                mappedKeys.Remove(code);
            }

            String text = "";
            foreach (SendInputAPI.Keyboard.KeyCode key in mappedKeys)
            {
                text += key.ToString() + " + ";
            }

            lvButtons.SelectedItems[0].SubItems[2].Text = (mappedKeys.Count > 0) ? text.Substring(0, text.Length - 3) : "";
        }

        private void menuNotifyIcon_ItemClick(object sender, EventArgs e)
        {
            if (sender.Equals(mitemSettings))
            {
                this.Show();
            }
            else if (sender.Equals(mitemExit))
            {
                if (DebugLog.isLogging) DebugLog.outputToFile(SETTINGS_DIRECTORY + "log.txt");

                Application.Exit();
            }
        }

        private void notifyIcon_DoubleClick(object sender, EventArgs e)
        {
            this.Show();
        }

        private void remote_BatteryLifeChanged(object sender, EventArgs e)
        {
            notifyIcon.Text = "PS3BluMote: Connected + (Battery: " + remote.getBatteryLife.ToString() + "%).";

            if (DebugLog.isLogging) DebugLog.write("Battery life: " + remote.getBatteryLife.ToString() + "%");
        }

        private void remote_ButtonDown(object sender, PS3Remote.ButtonData e)
        {
            if (DebugLog.isLogging) DebugLog.write("Button down: " + e.button.ToString());

            ButtonMapping mapping = buttonMappings[(int)e.button];

            if (mapping.repeat)
            {
                keyboard.sendKeysDown(mapping.keysMapped);
                keyboard.releaseLastKeys();

                if (DebugLog.isLogging) DebugLog.write("Keys repeat send on : { " + String.Join(",", mapping.keysMapped.ToArray()) + " }");

                timerRepeat.Enabled = true;
                return;
            }
            
            keyboard.sendKeysDown(mapping.keysMapped);

            if (DebugLog.isLogging) DebugLog.write("Keys down: { " + String.Join(",", mapping.keysMapped.ToArray()) + " }");
        }

        private void remote_ButtonReleased(object sender, PS3Remote.ButtonData e)
        {
            if (DebugLog.isLogging) DebugLog.write("Button released: " + e.button.ToString());

            if (timerRepeat.Enabled)
            {
                if (DebugLog.isLogging) DebugLog.write("Keys repeat send off: { " + String.Join(",", keyboard.lastKeysDown.ToArray()) + " }");

                timerRepeat.Enabled = false;
                return;
            }

            if (DebugLog.isLogging && this.keyboard.lastKeysDown!=null) DebugLog.write("Keys up: { " + String.Join(",", keyboard.lastKeysDown.ToArray()) + " }");

            keyboard.releaseLastKeys();
        }

        private void remote_Connected(object sender, EventArgs e)
        {
            if (DebugLog.isLogging) DebugLog.write("Remote connected");

            notifyIcon.Text = "PS3BluMote: Connected + (Battery: " + remote.getBatteryLife.ToString() + "%).";
            notifyIcon.Icon = Properties.Resources.Icon_Connected;
        }

        private void remote_Disconnected(object sender, EventArgs e)
        {
            if (DebugLog.isLogging) DebugLog.write("Remote disconnected");

            notifyIcon.Text = "PS3BluMote: Disconnected.";
            notifyIcon.Icon = Properties.Resources.Icon_Disconnected;
        }

        private void remote_Hibernated(object sender, EventArgs e)
        {
            if (DebugLog.isLogging) DebugLog.write("Remote Hibernated");

            notifyIcon.Text = "PS3BluMote: Hibernated + (Battery: " + remote.getBatteryLife.ToString() + "%).";
            notifyIcon.Icon = Properties.Resources.Icon_Hibernated;
        }



        private bool saveSettings()
        {
            string text = "<PS3BluMote version=\"" + SETTINGS_VERSION + "\">\r\n";
            text += "\t<settings>\r\n";
            text += "\t\t<vendorid>" + txtVendorId.Text.ToLower() + "</vendorid>\r\n";
            text += "\t\t<productid>" + txtProductId.Text.ToLower() + "</productid>\r\n";
            text += "\t\t<smsinput>" + cbSms.Checked.ToString().ToLower() + "</smsinput>\r\n";
            text += "\t\t<hibernation>" + (cbHibernation.Checked && cbHibernation.Enabled).ToString().ToLower() + "</hibernation>\r\n";
            text += "\t\t<btaddress>" + txtBtAddr.Text.ToLower() + "</btaddress>\r\n";
            text += "\t\t<minutes>" + txtMinutes.Text.ToLower() + "</minutes>\r\n";
            text += "\t\t<repeatinterval>" + txtRepeatInterval.Text + "</repeatinterval>\r\n";
            text += "\t\t<debug>" + cbDebugMode.Checked.ToString().ToLower() + "</debug>\r\n";
            if (insertSound.Length > 0 && removeSound.Length > 0)
            {
                text += "\t\t<isound>" + insertSound + "</isound>\r\n";
                text += "\t\t<rsound>" + removeSound + "</rsound>\r\n";
            }
            text += "\t</settings>\r\n";
            text += "\t<mappings>\r\n";

            for (int i = 0; i < buttonMappings.Length; i++)
            {
                text += "\t\t<button name=\"" + ((PS3Remote.Button)i).ToString() + "\" repeat=\"" 
                    + buttonMappings[i].repeat.ToString().ToLower() + "\">"
                    + String.Join(",", buttonMappings[i].keysMapped.ToArray()) + "</button>\r\n";
            }

            text += "\t</mappings>\r\n";
            text += "</PS3BluMote>";

            try
            {
                if (!Directory.Exists(SETTINGS_DIRECTORY))
                {
                    Directory.CreateDirectory(SETTINGS_DIRECTORY);
                }

                TextWriter tw = new StreamWriter(SETTINGS_FILE, false);
                tw.WriteLine(text);
                tw.Close();

                return true;
            }
            catch
            {
                MessageBox.Show("An error occured whilst attempting to save settings.", "PS3BluMote: Saving settings error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return false;
        }

        private void SettingsForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            saveSettings();

            if (e.CloseReason != CloseReason.UserClosing) return;
            
            e.Cancel = true;

            this.Hide();
        }

        private void SettingsForm_Shown(object sender, EventArgs e)
        {
            lvButtons.Items[0].Selected = true;
        }

        private void timerRepeat_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            keyboard.sendKeysDown(keyboard.lastKeysDown);
            keyboard.releaseLastKeys();
        }

        private void txtProductId_Validating(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                int i = int.Parse(txtProductId.Text.Remove(0, 2), System.Globalization.NumberStyles.HexNumber);
            }
            catch
            {
                e.Cancel = true;   
            }
        }

        private void txtRepeatInterval_Validating(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                int i = int.Parse(txtRepeatInterval.Text);
            }
            catch
            {
                e.Cancel = true;
            }
        }

        private void txtVendorId_Validating(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                int i = int.Parse(txtVendorId.Text.Remove(0, 2), System.Globalization.NumberStyles.HexNumber);
            }
            catch
            {
                e.Cancel = true;
            }
        }

        private class ButtonMapping
        {
            public List<SendInputAPI.Keyboard.KeyCode> keysMapped;
            public bool repeat;

            public ButtonMapping()
            {
                keysMapped = new List<SendInputAPI.Keyboard.KeyCode>();
                repeat = false;
            }
        }

        private void buttonDump_Click(object sender, EventArgs e)
        {
            DebugLog.outputToFile(SETTINGS_DIRECTORY + "log.txt");
        }

        private void txtBtAddr_TextChanged(object sender, EventArgs e)
        {
            if (txtBtAddr.Text.Length == 17 || txtBtAddr.Text.Length == 12)
            {
                cbHibernation.Enabled = true;
            }
            else
            {
                cbHibernation.Enabled = false;
            }
        }

        private void txtMinutes_TextChanged(object sender, EventArgs e)
        {
            try
            {
                int i = System.Convert.ToInt32(txtMinutes.Text) * 60 * 1000;
                if (remote != null) remote.hibernationInterval = i;
            }
            catch { }
        }
    }
}
