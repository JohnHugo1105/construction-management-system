using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MetroFramework.Controls;
using System.Drawing;

namespace GSG_Builders.RevisionedUI.Reusable_codes {
    class save_refresh_controls :Fields_reusable_base_class {
                 
        public void Clear_text_bill_mat ( ComboBox main_project_str, ComboBox sub_project_str , ComboBox DivisionNum_comboBox_str , ComboBox wbs_code_str, ComboBox main_scope_description_comboBox_str, ComboBox material_item_comboBox_str, ComboBox sub_scope_description_comboBox_str, TextBox item_description_textBox_str, TextBox remarks_textBox_str, TextBox quantity_txt_str , ComboBox unit_txt_str, MetroTextBox unit_cost_metroTextBox_str,MetroTextBox total_amount_metroTextBox, Label datepurchased_Lbl,MetroTextBox vat_exclusive_metroTextBox, MetroTextBox vat_metroTextBox ) {
            //main_project_str.ForeColor = Color.DarkGray; main_project_str.Text = "-Select main project-";
            sub_project_str.ForeColor = Color.DarkGray; sub_project_str.Text = "-Select sub - project-";
            DivisionNum_comboBox_str.ForeColor = Color.DarkGray; DivisionNum_comboBox_str.Text = "-Select division no.-";
            wbs_code_str.ForeColor = Color.DarkGray; wbs_code_str.Text = "-Select WBS code-"; wbs_code_str.Enabled = true;
            main_scope_description_comboBox_str.ForeColor = Color.DarkGray; main_scope_description_comboBox_str.Text = "-Select main scope description-"; main_scope_description_comboBox_str.Enabled = true;
            material_item_comboBox_str.ForeColor = Color.DarkGray; material_item_comboBox_str.Text = "-Select material/labor-";
            sub_scope_description_comboBox_str.ForeColor = Color.DarkGray; sub_scope_description_comboBox_str.Text = "-Select sub-scope description-";
            item_description_textBox_str.ForeColor = Color.DarkGray; item_description_textBox_str.Text= "Item description";
            remarks_textBox_str.ForeColor = Color.DarkGray; remarks_textBox_str.Text = "Remarks";
            quantity_txt_str.ForeColor = Color.DarkGray; quantity_txt_str.Text = "Quantity";
            unit_txt_str.ForeColor = Color.DarkGray; unit_txt_str.Text= "-Select unit measurement-";
            unit_cost_metroTextBox_str.ForeColor = Color.DarkGray; unit_cost_metroTextBox_str.Text = "Unit cost";
            total_amount_metroTextBox.ForeColor = Color.DarkGray; total_amount_metroTextBox.Text = "Total amount";
            datepurchased_Lbl.Visible = true;
            vat_exclusive_metroTextBox.Text = "VAT exclusive";
            vat_metroTextBox.Text = "VAT";
        }

        public void Clear_text_journal ( ComboBox main_project_str , ComboBox sub_project_str , ComboBox DivisionNum_comboBox_str , ComboBox wbs_code_str , ComboBox main_scope_description_comboBox_str , ComboBox material_item_comboBox_str , ComboBox sub_scope_description_comboBox_str , TextBox item_description_textBox_str , TextBox remarks_textBox_str , TextBox quantity_txt_str , ComboBox unit_txt_str , MetroTextBox unit_cost_metroTextBox_str , MetroTextBox total_amount_metroTextBox , Label datepurchased_Lbl , TextBox source_dealer_textBox , TextBox invoice_num_textBox , MetroTextBox vat_exclusive_metroTextBox , MetroTextBox vat_metroTextBox ) {

            //main_project_str.ForeColor = Color.DarkGray; main_project_str.Text = "-Select main project-";
            sub_project_str.ForeColor = Color.DarkGray; sub_project_str.Text = "-Select sub - project-";
            DivisionNum_comboBox_str.ForeColor = Color.DarkGray; DivisionNum_comboBox_str.Text = "-Select division no.-";
            wbs_code_str.ForeColor = Color.DarkGray; wbs_code_str.Text = "-Select WBS code-"; wbs_code_str.Enabled = true;
            main_scope_description_comboBox_str.ForeColor = Color.DarkGray; main_scope_description_comboBox_str.Text = "-Select main scope description-"; main_scope_description_comboBox_str.Enabled = true;
            material_item_comboBox_str.ForeColor = Color.DarkGray; material_item_comboBox_str.Text = "-Select material/labor-";
            sub_scope_description_comboBox_str.ForeColor = Color.DarkGray; sub_scope_description_comboBox_str.Text = "-Select sub-scope description-";
            item_description_textBox_str.ForeColor = Color.DarkGray; item_description_textBox_str.Text = "Item description";
            remarks_textBox_str.ForeColor = Color.DarkGray; remarks_textBox_str.Text = "Remarks";
            quantity_txt_str.ForeColor = Color.DarkGray; quantity_txt_str.Text = "Quantity";
            unit_txt_str.ForeColor = Color.DarkGray; unit_txt_str.Text = "-Select unit measurement-";
            unit_cost_metroTextBox_str.ForeColor = Color.DarkGray; unit_cost_metroTextBox_str.Text = "Unit cost";
            total_amount_metroTextBox.ForeColor = Color.DarkGray; total_amount_metroTextBox.Text = "Total amount";
            datepurchased_Lbl.Visible = true;
            source_dealer_textBox.ForeColor = Color.DarkGray; source_dealer_textBox.Text = "Source/Dealer";
            invoice_num_textBox.ForeColor = Color.DarkGray; invoice_num_textBox.Text = "Invoice/Sales no.";
            vat_exclusive_metroTextBox.ForeColor = Color.DarkGray;
            vat_metroTextBox.ForeColor = Color.DarkGray;
            vat_exclusive_metroTextBox.Text = "VAT exclusive";
            vat_metroTextBox.Text = "VAT";

        }
        public void refresh_warehouse_inventory(DateTimePicker date_purchased_dateTimePicker, TextBox textBox , TextBox textBox2 , TextBox textBox3 , TextBox textBox4 , TextBox textBox5 , TextBox textBox6 , TextBox textBox7 , TextBox textBox8 , TextBox textBox9 , TextBox textBox10 ,  MetroRadioButton radio_button , MetroRadioButton radio_button2 , MetroRadioButton radio_button3, GroupBox groupBox6, Bunifu.Framework.UI.BunifuMaterialTextbox status_bunifuMaterialTextbox , Bunifu.Framework.UI.BunifuMaterialTextbox warehouse_stock_bunifuMaterialTextbox, Bunifu.Framework.UI.BunifuMaterialTextbox out_bunifuMaterialTextbox, Bunifu.Framework.UI.BunifuMaterialTextbox total_stock_bunifuMaterialTextbox ) {
            date_purchased_dateTimePicker.Text = DateTime.Now.Date.ToShortDateString();
            textBox.Clear( ); textBox2.Clear( ); textBox3.Clear( ); textBox4.Clear( ); textBox5.Clear( ); textBox6.Clear( ); textBox7.Clear( ); textBox8.Clear( ); textBox9.Clear( ); textBox10.Clear( );
            radio_button.Checked = false; radio_button2.Checked = false; radio_button3.Checked = false;
            groupBox6.Text = "item stock details";
            status_bunifuMaterialTextbox.Text = string.Empty;
            warehouse_stock_bunifuMaterialTextbox.Text = string.Empty;
            out_bunifuMaterialTextbox.Text = string.Empty;
            total_stock_bunifuMaterialTextbox.Text = string.Empty;
        }
    }
}
