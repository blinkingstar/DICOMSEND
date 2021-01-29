using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Dicom;
using Dicom.Network;
using Dicom.Network.Client; 
using DicomClient = Dicom.Network.Client.DicomClient;
using MSAPI = Microsoft.WindowsAPICodePack;

namespace DICOMSEND
{
    public partial class Form1 : Form
    {

        //Processオブジェクトを作成
        System.Diagnostics.Process p = new System.Diagnostics.Process();
        int totalfiles = 0;
        int successfulfiles = 0;
        CancellationTokenSource tokenSource;
        public Form1()
        {
            InitializeComponent();
            Environment.SetEnvironmentVariable("PATH", System.AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\') + "\\bin" + @";" + Environment.GetEnvironmentVariable("Path"));
            this.Load += Form1_Load;
            this.FormClosing += Form1_FormClosing;
        }
        private async void Upload_Click(object sender, EventArgs e)
        {
            if (!Directory.Exists(FolderPathBox.Text))
            {
                MessageBox.Show("Folder doesn't exist");
                return;
            }

            UploadButton.Enabled = false;
            totalfiles = 0;
            successfulfiles = 0;
            ConsoleBox.Text = "Start uploading...";
            var skippattern = new Regex(@"(?:DICOMDIR|\.(?:jpg|jpeg|png|gif|exe|inf|htm|html|js|bat|dll|css|ini|txt|doc|pdf))$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

            string AET = AETBox.Text;
            if (AET == null || AET.Length == 0)
            {
                AET = @"STORESCU";
            }
            string AEC = AECBox.Text;
            if (AEC == null || AEC.Length == 0)
            {
                AEC = @"STORESCU";
            }
            SaveSettings();
            settextCount();

            var client = new DicomClient(HostBox.Text, int.Parse(PortBox.Text), false, AET, AEC);

            tokenSource = new CancellationTokenSource();
            var cancelToken = tokenSource.Token;

            string[] files = Directory.GetFiles(FolderPathBox.Text, "*", SearchOption.AllDirectories);
            
            foreach (string filename in files)
            {
                //if (count > 2)
                //{
                //    break;
                //}

                if (skippattern.IsMatch(filename))
                {
                    continue;
                }
                if (!DicomFile.HasValidHeader(filename))
                {
                    WriteLog(filename + ": not valid");
                    addTotal();
                    settextCount();
                    continue;
                }

                DicomFile file;
                try
                {
                    file = DicomFile.Open(filename, Encoding.Unicode);

                }
                catch (Exception error)
                {
                    WriteLog(filename + ": opening error");
                    addTotal();
                    settextCount();
                    continue;
                }
                if (file == null)
                {
                    WriteLog(filename + ": file is null");
                    addTotal();
                    settextCount();
                    continue;
                }
                if (file.IsPartial)
                {
                    WriteLog(filename + ": is partial");
                    addTotal();
                    settextCount();
                    continue;
                }
                if (file.Format != DicomFileFormat.DICOM3)
                {
                    WriteLog(filename + ": not valid");
                    addTotal();
                    settextCount();
                    continue;
                }
                if (!file.Dataset.Contains(DicomTag.PixelData))
                {
                    WriteLog(filename + ": not image file");
                    addTotal();
                    settextCount();
                    continue;
                }

                DicomDataset dicomDataset;

                // Debug.WriteLine(file.File.Exists);
                if (IDBox.Text != null && IDBox.Text.Length != 0)
                {
                    dicomDataset = file.Dataset;
                    dicomDataset.AddOrUpdate(DicomTag.PatientID, IDBox.Text);
                    var temp = Dicom.IO.TemporaryFile.Create();
                    await file.SaveAsync(temp.Name);
                }
                var request = new DicomCStoreRequest(file);
                var res = request.OnResponseReceived += (req, response) =>
                {
                    if (response.Status == DicomStatus.Success)
                    {
                        addSuccess();
                        settextCount();
                    }
                    else
                    {
                        addTotal();
                        settextCount();
                    }
                    WriteLog("Uploading " + filename + ": " + response.Status);

                };
                await client.AddRequestAsync(request);
            }
            // await client.SendAsync(cancelToken, DicomClientCancellationMode.ImmediatelyReleaseAssociation);
            await client.SendAsync();
            //if (cancelToken.IsCancellationRequested)
            //{
            //    Debug.WriteLine("Cancelled");
            //    UploadButton.IsEnabled = true;
            //    AbortButton.IsEnabled = false;
            //    WriteLog("Aborted");
            //}

            UploadButton.Enabled = true;
            WriteLog("...Uploaded");
            MessageBox.Show("Uploaded");

            // Console.ReadKey();
        }
        private void WriteLog(string log)
        {
            Task.Run(() =>
            {
                Action act = () =>
                {
                    ConsoleBox.AppendText(Environment.NewLine + log);
                };
                BeginInvoke(act);
            });
            Console.WriteLine(log);
        }

        private void settextCount()
        {
            Task.Run(() =>
            {
                Action act = () =>
                {
                    LabelCount.Text = (successfulfiles + @" / " + totalfiles + " files were uploaded");
                };
                BeginInvoke(act);
            });
        }
        private void addSuccess()
        {
            successfulfiles++;
            totalfiles++;
        }
        private void addTotal()
        {
            totalfiles++;
        }


        private void Form1_Load(object sender, EventArgs e)
        {
            Console.WriteLine(@"Path: " + Environment.GetEnvironmentVariable("PATH"));
            Properties.Settings.Default.Upgrade();
            //  MessageBox.Show(@"Folder Path: " + Properties.Settings.Default.FolderPathString);
            this.HostBox.Text = Properties.Settings.Default.HostString;
            this.PortBox.Text = Properties.Settings.Default.PortString.ToString();
            this.FolderPathBox.Text = Properties.Settings.Default.FolderPathString;
            this.AETBox.Text = Properties.Settings.Default.AETString;
            this.AECBox.Text = Properties.Settings.Default.AECString;

        }

        private void SaveSettings()
        {
            if (this.HostBox.Text != null && this.HostBox.Text.Length != 0)
            {
                Properties.Settings.Default.HostString = this.HostBox.Text;
            }
            if (this.PortBox.Text != null && this.PortBox.Text.Length != 0)
            {
                Properties.Settings.Default.PortString = int.Parse(this.PortBox.Text);
            }
            if (this.FolderPathBox != null && this.FolderPathBox.Text.Length != 0)
            {
                Properties.Settings.Default.FolderPathString = this.FolderPathBox.Text;
            }

            if (this.AETBox != null && this.AETBox.Text.Length != 0)
            {
                Properties.Settings.Default.AETString = this.AETBox.Text;
            }
            if (this.AECBox != null && this.AECBox.Text.Length != 0)
            {
                Properties.Settings.Default.AECString = this.AECBox.Text;
            }
            Properties.Settings.Default.Save();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveSettings();
        }
        private void Form1_Load_1(object sender, EventArgs e)
        {

        }

        private void FolderButton_Click(object sender, EventArgs e)
        {

            var dlg = new MSAPI::Dialogs.CommonOpenFileDialog();

            // フォルダ選択ダイアログ（falseにするとファイル選択ダイアログ）
            dlg.IsFolderPicker = true;
            // タイトル
            dlg.Title = "Choose folder";
            // 初期ディレクトリ
            dlg.InitialDirectory = FolderPathBox.Text;

            if (dlg.ShowDialog() == MSAPI::Dialogs.CommonFileDialogResult.Ok)
            {
                FolderPathBox.Text = dlg.FileName;
                // MessageBox.Show($"{dlg.FileName}が選択されました。");
            }
        }
    }
}
