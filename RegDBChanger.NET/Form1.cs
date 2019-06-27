using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Windows.Forms;

namespace RegDBChanger.NET
{
    public partial class frmMain : Form
    {
        public frmMain()
        {
            InitializeComponent();
            Constants.colMRU = new List<string>();
            GetMRUList();
        }

        private void btnChange_Click(object sender, EventArgs e)
        {
            //validate backup folder 
            if (chkBackupReg.Checked)
            {
                string filePath = txtBackupLocation.Text.Trim();
                if (filePath == null || filePath == "")
                {
                    MessageBox.Show(this, "Please select backup folder.", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    txtBackupLocation.Focus();
                    return;
                }
                else
                {
                    //check if backup folder is valid
                    if (!Directory.Exists(filePath))
                    {
                        //prompt to create, if user declines, present error message
                        if (MessageBox.Show(this, "The backup folder '" + filePath + "' does not exist. Create?", this.Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        {
                            try
                            {
                                System.IO.Directory.CreateDirectory(filePath);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(this, ex.Message, this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                                txtBackupLocation.SelectAll();
                                txtBackupLocation.Focus();
                                return;
                            }
                        }
                        else
                        {
                            MessageBox.Show(this, "The backup folder location is invalid.", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                            txtBackupLocation.SelectAll();
                            txtBackupLocation.Focus();
                            return;
                        }
                    }
                }
            }

            // validate new database name
            string newDBName = cboDBName.Text.Trim().ToString();
            cboDBName.Text = newDBName;
            if (newDBName == "" || newDBName == null)
            {
                MessageBox.Show(this, "Specify new database server name.", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                cboDBName.Focus();
                return;
            }

            // prompt go, no-go for the change
            if (MessageBox.Show(this, "The database source in the registry for DCA will be changed to '" + newDBName + "'. Continue?", this.Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                string workRegKeyDCA;
                string workRegKeyDCASec;
                string workRegKeyMCM;
                if (Environment.Is64BitOperatingSystem)
                {
                    workRegKeyDCA = Constants.PALION_DCADB_REG64;
                    workRegKeyDCASec = Constants.PALION_DCASECADMINDB_REG64;
                    workRegKeyMCM = Constants.PALION_MCMDB_REG64;
                }
                else
                {
                    workRegKeyDCA = Constants.PALION_DCADB_REG32;
                    workRegKeyDCASec = Constants.PALION_DCASECADMINDB_REG32;
                    workRegKeyMCM = Constants.PALION_MCMDB_REG32;
                }


                ChangeDBServer(workRegKeyDCA, newDBName, txtBackupLocation.Text.ToString());
                ChangeDBServer(workRegKeyDCASec, newDBName, txtBackupLocation.Text.ToString());
                ChangeDBServer(workRegKeyMCM, newDBName, txtBackupLocation.Text.ToString());
                RearrangeMRU(newDBName);
                MessageBox.Show(this, "Database change successful.", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            }
        }

        private void chkBackupReg_CheckedChanged(object sender, EventArgs e)
        {
            if (chkBackupReg.Checked)
            {
                grpBackupLoc.Enabled = true;
                txtBackupLocation.Focus();
            }
            else
            {
                grpBackupLoc.Enabled = false;
                txtBackupLocation.ResetText();
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            base.Close();
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            folderBrowserDialog1.ShowDialog();
            if (folderBrowserDialog1.SelectedPath != null) {
                txtBackupLocation.Text = folderBrowserDialog1.SelectedPath;
                
            }
        }

        private void ChangeDBServer(string regKey, string dbName, string filePath)
        {
            RegistryKey keyPalion = Registry.LocalMachine.OpenSubKey(regKey, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.FullControl);
            if (keyPalion != null)
            {
                string oldDBName = Constants.colMRU.ElementAt(0).ToString();
                if (chkBackupReg.Checked)
                {
                    string fixedDBName = oldDBName.Replace("\\", "-");

                    switch (regKey)
                    {
                        case Constants.PALION_DCADB_REG64:
                        case Constants.PALION_DCADB_REG32:
                            filePath = filePath + Constants.DCA_REG_FILE_BKUP + fixedDBName + ".txt";
                            break;
                        case Constants.PALION_DCASECADMINDB_REG64:
                        case Constants.PALION_DCASECADMINDB_REG32:
                            filePath = filePath + Constants.DCA_SECADMIN_FILE_BKUP + fixedDBName + ".txt";
                            break;
                        case Constants.PALION_MCMDB_REG64:
                        case Constants.PALION_MCMDB_REG32:
                            filePath = filePath + Constants.MCM_REG_FILE_BKUP + fixedDBName + ".txt";
                            break;
                        default:
                            break;
                    }

                    string regKeyPath = "";
                    regKeyPath = keyPalion.ToString();
                    if (regKeyPath != "")
                    {
                        CreateRegBackup(regKeyPath, filePath);
                    }
                }
                string[] valueNames = keyPalion.GetValueNames();
                foreach (string valueName in valueNames)
                {
                    string oldKeyValue = keyPalion.GetValue(valueName).ToString();
                    string newKeyValue = ParseKeyValueAndChange(oldKeyValue, dbName);
                    keyPalion.SetValue(valueName, newKeyValue);
                }
                keyPalion.Close();
            }
        }

        private void CreateRegBackup(string regKey, string savePath)
        {
            string path = "\"" + savePath + "\"";
            string key = "\"" + regKey + "\"";
            Process proc = new Process();
            try
            {
                proc.StartInfo.FileName = "regedit.exe";
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.Verb = "runas";
                proc = Process.Start("regedit.exe", ("/e " + path + " " + key) ?? "");
                proc?.WaitForExit();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,ex.Message.ToString(), this.Text);
            }
            finally
            {
                proc?.Dispose();
            }
        }

        private void GetMRUList()
        {
            RegistryKey keyRegDBChanger = Registry.CurrentUser.OpenSubKey(Constants.THISAPPREGKEY, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.FullControl);
            if (keyRegDBChanger != null)
            {
                for (int j = 0; j < 10; j++)
                {
                    Constants.colMRU.Add(keyRegDBChanger.GetValue("MRU" + j).ToString());
                }
            }
            else
            {
                RegistrySecurity rs = new RegistrySecurity();
                string user = Environment.UserDomainName + @"\" + Environment.UserName;
                rs.AddAccessRule(new RegistryAccessRule(user, RegistryRights.FullControl, InheritanceFlags.None, PropagationFlags.None, AccessControlType.Allow));
                keyRegDBChanger = Registry.CurrentUser.CreateSubKey(Constants.THISAPPREGKEY, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryOptions.None, rs);

                for (int i = 0; i < 10; i++)
                {
                    string keyValue = "";
                    if (i == 0)
                    {
                        // try and read the current DCA DB value and set this as the default first entry
                        RegistryKey keyPalion = Registry.LocalMachine.OpenSubKey(Constants.PALION_DCASECADMINDB_REG64, RegistryKeyPermissionCheck.ReadSubTree, RegistryRights.ReadKey);

                        string[] valueNames = keyPalion.GetValueNames();
                        string dbKeyValue = keyPalion.GetValue(valueNames[0]).ToString();
                        keyPalion.Close();

                        char[] delimSemicolon = new char[1] { ';' };
                        char[] delimEqual = new char[1] { '=' };

                        string[] connStringElements = dbKeyValue.Split(delimSemicolon);

                        foreach (string connStringElement in connStringElements)
                        {
                            string[] pairedKeyValues = connStringElement.Split(delimEqual);
                            if(pairedKeyValues[0].ToString().ToUpper() == Constants.DATASRCKEYWORD)
                            {
                                keyValue = pairedKeyValues[1].ToString();
                                break;
                            }
                        }
                        
                        keyRegDBChanger.SetValue("MRU" + i, keyValue);
                    }
                    else
                    {
                        keyValue = "";
                        keyRegDBChanger.SetValue("MRU" + i, "");
                    }
                    Constants.colMRU.Add(keyValue);
                }
            }
            keyRegDBChanger.Close();
            ReloadMRUList();
        }


        private string ParseKeyValueAndChange(string keyValue, string newDBName)
        {
            char[] delimSemicolon = new char[1] {';'};
            char[] delimEqual = new char[1] {'='};

            string[] connStringElements = keyValue.Split(delimSemicolon);
            int elementCount = connStringElements.Count();
            for (int i = 0; i < elementCount; i++)
            {
                string[] nameValuePair = connStringElements[i].Split(delimEqual);
                if (nameValuePair[0].ToUpper().ToString() == Constants.DATASRCKEYWORD || nameValuePair[0].ToUpper().ToString() == Constants.SERVERKEYWORD)
                {
                    nameValuePair[1] = newDBName.ToString();
                    string newValuePair = string.Join("=", nameValuePair);
                    connStringElements[i] = newValuePair.ToString();
                }
            }
            return string.Join(";", connStringElements);
        }

        private void RearrangeMRU(string dbName)
        {
            int colIndex = Constants.colMRU.IndexOf(dbName);
            if (colIndex > -1)
            {
                Constants.colMRU.RemoveAt(colIndex);
            }
            Constants.colMRU.Insert(0, dbName);
            if (Constants.colMRU.Count > 10)
            {
                Constants.colMRU.RemoveAt(9);
            }
            ReloadMRUList();
        }


        private void ReloadMRUList()
        {
            cboDBName.Items.Clear();
            foreach (string item in Constants.colMRU)
            {
                if (item.ToString() != "")
                {
                    cboDBName.Items.Add(item.ToString());
                }
            }
            cboDBName.SelectedIndex = 0;
        }

        private void SaveMRU()
        {
            RegistryKey keyRegDBChanger = Registry.CurrentUser.OpenSubKey(Constants.THISAPPREGKEY, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.FullControl);
            int counter = 0;
            foreach (string item in Constants.colMRU)
            {
                keyRegDBChanger.SetValue("MRU" + counter, item.ToString());
                counter++;
            }
            keyRegDBChanger.Close();
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveMRU();
        }

    }

    static class Constants
    {
        public const string PALION_DCADB_REG64 = @"SOFTWARE\Wow6432Node\Palion Pty Ltd\DCA\1.0\Database";
        public const string PALION_DCASECADMINDB_REG64 = @"SOFTWARE\Wow6432Node\Palion Pty Ltd\DCASecAdmin\1.0\Database";
        public const string PALION_MCMDB_REG64 = @"SOFTWARE\Wow6432Node\Palion Pty Ltd\MCM\1.0\Database";
        public const string PALION_DCACORE_REG64 = @"SOFTWARE\Wow6432Node\Palion Pty Ltd\DCA\1.0\Core";
        public const string PALION_DCASECADMINCORE_REG64 = @"SOFTWARE\Wow6432Node\Palion Pty Ltd\DCASecAdmin\1.0\Core";
        public const string PALION_MCMCORE_REG64 = @"SOFTWARE\Wow6432Node\Palion Pty Ltd\MCM\1.0\Core";

        public const string PALION_DCADB_REG32 = @"SOFTWARE\Palion Pty Ltd\DCA\1.0\Database";
        public const string PALION_DCASECADMINDB_REG32 = @"SOFTWARE\Palion Pty Ltd\DCASecAdmin\1.0\Database";
        public const string PALION_MCMDB_REG32 = @"SOFTWARE\Palion Pty Ltd\MCM\1.0\Database";
        public const string PALION_DCACORE_REG32 = @"SOFTWARE\Palion Pty Ltd\DCA\1.0\Core";
        public const string PALION_DCASECADMINCORE_REG32 = @"SOFTWARE\Palion Pty Ltd\DCASecAdmin\1.0\Core";
        public const string PALION_MCMCORE_REG32 = @"SOFTWARE\Palion Pty Ltd\MCM\1.0\Core";

        public const string THISAPPREGKEY = @"SOFTWARE\GBST\RegDBChanger";
        public const int MAX_MRU_ENTRIES = 10;
        public const string DATASRCKEYWORD = "DATA SOURCE";
        public const string SERVERKEYWORD = "SERVER";
        public static List<string> colMRU = new List<string>();

        public const string DCA_REG_FILE_BKUP = @"\DCARegBackup_";
        public const string DCA_SECADMIN_FILE_BKUP = @"\DCASecAdminRegBackup_";
        public const string MCM_REG_FILE_BKUP = @"\MCMRegBackup_";
    }   
}