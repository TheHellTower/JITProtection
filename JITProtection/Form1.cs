using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace JITProtection
{
    public partial class Form1 : Form
    {
        static ModuleDefMD Module = null;
        static List<string> JITMethods = new List<string>();
        static List<string> TempMethods = new List<string>();
        static bool x64 = false;
        public Form1()
        {
            InitializeComponent();

            this.DragDrop += new DragEventHandler((sender, e) => {
                string FilePath = ((string[])e.Data.GetData(DataFormats.FileDrop, false))[0];
                if (FilePath.EndsWith("exe") || FilePath.EndsWith("dll"))
                    guna2TextBox1.Text = FilePath;
                THTCheckAll.Checked = false;
            });

            this.DragEnter += new DragEventHandler((sender, e) => {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                    e.Effect = DragDropEffects.Copy;
                else
                    e.Effect = DragDropEffects.None;
            });

            TheHellTower.ItemCheck += THT_ItemCheck;
        }

        private void THT_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (THTCheckAll.Checked)
                THTCheckAll.Checked = false;
        }

        private void guna2TextBox1_TextChanged(object sender, EventArgs e)
        {
            try
            {
                if (JITMethods.Count != 0)
                    JITMethods.Clear();
                //Test the file
                Module = ModuleDefMD.Load(guna2TextBox1.Text);
                Setup();
                Module.Dispose();
                THTCheckAll.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Invalid File");
                MessageBox.Show(ex.ToString());
                guna2TextBox1.Text = "Drag & Drop File";
            }
        }

        private void guna2Button1_Click(object sender, EventArgs e)
        {
            Module = ModuleDefMD.Load(guna2TextBox1.Text);
            x64 = Module.Is32BitPreferred || Module.Is32BitRequired;

            for (int i = 0; i < TheHellTower.Items.Count; i++)
                if (TheHellTower.GetItemChecked(i))
                    JITMethods.Add(TheHellTower.Items[i].ToString());

            File.WriteAllBytes(Module.Location.Insert(Module.Location.Length - 4, "-JIT"), JIT.Execute(Module, !x64, JITMethods));

            Module.Dispose();
        }

        private void Setup()
        {
            #region "Clear Lists"
            if (TheHellTower.Items.Count != 0)
                TheHellTower.Items.Clear();
            if (TempMethods.Count != 0)
                TempMethods.Clear();
            #endregion

            #region "Setup Lists"
            foreach (TypeDef type in Module.GetTypes().Where(T => T.HasMethods).ToArray())
            {
                foreach (MethodDef method in type.Methods.Where(M => M.HasBody && M.Body.HasInstructions && M.Body.Instructions.Count() > 1 && !M.IsConstructor).ToArray())
                {
                    if (method.HasCustomAttributes)
                    {
                        bool hasJIT = false;
                        foreach (CustomAttribute attribute in method.CustomAttributes)
                            if (attribute.TypeFullName == "System.Reflection.ObfuscationAttribute")
                                foreach (var property in attribute.Properties)
                                    if (property.Name == "Feature" && property.Type.FullName == "System.String" && (property.Value.ToString().Equals("JIT")))
                                        hasJIT = true;
                        TheHellTower.Items.Add(method.FullName.ToString(), hasJIT);
                    }
                    else
                    {
                        TheHellTower.Items.Add(method.FullName.ToString(), false);
                    }
                    TempMethods.Add(method.FullName);
                }
            }
            #endregion
        }

        private void THTCheckAll_CheckedChanged(object sender, EventArgs e)
        {
            bool isChecked = THTCheckAll.Checked;
            for (int i = 0; i < TheHellTower.Items.Count; i++)
                TheHellTower.SetItemChecked(i, isChecked);

            THTCheckAll.Checked = isChecked;
        }

        private void guna2TextBox2_TextChanged(object sender, EventArgs e)
        {
            string text = this.guna2TextBox2.Text;
            if (!string.IsNullOrWhiteSpace(text))
            {
                CheckedListBox.CheckedItemCollection checkedItems = this.TheHellTower.CheckedItems;
                for (int i = this.TheHellTower.Items.Count - 1; i >= 0; i--)
                {
                    bool flag = false;
                    foreach (object obj in checkedItems)
                        if (this.TheHellTower.Items[i].Equals(obj))
                            flag = true;

                    if (!flag)
                        this.TheHellTower.Items.RemoveAt(i);
                }
                using (List<string>.Enumerator THT = TempMethods.GetEnumerator())
                {
                    while (THT.MoveNext())
                    {
                        string text2 = THT.Current;
                        if (text2.ToUpper().Contains(text.ToUpper()) && !this.TheHellTower.Items.Contains(text2))
                            this.TheHellTower.Items.Add(text2);
                    }
                    return;
                }
            }
            foreach (string text3 in TempMethods)
                if (!this.TheHellTower.Items.Contains(text3))
                    this.TheHellTower.Items.Add(text3);
        }
    }
}