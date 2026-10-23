using System;
using System.Drawing;
using System.Windows.Forms;

namespace HuntAndPeck.UiaFixture
{
    /// <summary>
    /// A small window with one control of each kind HuntAndPeck hints, for the UI Automation integration tests.
    /// Controls do not overlap, so a hint's bounds identify its control.
    /// </summary>
    internal sealed class MainForm : Form
    {
        public MainForm()
        {
            Text = "HAP UIA Fixture";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(400, 300);

            var result = new Label { Text = "Not pressed", Location = new Point(130, 14), Size = new Size(120, 23) };
            var button = new Button { Text = "Press me", Location = new Point(10, 10), Size = new Size(100, 30) };
            button.Click += (sender, e) => result.Text = "Pressed";

            var check = new CheckBox { Text = "Check me", Location = new Point(10, 55), Size = new Size(120, 24) };

            var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(10, 95), Size = new Size(150, 24) };
            combo.Items.AddRange(new object[] { "Combo one", "Combo two", "Combo three" });

            var list = new ListBox { Location = new Point(10, 135), Size = new Size(150, 60) };
            list.Items.AddRange(new object[] { "List one", "List two", "List three" });

            var text = new TextBox { Location = new Point(10, 210), Size = new Size(150, 23) };

            var staticText = new Label { Text = "Static text", Location = new Point(10, 250), Size = new Size(150, 23) };

            var menu = new ContextMenuStrip();
            menu.Items.Add("Menu one");
            menu.Items.Add("Menu two");
            ContextMenuStrip = menu;

            Controls.AddRange(new Control[] { button, result, check, combo, list, text, staticText });
        }
    }
}
