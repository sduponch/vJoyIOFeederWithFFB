﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using BackForceFeeder;
using BackForceFeeder.Configuration;
using BackForceFeeder.Utils;
using BackForceFeeder.vJoyIOFeederAPI;

namespace BackForceFeederGUI.GUI
{

    public partial class ButtonsEditor : Form
    {

        public ButtonsEditor()
        {
            InitializeComponent();
        }


        List<CheckBox> AllRawChkBox = new List<CheckBox>();
        List<CheckBox> AllvJoyChkBox = new List<CheckBox>();
        private void MainForm_Load(object sender, EventArgs e)
        {
            var panel = this.splitContainerMain.Panel1;

            for (int i = 1; i <= vJoyManager.Config.CurrentControlSet.vJoyMapping.RawInputTovJoyMap.Count; i++) {
                // Display
                var rawInput = new CheckBox();
                rawInput.AutoSize = true;
                rawInput.Enabled = false;
                rawInput.Location = new System.Drawing.Point(6 + 48*((i-1)>>3), 16 + 20*((i-1)&0b111));
                rawInput.Name = "chkRaw" + i;
                rawInput.Size = new System.Drawing.Size(32, 17);
                rawInput.TabIndex = i;
                rawInput.Text = i.ToString();
                rawInput.Tag = i;
                rawInput.UseVisualStyleBackColor = true;
                AllRawChkBox.Add(rawInput);

                panel.Controls.Add(rawInput);

                // Combo box
                cmbBtnMapFrom.Items.Add(i.ToString());
            }

            int nbvJoy = vJoyFeeder.MAX_BUTTONS_VJOY;
            if (Program.Manager.vJoy!=null && Program.Manager.vJoy.vJoyVersionMatch) {
                nbvJoy =  Program.Manager.vJoy.GetNumberOfButtons();
            }
            for (int i = 1; i <= nbvJoy; i++) {
                // Display
                var vJoyBtn = new CheckBox();
                vJoyBtn.AutoSize = true;
                vJoyBtn.Enabled = false;
                vJoyBtn.Location = new System.Drawing.Point(6 + 48*((i-1)>>3) + 250, 16 + 20*((i-1)&0b111));
                vJoyBtn.Name = "chkBtn" + i;
                vJoyBtn.Size = new System.Drawing.Size(32, 17);
                vJoyBtn.TabIndex = i;
                vJoyBtn.Text = i.ToString();
                vJoyBtn.Tag = i;
                vJoyBtn.UseVisualStyleBackColor = true;
                AllvJoyChkBox.Add(vJoyBtn);

                panel.Controls.Add(vJoyBtn);

                // Combo box
                cmbBtnMapTo.Items.Add(i.ToString());
            }

            cmbShifterDecoder.Items.Clear();
            foreach(var item in Enum.GetValues(typeof(ShifterDecoderMap))) {
                cmbShifterDecoder.Items.Add(item.ToString());
            }

            txtUpDnDelay.Text = vJoyManager.Config.CurrentControlSet.vJoyMapping.UpDownDelay_ms.ToString();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Program.Manager.SaveControlSetFiles();
        }


        private void timerRefresh_Tick(object sender, EventArgs e)
        {
            if (Program.Manager.vJoy != null) {
                // Raw inputs
                for (int i = 0; i < AllRawChkBox.Count; i++) {
                    var chk = AllRawChkBox[i];
                    if ((Program.Manager.RawInputsStates & (UInt64)(1 << i)) != 0)
                        chk.Checked = true;
                    else
                        chk.Checked = false;
                }
                // vJoy buttons
                for (int i = 0; i < AllvJoyChkBox.Count; i++) {
                    var chk = AllvJoyChkBox[i];
                    if ((Program.Manager.vJoy.Report.Buttons & (1 << i)) != 0)
                        chk.Checked = true;
                    else
                        chk.Checked = false;
                }
            }
        }

