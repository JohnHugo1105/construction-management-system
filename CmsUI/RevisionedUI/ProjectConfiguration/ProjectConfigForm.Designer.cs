namespace GSG_Builders.RevisionedUI.Bill_Mat {
    partial class ProjectConfigForm {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            this.label3 = new System.Windows.Forms.Label();
            this.sub_project_name_txt = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.date_started_dtp = new System.Windows.Forms.DateTimePicker();
            this.label1 = new System.Windows.Forms.Label();
            this.project_name_txt = new System.Windows.Forms.TextBox();
            this.bunifuElipse1 = new Bunifu.Framework.UI.BunifuElipse(this.components);
            this.bunifuElipse2 = new Bunifu.Framework.UI.BunifuElipse(this.components);
            this.bunifuElipse3 = new Bunifu.Framework.UI.BunifuElipse(this.components);
            this.panel2 = new System.Windows.Forms.Panel();
            this.bunifuDragControl1 = new Bunifu.Framework.UI.BunifuDragControl(this.components);
            this.toolStripSeparator16 = new System.Windows.Forms.ToolStripSeparator();
            this.projectToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.subprojectToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator11 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripSeparator12 = new System.Windows.Forms.ToolStripSeparator();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.main_project_dataGridView = new System.Windows.Forms.DataGridView();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.deleteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.sub_project_dataGridView = new System.Windows.Forms.DataGridView();
            this.bunifuSeparator1 = new Bunifu.Framework.UI.BunifuSeparator();
            this.bunifuSeparator2 = new Bunifu.Framework.UI.BunifuSeparator();
            this.BillMat_toolStrip = new System.Windows.Forms.ToolStrip();
            this.home_toolStripButton = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.Add_toolStripButton = new System.Windows.Forms.ToolStripDropDownButton();
            this.newProjectBtn = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.edit_toolStripButton = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.remove_toolStripButton = new System.Windows.Forms.ToolStripButton();
            this.CreateMainProjectBtn = new System.Windows.Forms.ToolStripMenuItem();
            this.CreateSubProjectBtn = new System.Windows.Forms.ToolStripMenuItem();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.main_project_dataGridView)).BeginInit();
            this.contextMenuStrip1.SuspendLayout();
            this.groupBox3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.sub_project_dataGridView)).BeginInit();
            this.BillMat_toolStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Century Gothic", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(346, 49);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(113, 16);
            this.label3.TabIndex = 99;
            this.label3.Text = "Sub-project name";
            // 
            // sub_project_name_txt
            // 
            this.sub_project_name_txt.Font = new System.Drawing.Font("Century Gothic", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.sub_project_name_txt.Location = new System.Drawing.Point(346, 23);
            this.sub_project_name_txt.Name = "sub_project_name_txt";
            this.sub_project_name_txt.Size = new System.Drawing.Size(314, 23);
            this.sub_project_name_txt.TabIndex = 98;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Century Gothic", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(676, 48);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(77, 16);
            this.label2.TabIndex = 97;
            this.label2.Text = "Date started";
            // 
            // date_started_dtp
            // 
            this.date_started_dtp.CalendarForeColor = System.Drawing.Color.DarkGray;
            this.date_started_dtp.CalendarTitleForeColor = System.Drawing.Color.DimGray;
            this.date_started_dtp.CustomFormat = "MMM. d, yyy";
            this.date_started_dtp.Font = new System.Drawing.Font("Century Gothic", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.date_started_dtp.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.date_started_dtp.Location = new System.Drawing.Point(676, 23);
            this.date_started_dtp.Name = "date_started_dtp";
            this.date_started_dtp.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.date_started_dtp.Size = new System.Drawing.Size(150, 22);
            this.date_started_dtp.TabIndex = 96;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Century Gothic", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(16, 49);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(86, 16);
            this.label1.TabIndex = 95;
            this.label1.Text = "Project name";
            this.label1.Click += new System.EventHandler(this.label1_Click);
            // 
            // project_name_txt
            // 
            this.project_name_txt.Font = new System.Drawing.Font("Century Gothic", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.project_name_txt.Location = new System.Drawing.Point(16, 23);
            this.project_name_txt.Name = "project_name_txt";
            this.project_name_txt.Size = new System.Drawing.Size(314, 23);
            this.project_name_txt.TabIndex = 94;
            // 
            // bunifuElipse1
            // 
            this.bunifuElipse1.ElipseRadius = 15;
            this.bunifuElipse1.TargetControl = this;
            // 
            // bunifuElipse2
            // 
            this.bunifuElipse2.ElipseRadius = 15;
            this.bunifuElipse2.TargetControl = this;
            // 
            // bunifuElipse3
            // 
            this.bunifuElipse3.ElipseRadius = 5;
            this.bunifuElipse3.TargetControl = this;
            // 
            // panel2
            // 
            this.panel2.BackColor = System.Drawing.Color.DodgerBlue;
            this.panel2.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel2.Location = new System.Drawing.Point(0, 0);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(867, 26);
            this.panel2.TabIndex = 1;
            // 
            // bunifuDragControl1
            // 
            this.bunifuDragControl1.Fixed = true;
            this.bunifuDragControl1.Horizontal = true;
            this.bunifuDragControl1.TargetControl = this.panel2;
            this.bunifuDragControl1.Vertical = true;
            // 
            // toolStripSeparator16
            // 
            this.toolStripSeparator16.AutoSize = false;
            this.toolStripSeparator16.BackColor = System.Drawing.Color.Transparent;
            this.toolStripSeparator16.ForeColor = System.Drawing.Color.Black;
            this.toolStripSeparator16.Name = "toolStripSeparator16";
            this.toolStripSeparator16.Size = new System.Drawing.Size(6, 52);
            // 
            // projectToolStripMenuItem
            // 
            this.projectToolStripMenuItem.Image = global::GSG_Builders.Properties.Resources.iconfinder_queue_326588;
            this.projectToolStripMenuItem.Name = "projectToolStripMenuItem";
            this.projectToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.projectToolStripMenuItem.Text = "Main project";
            // 
            // subprojectToolStripMenuItem
            // 
            this.subprojectToolStripMenuItem.Image = global::GSG_Builders.Properties.Resources.iconfinder_queue_326588;
            this.subprojectToolStripMenuItem.Name = "subprojectToolStripMenuItem";
            this.subprojectToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.subprojectToolStripMenuItem.Text = "Sub-project";
            // 
            // toolStripSeparator11
            // 
            this.toolStripSeparator11.AutoSize = false;
            this.toolStripSeparator11.BackColor = System.Drawing.Color.Transparent;
            this.toolStripSeparator11.ForeColor = System.Drawing.Color.Black;
            this.toolStripSeparator11.Name = "toolStripSeparator11";
            this.toolStripSeparator11.Size = new System.Drawing.Size(6, 52);
            // 
            // toolStripSeparator12
            // 
            this.toolStripSeparator12.AutoSize = false;
            this.toolStripSeparator12.BackColor = System.Drawing.Color.Transparent;
            this.toolStripSeparator12.ForeColor = System.Drawing.Color.Black;
            this.toolStripSeparator12.Name = "toolStripSeparator12";
            this.toolStripSeparator12.Size = new System.Drawing.Size(6, 52);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.project_name_txt);
            this.groupBox1.Controls.Add(this.sub_project_name_txt);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Controls.Add(this.date_started_dtp);
            this.groupBox1.Font = new System.Drawing.Font("Century Gothic", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox1.Location = new System.Drawing.Point(12, 92);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(843, 78);
            this.groupBox1.TabIndex = 103;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Project details";
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.main_project_dataGridView);
            this.groupBox2.Font = new System.Drawing.Font("Century Gothic", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox2.Location = new System.Drawing.Point(12, 202);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(411, 169);
            this.groupBox2.TabIndex = 104;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Main project list";
            // 
            // main_project_dataGridView
            // 
            this.main_project_dataGridView.AllowUserToAddRows = false;
            this.main_project_dataGridView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.main_project_dataGridView.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.main_project_dataGridView.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.AllCells;
            this.main_project_dataGridView.BackgroundColor = System.Drawing.Color.WhiteSmoke;
            this.main_project_dataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.main_project_dataGridView.ColumnHeadersVisible = false;
            this.main_project_dataGridView.ContextMenuStrip = this.contextMenuStrip1;
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle2.BackColor = System.Drawing.Color.White;
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Century Gothic", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle2.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.Color.Gainsboro;
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.Color.Black;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.main_project_dataGridView.DefaultCellStyle = dataGridViewCellStyle2;
            this.main_project_dataGridView.Location = new System.Drawing.Point(16, 23);
            this.main_project_dataGridView.Name = "main_project_dataGridView";
            this.main_project_dataGridView.ReadOnly = true;
            this.main_project_dataGridView.RowHeadersVisible = false;
            this.main_project_dataGridView.RowHeadersWidth = 23;
            this.main_project_dataGridView.Size = new System.Drawing.Size(379, 125);
            this.main_project_dataGridView.TabIndex = 0;
            this.main_project_dataGridView.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.bill_mat_settings_main_project_dataGridView_CellClick);
            this.main_project_dataGridView.CellDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.bill_mat_settings_main_project_dataGridView_CellContentDoubleClick);
            this.main_project_dataGridView.CellMouseDown += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.main_project_dataGridView_CellMouseDown);
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Font = new System.Drawing.Font("Century Gothic", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.deleteToolStripMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(115, 26);
            // 
            // deleteToolStripMenuItem
            // 
            this.deleteToolStripMenuItem.Image = global::GSG_Builders.Properties.Resources.delete_icon_24x24;
            this.deleteToolStripMenuItem.Name = "deleteToolStripMenuItem";
            this.deleteToolStripMenuItem.Size = new System.Drawing.Size(114, 22);
            this.deleteToolStripMenuItem.Text = "Delete";
            this.deleteToolStripMenuItem.Click += new System.EventHandler(this.deleteToolStripMenuItem_Click);
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.sub_project_dataGridView);
            this.groupBox3.Font = new System.Drawing.Font("Century Gothic", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox3.Location = new System.Drawing.Point(444, 202);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(411, 169);
            this.groupBox3.TabIndex = 105;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Sub-project list";
            // 
            // sub_project_dataGridView
            // 
            this.sub_project_dataGridView.AllowUserToAddRows = false;
            this.sub_project_dataGridView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.sub_project_dataGridView.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.sub_project_dataGridView.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.AllCells;
            this.sub_project_dataGridView.BackgroundColor = System.Drawing.Color.WhiteSmoke;
            this.sub_project_dataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.sub_project_dataGridView.ColumnHeadersVisible = false;
            this.sub_project_dataGridView.ContextMenuStrip = this.contextMenuStrip1;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle1.BackColor = System.Drawing.Color.White;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Century Gothic", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.Color.Gainsboro;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.Color.Black;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.sub_project_dataGridView.DefaultCellStyle = dataGridViewCellStyle1;
            this.sub_project_dataGridView.Location = new System.Drawing.Point(16, 23);
            this.sub_project_dataGridView.Name = "sub_project_dataGridView";
            this.sub_project_dataGridView.ReadOnly = true;
            this.sub_project_dataGridView.RowHeadersVisible = false;
            this.sub_project_dataGridView.Size = new System.Drawing.Size(379, 125);
            this.sub_project_dataGridView.TabIndex = 0;
            this.sub_project_dataGridView.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.bill_mat_settings_sub_project_dataGridView_CellClick);
            this.sub_project_dataGridView.CellMouseDown += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.sub_project_dataGridView_CellMouseDown);
            // 
            // bunifuSeparator1
            // 
            this.bunifuSeparator1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.bunifuSeparator1.BackColor = System.Drawing.Color.Transparent;
            this.bunifuSeparator1.LineColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.bunifuSeparator1.LineThickness = 2;
            this.bunifuSeparator1.Location = new System.Drawing.Point(0, -78);
            this.bunifuSeparator1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.bunifuSeparator1.Name = "bunifuSeparator1";
            this.bunifuSeparator1.Size = new System.Drawing.Size(742, 15);
            this.bunifuSeparator1.TabIndex = 106;
            this.bunifuSeparator1.Transparency = 255;
            this.bunifuSeparator1.Vertical = false;
            // 
            // bunifuSeparator2
            // 
            this.bunifuSeparator2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.bunifuSeparator2.BackColor = System.Drawing.Color.Transparent;
            this.bunifuSeparator2.LineColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.bunifuSeparator2.LineThickness = 2;
            this.bunifuSeparator2.Location = new System.Drawing.Point(0, 76);
            this.bunifuSeparator2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.bunifuSeparator2.Name = "bunifuSeparator2";
            this.bunifuSeparator2.Size = new System.Drawing.Size(867, 12);
            this.bunifuSeparator2.TabIndex = 107;
            this.bunifuSeparator2.Transparency = 255;
            this.bunifuSeparator2.Vertical = false;
            this.bunifuSeparator2.Load += new System.EventHandler(this.bunifuSeparator2_Load);
            // 
            // BillMat_toolStrip
            // 
            this.BillMat_toolStrip.BackColor = System.Drawing.Color.White;
            this.BillMat_toolStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.BillMat_toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.home_toolStripButton,
            this.toolStripSeparator1,
            this.Add_toolStripButton,
            this.toolStripSeparator2,
            this.edit_toolStripButton,
            this.toolStripSeparator3,
            this.remove_toolStripButton});
            this.BillMat_toolStrip.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.Flow;
            this.BillMat_toolStrip.Location = new System.Drawing.Point(0, 26);
            this.BillMat_toolStrip.Name = "BillMat_toolStrip";
            this.BillMat_toolStrip.RenderMode = System.Windows.Forms.ToolStripRenderMode.Professional;
            this.BillMat_toolStrip.Size = new System.Drawing.Size(867, 55);
            this.BillMat_toolStrip.TabIndex = 108;
            // 
            // home_toolStripButton
            // 
            this.home_toolStripButton.AutoSize = false;
            this.home_toolStripButton.Font = new System.Drawing.Font("Century Gothic", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.home_toolStripButton.Image = global::GSG_Builders.Properties.Resources.iconfinder_home_126572__1_;
            this.home_toolStripButton.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            this.home_toolStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.home_toolStripButton.Name = "home_toolStripButton";
            this.home_toolStripButton.Size = new System.Drawing.Size(107, 52);
            this.home_toolStripButton.Text = "Home";
            this.home_toolStripButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this.home_toolStripButton.Click += new System.EventHandler(this.back_toolStripButton_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.AutoSize = false;
            this.toolStripSeparator1.BackColor = System.Drawing.Color.Transparent;
            this.toolStripSeparator1.ForeColor = System.Drawing.Color.Black;
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(6, 52);
            // 
            // Add_toolStripButton
            // 
            this.Add_toolStripButton.AutoSize = false;
            this.Add_toolStripButton.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.newProjectBtn});
            this.Add_toolStripButton.Font = new System.Drawing.Font("Century Gothic", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Add_toolStripButton.Image = global::GSG_Builders.Properties.Resources.if_editor_folder_add_glyph_763355;
            this.Add_toolStripButton.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            this.Add_toolStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.Add_toolStripButton.Name = "Add_toolStripButton";
            this.Add_toolStripButton.Size = new System.Drawing.Size(107, 52);
            this.Add_toolStripButton.Text = "Add";
            this.Add_toolStripButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            // 
            // newProjectBtn
            // 
            this.newProjectBtn.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.CreateMainProjectBtn});
            this.newProjectBtn.Image = global::GSG_Builders.Properties.Resources.iconfinder_queue_326588;
            this.newProjectBtn.Name = "newProjectBtn";
            this.newProjectBtn.Size = new System.Drawing.Size(152, 22);
            this.newProjectBtn.Text = "New Project";
            this.newProjectBtn.Click += new System.EventHandler(this.addBothToolStripMenuItem_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.AutoSize = false;
            this.toolStripSeparator2.BackColor = System.Drawing.Color.Transparent;
            this.toolStripSeparator2.ForeColor = System.Drawing.Color.Black;
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(6, 52);
            // 
            // edit_toolStripButton
            // 
            this.edit_toolStripButton.AutoSize = false;
            this.edit_toolStripButton.Font = new System.Drawing.Font("Century Gothic", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.edit_toolStripButton.Image = global::GSG_Builders.Properties.Resources.if_pen_checkbox_3534302;
            this.edit_toolStripButton.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            this.edit_toolStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.edit_toolStripButton.Name = "edit_toolStripButton";
            this.edit_toolStripButton.Size = new System.Drawing.Size(107, 52);
            this.edit_toolStripButton.Text = "Edit";
            this.edit_toolStripButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this.edit_toolStripButton.Click += new System.EventHandler(this.edit_toolStripButton_Click);
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.AutoSize = false;
            this.toolStripSeparator3.BackColor = System.Drawing.Color.Transparent;
            this.toolStripSeparator3.ForeColor = System.Drawing.Color.Black;
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(6, 52);
            // 
            // remove_toolStripButton
            // 
            this.remove_toolStripButton.AutoSize = false;
            this.remove_toolStripButton.Enabled = false;
            this.remove_toolStripButton.Font = new System.Drawing.Font("Century Gothic", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.remove_toolStripButton.Image = global::GSG_Builders.Properties.Resources.iconfinder_074_ArchieveBox_183203__1_;
            this.remove_toolStripButton.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            this.remove_toolStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.remove_toolStripButton.Name = "remove_toolStripButton";
            this.remove_toolStripButton.Size = new System.Drawing.Size(107, 52);
            this.remove_toolStripButton.Text = "Move to archives";
            this.remove_toolStripButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this.remove_toolStripButton.Click += new System.EventHandler(this.remove_toolStripButton_Click);
            // 
            // CreateMainProjectBtn
            // 
            this.CreateMainProjectBtn.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.CreateSubProjectBtn});
            this.CreateMainProjectBtn.Name = "CreateMainProjectBtn";
            this.CreateMainProjectBtn.Size = new System.Drawing.Size(152, 22);
            this.CreateMainProjectBtn.Text = "Main project";
            // 
            // CreateSubProjectBtn
            // 
            this.CreateSubProjectBtn.Name = "CreateSubProjectBtn";
            this.CreateSubProjectBtn.Size = new System.Drawing.Size(152, 22);
            this.CreateSubProjectBtn.Text = "Sub-project";
            // 
            // ProjectConfigForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(867, 385);
            this.Controls.Add(this.BillMat_toolStrip);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.bunifuSeparator1);
            this.Controls.Add(this.bunifuSeparator2);
            this.Font = new System.Drawing.Font("Century Gothic", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.Name = "ProjectConfigForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "New_project_form";
            this.TopMost = true;
            this.Activated += new System.EventHandler(this.New_project_form_Activated);
            this.Deactivate += new System.EventHandler(this.New_project_form_Deactivate);
            this.Load += new System.EventHandler(this.New_project_form_Load);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.main_project_dataGridView)).EndInit();
            this.contextMenuStrip1.ResumeLayout(false);
            this.groupBox3.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.sub_project_dataGridView)).EndInit();
            this.BillMat_toolStrip.ResumeLayout(false);
            this.BillMat_toolStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox sub_project_name_txt;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.DateTimePicker date_started_dtp;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox project_name_txt;
        private Bunifu.Framework.UI.BunifuElipse bunifuElipse1;
        private Bunifu.Framework.UI.BunifuElipse bunifuElipse2;
        private Bunifu.Framework.UI.BunifuElipse bunifuElipse3;
        private System.Windows.Forms.Panel panel2;
        private Bunifu.Framework.UI.BunifuDragControl bunifuDragControl1;
        private System.Windows.Forms.GroupBox groupBox1;
     //   private System.Windows.Forms.ToolStripButton edit_toolStripButton;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator12;
       // private System.Windows.Forms.ToolStripButton remove_toolStripButton;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator16;
    //    private System.Windows.Forms.ToolStripButton home_toolStripButton;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.DataGridView main_project_dataGridView;
        private System.Windows.Forms.ToolStripMenuItem projectToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem subprojectToolStripMenuItem;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.DataGridView sub_project_dataGridView;
        private Bunifu.Framework.UI.BunifuSeparator bunifuSeparator1;
        private Bunifu.Framework.UI.BunifuSeparator bunifuSeparator2;
      //  public System.Windows.Forms.ToolStripDropDownButton Add_toolStripButton;
        public System.Windows.Forms.ToolStripSeparator toolStripSeparator11;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem deleteToolStripMenuItem;
        private System.Windows.Forms.ToolStrip BillMat_toolStrip;
        private System.Windows.Forms.ToolStripButton home_toolStripButton;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        public System.Windows.Forms.ToolStripDropDownButton Add_toolStripButton;
        public System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripButton edit_toolStripButton;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripButton remove_toolStripButton;
        private System.Windows.Forms.ToolStripMenuItem newProjectBtn;
        private System.Windows.Forms.ToolStripMenuItem CreateMainProjectBtn;
        private System.Windows.Forms.ToolStripMenuItem CreateSubProjectBtn;
    }
}