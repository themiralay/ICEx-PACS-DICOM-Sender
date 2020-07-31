using System;
using System.Threading;
using System.Windows.Forms;
using Dicom.Network.Client;
using Dicom.Network;
using Dicom.Data;

namespace pacstestt
{
    public delegate void BoolDelegate(bool state);
    public delegate void MessageBoxDelegate(string message, string caption, bool isError);

    public partial class Form1 : Form
    {
        private DicomTransferSyntax[] TransferSyntaxes;
        private string ConfigPath = null;
        private MyConfig Config = null;
        public Form1()
        {
            InitializeComponent();
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(RunDicomEcho));
        }

        private void RunDicomEcho(object state)
        {
            bool success = false;
            string msg = "Hata!";
            try
            {
                CEchoClient scu = new CEchoClient();
                scu.CallingAE =textBox1.Text;
                scu.CalledAE = textBox1.Text;
                scu.MaxPduSize = (uint)numericUpDown2.Value;
                scu.ConnectTimeout = (int)numericUpDown3.Value;
                scu.SocketTimeout = (int)numericUpDown3.Value;
                scu.DimseTimeout = (int)numericUpDown3.Value;
                scu.OnCEchoResponse += delegate (byte presentationId, ushort messageId, DcmStatus status) {
                    msg = status.ToString();
                };
                scu.Connect(textBox2.Text, (int)numericUpDown1.Value, false ? DcmSocketType.TLS : DcmSocketType.TCP);
                success = scu.Wait();

                if (!success)
                    msg = scu.ErrorMessage;
            }
            catch (Exception ex)
            {
                msg = ex.Message;
            }

            Invoke(new MessageBoxDelegate(ShowMessageBox), msg, "DICOM PACS Baglanti", !success);
            Invoke(new BoolDelegate(ToggleEchoButtons), true);
        }
        public void ShowMessageBox(string message, string caption, bool isError)
        {
            MessageBox.Show(this, message, caption, MessageBoxButtons.OK, isError ? MessageBoxIcon.Error : MessageBoxIcon.Information);
        }
        private void ToggleEchoButtons(bool state)
        {
            button1.Enabled = state;
        }
        [Serializable]
        public class MyConfig
        {
            public string LocalAE = "ENES";
            public string RemoteAE = "ENES";
            public string RemoteHost = "192.168.1.146";
            public int RemotePort = 104;
            public uint MaxPdu = 16384;
            public int Timeout = 30;
            public int TransferSyntax = 0;
            public int Quality = 90;
            public bool TLS = false;
        }

        private void Button2_Click(object sender, EventArgs e)
        {
            OpenFileDialog fd = new OpenFileDialog();
            fd.Multiselect = true;
            if (fd.ShowDialog(this) == DialogResult.OK)
            {
                foreach (string filename in fd.FileNames)
                {
                    try
                    {
                        CStoreRequestInfo info = new CStoreRequestInfo(filename);

                        ListViewItem item = new ListViewItem(filename, 0);
                        item.SubItems.Add(info.SOPClassUID.Description);
                        item.SubItems.Add(info.TransferSyntax.UID.Description);
                        item.SubItems.Add(info.Status.Description);

                        item.Tag = info;
                        info.UserState = item;

                        listView1.Items.Add(item);
                    }
                    catch { }
                }
            }
        }

        private void Button4_Click(object sender, EventArgs e)
        {
            Durum(false);


            CStoreClient scu = new CStoreClient();
            scu.DisableFileStreaming = true;
            scu.CallingAE = textBox1.Text;
            scu.CalledAE = textBox1.Text;
            scu.MaxPduSize = (uint)numericUpDown2.Value;
            scu.ConnectTimeout = (int)numericUpDown3.Value;
            scu.SocketTimeout = (int)numericUpDown3.Value;
            scu.DimseTimeout = (int)numericUpDown3.Value;
            scu.SerializedPresentationContexts = true;
            //scu.PreferredTransferSyntax = TransferSyntaxes[null];

            //if (scu.PreferredTransferSyntax == DicomTransferSyntax.JPEGProcess1 ||
            //    scu.PreferredTransferSyntax == DicomTransferSyntax.JPEGProcess2_4) {
            //    DcmJpegParameters param = new DcmJpegParameters();
            //    param.Quality = Config.Quality;
            //    scu.PreferredTransferSyntaxParams = param;
            //}
            //else if (scu.PreferredTransferSyntax == DicomTransferSyntax.JPEG2000Lossy) {
            //    DcmJpeg2000Parameters param = new DcmJpeg2000Parameters();
            //    param.Rate = Config.Quality;
            //    scu.PreferredTransferSyntaxParams = param;
            //}

            scu.OnCStoreResponseReceived = delegate (CStoreClient client, CStoreRequestInfo info) {
                Invoke(new CStoreRequestCallback(GonderInfo), client, info);
            };

            foreach (ListViewItem lvi in listView1.Items)
            {
                lvi.ImageIndex = 0;
                lvi.SubItems[3].Text = "Pending";

                CStoreRequestInfo info = (CStoreRequestInfo)lvi.Tag;
                scu.AddFile(info);
            }

            ThreadPool.QueueUserWorkItem(new WaitCallback(DicomSender), scu);
        }
        private void DicomSender(object state)
        {
            CStoreClient scu = (CStoreClient)state;
            scu.Connect(textBox2.Text, (int)numericUpDown1.Value, checkBox1.Checked ? DcmSocketType.TLS : DcmSocketType.TCP);
            if (!scu.Wait())
                Invoke(new MessageBoxDelegate(ShowMessageBox), scu.ErrorMessage, "DICOM C-Store Error", true);

            Invoke(new BoolDelegate(Durum), true);
        }
        private void Durum(bool state)
        {
            button4.Enabled = state;
            button3.Enabled = state;
            button2.Enabled = state;
        }
        private void GonderInfo(CStoreClient client, CStoreRequestInfo info)
        {
            ListViewItem lvi = (ListViewItem)info.UserState;
            lvi.ImageIndex = (info.Status == DcmStatus.Success) ? 1 : 2;
            lvi.SubItems[3].Text = info.Status.Description;
            listView1.EnsureVisible(lvi.Index);
        }
    }
}
