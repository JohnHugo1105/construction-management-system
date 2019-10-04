namespace GSG_Builders.RevisionedUI.Warehouse_inventory {
    partial class Outgoing_form {
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
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle7 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle8 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle9 = new System.Windows.Forms.DataGridViewCellStyle();
            BunifuAnimatorNS.Animation animation6 = new BunifuAnimatorNS.Animation();
            BunifuAnimatorNS.Animation animation5 = new BunifuAnimatorNS.Animation();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Outgoing_form));
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.project_list_selected_label = new System.Windows.Forms.Label();
            this.assigned_project_datagridView = new System.Windows.Forms.DataGridView();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.tableLayoutPanel5 = new System.Windows.Forms.TableLayoutPanel();
            this.label4 = new System.Windows.Forms.Label();
            this.date_out_dateTimePicker = new System.Windows.Forms.DateTimePicker();
            this.tableLayoutPanel3 = new System.Windows.Forms.TableLayoutPanel();
            this.label3 = new System.Windows.Forms.Label();
            this.without_amount_metroRadioButton = new MetroFramework.Controls.MetroRadioButton();
            this.quantity_textBox = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.with_amount_metroRadioButton = new MetroFramework.Controls.MetroRadioButton();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this.project_textBox = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.tableLayoutPanel6 = new System.Windows.Forms.TableLayoutPanel();
            this.label5 = new System.Windows.Forms.Label();
            this.remarks_textBox = new System.Windows.Forms.TextBox();
            this.cancel_bunifuThinButton = new Bunifu.Framework.UI.BunifuThinButton2();
            this.out_bunifuThinButton = new Bunifu.Framework.UI.BunifuThinButton2();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.item_description_textBox = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.bunifuTransition1 = new BunifuAnimatorNS.BunifuTransition(this.components);
            this.bunifuTransition2 = new BunifuAnimatorNS.BunifuTransition(this.components);
            this.groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.assigned_project_datagridView)).BeginInit();
            this.groupBox2.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.tableLayoutPanel5.SuspendLayout();
            this.tableLayoutPanel3.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            this.tableLayoutPanel6.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.project_list_selected_label);
            this.groupBox1.Controls.Add(this.assigned_project_datagridView);
            this.bunifuTransition2.SetDecoration(this.groupBox1, BunifuAnimatorNS.DecorationType.None);
            this.bunifuTransition1.SetDecoration(this.groupBox1, BunifuAnimatorNS.DecorationType.None);
            this.groupBox1.Font = new System.Drawing.Font("Century Gothic", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox1.Location = new System.Drawing.Point(12, 84);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(348, 235);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Assigned project/\'s";
            // 
            // project_list_selected_label
            // 
            this.project_list_selected_label.AutoSize = true;
            this.bunifuTransition1.SetDecoration(this.project_list_selected_label, BunifuAnimatorNS.DecorationType.None);
            this.bunifuTransition2.SetDecoration(this.project_list_selected_label, BunifuAnimatorNS.DecorationType.None);
            this.project_list_selected_label.Font = new System.Drawing.Font("Century Gothic", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.project_list_selected_label.Location = new System.Drawing.Point(219, 3);
            this.project_list_selected_label.Name = "project_list_selected_label";
            this.project_list_selected_label.Size = new System.Drawing.Size(123, 16);
            this.project_list_selected_label.TabIndex = 96;
            this.project_list_selected_label.Text = "click for selection";
            // 
            // assigned_project_datagridView
            // 
            this.assigned_project_datagridView.AllowUserToAddRows = false;
            this.assigned_project_datagridView.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.AllCells;
            this.assigned_project_datagridView.BackgroundColor = System.Drawing.Color.WhiteSmoke;
            dataGridViewCellStyle7.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle7.BackColor = System.Drawing.Color.DodgerBlue;
            dataGridViewCellStyle7.Font = new System.Drawing.Font("Century Gothic", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle7.ForeColor = System.Drawing.Color.White;
            dataGridViewCellStyle7.SelectionBackColor = System.Drawing.Color.Gainsboro;
            dataGridViewCellStyle7.SelectionForeColor = System.Drawing.Color.Black;
            dataGridViewCellStyle7.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.assigned_project_datagridView.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle7;
            this.assigned_project_datagridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.bunifuTransition2.SetDecoration(this.assigned_project_datagridView, BunifuAnimatorNS.DecorationType.None);
            this.bunifuTransition1.SetDecoration(this.assigned_project_datagridView, BunifuAnimatorNS.DecorationType.None);
            dataGridViewCellStyle8.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle8.BackColor = System.Drawing.Color.White;
            dataGridViewCellStyle8.Font = new System.Drawing.Font("Century Gothic", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle8.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle8.SelectionBackColor = System.Drawing.Color.Gainsboro;
            dataGridViewCellStyle8.SelectionForeColor = System.Drawing.Color.Black;
            dataGridViewCellStyle8.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.assigned_project_datagridView.DefaultCellStyle = dataGridViewCellStyle8;
            this.assigned_project_datagridView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.assigned_project_datagridView.EnableHeadersVisualStyles = false;
            this.assigned_project_datagridView.Location = new System.Drawing.Point(3, 22);
            this.assigned_project_datagridView.Name = "assigned_project_datagridView";
            this.assigned_project_datagridView.RowHeadersWidth = 23;
            dataGridViewCellStyle9.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle9.Font = new System.Drawing.Font("Century Gothic", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle9.ForeColor = System.Drawing.Color.Black;
            dataGridViewCellStyle9.SelectionBackColor = System.Drawing.Color.Gainsboro;
            dataGridViewCellStyle9.SelectionForeColor = System.Drawing.Color.Black;
            this.assigned_project_datagridView.RowsDefaultCellStyle = dataGridViewCellStyle9;
            this.assigned_project_datagridView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.assigned_project_datagridView.Size = new System.Drawing.Size(342, 210);
            this.assigned_project_datagridView.TabIndex = 95;
            this.assigned_project_datagridView.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.assigned_project_datagridView_CellClick);
            this.assigned_project_datagridView.Click += new System.EventHandler(this.assigned_project_datagridView_Click);
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.tableLayoutPanel1);
            this.bunifuTransition2.SetDecoration(this.groupBox2, BunifuAnimatorNS.DecorationType.None);
            this.bunifuTransition1.SetDecoration(this.groupBox2, BunifuAnimatorNS.DecorationType.None);
            this.groupBox2.Font = new System.Drawing.Font("Century Gothic", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox2.Location = new System.Drawing.Point(379, 30);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(419, 289);
            this.groupBox2.TabIndex = 1;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "To project";
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 1;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Controls.Add(this.tableLayoutPanel5, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.tableLayoutPanel3, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.tableLayoutPanel2, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.tableLayoutPanel6, 0, 3);
            this.bunifuTransition1.SetDecoration(this.tableLayoutPanel1, BunifuAnimatorNS.DecorationType.None);
            this.bunifuTransition2.SetDecoration(this.tableLayoutPanel1, BunifuAnimatorNS.DecorationType.None);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(3, 22);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 4;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(413, 264);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // tableLayoutPanel5
            // 
            this.tableLayoutPanel5.ColumnCount = 1;
            this.tableLayoutPanel5.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel5.Controls.Add(this.label4, 0, 1);
            this.tableLayoutPanel5.Controls.Add(this.date_out_dateTimePicker, 0, 0);
            this.bunifuTransition2.SetDecoration(this.tableLayoutPanel5, BunifuAnimatorNS.DecorationType.None);
            this.bunifuTransition1.SetDecoration(this.tableLayoutPanel5, BunifuAnimatorNS.DecorationType.None);
            this.tableLayoutPanel5.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel5.Location = new System.Drawing.Point(3, 135);
            this.tableLayoutPanel5.Name = "tableLayoutPanel5";
            this.tableLayoutPanel5.RowCount = 2;
            this.tableLayoutPanel5.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel5.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel5.Size = new System.Drawing.Size(407, 60);
            this.tableLayoutPanel5.TabIndex = 3;
            // 
            // label4
            // 
            this.label4.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.label4.AutoSize = true;
            this.bunifuTransition2.SetDecoration(this.label4, BunifuAnimatorNS.DecorationType.None);
            this.bunifuTransition1.SetDecoration(this.label4, BunifuAnimatorNS.DecorationType.None);
            this.label4.Font = new System.Drawing.Font("Century Gothic", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label4.Location = new System.Drawing.Point(3, 37);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(56, 16);
            this.label4.TabIndex = 1;
            this.label4.Text = "Date out";
            this.label4.Click += new System.EventHandler(this.label4_Click);
            // 
            // date_out_dateTimePicker
            // 
            this.date_out_dateTimePicker.CustomFormat = "MMMM dd, yyyy";
            this.bunifuTransition1.SetDecoration(this.date_out_dateTimePicker, BunifuAnimatorNS.DecorationType.None);
            this.bunifuTransition2.SetDecoration(this.date_out_dateTimePicker, BunifuAnimatorNS.DecorationType.None);
            this.date_out_dateTimePicker.Font = new System.Drawing.Font("Century Gothic", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.date_out_dateTimePicker.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.date_out_dateTimePicker.Location = new System.Drawing.Point(3, 3);
            this.date_out_dateTimePicker.Name = "date_out_dateTimePicker";
            this.date_out_dateTimePicker.Size = new System.Drawing.Size(173, 23);
            this.date_out_dateTimePicker.TabIndex = 4;
            // 
            // tableLayoutPanel3
            // 
            this.tableLayoutPanel3.ColumnCount = 3;
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 127F));
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 106F));
            this.tableLayoutPanel3.Controls.Add(this.label3, 1, 1);
            this.tableLayoutPanel3.Controls.Add(this.without_amount_metroRadioButton, 2, 0);
            this.tableLayoutPanel3.Controls.Add(this.quantity_textBox, 0, 0);
            this.tableLayoutPanel3.Controls.Add(this.label2, 0, 1);
            this.tableLayoutPanel3.Controls.Add(this.with_amount_metroRadioButton, 1, 0);
            this.bunifuTransition2.SetDecoration(this.tableLayoutPanel3, BunifuAnimatorNS.DecorationType.None);
            this.bunifuTransition1.SetDecoration(this.tableLayoutPanel3, BunifuAnimatorNS.DecorationType.None);
            this.tableLayoutPanel3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel3.Location = new System.Drawing.Point(3, 69);
            this.tableLayoutPanel3.Name = "tableLayoutPanel3";
            this.tableLayoutPanel3.RowCount = 2;
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel3.Size = new System.Drawing.Size(407, 60);
            this.tableLayoutPanel3.TabIndex = 1;
            // 
            // label3
            // 
            this.label3.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.label3.AutoSize = true;
            this.bunifuTransition2.SetDecoration(this.label3, BunifuAnimatorNS.DecorationType.None);
            this.bunifuTransition1.SetDecoration(this.label3, BunifuAnimatorNS.DecorationType.None);
            this.label3.Font = new System.Drawing.Font("Century Gothic", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(177, 37);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(117, 16);
            this.label3.TabIndex = 1;
            this.label3.Text = "Item cost selection";
            // 
            // without_amount_metroRadioButton
            // 
            this.without_amount_metroRadioButton.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.without_amount_metroRadioButton.AutoSize = true;
            this.bunifuTransition2.SetDecoration(this.without_amount_metroRadioButton, BunifuAnimatorNS.DecorationType.None);
            this.bunifuTransition1.SetDecoration(this.without_amount_metroRadioButton, BunifuAnimatorNS.DecorationType.None);
            this.without_amount_metroRadioButton.FontWeight = MetroFramework.MetroCheckBoxWeight.Bold;
            this.without_amount_metroRadioButton.Location = new System.Drawing.Point(304, 7);
            this.without_amount_metroRadioButton.Name = "without_amount_metroRadioButton";
            this.without_amount_metroRadioButton.Size = new System.Drawing.Size(93, 15);
            this.without_amount_metroRadioButton.TabIndex = 98;
            this.without_amount_metroRadioButton.Text = "W/o amount";
            this.without_amount_metroRadioButton.UseSelectable = true;
            // 
            // quantity_textBox
            // 
            this.bunifuTransition2.SetDecoration(this.quantity_textBox, BunifuAnimatorNS.DecorationType.None);
            this.bunifuTransition1.SetDecoration(this.quantity_textBox, BunifuAnimatorNS.DecorationType.None);
            this.quantity_textBox.Font = new System.Drawing.Font("Century Gothic", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.quantity_textBox.Location = new System.Drawing.Point(3, 3);
            this.quantity_textBox.Name = "quantity_textBox";
            this.quantity_textBox.Size = new System.Drawing.Size(90, 23);
            this.quantity_textBox.TabIndex = 0;
            // 
            // label2
            // 
            this.label2.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.label2.AutoSize = true;
            this.bunifuTransition2.SetDecoration(this.label2, BunifuAnimatorNS.DecorationType.None);
            this.bunifuTransition1.SetDecoration(this.label2, BunifuAnimatorNS.DecorationType.None);
            this.label2.Font = new System.Drawing.Font("Century Gothic", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(3, 37);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(56, 16);
            this.label2.TabIndex = 1;
            this.label2.Text = "Quantity";
            // 
            // with_amount_metroRadioButton
            // 
            this.with_amount_metroRadioButton.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.with_amount_metroRadioButton.AutoSize = true;
            this.bunifuTransition2.SetDecoration(this.with_amount_metroRadioButton, BunifuAnimatorNS.DecorationType.None);
            this.bunifuTransition1.SetDecoration(this.with_amount_metroRadioButton, BunifuAnimatorNS.DecorationType.None);
            this.with_amount_metroRadioButton.FontWeight = MetroFramework.MetroCheckBoxWeight.Bold;
            this.with_amount_metroRadioButton.Location = new System.Drawing.Point(177, 7);
            this.with_amount_metroRadioButton.Name = "with_amount_metroRadioButton";
            this.with_amount_metroRadioButton.Size = new System.Drawing.Size(86, 15);
            this.with_amount_metroRadioButton.TabIndex = 99;
            this.with_amount_metroRadioButton.Text = "W/ amount";
            this.with_amount_metroRadioButton.UseSelectable = true;
            // 
            // tableLayoutPanel2
            // 
            this.tableLayoutPanel2.ColumnCount = 1;
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel2.Controls.Add(this.project_textBox, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this.label1, 0, 1);
            this.bunifuTransition2.SetDecoration(this.tableLayoutPanel2, BunifuAnimatorNS.DecorationType.None);
            this.bunifuTransition1.SetDecoration(this.tableLayoutPanel2, BunifuAnimatorNS.DecorationType.None);
            this.tableLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel2.Location = new System.Drawing.Point(3, 3);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.RowCount = 2;
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel2.Size = new System.Drawing.Size(407, 60);
            this.tableLayoutPanel2.TabIndex = 0;
            // 
            // project_textBox
            // 
            this.bunifuTransition2.SetDecoration(this.project_textBox, BunifuAnimatorNS.DecorationType.None);
            this.bunifuTransition1.SetDecoration(this.project_textBox, BunifuAnimatorNS.DecorationType.None);
            this.project_textBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.project_textBox.Font = new System.Drawing.Font("Century Gothic", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.project_textBox.Location = new System.Drawing.Point(3, 3);
            this.project_textBox.Name = "project_textBox";
            this.project_textBox.Size = new System.Drawing.Size(401, 23);
            this.project_textBox.TabIndex = 0;
            this.project_textBox.Leave += new System.EventHandler(this.project_textBox_Leave);
            // 
            // label1
            // 
            this.label1.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.label1.AutoSize = true;
            this.bunifuTransition2.SetDecoration(this.label1, BunifuAnimatorNS.DecorationType.None);
            this.bunifuTransition1.SetDecoration(this.label1, BunifuAnimatorNS.DecorationType.None);
            this.label1.Font = new System.Drawing.Font("Century Gothic", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(3, 37);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(49, 16);
            this.label1.TabIndex = 1;
            this.label1.Text = "Project";
            // 
            // tableLayoutPanel6
            // 
            this.tableLayoutPanel6.ColumnCount = 1;
            this.tableLayoutPanel6.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel6.Controls.Add(this.label5, 0, 1);
            this.tableLayoutPanel6.Controls.Add(this.remarks_textBox, 0, 0);
            this.bunifuTransition2.SetDecoration(this.tableLayoutPanel6, BunifuAnimatorNS.DecorationType.None);
            this.bunifuTransition1.SetDecoration(this.tableLayoutPanel6, BunifuAnimatorNS.DecorationType.None);
            this.tableLayoutPanel6.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel6.Location = new System.Drawing.Point(3, 201);
            this.tableLayoutPanel6.Name = "tableLayoutPanel6";
            this.tableLayoutPanel6.RowCount = 2;
            this.tableLayoutPanel6.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel6.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel6.Size = new System.Drawing.Size(407, 60);
            this.tableLayoutPanel6.TabIndex = 4;
            // 
            // label5
            // 
            this.label5.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.label5.AutoSize = true;
            this.bunifuTransition2.SetDecoration(this.label5, BunifuAnimatorNS.DecorationType.None);
            this.bunifuTransition1.SetDecoration(this.label5, BunifuAnimatorNS.DecorationType.None);
            this.label5.Font = new System.Drawing.Font("Century Gothic", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label5.Location = new System.Drawing.Point(3, 37);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(58, 16);
            this.label5.TabIndex = 2;
            this.label5.Text = "Remarks";
            this.label5.Click += new System.EventHandler(this.label5_Click);
            // 
            // remarks_textBox
            // 
            this.bunifuTransition2.SetDecoration(this.remarks_textBox, BunifuAnimatorNS.DecorationType.None);
            this.bunifuTransition1.SetDecoration(this.remarks_textBox, BunifuAnimatorNS.DecorationType.None);
            this.remarks_textBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.remarks_textBox.Location = new System.Drawing.Point(3, 3);
            this.remarks_textBox.Name = "remarks_textBox";
            this.remarks_textBox.Size = new System.Drawing.Size(401, 26);
            this.remarks_textBox.TabIndex = 0;
            // 
            // cancel_bunifuThinButton
            // 
            this.cancel_bunifuThinButton.ActiveBorderThickness = 1;
            this.cancel_bunifuThinButton.ActiveCornerRadius = 20;
            this.cancel_bunifuThinButton.ActiveFillColor = System.Drawing.Color.Red;
            this.cancel_bunifuThinButton.ActiveForecolor = System.Drawing.Color.White;
            this.cancel_bunifuThinButton.ActiveLineColor = System.Drawing.Color.SeaGreen;
            this.cancel_bunifuThinButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cancel_bunifuThinButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.cancel_bunifuThinButton.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("cancel_bunifuThinButton.BackgroundImage")));
            this.cancel_bunifuThinButton.ButtonText = "CANCEL";
            this.cancel_bunifuThinButton.Cursor = System.Windows.Forms.Cursors.Hand;
            this.bunifuTransition2.SetDecoration(this.cancel_bunifuThinButton, BunifuAnimatorNS.DecorationType.None);
            this.bunifuTransition1.SetDecoration(this.cancel_bunifuThinButton, BunifuAnimatorNS.DecorationType.None);
            this.cancel_bunifuThinButton.Font = new System.Drawing.Font("Century Gothic", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cancel_bunifuThinButton.ForeColor = System.Drawing.Color.DarkSlateGray;
            this.cancel_bunifuThinButton.IdleBorderThickness = 1;
            this.cancel_bunifuThinButton.IdleCornerRadius = 20;
            this.cancel_bunifuThinButton.IdleFillColor = System.Drawing.Color.White;
            this.cancel_bunifuThinButton.IdleForecolor = System.Drawing.Color.DarkSlateGray;
            this.cancel_bunifuThinButton.IdleLineColor = System.Drawing.Color.DodgerBlue;
            this.cancel_bunifuThinButton.Location = new System.Drawing.Point(705, 327);
            this.cancel_bunifuThinButton.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.cancel_bunifuThinButton.Name = "cancel_bunifuThinButton";
            this.cancel_bunifuThinButton.Size = new System.Drawing.Size(80, 32);
            this.cancel_bunifuThinButton.TabIndex = 11;
            this.cancel_bunifuThinButton.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.cancel_bunifuThinButton.Click += new System.EventHandler(this.cancel_bunifuThinButton_Click);
            // 
            // out_bunifuThinButton
            // 
            this.out_bunifuThinButton.ActiveBorderThickness = 1;
            this.out_bunifuThinButton.ActiveCornerRadius = 20;
            this.out_bunifuThinButton.ActiveFillColor = System.Drawing.Color.DodgerBlue;
            this.out_bunifuThinButton.ActiveForecolor = System.Drawing.Color.White;
            this.out_bunifuThinButton.ActiveLineColor = System.Drawing.Color.SeaGreen;
            this.out_bunifuThinButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.out_bunifuThinButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.out_bunifuThinButton.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("out_bunifuThinButton.BackgroundImage")));
            this.out_bunifuThinButton.ButtonText = "Out";
            this.out_bunifuThinButton.Cursor = System.Windows.Forms.Cursors.Hand;
            this.bunifuTransition2.SetDecoration(this.out_bunifuThinButton, BunifuAnimatorNS.DecorationType.None);
            this.bunifuTransition1.SetDecoration(this.out_bunifuThinButton, BunifuAnimatorNS.DecorationType.None);
            this.out_bunifuThinButton.Font = new System.Drawing.Font("Century Gothic", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.out_bunifuThinButton.ForeColor = System.Drawing.Color.DarkSlateGray;
            this.out_bunifuThinButton.IdleBorderThickness = 1;
            this.out_bunifuThinButton.IdleCornerRadius = 20;
            this.out_bunifuThinButton.IdleFillColor = System.Drawing.Color.White;
            this.out_bunifuThinButton.IdleForecolor = System.Drawing.Color.DarkSlateGray;
            this.out_bunifuThinButton.IdleLineColor = System.Drawing.Color.DodgerBlue;
            this.out_bunifuThinButton.Location = new System.Drawing.Point(617, 327);
            this.out_bunifuThinButton.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.out_bunifuThinButton.Name = "out_bunifuThinButton";
            this.out_bunifuThinButton.Size = new System.Drawing.Size(80, 32);
            this.out_bunifuThinButton.TabIndex = 10;
            this.out_bunifuThinButton.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.out_bunifuThinButton.Click += new System.EventHandler(this.out_bunifuThinButton_Click);
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.item_description_textBox);
            this.bunifuTransition2.SetDecoration(this.groupBox3, BunifuAnimatorNS.DecorationType.None);
            this.bunifuTransition1.SetDecoration(this.groupBox3, BunifuAnimatorNS.DecorationType.None);
            this.groupBox3.Font = new System.Drawing.Font("Century Gothic", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox3.Location = new System.Drawing.Point(12, 29);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(348, 56);
            this.groupBox3.TabIndex = 12;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Item";
            // 
            // item_description_textBox
            // 
            this.bunifuTransition1.SetDecoration(this.item_description_textBox, BunifuAnimatorNS.DecorationType.None);
            this.bunifuTransition2.SetDecoration(this.item_description_textBox, BunifuAnimatorNS.DecorationType.None);
            this.item_description_textBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.item_description_textBox.Font = new System.Drawing.Font("Century Gothic", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.item_description_textBox.Location = new System.Drawing.Point(3, 23);
            this.item_description_textBox.Name = "item_description_textBox";
            this.item_description_textBox.Size = new System.Drawing.Size(342, 23);
            this.item_description_textBox.TabIndex = 1;
            // 
            // label6
            // 
            this.bunifuTransition1.SetDecoration(this.label6, BunifuAnimatorNS.DecorationType.None);
            this.bunifuTransition2.SetDecoration(this.label6, BunifuAnimatorNS.DecorationType.None);
            this.label6.Location = new System.Drawing.Point(12, 327);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(528, 32);
            this.label6.TabIndex = 13;
            this.label6.Text = "NOTE : you can assign items that are assigned in projects that are on the list by" +
    " selecting it or use from warehouse stock by unselecting the selected project in" +
    " the list";
            // 
            // bunifuTransition1
            // 
            this.bunifuTransition1.AnimationType = BunifuAnimatorNS.AnimationType.Transparent;
            this.bunifuTransition1.Cursor = null;
            animation6.AnimateOnlyDifferences = true;
            animation6.BlindCoeff = ((System.Drawing.PointF)(resources.GetObject("animation6.BlindCoeff")));
            animation6.LeafCoeff = 0F;
            animation6.MaxTime = 1F;
            animation6.MinTime = 0F;
            animation6.MosaicCoeff = ((System.Drawing.PointF)(resources.GetObject("animation6.MosaicCoeff")));
            animation6.MosaicShift = ((System.Drawing.PointF)(resources.GetObject("animation6.MosaicShift")));
            animation6.MosaicSize = 0;
            animation6.Padding = new System.Windows.Forms.Padding(0);
            animation6.RotateCoeff = 0F;
            animation6.RotateLimit = 0F;
            animation6.ScaleCoeff = ((System.Drawing.PointF)(resources.GetObject("animation6.ScaleCoeff")));
            animation6.SlideCoeff = ((System.Drawing.PointF)(resources.GetObject("animation6.SlideCoeff")));
            animation6.TimeCoeff = 0F;
            animation6.TransparencyCoeff = 1F;
            this.bunifuTransition1.DefaultAnimation = animation6;
            this.bunifuTransition1.Interval = 25;
            // 
            // bunifuTransition2
            // 
            this.bunifuTransition2.AnimationType = BunifuAnimatorNS.AnimationType.Transparent;
            this.bunifuTransition2.Cursor = null;
            animation5.AnimateOnlyDifferences = true;
            animation5.BlindCoeff = ((System.Drawing.PointF)(resources.GetObject("animation5.BlindCoeff")));
            animation5.LeafCoeff = 0F;
            animation5.MaxTime = 1F;
            animation5.MinTime = 0F;
            animation5.MosaicCoeff = ((System.Drawing.PointF)(resources.GetObject("animation5.MosaicCoeff")));
            animation5.MosaicShift = ((System.Drawing.PointF)(resources.GetObject("animation5.MosaicShift")));
            animation5.MosaicSize = 0;
            animation5.Padding = new System.Windows.Forms.Padding(0);
            animation5.RotateCoeff = 0F;
            animation5.RotateLimit = 0F;
            animation5.ScaleCoeff = ((System.Drawing.PointF)(resources.GetObject("animation5.ScaleCoeff")));
            animation5.SlideCoeff = ((System.Drawing.PointF)(resources.GetObject("animation5.SlideCoeff")));
            animation5.TimeCoeff = 0F;
            animation5.TransparencyCoeff = 1F;
            this.bunifuTransition2.DefaultAnimation = animation5;
            // 
            // Outgoing_form
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(812, 361);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.cancel_bunifuThinButton);
            this.Controls.Add(this.out_bunifuThinButton);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.bunifuTransition2.SetDecoration(this, BunifuAnimatorNS.DecorationType.None);
            this.bunifuTransition1.SetDecoration(this, BunifuAnimatorNS.DecorationType.None);
            this.Font = new System.Drawing.Font("Century Gothic", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.MaximizeBox = false;
            this.Name = "Outgoing_form";
            this.Padding = new System.Windows.Forms.Padding(23, 69, 23, 23);
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.Activated += new System.EventHandler(this.Outgoing_form_Activated);
            this.Deactivate += new System.EventHandler(this.Outgoing_form_Deactivate);
            this.Load += new System.EventHandler(this.Outgoing_form_Load);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.assigned_project_datagridView)).EndInit();
            this.groupBox2.ResumeLayout(false);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel5.ResumeLayout(false);
            this.tableLayoutPanel5.PerformLayout();
            this.tableLayoutPanel3.ResumeLayout(false);
            this.tableLayoutPanel3.PerformLayout();
            this.tableLayoutPanel2.ResumeLayout(false);
            this.tableLayoutPanel2.PerformLayout();
            this.tableLayoutPanel6.ResumeLayout(false);
            this.tableLayoutPanel6.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel3;
        private System.Windows.Forms.TextBox quantity_textBox;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox project_textBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.DateTimePicker date_out_dateTimePicker;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel5;
        private Bunifu.Framework.UI.BunifuThinButton2 cancel_bunifuThinButton;
        private Bunifu.Framework.UI.BunifuThinButton2 out_bunifuThinButton;
        private MetroFramework.Controls.MetroRadioButton without_amount_metroRadioButton;
        private MetroFramework.Controls.MetroRadioButton with_amount_metroRadioButton;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.TextBox item_description_textBox;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel6;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox remarks_textBox;
        private System.Windows.Forms.DataGridView assigned_project_datagridView;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label project_list_selected_label;
        private BunifuAnimatorNS.BunifuTransition bunifuTransition1;
        private BunifuAnimatorNS.BunifuTransition bunifuTransition2;
    }
}