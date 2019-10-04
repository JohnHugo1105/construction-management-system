namespace GSG_Builders.RevisionedUI.HR {
    partial class maximizing_form {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose( bool disposing ) {
            if( disposing && ( components != null ) )
            {
                components.Dispose( );
            }
            base.Dispose( disposing );
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent( ) {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(maximizing_form));
            this.maximize_value_bunifuSlider = new Bunifu.Framework.UI.BunifuSlider();
            this.set_bunifuThinButton = new Bunifu.Framework.UI.BunifuThinButton2();
            this.value_bunifuMetroTextbox = new Bunifu.Framework.UI.BunifuMetroTextbox();
            this.label1 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // maximize_value_bunifuSlider
            // 
            this.maximize_value_bunifuSlider.BackColor = System.Drawing.Color.Transparent;
            this.maximize_value_bunifuSlider.BackgroudColor = System.Drawing.SystemColors.ControlDarkDark;
            this.maximize_value_bunifuSlider.BorderRadius = 0;
            this.maximize_value_bunifuSlider.IndicatorColor = System.Drawing.Color.SteelBlue;
            this.maximize_value_bunifuSlider.Location = new System.Drawing.Point(23, 32);
            this.maximize_value_bunifuSlider.MaximumValue = 100;
            this.maximize_value_bunifuSlider.Name = "maximize_value_bunifuSlider";
            this.maximize_value_bunifuSlider.Size = new System.Drawing.Size(285, 30);
            this.maximize_value_bunifuSlider.TabIndex = 1;
            this.maximize_value_bunifuSlider.Value = 0;
            this.maximize_value_bunifuSlider.ValueChanged += new System.EventHandler(this.maximize_value_bunifuSlider_ValueChanged);
            this.maximize_value_bunifuSlider.Scroll += new System.Windows.Forms.ScrollEventHandler(this.maximize_value_bunifuSlider_Scroll);
            // 
            // set_bunifuThinButton
            // 
            this.set_bunifuThinButton.ActiveBorderThickness = 1;
            this.set_bunifuThinButton.ActiveCornerRadius = 20;
            this.set_bunifuThinButton.ActiveFillColor = System.Drawing.Color.DodgerBlue;
            this.set_bunifuThinButton.ActiveForecolor = System.Drawing.Color.White;
            this.set_bunifuThinButton.ActiveLineColor = System.Drawing.Color.SeaGreen;
            this.set_bunifuThinButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.set_bunifuThinButton.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("set_bunifuThinButton.BackgroundImage")));
            this.set_bunifuThinButton.ButtonText = "Set";
            this.set_bunifuThinButton.Cursor = System.Windows.Forms.Cursors.Hand;
            this.set_bunifuThinButton.Font = new System.Drawing.Font("Century Gothic", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.set_bunifuThinButton.ForeColor = System.Drawing.Color.DarkSlateGray;
            this.set_bunifuThinButton.IdleBorderThickness = 1;
            this.set_bunifuThinButton.IdleCornerRadius = 20;
            this.set_bunifuThinButton.IdleFillColor = System.Drawing.Color.White;
            this.set_bunifuThinButton.IdleForecolor = System.Drawing.Color.DarkSlateGray;
            this.set_bunifuThinButton.IdleLineColor = System.Drawing.Color.DodgerBlue;
            this.set_bunifuThinButton.Location = new System.Drawing.Point(125, 61);
            this.set_bunifuThinButton.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.set_bunifuThinButton.Name = "set_bunifuThinButton";
            this.set_bunifuThinButton.Size = new System.Drawing.Size(48, 32);
            this.set_bunifuThinButton.TabIndex = 97;
            this.set_bunifuThinButton.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.set_bunifuThinButton.Click += new System.EventHandler(this.set_bunifuThinButton_Click);
            // 
            // value_bunifuMetroTextbox
            // 
            this.value_bunifuMetroTextbox.BorderColorFocused = System.Drawing.Color.Blue;
            this.value_bunifuMetroTextbox.BorderColorIdle = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.value_bunifuMetroTextbox.BorderColorMouseHover = System.Drawing.Color.Blue;
            this.value_bunifuMetroTextbox.BorderThickness = 3;
            this.value_bunifuMetroTextbox.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.value_bunifuMetroTextbox.Font = new System.Drawing.Font("Century Gothic", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.value_bunifuMetroTextbox.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.value_bunifuMetroTextbox.isPassword = false;
            this.value_bunifuMetroTextbox.Location = new System.Drawing.Point(181, 63);
            this.value_bunifuMetroTextbox.Margin = new System.Windows.Forms.Padding(4);
            this.value_bunifuMetroTextbox.Name = "value_bunifuMetroTextbox";
            this.value_bunifuMetroTextbox.Size = new System.Drawing.Size(37, 27);
            this.value_bunifuMetroTextbox.TabIndex = 98;
            this.value_bunifuMetroTextbox.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.value_bunifuMetroTextbox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.value_bunifuMetroTextbox_KeyDown);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Century Gothic", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(218, 69);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(19, 16);
            this.label1.TabIndex = 99;
            this.label1.Text = "%";
            // 
            // maximizing_form
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(328, 99);
            this.Controls.Add(this.value_bunifuMetroTextbox);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.maximize_value_bunifuSlider);
            this.Controls.Add(this.set_bunifuThinButton);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "maximizing_form";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.Load += new System.EventHandler(this.maximizing_form_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private Bunifu.Framework.UI.BunifuSlider maximize_value_bunifuSlider;
        private Bunifu.Framework.UI.BunifuThinButton2 set_bunifuThinButton;
        private Bunifu.Framework.UI.BunifuMetroTextbox value_bunifuMetroTextbox;
        private System.Windows.Forms.Label label1;
    }
}