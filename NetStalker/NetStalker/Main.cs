﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Windows.Forms;
using BrightIdeasSoftware;
using CSArp;
using MaterialSkin;
using MaterialSkin.Controls;
using MetroFramework;
using SharpPcap;
using Timer = System.Windows.Forms.Timer;

namespace NetStalker
{
    public partial class Main : MaterialForm, IView
    {
        private bool scanStarted;
        private bool arpState;
        public static bool operationinprogress;
        public string resizestate;
        public TextOverlay textOverlay;
        private bool resizeDone;
        public Loading loading;
        private bool SnifferStarted;
        private Timer ValuesTimer;
        private int timerCount;
        private List<Device> LoDevices;
        private List<Device> LODFB;
        private Controller _controller;
        private bool PopulateCalled;

        public Main()
        {
            InitializeComponent();

            _controller = new Controller(this);
            var materialSkinManager = MaterialSkinManager.Instance;
            materialSkinManager.AddFormToManage(this);
            notifyIcon1.ContextMenu = new ContextMenu(new MenuItem[]{new MenuItem("Show", (sender, args) =>
            {
                Show();
                WindowState = FormWindowState.Normal;
                notifyIcon1.Visible = false;

            }), new MenuItem("Exit",(sender, args) => { Application.Exit(); }),   });

            this.olvColumn1.ImageGetter = delegate (object rowObject) //Set the images from the image list (with setting small image list in properties)
            {
                Device s = (Device)rowObject;
                if (s.IsGateway)
                    return "router"; //Image name in the image list
                else
                    return "pc"; //Image name in the image list
            };
            this.olvColumn1.GroupKeyGetter = delegate (object rowObject) //Give every model object a key so items with the same key are grouped together
            {
                var Device = rowObject as Device;

                if (Device.IsGateway)
                {
                    return "Gateway";
                }
                else if (Device.IsLocalDevice)
                {
                    return "Own Device";
                }
                else
                {
                    return "Devices";
                }
            };
            this.olvColumn1.GroupKeyToTitleConverter = delegate (object key) { return key.ToString(); }; //Convert the key to a title for the groups
            fastObjectListView1.ShowGroups = true;

            textOverlay = this.fastObjectListView1.EmptyListMsgOverlay as TextOverlay;
            textOverlay.Font = new Font("Roboto", 25);
            ValuesTimer = new Timer();
            ValuesTimer.Interval = 1000;
            ValuesTimer.Tick += ValuesTimerOnTick;

        }

        public void PopulateDeviceList()
        {
            if (!PopulateCalled)
            {
                LoDevices = fastObjectListView1.Objects.Cast<Device>().ToList();
            }

        }

        private void ValuesTimerOnTick(object sender, EventArgs e)
        {
            timerCount++;
            foreach (Device Device in LoDevices)
            {
                if (Device.LimiterStarted)
                {
                    string D = ((float)Device.PacketsReceivedSinceLastReset * 0.0009765625f / (float)(this.ValuesTimer.Interval / 1000) / (float)this.timerCount).ToString();
                    string U = ((float)Device.PacketsSentSinceLastReset * 0.0009765625f / (float)(this.ValuesTimer.Interval / 1000) / (float)this.timerCount).ToString();

                    if (D.Length - D.IndexOf(".") > 1)
                    {
                        int num = -2 - D.IndexOf(".");
                        string str3 = D;
                        D = str3.Remove(str3.IndexOf(".") + 1, D.Length + num);
                    }
                    if (U.Length - U.IndexOf(".") > 1)
                    {
                        int num = -2 - U.IndexOf(".");
                        string str3 = U;
                        U = str3.Remove(str3.IndexOf(".") + 1, U.Length + num);
                    }
                    Device.DownloadSpeed = D + " KB/s";
                    Device.UploadSpeed = U + " KB/s";
                    fastObjectListView1.UpdateObject(Device);


                }
            }
            ResetPacketCount();
            timerCount = 0;
        }

        public void ResetPacketCount()
        {
            foreach (var device in LoDevices)
            {
                device.PacketsSentSinceLastReset = 0;
                device.PacketsReceivedSinceLastReset = 0;
            }
        }

        #region IView members
        public FastObjectListView ListView1
        {
            get
            {
                return fastObjectListView1;
            }
        }
        public MaterialLabel StatusLabel
        {
            get
            {
                return materialLabel2;
            }
        }
        public MaterialLabel StatusLabel2
        {
            get
            {
                return materialLabel3;
            }
        }
        public Form MainForm
        {
            get
            {
                return this;
            }
        }

