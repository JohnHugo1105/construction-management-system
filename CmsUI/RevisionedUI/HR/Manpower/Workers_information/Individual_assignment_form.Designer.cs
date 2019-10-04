namespace GSG_Builders.RevisionedUI.HR.Manpower.Workers_information {
    partial class Individual_assignment_form {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose ( bool disposing ) {
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
        private void InitializeComponent ( ) {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Individual_assignment_form));
            this.assigned_project_comboBox = new System.Windows.Forms.ComboBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.cancel_bunifuThinButton = new Bunifu.Framework.UI.BunifuThinButton2();
            this.label1 = new System.Windows.Forms.Label();
            this.date_assigned_DateTimePicker = new System.Windows.Forms.DateTimePicker();
            this.okay_bunifuThinButton = new Bunifu.Framework.UI.BunifuThinButton2();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // assigned_project_comboBox
            // 
            this.assigned_project_comboBox.Font = new System.Drawing.Font("Century Gothic", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.assigned_project_comboBox.FormattingEnabled = true;
            this.assigned_project_comboBox.Location = new System.Drawing.Point(13, 35);
            this.assigned_project_comboBox.Name = "assigned_project_comboBox";
            this.assigned_project_comboBox.Size = new System.Drawing.Size(341, 24);
            this.assigned_project_comboBox.TabIndex = 0;
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.cancel_bunifuThinButton);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Controls.Add(this.date_assigned_DateTimePicker);
            this.groupBox1.Controls.Add(this.assigned_project_comboBox);
            this.groupBox1.Controls.Add(this.okay_bunifuThinButton);
            this.groupBox1.Font = new System.Drawing.Font("Century Gothic", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox1.Location = new System.Drawing.Point(7, 23);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(369, 140);
            this.groupBox1.TabIndex = 1;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Project to assign";
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
            this.cancel_bunifuThinButton.Location = new System.Drawing.Point(274, 100);
            this.cancel_bunifuThinButton.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.cancel_bunifuThinButton.Name = "cancel_bunifuThinButton";
            this.cancel_bunifuThinButton.Size = new System.Drawing.Size(80, 32);
            this.cancel_bunifuThinButton.TabIndex = 9;
            this.cancel_bunifuThinButton.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.cancel_bunifuThinButton.Click += new System.EventHandler(this.cancel_bunifuThinButton_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(123, 73);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(109, 16);
            this.label1.TabIndex = 8;
            this.label1.Text = "Date assigned :";
            // 
            // date_assigned_DateTimePicker
            // 
            this.date_assigned_DateTimePicker.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.date_assigned_DateTimePicker.CalendarForeColor = System.Drawing.Color.DarkGray;
            this.date_assigned_DateTimePicker.CalendarTitleForeColor = System.Drawing.Color.DimGray;
            this.date_assigned_DateTimePicker.CustomFormat = "MMM. d, yyy";
            this.date_assigned_DateTimePicker.Font = new System.Drawing.Font("Century Gothic", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.date_assigned_DateTimePicker.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.date_assigned_DateTimePicker.Location = new System.Drawing.Point(237, 70);
            this.date_assigned_DateTimePicker.Name = "date_assigned_DateTimePicker";
            this.date_assigned_DateTimePicker.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.date_assigned_DateTimePicker.Size = new System.Drawing.Size(117, 22);
            this.date_assigned_DateTimePicker.TabIndex = 7;
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
            this.okay_bunifuThinButton.Location = new System.Drawing.Point(186, 100);
            this.okay_bunifuThinButton.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.okay_bunifuThinButton.Name = "okay_bunifuThinButton";
            this.okay_bunifuThinButton.Size = new System.Drawing.Size(80, 32);
            this.okay_bunifuThinButton.TabIndex = 6;
            this.okay_bunifuThinButton.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.okay_bunifuThinButton.Click += new System.EventHandler(this.okay_bunifuThinButton_Click);
            // 
            // Individual_assignment_form
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BorderStyle = MetroFramework.Forms.MetroFormBorderStyle.FixedSingle;
            this.ClientSize = new System.Drawing.Size(383, 170);
            this.Controls.Add(this.groupBox1);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Individual_assignment_form";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.Load += new System.EventHandler(this.Individual_assignment_form_Load);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ComboBox assigned_project_comboBox;
        private System.Windows.Forms.GroupBox groupBox1;
        private Bunifu.Framework.UI.BunifuThinButton2 okay_bunifuThinButton;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.DateTimePicker date_assigned_DateTimePicker;
        private Bunifu.Framework.UI.BunifuThinButton2 cancel_bunifuThinButton;
    }
}