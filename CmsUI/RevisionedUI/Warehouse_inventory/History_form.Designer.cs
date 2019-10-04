namespace GSG_Builders.RevisionedUI.Warehouse_inventory {
    partial class History_form {
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
            this.item_history_dataGridView = new System.Windows.Forms.DataGridView();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.editItemToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.groupBox5 = new System.Windows.Forms.GroupBox();
            this.date_purchased_label = new System.Windows.Forms.Label();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.serial_num_label = new System.Windows.Forms.Label();
            this.groupBox6 = new System.Windows.Forms.GroupBox();
            this.brand_label1 = new System.Windows.Forms.Label();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.invoice_num_label = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.item_label = new System.Windows.Forms.Label();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.category_label = new System.Windows.Forms.Label();
            this.dateTimePicker2 = new System.Windows.Forms.DateTimePicker();
            this.project_from_assigned_textBox = new System.Windows.Forms.TextBox();
            ((System.ComponentModel.ISupportInitialize)(this.item_history_dataGridView)).BeginInit();
            this.contextMenuStrip1.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.groupBox5.SuspendLayout();
            this.groupBox4.SuspendLayout();
            this.groupBox6.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.SuspendLayout();
            // 
            // item_history_dataGridView
            // 
            this.item_history_dataGridView.AllowUserToAddRows = false;
            this.item_history_dataGridView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.item_history_dataGridView.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.AllCells;
            this.item_history_dataGridView.BackgroundColor = System.Drawing.Color.WhiteSmoke;
            dataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle4.BackColor = System.Drawing.Color.DodgerBlue;
            dataGridViewCellStyle4.Font = new System.Drawing.Font("Century Gothic", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle4.ForeColor = System.Drawing.Color.White;
            dataGridViewCellStyle4.SelectionBackColor = System.Drawing.Color.Gainsboro;
            dataGridViewCellStyle4.SelectionForeColor = System.Drawing.Color.Black;
            dataGridViewCellStyle4.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.item_history_dataGridView.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle4;
            this.item_history_dataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.item_history_dataGridView.ContextMenuStrip = this.contextMenuStrip1;
            dataGridViewCellStyle5.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle5.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle5.Font = new System.Drawing.Font("Century Gothic", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle5.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle5.SelectionBackColor = System.Drawing.Color.Gainsboro;
            dataGridViewCellStyle5.SelectionForeColor = System.Drawing.Color.Black;
            dataGridViewCellStyle5.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.item_history_dataGridView.DefaultCellStyle = dataGridViewCellStyle5;
            this.item_history_dataGridView.EnableHeadersVisualStyles = false;
            this.item_history_dataGridView.Location = new System.Drawing.Point(15, 103);
            this.item_history_dataGridView.Name = "item_history_dataGridView";
            this.item_history_dataGridView.ReadOnly = true;
            this.item_history_dataGridView.RowHeadersWidth = 23;
            dataGridViewCellStyle6.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle6.Font = new System.Drawing.Font("Century Gothic", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle6.ForeColor = System.Drawing.Color.Black;
            dataGridViewCellStyle6.SelectionBackColor = System.Drawing.Color.Gainsboro;
            dataGridViewCellStyle6.SelectionForeColor = System.Drawing.Color.Black;
            this.item_history_dataGridView.RowsDefaultCellStyle = dataGridViewCellStyle6;
            this.item_history_dataGridView.Size = new System.Drawing.Size(1122, 451);
            this.item_history_dataGridView.TabIndex = 0;
            this.item_history_dataGridView.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.item_history_dataGridView_CellClick);
            this.item_history_dataGridView.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.item_history_dataGridView_CellEndEdit);
            this.item_history_dataGridView.CellMouseDown += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.item_history_dataGridView_CellMouseDown);
            this.item_history_dataGridView.CurrentCellDirtyStateChanged += new System.EventHandler(this.item_history_dataGridView_CurrentCellDirtyStateChanged);
            this.item_history_dataGridView.RowLeave += new System.Windows.Forms.DataGridViewCellEventHandler(this.item_history_dataGridView_RowLeave);
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Font = new System.Drawing.Font("Century Gothic", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.editItemToolStripMenuItem,
            this.removeToolStripMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(145, 48);
            // 
            // editItemToolStripMenuItem
            // 
            this.editItemToolStripMenuItem.Name = "editItemToolStripMenuItem";
            this.editItemToolStripMenuItem.Size = new System.Drawing.Size(144, 22);
            this.editItemToolStripMenuItem.Text = "Edit records";
            this.editItemToolStripMenuItem.Visible = false;
            this.editItemToolStripMenuItem.Click += new System.EventHandler(this.editItemToolStripMenuItem_Click);
            // 
            // removeToolStripMenuItem
            // 
            this.removeToolStripMenuItem.Name = "removeToolStripMenuItem";
            this.removeToolStripMenuItem.Size = new System.Drawing.Size(144, 22);
            this.removeToolStripMenuItem.Text = "Remove";
            this.removeToolStripMenuItem.Visible = false;
            this.removeToolStripMenuItem.Click += new System.EventHandler(this.removeToolStripMenuItem_Click);
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 6;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 30F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 12F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 12F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 12F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 17F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 17F));
            this.tableLayoutPanel1.Controls.Add(this.groupBox5, 5, 0);
            this.tableLayoutPanel1.Controls.Add(this.groupBox4, 3, 0);
            this.tableLayoutPanel1.Controls.Add(this.groupBox6, 4, 0);
            this.tableLayoutPanel1.Controls.Add(this.groupBox3, 2, 0);
            this.tableLayoutPanel1.Controls.Add(this.groupBox1, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.groupBox2, 1, 0);
            this.tableLayoutPanel1.Location = new System.Drawing.Point(15, 50);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 1;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(1122, 47);
            this.tableLayoutPanel1.TabIndex = 1;
            // 
            // groupBox5
            // 
            this.groupBox5.Controls.Add(this.date_purchased_label);
            this.groupBox5.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox5.Location = new System.Drawing.Point(931, 3);
            this.groupBox5.Name = "groupBox5";
            this.groupBox5.Size = new System.Drawing.Size(188, 41);
            this.groupBox5.TabIndex = 4;
            this.groupBox5.TabStop = false;
            this.groupBox5.Text = "DATE PURCHASED";
            // 
            // date_purchased_label
            // 
            this.date_purchased_label.AutoSize = true;
            this.date_purchased_label.Font = new System.Drawing.Font("Century Gothic", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.date_purchased_label.Location = new System.Drawing.Point(4, 20);
            this.date_purchased_label.Name = "date_purchased_label";
            this.date_purchased_label.Size = new System.Drawing.Size(0, 16);
            this.date_purchased_label.TabIndex = 0;
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.serial_num_label);
            this.groupBox4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox4.Location = new System.Drawing.Point(607, 3);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(128, 41);
            this.groupBox4.TabIndex = 3;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "SERIAL-#";
            // 
            // serial_num_label
            // 
            this.serial_num_label.AutoSize = true;
            this.serial_num_label.Font = new System.Drawing.Font("Century Gothic", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.serial_num_label.Location = new System.Drawing.Point(4, 20);
            this.serial_num_label.Name = "serial_num_label";
            this.serial_num_label.Size = new System.Drawing.Size(0, 16);
            this.serial_num_label.TabIndex = 0;
            // 
            // groupBox6
            // 
            this.groupBox6.Controls.Add(this.brand_label1);
            this.groupBox6.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox6.Location = new System.Drawing.Point(741, 3);
            this.groupBox6.Name = "groupBox6";
            this.groupBox6.Size = new System.Drawing.Size(184, 41);
            this.groupBox6.TabIndex = 5;
            this.groupBox6.TabStop = false;
            this.groupBox6.Text = "BRAND";
            // 
            // brand_label1
            // 
            this.brand_label1.AutoSize = true;
            this.brand_label1.Font = new System.Drawing.Font("Century Gothic", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.brand_label1.Location = new System.Drawing.Point(4, 20);
            this.brand_label1.Name = "brand_label1";
            this.brand_label1.Size = new System.Drawing.Size(0, 16);
            this.brand_label1.TabIndex = 0;
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.invoice_num_label);
            this.groupBox3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox3.Location = new System.Drawing.Point(473, 3);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(128, 41);
            this.groupBox3.TabIndex = 2;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "INVOICE-#";
            // 
            // invoice_num_label
            // 
            this.invoice_num_label.AutoSize = true;
            this.invoice_num_label.Font = new System.Drawing.Font("Century Gothic", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.invoice_num_label.Location = new System.Drawing.Point(4, 20);
            this.invoice_num_label.Name = "invoice_num_label";
            this.invoice_num_label.Size = new System.Drawing.Size(0, 16);
            this.invoice_num_label.TabIndex = 0;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.item_label);
            this.groupBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox1.Location = new System.Drawing.Point(3, 3);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(330, 41);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "ITEM";
            // 
            // item_label
            // 
            this.item_label.AutoSize = true;
            this.item_label.Font = new System.Drawing.Font("Century Gothic", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.item_label.Location = new System.Drawing.Point(4, 20);
            this.item_label.Name = "item_label";
            this.item_label.Size = new System.Drawing.Size(0, 16);
            this.item_label.TabIndex = 0;
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.category_label);
            this.groupBox2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox2.Location = new System.Drawing.Point(339, 3);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(128, 41);
            this.groupBox2.TabIndex = 1;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "CATEGORY";
            // 
            // category_label
            // 
            this.category_label.AutoSize = true;
            this.category_label.Font = new System.Drawing.Font("Century Gothic", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.category_label.Location = new System.Drawing.Point(4, 20);
            this.category_label.Name = "category_label";
            this.category_label.Size = new System.Drawing.Size(0, 16);
            this.category_label.TabIndex = 0;
            // 
            // dateTimePicker2
            // 
            this.dateTimePicker2.CustomFormat = "MMMM dd, yyyy";
            this.dateTimePicker2.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.dateTimePicker2.Location = new System.Drawing.Point(250, 21);
            this.dateTimePicker2.Name = "dateTimePicker2";
            this.dateTimePicker2.Size = new System.Drawing.Size(171, 23);
            this.dateTimePicker2.TabIndex = 107;
            this.dateTimePicker2.Visible = false;
            this.dateTimePicker2.CloseUp += new System.EventHandler(this.dateTimePicker2_CloseUp);
            this.dateTimePicker2.Leave += new System.EventHandler(this.dateTimePicker2_Leave);
            this.dateTimePicker2.MouseDown += new System.Windows.Forms.MouseEventHandler(this.dateTimePicker2_MouseDown);
            this.dateTimePicker2.MouseLeave += new System.EventHandler(this.dateTimePicker2_MouseLeave);
            this.dateTimePicker2.MouseHover += new System.EventHandler(this.dateTimePicker2_MouseHover);
            // 
            // project_from_assigned_textBox
            // 
            this.project_from_assigned_textBox.Location = new System.Drawing.Point(495, 21);
            this.project_from_assigned_textBox.Name = "project_from_assigned_textBox";
            this.project_from_assigned_textBox.Size = new System.Drawing.Size(321, 23);
            this.project_from_assigned_textBox.TabIndex = 108;
            this.project_from_assigned_textBox.Visible = false;
            this.project_from_assigned_textBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.project_from_assigned_textBox_KeyDown);
            this.project_from_assigned_textBox.MouseLeave += new System.EventHandler(this.project_from_assigned_textBox_MouseLeave);
            // 
            // History_form
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1150, 566);
            this.Controls.Add(this.project_from_assigned_textBox);
            this.Controls.Add(this.dateTimePicker2);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Controls.Add(this.item_history_dataGridView);
            this.Font = new System.Drawing.Font("Century Gothic", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Margin = new System.Windows.Forms.Padding(4);
            this.MaximizeBox = false;
            this.Name = "History_form";
            this.Padding = new System.Windows.Forms.Padding(27, 74, 27, 25);
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.TextAlign = MetroFramework.Forms.MetroFormTextAlign.Center;
            this.Activated += new System.EventHandler(this.History_form_Activated);
            this.Deactivate += new System.EventHandler(this.History_form_Deactivate);
            this.Load += new System.EventHandler(this.History_form_Load);
            ((System.ComponentModel.ISupportInitialize)(this.item_history_dataGridView)).EndInit();
            this.contextMenuStrip1.ResumeLayout(false);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.groupBox5.ResumeLayout(false);
            this.groupBox5.PerformLayout();
            this.groupBox4.ResumeLayout(false);
            this.groupBox4.PerformLayout();
            this.groupBox6.ResumeLayout(false);
            this.groupBox6.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.DataGridView item_history_dataGridView;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox5;
        private System.Windows.Forms.Label date_purchased_label;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.Label serial_num_label;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.Label invoice_num_label;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Label category_label;
        private System.Windows.Forms.Label item_label;
        private System.Windows.Forms.GroupBox groupBox6;
        private System.Windows.Forms.Label brand_label1;
        private System.Windows.Forms.DateTimePicker dateTimePicker2;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem editItemToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removeToolStripMenuItem;
        private System.Windows.Forms.TextBox project_from_assigned_textBox;
    }
}