        int SelectedRawInput = -1;
        private void cmbBtnMapFrom_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!int.TryParse(cmbBtnMapFrom.SelectedItem.ToString(), out SelectedRawInput)) {
                SelectedRawInput = -1;
            }
            RefresList();
        }


        void RefresList()
        {
            if ((SelectedRawInput>0) && (SelectedRawInput<=vJoyManager.Config.CurrentControlSet.vJoyMapping.RawInputTovJoyMap.Count)) {
                lstJoyBtn.Items.Clear();
                var raw = vJoyManager.Config.CurrentControlSet.vJoyMapping.RawInputTovJoyMap[SelectedRawInput-1];
                chkInvertRawLogic.Checked = raw.IsInvertedLogic;
                chkToggling.Checked = raw.IsToggle;
                chkAutofire.Checked = raw.IsAutoFire;
                chkSequenced.Checked = raw.IsSequencedvJoy;
                chkNeutralIsFirstBtn.Checked = raw.IsNeutralFirstBtn;

                cmbShifterDecoder.SelectedItem = raw.ShifterDecoder.ToString();

                var btns = raw.MappedvJoyBtns;
                foreach (var btn in btns) {
                    lstJoyBtn.Items.Add((btn+1).ToString());
                }
            }
        }

        int SelectedJoyBtn = -1;
        private void cmbBtnMapTo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!int.TryParse(cmbBtnMapTo.SelectedItem.ToString(), out SelectedJoyBtn)) {
                SelectedJoyBtn = -1;
            }
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            if ((SelectedRawInput>0) && (SelectedRawInput<=vJoyManager.Config.CurrentControlSet.vJoyMapping.RawInputTovJoyMap.Count)) {
                if (SelectedJoyBtn>0) {
                    var btns = vJoyManager.Config.CurrentControlSet.vJoyMapping.RawInputTovJoyMap[SelectedRawInput-1].MappedvJoyBtns;
                    if (!btns.Exists(x => (x==(SelectedJoyBtn-1)))) {
                        btns.Add(SelectedJoyBtn-1);
                        btns.Sort();
                        RefresList();
                    }
                }
            }
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            if ((SelectedRawInput>0) && (SelectedRawInput<=vJoyManager.Config.CurrentControlSet.vJoyMapping.RawInputTovJoyMap.Count)) {
                if (SelectedJoyBtn>0) {
                    var btns = vJoyManager.Config.CurrentControlSet.vJoyMapping.RawInputTovJoyMap[SelectedRawInput-1].MappedvJoyBtns;
                    if (btns.Exists(x => (x==(SelectedJoyBtn-1)))) {
                        btns.Remove((SelectedJoyBtn-1));
                        btns.Sort();
                        RefresList();
                    }
                }
            }
        }

        private void chkInvertRawLogic_Click(object sender, EventArgs e)
        {
            if ((SelectedRawInput>0) && (SelectedRawInput<=vJoyManager.Config.CurrentControlSet.vJoyMapping.RawInputTovJoyMap.Count)) {
                var raw = vJoyManager.Config.CurrentControlSet.vJoyMapping.RawInputTovJoyMap[SelectedRawInput-1];
                raw.IsInvertedLogic = chkInvertRawLogic.Checked;
            }
        }

        private void chkToggling_Click(object sender, EventArgs e)
        {
            if ((SelectedRawInput>0) && (SelectedRawInput<=vJoyManager.Config.CurrentControlSet.vJoyMapping.RawInputTovJoyMap.Count)) {
                var raw = vJoyManager.Config.CurrentControlSet.vJoyMapping.RawInputTovJoyMap[SelectedRawInput-1];
                raw.IsToggle = chkToggling.Checked;
            }
        }

        private void chkAutofire_Click(object sender, EventArgs e)
        {
            if ((SelectedRawInput>0) && (SelectedRawInput<=vJoyManager.Config.CurrentControlSet.vJoyMapping.RawInputTovJoyMap.Count)) {
                var raw = vJoyManager.Config.CurrentControlSet.vJoyMapping.RawInputTovJoyMap[SelectedRawInput-1];
                raw.IsAutoFire = chkAutofire.Checked;
            }
        }

        private void chkSequenced_Click(object sender, EventArgs e)
        {
            if ((SelectedRawInput>0) && (SelectedRawInput<=vJoyManager.Config.CurrentControlSet.vJoyMapping.RawInputTovJoyMap.Count)) {
                var raw = vJoyManager.Config.CurrentControlSet.vJoyMapping.RawInputTovJoyMap[SelectedRawInput-1];
                raw.IsSequencedvJoy = chkSequenced.Checked;
            }
        }

        private void lstJoyBtn_SelectedValueChanged(object sender, EventArgs e)
        {
            if (lstJoyBtn.SelectedItem!=null && int.TryParse(lstJoyBtn.SelectedItem.ToString(), out var joyBtn)) {
                cmbBtnMapTo.SelectedIndex = (joyBtn-1)%cmbBtnMapTo.Items.Count;
            }
        }

        private void btnResetAll_Click(object sender, EventArgs e)
        {
            var res = MessageBox.Show("Reset configuration\nAre you sure ?", "Reset configuration", MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation);
            if (res == DialogResult.OK) {
                vJoyManager.Config.CurrentControlSet.vJoyMapping.RawInputTovJoyMap.Clear();
                for (int i = vJoyManager.Config.CurrentControlSet.vJoyMapping.RawInputTovJoyMap.Count; i<BackForceFeeder.vJoyIOFeederAPI.vJoyFeeder.MAX_BUTTONS_VJOY; i++) {
                    var db = new RawInputDB();
                    db.MappedvJoyBtns = new List<int>(1) { i };
                    vJoyManager.Config.CurrentControlSet.vJoyMapping.RawInputTovJoyMap.Add(db);
                }

                RefresList();
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }


        private void cmbShifterDecoder_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (SelectedRawInput<0 || SelectedRawInput>=vJoyManager.Config.CurrentControlSet.vJoyMapping.RawInputTovJoyMap.Count) {
                MessageBox.Show("Please select a raw input first", "Error", MessageBoxButtons.OK);
            } else {
                var raw = vJoyManager.Config.CurrentControlSet.vJoyMapping.RawInputTovJoyMap[SelectedRawInput-1];
                Enum.TryParse<ShifterDecoderMap>(cmbShifterDecoder.SelectedItem.ToString(), out raw.ShifterDecoder);
            }
        }

        private void chkNeutralIsFirstBtn_Click(object sender, EventArgs e)
        {
            if ((SelectedRawInput>0) && (SelectedRawInput<=vJoyManager.Config.CurrentControlSet.vJoyMapping.RawInputTovJoyMap.Count)) {
                var raw = vJoyManager.Config.CurrentControlSet.vJoyMapping.RawInputTovJoyMap[SelectedRawInput-1];
                raw.IsNeutralFirstBtn = chkNeutralIsFirstBtn.Checked;
            }
        }

        private void txtUpDnDelay_KeyPress(object sender, KeyPressEventArgs e)
        {
            // Catch only Enter
            if (e.KeyChar != Convert.ToChar(Keys.Enter))
                return;
            UpdatetxtUpDnDelay();
        }
        private void txtUpDnDelay_Leave(object sender, EventArgs e)
        {
            UpdatetxtUpDnDelay();
        }

        private void UpdatetxtUpDnDelay()
        {
            // Check an item is selected
            if (vJoyManager.Config.CurrentControlSet.vJoyMapping==null)
                return;
            // Retrieve item and check only one exist
            if (int.TryParse(txtUpDnDelay.Text, out var value)) {
                vJoyManager.Config.CurrentControlSet.vJoyMapping.UpDownDelay_ms = value;
            }
            txtUpDnDelay.Text = vJoyManager.Config.CurrentControlSet.vJoyMapping.UpDownDelay_ms.ToString();

        }

    }
}
