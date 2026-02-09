// -----------------------------------------------------------------------
// <copyright file="PasswordTextBox.cs" company="Doughnuts Publishing LLC">
//     Author: Doug Hunt
//     Copyright (c)  Doughnuts Publishing LLC. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace Dnp.Controls.Lib
{
    // Simple composite control: a TextBox with a toggle button (eye) to show/hide text
    public class PasswordTextBox : UserControl
    {
        private TextBox _textBox;
        private Button _toggle;
        private bool _isPasswordShown = false;

        public PasswordTextBox()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            _textBox = new TextBox { BorderStyle = BorderStyle.FixedSingle, Dock = DockStyle.Fill }; 
            _toggle = new Button { Width = 30, Dock = DockStyle.Right, FlatStyle = FlatStyle.Flat };
            _toggle.FlatAppearance.BorderSize = 0;
            _toggle.Text = "\u{1F441}"; // eye-like glyph fallback
            _toggle.Font = new Font("Segoe UI Symbol", 9F, FontStyle.Regular, GraphicsUnit.Point);
            _toggle.Click += Toggle_Click;

            this.Controls.Add(_textBox);
            this.Controls.Add(_toggle);

            this.Height = _textBox.Height + 6;
            this.MinimumSize = new Size(100, _toggle.Height + 4);
        }

        private void Toggle_Click(object? sender, EventArgs e)
        {
            IsPasswordShown = !IsPasswordShown;
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        public bool IsPasswordShown
        {
            get => _isPasswordShown;
            set
            {
                _isPasswordShown = value;
                _textBox.UseSystemPasswordChar = !_isPasswordShown;
                UpdateToggleText();
            }
        }

        private void UpdateToggleText()
        {
            // simple text toggle (could be replaced with an image)
            _toggle.Text = _isPasswordShown ? "\u{1F441}" : "\u{1F441}";
        }

        [Category("Appearance")]
        public override string Text
        {
            get => _textBox.Text;
            set => _textBox.Text = value;
        }

        [Category("Behavior")]
        public char PasswordChar
        {
            get => _textBox.PasswordChar;
            set => _textBox.PasswordChar = value;
        }

        [Category("Behavior")]
        public bool UseSystemPasswordChar
        {
            get => _textBox.UseSystemPasswordChar;
            set => _textBox.UseSystemPasswordChar = value;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            // ensure toggle button height matches textbox
            _toggle.Height = this.Height - 4;
        }

        // expose TextBox properties as needed
        public TextBox InnerTextBox => _textBox;
    }
}
