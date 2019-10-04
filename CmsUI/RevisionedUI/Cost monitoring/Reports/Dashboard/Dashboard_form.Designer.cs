namespace GSG_Builders.RevisionedUI.Reports.Dashboard {
    partial class Dashboard_form {
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
            this.crystalReportViewer1 = new CrystalDecisions.Windows.Forms.CrystalReportViewer();
            this.budget_report1 = new GSG_Builders.budget_report();
            this.main_report1 = new GSG_Builders.main_report();
            this.variance_report_CrystalReport1 = new GSG_Builders.variance_report_CrystalReport();
            this.CrystalReport12 = new GSG_Builders.CrystalReport1();
            this.dashboard_with_wizard2 = new GSG_Builders.dashboard_with_wizard();
            this.dashboard_with_wizard1 = new GSG_Builders.dashboard_with_wizard();
            this.dashboard_report1 = new GSG_Builders.dashboard_report();
            this.reportDocument1 = new CrystalDecisions.CrystalReports.Engine.ReportDocument();
            this.CrystalReport11 = new GSG_Builders.CrystalReport1();
            this.construction_management_system_dataset1 = new GSG_Builders.construction_management_system_dataset();
            ((System.ComponentModel.ISupportInitialize)(this.construction_management_system_dataset1)).BeginInit();
            this.SuspendLayout();
            // 
            // crystalReportViewer1
            // 
            this.crystalReportViewer1.ActiveViewIndex = -1;
            this.crystalReportViewer1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.crystalReportViewer1.Cursor = System.Windows.Forms.Cursors.Default;
            this.crystalReportViewer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.crystalReportViewer1.Location = new System.Drawing.Point(20, 60);
            this.crystalReportViewer1.Name = "crystalReportViewer1";
            this.crystalReportViewer1.Size = new System.Drawing.Size(1074, 589);
            this.crystalReportViewer1.TabIndex = 0;
            this.crystalReportViewer1.Load += new System.EventHandler(this.crystalReportViewer1_Load);
            // 
            // construction_management_system_dataset1
            // 
            this.construction_management_system_dataset1.DataSetName = "construction_management_system_dataset";
            this.construction_management_system_dataset1.SchemaSerializationMode = System.Data.SchemaSerializationMode.IncludeSchema;
            // 
            // Dashboard_form
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1114, 669);
            this.Controls.Add(this.crystalReportViewer1);
            this.Name = "Dashboard_form";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.Dashboard_form_FormClosed);
            this.Load += new System.EventHandler(this.Dashboard_form_Load);
            ((System.ComponentModel.ISupportInitialize)(this.construction_management_system_dataset1)).EndInit();
            this.ResumeLayout(false);

        }

        private CrystalDecisions.Windows.Forms.CrystalReportViewer crystalReportViewer1;
        private CrystalDecisions.CrystalReports.Engine.ReportDocument reportDocument1;
        private GSG_Builders.dashboard_report dashboard_report1;
        private GSG_Builders.dashboard_with_wizard dashboard_with_wizard1;
        private GSG_Builders.CrystalReport1 CrystalReport11;
        private GSG_Builders.dashboard_with_wizard dashboard_with_wizard2;
        private GSG_Builders.CrystalReport1 CrystalReport12;
        private construction_management_system_dataset construction_management_system_dataset1;
        private GSG_Builders.variance_report_CrystalReport variance_report_CrystalReport1;
        private GSG_Builders.main_report main_report1;
        private GSG_Builders.budget_report budget_report1;


        #endregion
        //  private Dash_board Dash_board1;
    }
}