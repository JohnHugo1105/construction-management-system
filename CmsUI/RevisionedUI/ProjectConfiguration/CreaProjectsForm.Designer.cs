namespace GSG_Builders.RevisionedUI.ProjectConfiguration {
    partial class CreaProjectsForm {
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CreaProjectsForm));
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.project_name_txt = new System.Windows.Forms.TextBox();
            this.sub_project_name_txt = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.date_started_dtp = new System.Windows.Forms.DateTimePicker();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.okay_bunifuThinButton = new Bunifu.Framework.UI.BunifuThinButton2();
            this.cancel_bunifuThinButton = new Bunifu.Framework.UI.BunifuThinButton2();
            this.label4 = new System.Windows.Forms.Label();
            this.groupBox1.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.tableLayoutPanel1);
            this.groupBox1.Font = new System.Drawing.Font("Century Gothic", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox1.Location = new System.Drawing.Point(23, 82);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(341, 272);
            this.groupBox1.TabIndex = 104;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Project details";
            // 
            // project_name_txt
            // 
            this.project_name_txt.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.project_name_txt.Font = new System.Drawing.Font("Century Gothic", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.project_name_txt.Location = new System.Drawing.Point(3, 25);
            this.project_name_txt.Name = "project_name_txt";
            this.project_name_txt.Size = new System.Drawing.Size(314, 23);
            this.project_name_txt.TabIndex = 94;
            // 
            // sub_project_name_txt
            // 
            this.sub_project_name_txt.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.sub_project_name_txt.Font = new System.Drawing.Font("Century Gothic", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.sub_project_name_txt.Location = new System.Drawing.Point(3, 99);
            this.sub_project_name_txt.Name = "sub_project_name_txt";
            this.sub_project_name_txt.Size = new System.Drawing.Size(314, 23);
            this.sub_project_name_txt.TabIndex = 98;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Century Gothic", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(3, 200);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(77, 16);
            this.label2.TabIndex = 97;
            this.label2.Text = "Date started";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Century Gothic", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(3, 125);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(113, 16);
            this.label3.TabIndex = 99;
            this.label3.Text = "Sub-project name";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Century Gothic", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(3, 51);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(86, 16);
            this.label1.TabIndex = 95;
            this.label1.Text = "Project name";
            // 
            // date_started_dtp
            // 
            this.date_started_dtp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.date_started_dtp.CalendarForeColor = System.Drawing.Color.DarkGray;
            this.date_started_dtp.CalendarTitleForeColor = System.Drawing.Color.DimGray;
            this.date_started_dtp.CustomFormat = "MMM. d, yyy";
            this.date_started_dtp.Font = new System.Drawing.Font("Century Gothic", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.date_started_dtp.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.date_started_dtp.Location = new System.Drawing.Point(3, 175);
            this.date_started_dtp.Name = "date_started_dtp";
            this.date_started_dtp.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.date_started_dtp.Size = new System.Drawing.Size(150, 22);
            this.date_started_dtp.TabIndex = 96;
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 1;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Controls.Add(this.sub_project_name_txt, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.label2, 0, 5);
            this.tableLayoutPanel1.Controls.Add(this.project_name_txt, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.date_started_dtp, 0, 4);
            this.tableLayoutPanel1.Controls.Add(this.label1, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.label3, 0, 3);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(3, 19);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 6;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 20.8F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 10.4F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 18.8F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 19.6F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(335, 250);
            this.tableLayoutPanel1.TabIndex = 0;
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
            this.okay_bunifuThinButton.Location = new System.Drawing.Point(82, 365);
            this.okay_bunifuThinButton.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.okay_bunifuThinButton.Name = "okay_bunifuThinButton";
            this.okay_bunifuThinButton.Size = new System.Drawing.Size(90, 37);
            this.okay_bunifuThinButton.TabIndex = 106;
            this.okay_bunifuThinButton.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
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
            this.cancel_bunifuThinButton.Location = new System.Drawing.Point(224, 365);
            this.cancel_bunifuThinButton.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.cancel_bunifuThinButton.Name = "cancel_bunifuThinButton";
            this.cancel_bunifuThinButton.Size = new System.Drawing.Size(90, 37);
            this.cancel_bunifuThinButton.TabIndex = 105;
            this.cancel_bunifuThinButton.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("Century Gothic", 18F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label4.Location = new System.Drawing.Point(14, 40);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(189, 28);
            this.label4.TabIndex = 107;
            this.label4.Text = "Create Projects";
            // 
            // CreaProjectsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(389, 415);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.okay_bunifuThinButton);
            this.Controls.Add(this.cancel_bunifuThinButton);
            this.Controls.Add(this.groupBox1);
            this.Name = "CreaProjectsForm";
            this.Resizable = false;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.groupBox1.ResumeLayout(false);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.TextBox project_name_txt;
        private System.Windows.Forms.TextBox sub_project_name_txt;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.DateTimePicker date_started_dtp;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private Bunifu.Framework.UI.BunifuThinButton2 okay_bunifuThinButton;
        private Bunifu.Framework.UI.BunifuThinButton2 cancel_bunifuThinButton;
        private System.Windows.Forms.Label label4;
    }
}