        public PictureBox LoadingBar
        {
            get
            {
                return pictureBox2;
            }
        }

        public PictureBox PictureBox
        {
            get { return pictureBox1; }
        }

        public ToolTip TTip
        {
            get { return toolTip1; }
        }


        #endregion

        private void Main_Shown(object sender, EventArgs e)
        {
            NicSelection nicform = new NicSelection();
            nicform.ShowDialog();
        }

        private void materialFlatButton1_Click(object sender, EventArgs e)
        {

            if (!operationinprogress)
            {
                if (fastObjectListView1.GetItemCount() > 0)
                {
                    if (MetroMessageBox.Show(this, "The list will be cleared and a new scan will be initiated are you sure?\nNote: The Scan button is recommended when the list is empty, NetStalker always performs background scans for new devices after the initial scan.", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                    {
                        arpState = false;
                        scanStarted = true;
                        operationinprogress = true;
                        olvColumn7.MaximumWidth = 100;
                        olvColumn7.MinimumWidth = 100;
                        olvColumn7.Width = 100;
                        resizeDone = false;
                        materialLabel3.Text = "Working";
                        fastObjectListView1.EmptyListMsg = "Scanning...";
                        pictureBox1.Image = Properties.Resources.icons8_attention_96px;

                        var devices = fastObjectListView1.Objects.Cast<Device>().ToList();

                        foreach (var Device in devices)
                        {
                            if (Device.Redirected || Device.Blocked)
                            {
                                Device.Blocked = false;
                                Device.Redirected = false;
                                Device.RedirectorActive = false;
                                Device.BlockerActive = false;
                                Device.LimiterStarted = false;
                                Device.DownloadCap = 0;
                                Device.UploadCap = 0;
                                Device.DownloadSpeed = "";
                                Device.UploadSpeed = "";
                            }
                        }

                        new Thread(() =>
                        {
                            GetReady();
                            _controller.RefreshClients();


                        }).Start();
                    }
                }
                else
                {
                    arpState = false;
                    scanStarted = true;
                    operationinprogress = true;
                    olvColumn7.MaximumWidth = 100;
                    olvColumn7.MinimumWidth = 100;
                    olvColumn7.Width = 100;
                    resizeDone = false;
                    materialLabel3.Text = "Working";
                    fastObjectListView1.EmptyListMsg = "Scanning...";
                    pictureBox1.Image = Properties.Resources.icons8_attention_96px;

                    new Thread(() =>
                    {
                        GetReady();
                        _controller.RefreshClients();
                        operationinprogress = false;

                    }).Start();
                }
            }
            else
            {
                MetroMessageBox.Show(this, "A scan is still in progress please wait until its finished", "Info",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }


        }

        private void Main_Load(object sender, EventArgs e)
        {
            _controller.AttachOnExitEventHandler();

        }

        public class NoScanStartedException : Exception
        {

            public NoScanStartedException() : base() { }

            public NoScanStartedException(string message) : base(message) { }
        }

        public class NoDeviceSelectedException : Exception
        {

            public NoDeviceSelectedException() : base() { }

            public NoDeviceSelectedException(string message) : base(message) { }
        }

        public class ArpStateFalse : Exception
        {

            public ArpStateFalse() : base() { }

            public ArpStateFalse(string message) : base(message) { }
        }

        private void materialFlatButton4_Click(object sender, EventArgs e)
        {
            Options options = new Options();
            options.ShowDialog();
        }

        public void Main_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
                notifyIcon1.Visible = true;
                notifyIcon1.ShowBalloonTip(2);
            }

        }

        private void notifyIcon1_Click(object sender, EventArgs e)
        {
            Show();
            WindowState = FormWindowState.Normal;
            notifyIcon1.Visible = false;
        }

        private void metroTile1_Click(object sender, EventArgs e)
        {
            try
            {

                if (fastObjectListView1.SelectedObjects.Count == 0)
                {
                    throw new ArgumentException();
                }

                var Devices = fastObjectListView1.Objects.Cast<Device>().ToList();

                var selectedDevice = fastObjectListView1.SelectedObject as Device;

                if (!selectedDevice.Redirected && !(selectedDevice.IsGateway || selectedDevice.IsLocalDevice))
                {
                    throw new RedirectionNotActiveException();
                }

                foreach (var Device in Devices)
                {
                    if (Device.LimiterStarted)
                    {
                        if (MetroMessageBox.Show(this, "The Packet Sniffer can't function properly if the limiter is active, Stop the Limiter and start the Sniffer?\nNote: All limiting operations will be stopped", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                        {
                            break;
                        }
                        else
                        {
                            return;
                        }
                    }
                }

                new Thread(() =>
                {
                    foreach (var Device in Devices)
                    {
                        if (Device != selectedDevice)
                        {
                            Device.Blocked = false;
                            Device.Redirected = false;
                            Device.RedirectorActive = false;
                            Device.LimiterStarted = false;
                            Device.DownloadCap = 0;
                            Device.UploadCap = 0;
                            Device.DownloadSpeed = "";
                            Device.UploadSpeed = "";
                            fastObjectListView1.UpdateObject(Device);
                        }
                    }
                }).Start();

                ValuesTimer.Stop();
                selectedDevice.LimiterStarted = false;



                new Thread(() =>
                {
                    loading = new Loading();
                    loading.ShowDialog();

                }).Start();

                SnifferStarted = true;
                GetClientList.CloseAllCapturesForLimiter(this);
                Sniffer sniff = new Sniffer(selectedDevice.IP.ToString(), GetClientList.GetMACString(selectedDevice.MAC), GetClientList.GetMACString(GetGatewayMAC()), GetGatewayIP().ToString(), loading);//for the berkeley packet filter macs should have ':' separating each hex number
                sniff.ShowDialog(this);
                fastObjectListView1.SelectedObjects.Clear();
                sniff.Dispose();
                SnifferStarted = false;
                selectedDevice.Blocked = false;
                selectedDevice.Redirected = false;
                selectedDevice.RedirectorActive = false;
                selectedDevice.DownloadCap = 0;
                selectedDevice.UploadCap = 0;
                selectedDevice.DownloadSpeed = "";
                selectedDevice.UploadSpeed = "";
                fastObjectListView1.UpdateObject(selectedDevice);
                GetClientList.CalledFromSniffer = true;
                new Thread(() =>
                {
                    GetClientList.BackgroundScanStart(this, Properties.Settings.Default.friendlyname);

                }).Start();
            }
            catch (ArgumentException exception)
            {
                MetroMessageBox.Show(this, "Select a device first!", "Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch (OperationInProgressException ex)
            {
                MetroMessageBox.Show(this, "The Packet Sniffer can't be used while the limiter is active!", "Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                loading.BeginInvoke(new Action(() => { loading.Close(); }));
            }
            catch (RedirectionNotActiveException)
            {
                MetroMessageBox.Show(this, "Redirection must be active for this device!", "Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }

        }

        public void DisengageLimitingOperations()
        {
            var Devices = fastObjectListView1.Objects.Cast<Device>().ToList();
            foreach (var device in Devices)
            {
                device.Redirected = false;
                device.LimiterStarted = false;
            }

        }

        private void metroTile2_Click(object sender, EventArgs e)
        {
            try
            {

                if (fastObjectListView1.SelectedObjects.Count == 0)
                {
                    throw new ArgumentException();
                }

                var device = fastObjectListView1.SelectedObject as Device;

                if (device.IsGateway || device.IsLocalDevice)
                {
                    throw new Controller.LocalHostTargeted();
                }

                if (!device.Redirected)
                {
                    throw new RedirectionNotActiveException();
                }

                LimiterSpeed ls = new LimiterSpeed(device);
                if (ls.ShowDialog() == DialogResult.OK)
                {
                    if (device.LimiterStarted)
                    {
                        fastObjectListView1.UpdateObject(device);
                        ls.Dispose();
                    }
                    else
                    {
                        MetroMessageBox.Show(this, "Start redirection first!", "Error", MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }




            }
            catch (ArgumentException exception)
            {
                MetroMessageBox.Show(this, "Choose a device first and activate redirection for it!", "Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch (Controller.LocalHostTargeted)
            {
                MetroMessageBox.Show(this, "This operation can not target the gateway or your own ip address!", "Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch (RedirectionNotActiveException)
            {
                MetroMessageBox.Show(this, "Redirection must be active for this device!", "Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }

        }


        public static void GetReady()
        {
            if (string.IsNullOrEmpty(Properties.Settings.Default.AdapterName))
            {
                CaptureDeviceList capturedevicelist = CaptureDeviceList.Instance;
                ICaptureDevice capturedevice;
                capturedevicelist.Refresh();
                capturedevice = (from devicex in capturedevicelist where ((SharpPcap.WinPcap.WinPcapDevice)devicex).Interface.FriendlyName == NetStalker.Properties.Settings.Default.friendlyname select devicex).ToList()[0];
                Properties.Settings.Default.AdapterName = capturedevice.Name;
                Properties.Settings.Default.Save();

            }
        }

        private void Main_FormClosing(object sender, FormClosingEventArgs e)
        {
            var nic = Application.OpenForms["NicSelection"] as NicSelection;

            if (nic == null)
            {
                if (MetroMessageBox.Show(this, "Quit the application ?", "Quit", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.Cancel)
                {
                    e.Cancel = true;
                }
            }


        }

        private void FastObjectListView1_MouseDown(object sender, MouseEventArgs e)
        {
            var item = fastObjectListView1.GetItemAt(e.X, e.Y);
            if (item == null)
            {
                fastObjectListView1.ContextMenu = null;
                fastObjectListView1.SelectedObjects.Clear();
            }

        }

        private void FastObjectListView1_ItemsAdding(object sender, ItemsAddingEventArgs e)
        {
            if (fastObjectListView1.Items.Count >= 8 && !resizeDone)
            {
                olvColumn7.MaximumWidth = 83;
                olvColumn7.MinimumWidth = 83;
                olvColumn7.Width = 83;
                resizeDone = true;
            }
        }

        private void FastObjectListView1_SubItemChecking(object sender, SubItemCheckingEventArgs e)
        {
            try
            {
                if (SnifferStarted)//deal with the small hang on activation
                {
                    throw new OperationInProgressException();
                }


                fastObjectListView1.SelectObject(e.RowObject);
                Device device = e.RowObject as Device;
                if (device.IsGateway || device.IsLocalDevice)
                {
                    throw new Controller.GatewayTargeted();
                }
                device.GatewayMAC = GetGatewayMAC();
                device.GatewayIP = GetGatewayIP();
                device.LocalIP = GetLocalIP();

                if (e.NewValue == CheckState.Checked && e.Column.Index == 6 && !device.BlockerActive && !device.RedirectorActive)
                {
                    device.Blocked = true;
                    device.BlockerActive = true;
                    device.DeviceStatus = "Offline";
                    device.BlockOrRedirect();
                    fastObjectListView1.UpdateObject(device);
                    pictureBox1.Image = NetStalker.Properties.Resources.icons8_ok_96;
                }
                else if (e.NewValue == CheckState.Checked && e.Column.Index == 5 && !device.RedirectorActive && !device.BlockerActive)
                {

                    new Thread(() =>
                    {
                        loading = new Loading();
                        loading.ShowDialog();

                    }).Start();


                    device.Blocked = true;
                    device.Redirected = true;
                    device.RedirectorActive = true;
                    device.BlockOrRedirect();
                    GetReady();
                    device.LimiterStarted = true;
                    device.DownloadCap = 0;
                    device.UploadCap = 0;
                    LimiterClass LimitDevice = new LimiterClass(device);
                    if (!ValuesTimer.Enabled)
                    {
                        PopulateDeviceList();
                        PopulateCalled = true;
                        ValuesTimer.Start();
                    }
                    LimitDevice.StartLimiter();

                    loading.BeginInvoke(new Action(() => { loading.Close(); }));
                    pictureBox1.Image = NetStalker.Properties.Resources.icons8_ok_96;

                }
                else if (e.NewValue == CheckState.Unchecked && e.Column.Index == 6 && device.BlockerActive)
                {
                    device.Blocked = false;
                    device.BlockerActive = false;
                    device.DeviceStatus = "Online";
                    fastObjectListView1.UpdateObject(device);
                    PopulateForBlocker();
                    foreach (var Dev in LODFB)
                    {
                        if (Dev.Blocked)
                        {
                            return;
                        }
                    }
                    pictureBox1.Image = NetStalker.Properties.Resources.icons8_ok_96px;

                }
                else if (e.NewValue == CheckState.Unchecked && e.Column.Index == 5 && device.RedirectorActive)
                {

                    device.Blocked = false;
                    device.Redirected = false;
                    device.RedirectorActive = false;
                    device.LimiterStarted = false;
                    device.DownloadCap = 0;
                    device.UploadCap = 0;
                    device.DownloadSpeed = "";
                    device.UploadSpeed = "";
                    fastObjectListView1.UpdateObject(device);
                    foreach (Device Device in LoDevices)
                    {
                        if (Device.LimiterStarted)
                        {
                            return;
                        }
                    }
                    ValuesTimer.Stop();
                    PopulateCalled = false;
                    fastObjectListView1.UpdateObject(device);
                    pictureBox1.Image = NetStalker.Properties.Resources.icons8_ok_96px;

                }
                else
                {
                    e.Canceled = true;
                    e.NewValue = e.CurrentValue;
                }
            }
            catch (Controller.GatewayTargeted exception)
            {
                MetroMessageBox.Show(this, "This operation can not target the gateway or your own ip address!", "Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                e.Canceled = true;
            }
            catch (OperationInProgressException exception)
            {
                MetroMessageBox.Show(this, "The Speed Limiter can't be used while the sniffer is active!", "Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }


        }


        public void PopulateForBlocker()
        {
            LODFB = fastObjectListView1.Objects.Cast<Device>().ToList();
        }

        public PhysicalAddress GetGatewayMAC()
        {
            foreach (Device device in fastObjectListView1.Objects)
            {
                if (device.IsGateway)
                {
                    return device.MAC;
                }
            }

            return default;
        }

        public IPAddress GetGatewayIP()
        {
            foreach (Device device in fastObjectListView1.Objects)
            {
                if (device.IsGateway)
                {
                    return device.IP;
                }
            }

            return default;
        }

        public IPAddress GetLocalIP()
        {
            foreach (Device device in fastObjectListView1.Objects)
            {
                if (device.IsLocalDevice)
                {
                    return device.IP;
                }
            }

            return default;
        }

        private void MaterialFlatButton3_Click_1(object sender, EventArgs e)
        {
            AboutForm af = new AboutForm();
            af.ShowDialog();
        }

        private void ToolTip1_Draw(object sender, DrawToolTipEventArgs e)
        {
            e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(51, 51, 51)), e.Bounds);//background color e.bounds

            e.Graphics.DrawRectangle(new Pen(Color.FromArgb(204, 204, 204), 1), new Rectangle(e.Bounds.X, e.Bounds.Y,
                e.Bounds.Width - 1, e.Bounds.Height - 1));//the white bounds

            e.Graphics.DrawString(e.ToolTipText, new Font("Roboto", 9), new SolidBrush(Color.FromArgb(204, 204, 204)), e.Bounds.X + 8, e.Bounds.Y + 7); //text with image location

        }

        private void ToolTip1_Popup(object sender, PopupEventArgs e)
        {
            e.ToolTipSize = new Size(e.ToolTipSize.Width - 7, e.ToolTipSize.Height - 5);
        }

        private void MaterialFlatButton2_Click(object sender, EventArgs e)
        {
            MetroMessageBox.Show(this, "Some guidelines on how to use this software properly:\n\n1- In order to use the Packet Sniffer you have to activate redirection for the selected device first. Note: For the Packet Sniffer to work properly, redirection and speed limitation will be deactivated for all but the selected device.\n2- In order to use the Speed Limiter you have to activate redirection for the selected device, once activated it will start redirecting packets for the selected device with no speed limitation, then you can open the speed limiter (on the bottom right) and set the desired speed for each device (0 means no limitation).\n3- Blocking and redirection can not be activated at the sametime, you either block a device or limit its speed.\n4- It's recommended for most stability to wait until the scanner is done before performing any action.\n5- NetStalker can be protected with a password, and can be set or removed via Options.\n6- NetStalker is available in dark and light modes.\n7- NetStalker has an option for spoof protection, if activated it can prevent your pc from being redirected or blocked by the same tool or any other spoofing software.\n8- Background scanning is always active so you don't have to consistently press scan to discover newly connected devices.", "Help", MessageBoxButtons.OK,
                MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, 375);
        }

        private void MaterialFlatButton5_Click(object sender, EventArgs e)
        {
            GetClientList.StopTheLoadingBar(this);
        }


    }

    public class OperationInProgressException : Exception
    {
        public OperationInProgressException() : base() { }

        public OperationInProgressException(string message) : base(message) { }

    }


    public class RedirectionNotActiveException : Exception
    {
        public RedirectionNotActiveException() : base() { }

        public RedirectionNotActiveException(string message) : base(message) { }

    }

    public class LimiterActiveException : Exception
    {
        public LimiterActiveException() : base() { }

        public LimiterActiveException(string message) : base(message) { }

    }


}