using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//using MySql.Data.MySqlClient;
using System.Windows.Forms;
using MetroFramework.Controls;
using System.Data;
using System.Data.SqlClient;

namespace GSG_Builders.RevisionedUI.Reusable_codes {
    class Fields_reusable_base_class {
        #region MSQL fields
       //public  MySqlCommand cmd;
       // public static MySqlCommand cmd2;

        public static SqlConnection con_ms_sql;
        public static SqlCommand cmd_ms_sql;
        #endregion

        #region autocomplete source class
        public AutoCompleteStringCollection Collection;
        #endregion

        #region database and table string  value connection
        public const string bill_mat_database_table = " construction_management_system.bill_mat ";
        public const string bill_mat_archives_database_table = " construction_management_system.bill_mat_archives ";
        public const string bill_mat_archives_main_database_table = " construction_management_system.bill_mat_archives_main ";
        public const string main_project_database_table = " construction_management_system.main_project ";
        public const string main_project_archives_database_table = " construction_management_system.main_project_archives ";
        public const string sub_project_database_table = " construction_management_system.sub_project ";
        public const string sub_project_archives_database_table = " construction_management_system.sub_project_archives ";
        public const string journal_database_table = " construction_management_system.journal ";
        public const string journal_archives_database_table = " construction_management_system.journal_archives ";
        public const string journal_archives_main_database_table = " construction_management_system.journal_archives_main ";
        public const string warehouse_inventory_equipment_database_table = " construction_management_system.warehouse_inventory_equipment ";
        public const string warehouse_inventory_equipment_history_assignment_database_table = " construction_management_system.warehouse_inventory_tool_equipment_history_assignment ";
        public const string warehouse_inventory_tool_database_table = " construction_management_system.warehouse_inventory_tool ";
        public const string hr_worker_biodata_database_table = " construction_management_system.hr_worker_biodata ";
        public const string hr_worker_assignment_history_database_table = " construction_management_system.hr_worker_assignment_history ";
        public const string hr_worker_case_database_table = " construction_management_system.hr_worker_case ";
        public const string hr_worker_designation_rate_database_table = " construction_management_system.hr_worker_designation_rate ";

        public const string account_privilege_code_table = "account_privilege_code";
        public const string bill_mat_table = "bill_mat";
        public const string bill_mat_archives_table = "bill_mat_archives";
        public const string bill_mat_archives_main_table = "bill_mat_archives_main";
        public const string main_project_table = "main_project";
        public const string main_project_archives_table = "main_project_archives";
        public const string sub_project_table = "sub_project";
        public const string sub_project_archives_table = "sub_project_archives";
        public const string journal_table = "journal";
        public const string journal_archives_table = "journal_archives";
        public const string journal_archives_main_table = "journal_archives_main";
        public const string warehouse_inventory_equipment_table = "warehouse_inventory_equipment";
        public const string warehouse_inventory_equipment_history_assignment_table = "warehouse_inventory_tool_equipment_history_assignment";
        public const string warehouse_inventory_tool_table = "warehouse_inventory_tool";
        public const string hr_worker_biodata_table = "hr_worker_biodata";
        public const string hr_worker_assignment_history_table = "hr_worker_assignment_history";
        public const string hr_worker_case_table = "hr_worker_case";
        public const string hr_worker_designation_rate_table = "hr_worker_designation_rate";
        public const string user_accounts_table = "user_accounts";
        #endregion

        public const string event_parameter = "@event";
        public const string select_event = "select";
        public const string insert_event = "insert";
        public const string delete_event = "delete";
        public const string update_event = "update";
        public const string database_checking_event = "database_checking";

        public const string table_parameter= "@table_name";
        public const string columns_update_query = "@col_udpate_query";
        public const string col_insert_query = "@col_insert_query";
        public const string col_insert_values = "@col_insert_values";
        public const string where_column_parameter = "@where_column";
        public const string where_column_value_parameter = "@where_column_value";
        public const string other_query = "@other_query";
        public const string file_path = "@file_path";

        public const string col_1 = "@col_1";
        public const string col_2 = "@col_2";
        public const string col_3 = "@col_3";
        public const string col_4 = "@col_4";
        public const string col_5 = "@col_5";

        public const string col_1_value = "@col_1_value";
        public const string col_2_value = "@col_2_value";
        public const string col_3_value = "@col_3_value";
        public const string col_4_value = "@col_4_value";
        public const string col_5_value = "@col_5_value";

        public const string ASC = "@ASC";
        public const string DESC = "@DESC";
        public const string col_select = "@col_select";


        #region  computation class
        public TextBox quantity_txt;
        public MetroTextBox unit_cost_metroTextBox;
        public MetroTextBox total_amount_metroTextBox;
      //  public MetroTextBox total_budget_metroTextBox;
     //   public MetroTextBox remaining_budget_metroTextBox;
        public double? quantity_valueholder, unitcost_valueholder,
       total_amount_valueholder;
        #endregion

        #region save record class
        public int main_project_id;
        public int sub_project_id;
        public int equipment_main_record_id;
        public string main_project_comboBox;
        public string main_project_name;
        public string sub_project_name;
        public string date_purchased;
        public string date_started;
        public string division_number;
        public string wbs_code;
        public string material_labor;
        public string main_scope_description;
        public string sub_scope_description;
        public string item_description;
        public string unit_string;
        public double quantity;
        public string unit_cost_string;
        public TextBox unit_cost_text;
        public string remarks;
        public string total_amount_string;
        public TextBox total_amount_text;
        public string date_for_sorting;
        public string source_dealer_textBox;
        public string invoice_num_textBox;
        public string expense_revenue;
        public string brand;
        public string serial_number;
        public string reason_for_archiving;
        public string category;
        public string status;
        public string quantity_string;
        public TextBox quantity_text;
        public string vat_exclusive;
        public string vat;

        public Bunifu.Framework.UI.BunifuImageButton item_image_BunifuImageButton;
        public byte[ ] item_image =null;
        public string item_cost_selection;
        public string amount;
        public string project_to;
        public string project_from;
        #endregion

        #region update record class
        //     public string bill_mat_datagridView_str;
        #endregion

        #region datagridview
        public DataGridView datagridview;
        #endregion

        #region Others
        public ToolStripLabel page_count_toolStripLabel;
        public double start;
        public int pageCount = 1;     
        public ToolStripLabel page_number_toolStripTextBox;
        //public MySqlDataAdapter adapter;
        public DataSet ds = new DataSet();
        #endregion

        public ToolStripButton prev_page_toolStripButton;
        public ToolStripButton next_page_toolStripButton;
        public ToolStripLabel current_record_count_toolStripLabel;
        public int project_name_id_value;
        public int sub_project_name_id_value;

        #region Backup records
        public FolderBrowserDialog folderBrowserDialog1 = new FolderBrowserDialog();
        public const string BACKUP_EXTENSION = ".sql";
        #endregion

        public static int record_count_progress_bar_value=1;
    }
}
