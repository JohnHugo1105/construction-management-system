namespace GSG_Builders.RevisionedUI.Archives {
    partial class Archives_reason_form {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose ( bool disposing ) {
            if ( disposing && ( components != null ) )
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
        private void InitializeComponent ( ) {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Archives_reason_form));
            this.panel1 = new System.Windows.Forms.Panel();
            this.okay_bunifuThinButton = new Bunifu.Framework.UI.BunifuThinButton2();
            this.cancel_bunifuThinButton = new Bunifu.Framework.UI.BunifuThinButton2();
            this.other_reason_richTextBox = new System.Windows.Forms.RichTextBox();
            this.others_specify_reason_radioButton = new System.Windows.Forms.RadioButton();
            this.finished_radioButton = new System.Windows.Forms.RadioButton();
            this.backgroundWorker1 = new System.ComponentModel.BackgroundWorker();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel1.Controls.Add(this.okay_bunifuThinButton);
            this.panel1.Controls.Add(this.cancel_bunifuThinButton);
            this.panel1.Controls.Add(this.other_reason_richTextBox);
            this.panel1.Controls.Add(this.others_specify_reason_radioButton);
            this.panel1.Controls.Add(this.finished_radioButton);
            this.panel1.Location = new System.Drawing.Point(20, 28);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(290, 146);
            this.panel1.TabIndex = 0;
            // 
            // okay_bunifuThinButton
            // 
            this.okay_bunifuThinButton.ActiveBorderThickness = 1;
            this.okay_bunifuThinButton.ActiveCornerRadius = 20;
            this.okay_bunifuThinButton.ActiveFillColor = System.Drawing.Color.DodgerBlue;
            this.okay_bunifuThinButton.ActiveForecolor = System.Drawing.Color.White;
            this.okay_bunifuThinButton.ActiveLineColor = System.Drawing.Color.SeaGreen;
            this.okay_bunifuThinButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.okay_bunifuThinButton.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("okay_bunifuThinButton.BackgroundImage")));
            this.okay_bunifuThinButton.ButtonText = "OK";
            this.okay_bunifuThinButton.Cursor = System.Windows.Forms.Cursors.Hand;
            this.okay_bunifuThinButton.Font = new System.Drawing.Font("Century Gothic", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.okay_bunifuThinButton.ForeColor = System.Drawing.Color.DarkSlateGray;
            this.okay_bunifuThinButton.IdleBorderThickness = 1;
            this.okay_bunifuThinButton.IdleCornerRadius = 20;
            this.okay_bunifuThinButton.IdleFillColor = System.Drawing.Color.White;
            this.okay_bunifuThinButton.IdleForecolor = System.Drawing.Color.DarkSlateGray;
            this.okay_bunifuThinButton.IdleLineColor = System.Drawing.Color.DodgerBlue;
            this.okay_bunifuThinButton.Location = new System.Drawing.Point(40, 102);
            this.okay_bunifuThinButton.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.okay_bunifuThinButton.Name = "okay_bunifuThinButton";
            this.okay_bunifuThinButton.Size = new System.Drawing.Size(90, 37);
            this.okay_bunifuThinButton.TabIndex = 5;
            this.okay_bunifuThinButton.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.okay_bunifuThinButton.Click += new System.EventHandler(this.okay_bunifuThinButton_Click);
            // 
            // cancel_bunifuThinButton
            // 
            this.cancel_bunifuThinButton.ActiveBorderThickness = 1;
            this.cancel_bunifuThinButton.ActiveCornerRadius = 20;
            this.cancel_bunifuThinButton.ActiveFillColor = System.Drawing.Color.Red;
            this.cancel_bunifuThinButton.ActiveForecolor = System.Drawing.Color.White;
            this.cancel_bunifuThinButton.ActiveLineColor = System.Drawing.Color.SeaGreen;
            this.cancel_bunifuThinButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.cancel_bunifuThinButton.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("cancel_bunifuThinButton.BackgroundImage")));
            this.cancel_bunifuThinButton.ButtonText = "CANCEL";
            this.cancel_bunifuThinButton.Cursor = System.Windows.Forms.Cursors.Hand;
            this.cancel_bunifuThinButton.Font = new System.Drawing.Font("Century Gothic", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cancel_bunifuThinButton.ForeColor = System.Drawing.Color.DarkSlateGray;
            this.cancel_bunifuThinButton.IdleBorderThickness = 1;
            this.cancel_bunifuThinButton.IdleCornerRadius = 20;
            this.cancel_bunifuThinButton.IdleFillColor = System.Drawing.Color.White;
            this.cancel_bunifuThinButton.IdleForecolor = System.Drawing.Color.DarkSlateGray;
            this.cancel_bunifuThinButton.IdleLineColor = System.Drawing.Color.DodgerBlue;
            this.cancel_bunifuThinButton.Location = new System.Drawing.Point(163, 102);
            this.cancel_bunifuThinButton.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.cancel_bunifuThinButton.Name = "cancel_bunifuThinButton";
            this.cancel_bunifuThinButton.Size = new System.Drawing.Size(90, 37);
            this.cancel_bunifuThinButton.TabIndex = 4;
            this.cancel_bunifuThinButton.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.cancel_bunifuThinButton.Click += new System.EventHandler(this.cancel_bunifuThinButton_Click);
            // 
            // other_reason_richTextBox
            // 
            this.other_reason_richTextBox.AutoWordSelection = true;
            this.other_reason_richTextBox.BackColor = System.Drawing.Color.White;
            this.other_reason_richTextBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.other_reason_richTextBox.EnableAutoDragDrop = true;
            this.other_reason_richTextBox.Enabled = false;
            this.other_reason_richTextBox.Location = new System.Drawing.Point(16, 55);
            this.other_reason_richTextBox.Name = "other_reason_richTextBox";
            this.other_reason_richTextBox.Size = new System.Drawing.Size(256, 41);
            this.other_reason_richTextBox.TabIndex = 3;
            this.other_reason_richTextBox.Text = "";
            this.other_reason_richTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.other_reason_richTextBox_KeyDown);
            // 
            // others_specify_reason_radioButton
            // 
            this.others_specify_reason_radioButton.AutoSize = true;
            this.others_specify_reason_radioButton.ForeColor = System.Drawing.Color.Black;
            this.others_specify_reason_radioButton.Location = new System.Drawing.Point(131, 22);
            this.others_specify_reason_radioButton.Name = "others_specify_reason_radioButton";
            this.others_specify_reason_radioButton.Size = new System.Drawing.Size(155, 19);
            this.others_specify_reason_radioButton.TabIndex = 2;
            this.others_specify_reason_radioButton.TabStop = true;
            this.others_specify_reason_radioButton.Text = "Others (specify reason) :";
            this.others_specify_reason_radioButton.UseVisualStyleBackColor = true;
            this.others_specify_reason_radioButton.CheckedChanged += new System.EventHandler(this.others_specify_reason_radioButton_CheckedChanged);
            // 
            // finished_radioButton
            // 
            this.finished_radioButton.AutoSize = true;
            this.finished_radioButton.Font = new System.Drawing.Font("Century Gothic", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.finished_radioButton.ForeColor = System.Drawing.Color.Black;
            this.finished_radioButton.Location = new System.Drawing.Point(7, 22);
            this.finished_radioButton.Name = "finished_radioButton";
            this.finished_radioButton.Size = new System.Drawing.Size(118, 20);
            this.finished_radioButton.TabIndex = 1;
            this.finished_radioButton.TabStop = true;
            this.finished_radioButton.Text = "Finished project";
            this.finished_radioButton.UseVisualStyleBackColor = true;
            // 
            // backgroundWorker1
            // 
            this.backgroundWorker1.DoWork += new System.ComponentModel.DoWorkEventHandler(this.backgroundWorker1_DoWork);
            this.backgroundWorker1.ProgressChanged += new System.ComponentModel.ProgressChangedEventHandler(this.backgroundWorker1_ProgressChanged);
            this.backgroundWorker1.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.backgroundWorker1_RunWorkerCompleted);
            // 
            // Archives_reason_form
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(329, 195);
            this.Controls.Add(this.panel1);
            this.Font = new System.Drawing.Font("Century Gothic", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Archives_reason_form";
            this.Resizable = false;
            this.ShadowType = MetroFramework.Forms.MetroFormShadowType.AeroShadow;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.Text = "Archives_reason_form";
            this.TopMost = true;
            this.Activated += new System.EventHandler(this.Archives_reason_form_Activated);
            this.Deactivate += new System.EventHandler(this.Archives_reason_form_Deactivate);
            this.Load += new System.EventHandler(this.Archives_reason_form_Load);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.RichTextBox other_reason_richTextBox;
        private System.Windows.Forms.RadioButton others_specify_reason_radioButton;
        private System.Windows.Forms.RadioButton finished_radioButton;
        private System.ComponentModel.BackgroundWorker backgroundWorker1;
        private Bunifu.Framework.UI.BunifuThinButton2 cancel_bunifuThinButton;
        private Bunifu.Framework.UI.BunifuThinButton2 okay_bunifuThinButton;
    }
}