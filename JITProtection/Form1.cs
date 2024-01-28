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
        static bool x64 = false;
        public Form1()
        {
            InitializeComponent();

            this.DragDrop += new DragEventHandler((sender, e) => {
                string FilePath = ((string[])e.Data.GetData(DataFormats.FileDrop, false))[0];
                if (FilePath.EndsWith("exe") || FilePath.EndsWith("dll"))
                    guna2TextBox1.Text = FilePath;
            });

            this.DragEnter += new DragEventHandler((sender, e) => {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                    e.Effect = DragDropEffects.Copy;
                else
                    e.Effect = DragDropEffects.None;
            });
        }

        private void guna2TextBox1_TextChanged(object sender, EventArgs e)
        {
            try
            {
                if (JITMethods.Count != 0)
                    JITMethods.Clear();
                //Test the file
                Module = ModuleDefMD.Load(guna2TextBox1.Text);
                DetectJITAttributeAndFill();
                Module.Dispose();
            } catch (Exception ex)
            {
                MessageBox.Show("Invalid File");
                MessageBox.Show(ex.ToString());
                guna2TextBox1.Text = "Drag & Drop File";
            }
        }

        private void guna2Button1_Click(object sender, EventArgs e)
        {
            Module = ModuleDefMD.Load(guna2TextBox1.Text);
            DetectJITAttributeAndDelete();
            x64 = Module.Is32BitPreferred || Module.Is32BitRequired;

            for (int i = 0; i < TheHellTower.Items.Count; i++)
                if (TheHellTower.GetItemChecked(i))
                    JITMethods.Add(TheHellTower.Items[i].ToString());

            File.WriteAllBytes(Module.Location.Insert(Module.Location.Length - 4, "-JIT"), JIT.Execute(Module, !x64, JITMethods));

            Module.Dispose();
        }

        //Yes DetectJITAttributeAndDelete + DetectJITAttributeAndFill can be a single method
        private void DetectJITAttributeAndDelete()
        {
            foreach (TypeDef type in Module.GetTypes().Where(T => T.HasMethods))
                foreach (MethodDef method in type.Methods.Where(M => M.HasBody && M.Body.HasInstructions && M.Body.Instructions.Count() > 1 && !M.IsConstructor))
                    foreach (CustomAttribute attribute in method.CustomAttributes.ToArray())
                        if (attribute.TypeFullName == "System.Reflection.ObfuscationAttribute")
                            foreach (var property in attribute.Properties)
                                if (property.Name == "Feature" && property.Type.FullName == "System.String" && (property.Value.ToString().Equals("JIT")))
                                    method.CustomAttributes.Remove(attribute);
        }

        private void DetectJITAttributeAndFill()
        {
            if (TheHellTower.Items.Count != 0)
                TheHellTower.Items.Clear();
            foreach (TypeDef type in Module.GetTypes().Where(T => T.HasMethods))
            {
                foreach (MethodDef method in type.Methods.Where(M => M.HasBody && M.Body.HasInstructions && M.Body.Instructions.Count() > 1 && !M.IsConstructor))
                    foreach (CustomAttribute attribute in method.CustomAttributes)
                        if (attribute.TypeFullName == "System.Reflection.ObfuscationAttribute")
                            foreach (var property in attribute.Properties)
                                if (property.Name == "Feature" && property.Type.FullName == "System.String" && (property.Value.ToString().Equals("JIT")))
                                    TheHellTower.Items.Add(method.FullName.ToString(), true);

                //Second to add unchecked
                foreach (MethodDef method in type.Methods.Where(M => M.HasBody && M.Body.HasInstructions && M.Body.Instructions.Count() > 1 && !M.IsConstructor))
                    if (!TheHellTower.Items.Contains(method.FullName))
                        TheHellTower.Items.Add(method.FullName.ToString(), false);
            }
        }
    }
}