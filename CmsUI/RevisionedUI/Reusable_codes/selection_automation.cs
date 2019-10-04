using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace GSG_Builders.RevisionedUI.Bill_Mat {
    class selection_automation {

        #region for wbs code selection

        #region for wbs code to be listed
        public string[] set_wbs_code_list ( ComboBox wbsCode_cmb , ComboBox DivisionNum_cmb /*, ComboBox main_scope_description_comboBox */) {

            string[ ] Div1_arraylist = new string[ 9 ];
            string[ ] Div2_arraylist = new string[ 12 ];
            string[ ] Div3_arraylist = new string[ 9 ];
            string[ ] Div4_arraylist = new string[ 8 ];
            string[ ] Div5_arraylist = new string[ 10 ];
            string[ ] Div6_arraylist = new string[ 7 ];
            string[ ] Div7_arraylist = new string[ 10];
            string[ ] Div8_arraylist = new string[ 10 ];
            string[ ] Div9_arraylist = new string[ 10 ];
            string[ ] Div10_arraylist = new string[ 24 ];
            string[ ] Div11_arraylist = new string[ 36 ];
            string[ ] Div12_arraylist = new string[ 9 ];
            string[ ] Div13_arraylist = new string[ 29 ];
            string[ ] Div14_arraylist = new string[ 9 ];
            string[ ] Div15_arraylist = new string[ 11 ];
            string[ ] Div16_arraylist = new string[ 8 ];
            string[ ] main_array_value_holder = new string[ 1];

            int Div2Int = 2000;
            int Div3Int = 3000;
            int Div4Int = 4000;
            int Div5Int = 5000;
            int Div6Int = 6000;
            int Div7Int = 7000;
            int Div8Int = 8000;
            int Div9Int = 9000;
            int Div10Int = 10050;
            int Div11Int = 11000;
            int Div12Int = 12000;
            int Div13Int = 13000;
            int Div14Int = 14000;
            int Div15Int = 15000;
            int Div16Int = 16000;

            if( DivisionNum_cmb.Text == "Division  - 1" )
            {
                for( int i = 1 ; i < 10 ; i++ )
                {
                    Div1_arraylist[ i - 1 ] = "1" + i + "00";
                }
                main_array_value_holder = Div1_arraylist;
                wbsCode_cmb.Enabled = false;
              
            }
            else if( DivisionNum_cmb.Text == "Division  - 2" )
            {
                for( int i = 1 ; i < 13 ; i++ )
                {
                    if( Div2Int == 2000 || Div2Int == 2050 || Div2Int == 2400 || Div2Int == 2450 || Div2Int == 2900 )
                    {
                        Div2Int += 50;
                      Div2_arraylist[i-1]  =Div2Int.ToString( ) ;
                    }
                    else
                    {
                        Div2Int += 100;
                        Div2_arraylist[ i - 1 ]= Div2Int.ToString( ) ;
                    }
                }
                main_array_value_holder = Div2_arraylist;
                wbsCode_cmb.Enabled = false;
            }
            else if( DivisionNum_cmb.Text == "Division  - 3" )
            {
                for( int i = 1 ; i < 10 ; i++ )
                {
                    if( Div3Int == 3000 || Div3Int == 3050 )
                    {
                        Div3Int += 50;
                        Div3_arraylist[ i - 1 ]= Div3Int.ToString( );
                    }
                    else if( Div3Int == 3700 )
                    {
                        Div3Int += 200;
                        Div3_arraylist[ i - 1 ]= Div3Int.ToString( );
                    }
                    else
                    {
                        Div3Int += 100;
                        Div3_arraylist[ i - 1 ] = Div3Int.ToString( );
                    }
                }
                main_array_value_holder = Div3_arraylist;
                wbsCode_cmb.Enabled = false;
            }
            else if( DivisionNum_cmb.Text == "Division  - 4" )
            {
                
                for( int i = 1 ; i < 9 ; i++ )
                {
                    if( Div4Int == 4000 )
                    {
                        Div4Int += 50;
                        Div4_arraylist[ i - 1 ]= Div4Int.ToString( );
                    }
                    else if( Div4Int == 4050 )
                    {
                        Div4Int += 150;
                        Div4_arraylist[ i - 1 ] = Div4Int.ToString( );
                    }
                    else if( Div4Int == 4200 )
                    {
                        Div4Int += 200;
                        Div4_arraylist[ i - 1 ] = Div4Int.ToString( ) ;
                    }
                    else
                    {
                        Div4Int += 100;
                        Div4_arraylist[ i - 1 ] = Div4Int.ToString( );
                    }
                }
                main_array_value_holder = Div4_arraylist;              
               wbsCode_cmb.Enabled = false;
            }
            else if( DivisionNum_cmb.Text == "Division  - 5" )
            {
                for( int i = 1 ; i < 11 ; i++ )
                {
                    if( Div5Int == 5000 || Div5Int == 5050 )
                    {
                        Div5Int += 50;
                        Div5_arraylist[ i - 1 ] = Div5Int.ToString( );
                    }
                    else
                    {
                        Div5Int += 100;
                        Div5_arraylist[ i - 1 ] = Div5Int.ToString( );
                    }
                }
                main_array_value_holder = Div5_arraylist;
               wbsCode_cmb.Enabled = false;
            }
            else if( DivisionNum_cmb.Text == "Division  - 6" )
            {
                for( int i = 1 ; i < 8 ; i++ )
                {
                    if( Div6Int == 6000 || Div6Int == 6050 )
                    {
                        Div6Int += 50;
                        Div6_arraylist[ i - 1 ] = Div6Int.ToString( );
                    }
                    else if( Div6Int == 6200 )
                    {
                        Div6Int += 200;
                        Div6_arraylist[ i - 1 ] = Div6Int.ToString( );
                    }
                    else if( Div6Int == 6600 )
                    {
                        Div6Int += 300;
                        Div6_arraylist[ i - 1 ] = Div6Int.ToString( );
                    }
                    else
                    {
                        Div6Int += 100;
                        Div6_arraylist[ i - 1 ] = Div6Int.ToString( );
                    }
                }
                main_array_value_holder = Div6_arraylist;
                wbsCode_cmb.Enabled = false;
            }
            else if( DivisionNum_cmb.Text == "Division  - 7" )
            {
                for( int i = 1 ; i < 11 ; i++ )
                {
                    if( Div7Int == 7000 || Div7Int == 7050 )
                    {
                        Div7Int += 50;
                        Div7_arraylist[ i - 1 ] = Div7Int.ToString( );
                    }
                    else
                    {
                        Div7Int += 100;
                        Div7_arraylist[ i - 1 ] = Div7Int.ToString( );
                    }
                }
                main_array_value_holder = Div7_arraylist;
                wbsCode_cmb.Enabled = false;
            }
            else if( DivisionNum_cmb.Text == "Division  - 8" )
            {
                for( int i = 1 ; i < 11 ; i++ )
                {
                    if( Div8Int == 8000 || Div8Int == 8050 )
                    {
                        Div8Int += 50;
                        Div8_arraylist[ i - 1 ] = Div8Int.ToString( );
                    }
                    else
                    {
                        Div8Int += 100;
                        Div8_arraylist[ i - 1 ] = Div8Int.ToString( );
                    }
                }
                main_array_value_holder = Div8_arraylist;
                wbsCode_cmb.Enabled = false;
            }
            else if( DivisionNum_cmb.Text == "Division  - 9" )
            {
                for( int i = 1 ; i < 11 ; i++ )
                {
                    if( Div9Int == 9000 || Div9Int == 9050 )
                    {
                        Div9Int += 50;
                        Div9_arraylist[ i - 1 ] = Div9Int.ToString( );
                    }
                    else
                    {
                        Div9Int += 100;
                        Div9_arraylist[ i - 1 ] = Div9Int.ToString( ) ;
                    }
                }
                main_array_value_holder = Div9_arraylist;
                wbsCode_cmb.Enabled = false;
            }
            else if( DivisionNum_cmb.Text == "Division  - 10" )
            {
                for( int i = 1 ; i < 25 ; i++ )
                {
                    if( Div10Int == 10050 || Div10Int == 10100 || Div10Int == 10150 || Div10Int == 10350 || Div10Int == 10400 || Div10Int == 10450 || Div10Int == 10550 || Div10Int == 10700 || Div10Int == 10750 )
                    {
                        Div10Int += 50;
                        Div10_arraylist[ i - 1 ] = Div10Int.ToString( );
                    }
                    else if( Div10Int == 10200 || Div10Int == 10300 )
                    {
                        Div10Int += 40;
                        Div10_arraylist[ i - 1 ] = Div10Int.ToString( ) ;
                    }
                    else if( Div10Int == 10240 || Div10Int == 10290 || Div10Int == 10250 || Div10Int == 10260 || Div10Int == 10340 || Div10Int == 10520 )
                    {
                        Div10Int += 10;
                        Div10_arraylist[ i - 1 ] = Div10Int.ToString( );
                    }
                    else if( Div10Int == 10270 || Div10Int == 10500 || Div10Int == 10530 || Div10Int == 10880 )
                    {
                        Div10Int += 20;
                        Div10_arraylist[ i - 1 ] = Div10Int.ToString( );
                    }
                    else if( Div10Int == 10670 )
                    {
                        Div10Int += 30;
                        Div10_arraylist[ i - 1 ] = Div10Int.ToString( ) ;
                    }
                    else if( Div10Int == 10600 )
                    {
                        Div10Int += 70;
                        Div10_arraylist[ i - 1 ] = Div10Int.ToString( );
                    }
                    else if( Div10Int == 10800 )
                    {
                        Div10Int += 80;
                        Div10_arraylist[ i - 1 ] = Div10Int.ToString( );
                    }
                }
                main_array_value_holder = Div10_arraylist;
                wbsCode_cmb.Enabled = false;
            }
            else if( DivisionNum_cmb.Text == "Division  - 11" )
            {
                for( int i = 1 ; i < 37 ; i++ )
                {
                    if( Div11Int == 11000 || Div11Int == 11010 || Div11Int == 11020 || Div11Int == 11030 || Div11Int == 11040 || Div11Int == 11050 || Div11Int == 11060 || Div11Int == 11070 || Div11Int == 11080 || Div11Int == 11090 || Div11Int == 11100 || Div11Int == 11110 || Div11Int == 11120 || Div11Int == 11130 || Div11Int == 11140 || Div11Int == 11150 || Div11Int == 11160 || Div11Int == 11190 || Div11Int == 11450 || Div11Int == 11460 || Div11Int == 11470 || Div11Int == 11650 )
                    {
                        Div11Int += 10;
                        Div11_arraylist[ i - 1 ] = Div11Int.ToString( );
                    }
                    else if( Div11Int == 11170 || Div11Int == 11660 || Div11Int == 11680 || Div11Int == 11850 || Div11Int == 11280 || Div11Int == 11480 )
                    {
                        Div11Int += 20;
                        Div11_arraylist[ i - 1 ] = Div11Int.ToString( );
                    }
                    else if( Div11Int == 11200 || Div11Int == 11700 )
                    {
                        Div11Int += 80;
                        Div11_arraylist[ i - 1 ] = Div11Int.ToString( );
                    }
                    else if( Div11Int == 11400 || Div11Int == 11600 )
                    {
                        Div11Int += 50;
                        Div11_arraylist[ i - 1 ] = Div11Int.ToString( );
                    }
                    else if( Div11Int == 11780 )
                    {
                        Div11Int += 70;
                        Div11_arraylist[ i - 1 ] = Div11Int.ToString( );
                    }
                    else if( Div11Int == 11870 )
                    {
                        Div11Int += 30;
                        Div11_arraylist[ i - 1 ] = Div11Int.ToString( );
                    }
                    else
                    {
                        Div11Int += 100;
                        Div11_arraylist[ i - 1 ] = Div11Int.ToString( );
                    }
                }
                main_array_value_holder = Div11_arraylist;
                wbsCode_cmb.Enabled = false;
            }
            else if( DivisionNum_cmb.Text == "Division  - 12" )
            {
                for( int i = 1 ; i < 10 ; i++ )
                {
                    if( Div12Int == 12000 || Div12Int == 12050 )
                    {
                        Div12Int += 50;
                        Div12_arraylist[ i - 1 ] = Div12Int.ToString( );
                    }
                    else if( Div12Int == 12100 )
                    {
                        Div12Int += 200;
                        Div12_arraylist[ i - 1 ] = Div12Int.ToString( );
                    }
                    else
                    {
                        Div12Int += 100;
                        Div12_arraylist[ i - 1 ] = Div12Int.ToString( );
                    }
                }
                main_array_value_holder = Div12_arraylist;
                wbsCode_cmb.Enabled = false;
            }
            else if( DivisionNum_cmb.Text == "Division  - 13" )
            {
                for( int i = 1 ; i < 30 ; i++ )
                {
                    if( Div13Int == 13000 || Div13Int == 13010 || Div13Int == 13020 || Div13Int == 13080 || Div13Int == 13090 || Div13Int == 13100 || Div13Int == 13110 || Div13Int == 13150 || Div13Int == 13175 || Div13Int == 13190 || Div13Int == 13220 || Div13Int == 13230 || Div13Int == 13000 )
                    {
                        Div13Int += 10;
                        Div13_arraylist[ i - 1 ] = Div13Int.ToString( );
                    }
                    else if( Div13Int == 13030 || Div13Int == 13500 || Div13Int == 13550 || Div13Int == 13800 || Div13Int == 13850 )
                    {
                        Div13Int += 50;
                        Div13_arraylist[ i - 1 ] = Div13Int.ToString( );
                    }
                    else if( Div13Int == 13120 )
                    {
                        Div13Int += 30;
                        Div13_arraylist[ i - 1 ] = Div13Int.ToString( );
                    }
                    else if( Div13Int == 13160 || Div13Int == 13165 || Div13Int == 13170 || Div13Int == 13185 )
                    {
                        Div13Int += 5;
                        Div13_arraylist[ i - 1 ] = Div13Int.ToString( );
                    }
                    else if( Div13Int == 13200 || Div13Int == 13220 || Div13Int == 13240 || Div13Int == 13260 )
                    {
                        Div13Int += 20;
                        Div13_arraylist[ i - 1 ] = Div13Int.ToString( );
                    }
                    else if( Div13Int == 13280 )
                    {
                        Div13Int += 120;
                        Div13_arraylist[ i - 1 ] = Div13Int.ToString( );
                    }
                    else
                    {
                        Div13Int += 100;
                        Div13_arraylist[ i - 1 ] = Div13Int.ToString( );
                    }
                }
                main_array_value_holder = Div13_arraylist;
                wbsCode_cmb.Enabled = false;
            }
            else if( DivisionNum_cmb.Text == "Division  - 14" )
            {
                for( int i = 1 ; i < 10 ; i++ )
                {
                    if( Div14Int >= 14000 )
                    {
                        Div14Int += 100;
                        Div14_arraylist[ i - 1 ] = Div14Int.ToString( );
                    }
                }
                main_array_value_holder = Div14_arraylist;
                wbsCode_cmb.Enabled = false;
            }
            else if( DivisionNum_cmb.Text == "Division  - 15" )
            {
                for( int i = 1 ; i < 12 ; i++ )
                {
                    if( Div15Int == 15000 || Div15Int == 15050 || Div15Int == 15900 )
                    {
                        Div15Int += 50;
                        Div15_arraylist[ i - 1 ] = Div15Int.ToString( );
                    }
                    else
                    {
                        Div15Int += 100;
                        Div15_arraylist[ i - 1 ] = Div15Int.ToString( );
                    }
                }
                main_array_value_holder = Div15_arraylist;
                wbsCode_cmb.Enabled = false;
            }
            else if( DivisionNum_cmb.Text == "Division  - 16" )
            {
                for( int i = 1 ; i < 9 ; i++ )
                {
                    if( Div16Int == 16000 || Div16Int == 16050 )
                    {
                        Div16Int += 50;
                        Div16_arraylist[ i - 1 ] = Div16Int.ToString( ) ;
                    }
                    else if( Div16Int == 16500 )
                    {
                        Div16Int += 200;
                        Div16_arraylist[ i - 1 ] = Div16Int.ToString( );
                    }
                    else
                    {
                        Div16Int += 100;
                        Div16_arraylist[ i - 1 ] = Div16Int.ToString( );
                    }
                }
                main_array_value_holder = Div16_arraylist;
                wbsCode_cmb.Enabled = false;
            }
            else
            {
                //if( wbsCode_cmb.Enabled == false )
                //{
                //    wbsCode_cmb.Enabled = true;
                //}
            }          
            return main_array_value_holder;
        }
        #endregion

        public void wbs_code_selection ( ComboBox DivisionNum_comboBox , ComboBox wbsCode_comboBox, ComboBox sub_scope_description_comboBox,ComboBox main_scope_description_comboBox ) {
            string[ ][ ] indexArr = new string[ ][ ] { new string[ set_wbs_code_list( wbsCode_comboBox , DivisionNum_comboBox ).Length ] };

            int count = 0;
            if( set_wbs_code_list( wbsCode_comboBox , DivisionNum_comboBox ).Length < 0 )
            {
                sub_scope_description_comboBox.Items.Clear( );
                main_scope_description_comboBox.Items.Clear( );              
            }
            if ( set_wbs_code_list( wbsCode_comboBox, DivisionNum_comboBox ).Length > 0 )
            {
                foreach ( var item in set_wbs_code_list( wbsCode_comboBox, DivisionNum_comboBox ) )
                {
                    indexArr[ 0 ][ count ] = item;
                  //  wbsCode_comboBox.Items.Add( item );
                    count++;
                }
                wbsCode_comboBox.Text = indexArr[ 0 ][ sub_scope_description_comboBox.SelectedIndex ];
            }         
            wbsCode_comboBox.ForeColor = Color.Black;
        }
        #endregion

        #region for sub scope selection
        private static string General_Requirements = "General Requirements";
        private static string Site_Construction = "Site Construction";
        private static string Concrete = "Concrete";
        private static string Masonry = "Masonry";
        private static string Metals = "Metals";
        private static string Wood_and_Plastics = "Wood and Plastics";
        private static string Thermal_and_Moisture_Protection = "Thermal and Moisture Protection";
        private static string Doors_and_Windows = "Doors and Windows";
        private static string Finishes = "Finishes";
        private static string Specialties = "Specialties";
        private static string Equipment = "Equipment";
        private static string Furnishings = "Furnishings";
        private static string Special_Construction = "Special Construction";
        private static string Conveying_Systems = "Conveying Systems";
        private static string Mechanical = "Mechanical";
        private static string Electrical = "Electrical";

        
        private string Division_number ( ComboBox division_number ) {
            string value = "";
            if(  division_number.DroppedDown == false )
            {
                value = division_number.Text;            
            }            
            else if(division_number.DroppedDown==true)
            {               
                value = division_number.SelectedItem.ToString();            
            }         
            return value;
        }
        public void sub_scope_selection(ComboBox sub_scope_description_comboBox, ComboBox division_number, ComboBox main_scope_description_comboBox) {
            List<string> DivisionNum_cmbSelectedText = new List<string>( );
          
            DivisionNum_cmbSelectedText.Add( "Division  - " + Division_number(division_number) );
            
            division_number.DataSource = DivisionNum_cmbSelectedText;

            main_scope_description_comboBox.ForeColor = Color.Black;

         //   sub_scope_description_comboBox.ForeColor = Color.DarkGray;
            List<string> DivListStr = new List<string>();

            if( division_number.Text == "Division  - 1" )
            {
                DivListStr.Add( "Summary" );
                DivListStr.Add( "Price and Payment Procedures" );
                DivListStr.Add( "Administrative Requirements" );
                DivListStr.Add( "Quality Requirements" );
                DivListStr.Add( "Temporary Facilities and Controls" );
                DivListStr.Add( "Product Requirements" );
                DivListStr.Add( "Execution Requirements" );
                DivListStr.Add( "Facility Operation" );
                DivListStr.Add( "Facility Decommissioning" );

                sub_scope_description_comboBox.Items.Clear( );
                foreach( var item in DivListStr )
                {
                    sub_scope_description_comboBox.Items.Add( item );
                }
                
                main_scope_description_comboBox.Text = General_Requirements;
                main_scope_description_comboBox.Enabled = false;
            }
            else if( division_number.Text == "Division  - 2" )
            {
                DivListStr.Add( "Basic Site Materials and Methods" );
                DivListStr.Add( "Site Remediation" );
                DivListStr.Add( "Site Preparation" );
                DivListStr.Add( "Earthwork" );
                DivListStr.Add( "Tunneling, Boring and Jacking" );
                DivListStr.Add( "Foundation and Load-Bearing Elements" );
                DivListStr.Add( "Utility Services" );
                DivListStr.Add( "Drainage and Containment" );
                DivListStr.Add( "Bases, Ballasts, Pavements and Appurtenances" );
                DivListStr.Add( "Site Improvements and Amenities" );
                DivListStr.Add( "Planting" );
                DivListStr.Add( "Site Restoration and Rehabilitation" );

                sub_scope_description_comboBox.Items.Clear( );
                foreach( var item in DivListStr )
                {
                    sub_scope_description_comboBox.Items.Add( item );
                }
               
                main_scope_description_comboBox.Text = Site_Construction;
                main_scope_description_comboBox.Enabled = false; 
            }
            else if( division_number.Text == "Division  - 3" )
            {
                DivListStr.Add( "Basic Concrete Materials and Methods" );
                DivListStr.Add( "Concrete Forms and Accessories" );
                DivListStr.Add( "Concrete Reinforcement" );
                DivListStr.Add( "Cast-In-Place Concrete" );
                DivListStr.Add( "Precast Concrete" );
                DivListStr.Add( "Cementitious Decks and Underlayment" );
                DivListStr.Add( "Grouts" );
                DivListStr.Add( "Mass Concrete" );
                DivListStr.Add( "Concrete Restoration and Cleaning" );

                sub_scope_description_comboBox.Items.Clear( );
                foreach( var item in DivListStr )
                {
                    sub_scope_description_comboBox.Items.Add( item );
                }

                main_scope_description_comboBox.Text = Concrete;
                main_scope_description_comboBox.Enabled = false; 
            }
            else if( division_number.Text == "Division  - 4" )
            {
                DivListStr.Add( "Basic Masonry Materials and Methods" );
                DivListStr.Add( "Masonry Units" );
                DivListStr.Add( "Stone" );
                DivListStr.Add( "Refractories" );
                DivListStr.Add( "Corrosion-Resistant Masonry" );
                DivListStr.Add( "Simulated Masonry" );
                DivListStr.Add( "Masonry Assemblies" );
                DivListStr.Add( "Masonry Restoration and Cleaning" );

                sub_scope_description_comboBox.Items.Clear( );
                foreach( var item in DivListStr )
                {
                    sub_scope_description_comboBox.Items.Add( item );
                }

                main_scope_description_comboBox.Text = Masonry;
                main_scope_description_comboBox.Enabled = false; 
            }
            else if( division_number.Text == "Division  - 5" )
            {
                DivListStr.Add( "Basic Metal Materials and Methods" );
                DivListStr.Add( "Structural Metal Framing" );
                DivListStr.Add( "Metal Joists" );
                DivListStr.Add( "Metal Deck" );
                DivListStr.Add( "Cold-Formed Metal Framing" );
                DivListStr.Add( "Metal Fabrications" );
                DivListStr.Add( "Hydraulic Fabrications" );
                DivListStr.Add( "Ornamental Metal" );
                DivListStr.Add( "Expansion Control" );
                DivListStr.Add( "Metal Restoration and Cleaning" );

                sub_scope_description_comboBox.Items.Clear( );
                foreach( var item in DivListStr )
                {
                    sub_scope_description_comboBox.Items.Add( item );
                }

                main_scope_description_comboBox.Text = Metals;
                main_scope_description_comboBox.Enabled = false;
            }
            else if( division_number.Text == "Division  - 6" )
            {
                DivListStr.Add( "Basic Wood and Plastic Materials and Methods" );
                DivListStr.Add( "Rough Carpentry" );
                DivListStr.Add( "Finish Carpentry" );
                DivListStr.Add( "Architectural Woodwork" );
                DivListStr.Add( "Structural Plastics" );
                DivListStr.Add( "Plastic Fabrications" );
                DivListStr.Add( "Wood and Plastic Restoration and Cleaning" );

                sub_scope_description_comboBox.Items.Clear( );
                foreach( var item in DivListStr )
                {
                    sub_scope_description_comboBox.Items.Add( item );
                }

                main_scope_description_comboBox.Text = Wood_and_Plastics;
                main_scope_description_comboBox.Enabled = false; 
            }
            else if( division_number.Text == "Division  - 7" )
            {
                DivListStr.Add( "Basic Thermal and Moisture Protection Materials and Methods" );
                DivListStr.Add( "Damproofing and Waterproofing" );
                DivListStr.Add( "Thermal Protection" );
                DivListStr.Add( "Shingles, Roof Tiles, and Roof Coverings" );
                DivListStr.Add( "Roofing and Siding Panels" );
                DivListStr.Add( "Membrane Roofing" );
                DivListStr.Add( "Flashing and Sheet Metal" );
                DivListStr.Add( "Roof Specialties and Accessories" );
                DivListStr.Add( "Fire and Smoke Protection" );
                DivListStr.Add( "Joint Sealers" );

                sub_scope_description_comboBox.Items.Clear( );
                foreach( var item in DivListStr )
                {
                    sub_scope_description_comboBox.Items.Add( item );
                }

                main_scope_description_comboBox.Text = Thermal_and_Moisture_Protection;
                main_scope_description_comboBox.Enabled = false;
            }
            else if( division_number.Text == "Division  - 8" )
            {
                DivListStr.Add( "Basic Door and Window Materials and Methods" );
                DivListStr.Add( "Metal Doors and Frames" );
                DivListStr.Add( "Wood and Plastic Doors" );
                DivListStr.Add( "Specialty Doors" );
                DivListStr.Add( "Entrances and Storefronts" );
                DivListStr.Add( "Windows" );
                DivListStr.Add( "Skylights" );
                DivListStr.Add( "Hardware" );
                DivListStr.Add( "Glazing" );
                DivListStr.Add( "Glazed Curtain Wall" );

                sub_scope_description_comboBox.Items.Clear( );
                foreach( var item in DivListStr )
                {
                    sub_scope_description_comboBox.Items.Add( item );
                }

                main_scope_description_comboBox.Text = Doors_and_Windows;
                main_scope_description_comboBox.Enabled = false;
            }
            else if( division_number.Text == "Division  - 9" )
            {
                DivListStr.Add( "Basic Finish Materials and Methods" );
                DivListStr.Add( "Metal Support Assemblies" );
                DivListStr.Add( "Plaster and Gypsum Board" );
                DivListStr.Add( "Tile" );
                DivListStr.Add( "Terrazzo" );
                DivListStr.Add( "Ceilings" );
                DivListStr.Add( "Flooring" );
                DivListStr.Add( "Wall Finishes" );
                DivListStr.Add( "Acoustical Treatment" );
                DivListStr.Add( "Paints and Coatings" );

                sub_scope_description_comboBox.Items.Clear( );
                foreach( var item in DivListStr )
                {
                    sub_scope_description_comboBox.Items.Add( item );
                }

                main_scope_description_comboBox.Text = Finishes;
                main_scope_description_comboBox.Enabled = false; 
            }
            else if( division_number.Text == "Division  - 10" )
            {
                DivListStr.Add( "Visual Display Boards" );
                DivListStr.Add( "Compartments and Cubicles" );
                DivListStr.Add( "Louvers and Vents" );
                DivListStr.Add( "Grilles and Screens" );
                DivListStr.Add( "Service Walls" );
                DivListStr.Add( "Wall and Corner Guards" );
                DivListStr.Add( "Access Flooring" );
                DivListStr.Add( "Pest Control" );
                DivListStr.Add( "Fireplaces and Stoves" );
                DivListStr.Add( "Manufactured Exterior Specialties" );
                DivListStr.Add( "Flagpoles" );
                DivListStr.Add( "Identification Devices" );
                DivListStr.Add( "Pedestrian Control Devices" );
                DivListStr.Add( "Lockers" );
                DivListStr.Add( "Fire Protection Specialties" );
                DivListStr.Add( "Protective Covers" );
                DivListStr.Add( "Postal Specialties" );
                DivListStr.Add( "Partitions" );
                DivListStr.Add( "Storage Shelving" );
                DivListStr.Add( "Exterior Protection" );
                DivListStr.Add( "Telephone Specialties" );
                DivListStr.Add( "Toilet, Bath, and Laundry Specialties" );
                DivListStr.Add( "Scales" );
                DivListStr.Add( "Wardrobe and Closet Specialties" );

                sub_scope_description_comboBox.Items.Clear( );
                foreach( var item in DivListStr )
                {
                    sub_scope_description_comboBox.Items.Add( item );
                }

                main_scope_description_comboBox.Text = Specialties;
                main_scope_description_comboBox.Enabled = false; 
            }
            else if( division_number.Text == "Division  - 11" )
            {
                DivListStr.Add( "Maintenance Equipment" ); DivListStr.Add( "Security and Vault Equipment" ); DivListStr.Add( "Teller and Service Equipment" ); DivListStr.Add( "Ecclesiastical Equipment" ); DivListStr.Add( "Library Equipment" ); DivListStr.Add( "Theater and Stage Equipment" ); DivListStr.Add( "Instrumental Equipment" ); DivListStr.Add( "Registration Equipment" );
                DivListStr.Add( "Checkroom Equipment" ); DivListStr.Add( "Mercantile Equipment" ); DivListStr.Add( "Commercial Laundry and Dry Cleaning Equipment" ); DivListStr.Add( "Vending Equipment" ); DivListStr.Add( "Audio-Visual Equipment" ); DivListStr.Add( "Vehicle Service Equipment" ); DivListStr.Add( "Parking Control Equipment" );
                DivListStr.Add( "Loading Dock Equipment" ); DivListStr.Add( "Solid Waste Handling Equipment" ); DivListStr.Add( "Detention Equipment" ); DivListStr.Add( "Water Supply and Treatment Equipment" ); DivListStr.Add( "Hydraulic Gates and Valves" ); DivListStr.Add( "Fluid Waste Treatment and Disposal Equipment" ); DivListStr.Add( "Food Service Equipment" );
                DivListStr.Add( "Residential Equipment" ); DivListStr.Add( "Unit Kitchens" ); DivListStr.Add( "Darkroom Equipment" ); DivListStr.Add( "Athletic, Recreational, and Therapeutic Equipment" ); DivListStr.Add( "Industrial and Process Equipment" ); DivListStr.Add( "Laboratory Equipment" ); DivListStr.Add( "Planetarium Equipment" );
                DivListStr.Add( "Observatory Equipment" ); DivListStr.Add( "Office Equipment" ); DivListStr.Add( "Medical Equipment" ); DivListStr.Add( "Mortuary Equipment" ); DivListStr.Add( "Navigation Equipment" ); DivListStr.Add( "Agricultural Equipment" ); DivListStr.Add( "Exhibit Equipment" );

                sub_scope_description_comboBox.Items.Clear( );
                foreach( var item in DivListStr )
                {
                    sub_scope_description_comboBox.Items.Add( item );
                }

                main_scope_description_comboBox.Text = Equipment;
                main_scope_description_comboBox.Enabled = false; 
            }
            else if( division_number.Text == "Division  - 12" )
            {
                DivListStr.Add( "Fabrics" ); DivListStr.Add( "Art" ); DivListStr.Add( "Manufactured Casework" ); DivListStr.Add( "Furnishings and Accessories" ); DivListStr.Add( "Furniture" );
                DivListStr.Add( "Multiple Seating" ); DivListStr.Add( "Systems Furniture" ); DivListStr.Add( "Interior Plants and Planters" ); DivListStr.Add( "Furnishings Restoration and Repair" );

                sub_scope_description_comboBox.Items.Clear( );
                foreach( var item in DivListStr )
                {
                    sub_scope_description_comboBox.Items.Add( item );
                }

                main_scope_description_comboBox.Text = Furnishings;
                main_scope_description_comboBox.Enabled = false; 
            }
            else if( division_number.Text == "Division  - 13" )
            {
                DivListStr.Add( "Air-Supported Structures" ); DivListStr.Add( "Building Modules" ); DivListStr.Add( "Special Purpose Rooms" ); DivListStr.Add( "Sound, Vibration, and Seismic Control" ); DivListStr.Add( "Radiation Protection" ); DivListStr.Add( "Lightning Protection" ); DivListStr.Add( "Cathodic Protection" );
                DivListStr.Add( "Pre-Engineered Structures" ); DivListStr.Add( "Swimming Pools" ); DivListStr.Add( "Aquariums" ); DivListStr.Add( "Aquatic Park Facilities" ); DivListStr.Add( "Tubs and Pools" ); DivListStr.Add( "Ice Rinks" ); DivListStr.Add( "Kennels and Animal Shelters" );
                DivListStr.Add( "Site-Constructed Incinerators" ); DivListStr.Add( "Storage Tanks" ); DivListStr.Add( "Filter Underdrains and Media" ); DivListStr.Add( "Digester Covers and Appurtenances" ); DivListStr.Add( "Oxygenation Systems" ); DivListStr.Add( "Sludge Conditioning Systems" ); DivListStr.Add( "Hazardous Material Remediation" );
                DivListStr.Add( "Measurement and Control Instrumentation" ); DivListStr.Add( "Recording Instrumentation" ); DivListStr.Add( "Transportation Control Instrumentation" ); DivListStr.Add( "Solar and Wind Energy Equipment" ); DivListStr.Add( "Security Access and Surveillance" ); DivListStr.Add( "Building Automation and Control" ); DivListStr.Add( "Detection and Alarm" );
                DivListStr.Add( "Fire Suppression" );

                sub_scope_description_comboBox.Items.Clear( );
                foreach( var item in DivListStr )
                {
                    sub_scope_description_comboBox.Items.Add( item );
                }

                main_scope_description_comboBox.Text = Special_Construction;
                main_scope_description_comboBox.Enabled = false;
            }
            else if( division_number.Text == "Division  - 14" )
            {
                DivListStr.Add( "Dumbwaiters" ); DivListStr.Add( "Elevators" ); DivListStr.Add( "Escalators and Moving Walks" ); DivListStr.Add( "Lifts" ); DivListStr.Add( "Material Handling" ); DivListStr.Add( "Hoists and Cables" ); DivListStr.Add( "Turntables" ); DivListStr.Add( "Scaffolding" ); DivListStr.Add( "Transportation" );

                sub_scope_description_comboBox.Items.Clear( );
                foreach( var item in DivListStr )
                {
                    sub_scope_description_comboBox.Items.Add( item );
                }

                main_scope_description_comboBox.Text = Conveying_Systems;
                main_scope_description_comboBox.Enabled = false; 
            }
            else if( division_number.Text == "Division  - 15" )
            {
                DivListStr.Add( "Basic Mechanical Materials and Methods" ); DivListStr.Add( "Building Service Piping" ); DivListStr.Add( "Process Piping" ); DivListStr.Add( "Fire Protection Piping" ); DivListStr.Add( "Plumbing Fixtures and Equipment" ); DivListStr.Add( "Heat-Generation Equipment" );
                DivListStr.Add( "Refrigeration Equipment" ); DivListStr.Add( "Heating, Ventilating, and Air Conditioning Equipment" ); DivListStr.Add( "Air Distribution" ); DivListStr.Add( "HVAC Instrumentation and Controls" ); DivListStr.Add( "Testing, Adjusting, and Balancing" );

                sub_scope_description_comboBox.Items.Clear( );
                foreach( var item in DivListStr )
                {
                    sub_scope_description_comboBox.Items.Add( item );
                }

                main_scope_description_comboBox.Text = Mechanical;
                main_scope_description_comboBox.Enabled = false;  
            }
            else if( division_number.Text == "Division  - 16" )
            {
                DivListStr.Add( "Basic Electrical Materials and Methods" ); DivListStr.Add( "Wiring Methods" ); DivListStr.Add( "Electrical Power" ); DivListStr.Add( "Transmission and Distribution" ); DivListStr.Add( "Low-Voltage Distribution" ); DivListStr.Add( "Lighting" ); DivListStr.Add( "Communications" );
                DivListStr.Add( "Sound and Video" );

                sub_scope_description_comboBox.Items.Clear( );
                foreach( var item in DivListStr )
                {
                    sub_scope_description_comboBox.Items.Add( item );
                }

                main_scope_description_comboBox.Text = Electrical;
                main_scope_description_comboBox.Enabled = false; 
            }
            else
            {
               // main_scope_description_comboBox.Enabled = true;
                main_scope_description_comboBox.Text = string.Empty;
                main_scope_description_comboBox.Items.Clear( );
                main_scope_description_comboBox.Focus( );
                main_scope_description_comboBox.ForeColor = Color.DarkGray;                
            }
        }
        
        #endregion

        #region Old method for  selection WBS code
        //public void DivnumWbsCode(ComboBox wbsCode_cmb, ComboBox DivisionNum_cmb,ComboBox main_scope_description_comboBox ) {
        //    List<string> DivisionNum_cmbSelectedText = new List<string>();

        //    if ((string)DivisionNum_cmb.SelectedItem != "-Select division no.-")
        //    {
        //        DivisionNum_cmbSelectedText.Add("Division no. - " + DivisionNum_cmb.SelectedItem);
        //        DivisionNum_cmb.DataSource = DivisionNum_cmbSelectedText;
        //        main_scope_description_comboBox.ForeColor = Color.Black;
        //    }

        //    List<string> DivListStr = new List<string>();
        //    DivListStr.Add("-Select WBS code-");
        //    int Div2Int = 2000;
        //    int Div3Int = 3000;
        //    int Div4Int = 4000;
        //    int Div5Int = 5000;
        //    int Div6Int = 6000;
        //    int Div7Int = 7000;
        //    int Div8Int = 8000;
        //    int Div9Int = 9000;
        //    int Div10Int = 10050;
        //    int Div11Int = 11000;
        //    int Div12Int = 12000;
        //    int Div13Int = 13000;
        //    int Div14Int = 14000;
        //    int Div15Int = 15000;
        //    int Div16Int = 16000;

        //    if (DivisionNum_cmb.Text == "Division no. - 1")
        //    {
        //        for (int i = 1; i < 10; i++)
        //        {
        //            DivListStr.Add("1" + i + "00");
        //        }
        //        wbsCode_cmb.DataSource = DivListStr;

        //        wbsCode_cmb.Text = General_Requirements;
        //    }
        //    else if (DivisionNum_cmb.Text == "Division no. - 2")
        //    {
        //        for (int i = 1; i < 13; i++)
        //        {
        //            if (Div2Int == 2000 || Div2Int == 2050 || Div2Int == 2400 || Div2Int == 2450 || Div2Int == 2900)
        //            {
        //                Div2Int += 50;
        //                DivListStr.Add(Div2Int.ToString());
        //            }
        //            else
        //            {
        //                Div2Int += 100;
        //                DivListStr.Add(Div2Int.ToString());
        //            }
        //        }
        //        wbsCode_cmb.DataSource = DivListStr;
        //        main_scope_description_comboBox.Text = Site_Construction;
        //    }
        //    else if (DivisionNum_cmb.Text == "Division no. - 3")
        //    {
        //        for (int i = 1; i < 10; i++)
        //        {
        //            if (Div3Int == 3000 || Div3Int == 3050)
        //            {
        //                Div3Int += 50;
        //                DivListStr.Add(Div3Int.ToString());
        //            }
        //            else if (Div3Int == 3700)
        //            {
        //                Div3Int += 200;
        //                DivListStr.Add(Div3Int.ToString());
        //            }
        //            else
        //            {
        //                Div3Int += 100;
        //                DivListStr.Add(Div3Int.ToString());
        //            }
        //        }
        //        wbsCode_cmb.DataSource = DivListStr;
        //        main_scope_description_comboBox.Text = Concrete;
        //    }
        //    else if (DivisionNum_cmb.Text == "Division no. - 4")
        //    {
        //        for (int i = 1; i < 9; i++)
        //        {
        //            if (Div4Int == 4000)
        //            {
        //                Div4Int += 50;
        //                DivListStr.Add(Div4Int.ToString());
        //            }
        //            else if (Div4Int == 4050)
        //            {
        //                Div4Int += 150;
        //                DivListStr.Add(Div4Int.ToString());
        //            }
        //            else if (Div4Int == 4200)
        //            {
        //                Div4Int += 200;
        //                DivListStr.Add(Div4Int.ToString());
        //            }
        //            else
        //            {
        //                Div4Int += 100;
        //                DivListStr.Add(Div4Int.ToString());
        //            }
        //        }
        //        wbsCode_cmb.DataSource = DivListStr;
        //        main_scope_description_comboBox.Text = Masonry;
        //    }
        //    else if (DivisionNum_cmb.Text == "Division no. - 5")
        //    {
        //        for (int i = 1; i < 11; i++)
        //        {
        //            if (Div5Int == 5000 || Div5Int == 5050)
        //            {
        //                Div5Int += 50;
        //                DivListStr.Add(Div5Int.ToString());
        //            }
        //            else
        //            {
        //                Div5Int += 100;
        //                DivListStr.Add(Div5Int.ToString());
        //            }
        //        }
        //        wbsCode_cmb.DataSource = DivListStr;
        //        main_scope_description_comboBox.Text = Metals;
        //    }
        //    else if (DivisionNum_cmb.Text == "Division no. - 6")
        //    {
        //        for (int i = 1; i < 8; i++)
        //        {
        //            if (Div6Int == 6000 || Div6Int == 6050)
        //            {
        //                Div6Int += 50;
        //                DivListStr.Add(Div6Int.ToString());
        //            }
        //            else if (Div6Int == 6200)
        //            {
        //                Div6Int += 200;
        //                DivListStr.Add(Div6Int.ToString());
        //            }
        //            else if (Div6Int == 6600)
        //            {
        //                Div6Int += 300;
        //                DivListStr.Add(Div6Int.ToString());
        //            }
        //            else
        //            {
        //                Div6Int += 100;
        //                DivListStr.Add(Div6Int.ToString());
        //            }
        //        }
        //        wbsCode_cmb.DataSource = DivListStr;
        //        main_scope_description_comboBox.Text = Wood_and_Plastics;
        //    }
        //    else if (DivisionNum_cmb.Text == "Division no. - 7")
        //    {
        //        for (int i = 1; i < 11; i++)
        //        {
        //            if (Div7Int == 7000 || Div7Int == 7050)
        //            {
        //                Div7Int += 50;
        //                DivListStr.Add(Div7Int.ToString());
        //            }
        //            else
        //            {
        //                Div7Int += 100;
        //                DivListStr.Add(Div7Int.ToString());
        //            }
        //        }
        //        wbsCode_cmb.DataSource = DivListStr;
        //        main_scope_description_comboBox.Text = Thermal_and_Moisture_Protection;
        //    }
        //    else if (DivisionNum_cmb.Text == "Division no. - 8")
        //    {
        //        for (int i = 1; i < 11; i++)
        //        {
        //            if (Div8Int == 8000 || Div8Int == 8050)
        //            {
        //                Div8Int += 50;
        //                DivListStr.Add(Div8Int.ToString());
        //            }
        //            else
        //            {
        //                Div8Int += 100;
        //                DivListStr.Add(Div8Int.ToString());
        //            }
        //        }
        //        wbsCode_cmb.DataSource = DivListStr;
        //        main_scope_description_comboBox.Text = Doors_and_Windows;
        //    }
        //    else if (DivisionNum_cmb.Text == "Division no. - 9")
        //    {
        //        for (int i = 1; i < 11; i++)
        //        {
        //            if (Div9Int == 9000 || Div9Int == 9050)
        //            {
        //                Div9Int += 50;
        //                DivListStr.Add(Div9Int.ToString());
        //            }
        //            else
        //            {
        //                Div9Int += 100;
        //                DivListStr.Add(Div9Int.ToString());
        //            }
        //        }
        //        wbsCode_cmb.DataSource = DivListStr;
        //        main_scope_description_comboBox.Text = Finishes;
        //    }
        //    else if (DivisionNum_cmb.Text == "Division no. - 10")
        //    {
        //        for (int i = 1; i < 25; i++)
        //        {
        //            if (Div10Int == 10050 || Div10Int == 10100 || Div10Int == 10150 || Div10Int == 10350 || Div10Int == 10400 || Div10Int == 10450 || Div10Int == 10550 || Div10Int == 10700 || Div10Int == 10750)
        //            {
        //                Div10Int += 50;
        //                DivListStr.Add(Div10Int.ToString());
        //            }
        //            else if (Div10Int == 10200 || Div10Int == 10300)
        //            {
        //                Div10Int += 40;
        //                DivListStr.Add(Div10Int.ToString());
        //            }
        //            else if (Div10Int == 10240 || Div10Int == 10290 || Div10Int == 10250 || Div10Int == 10260 || Div10Int == 10340 || Div10Int == 10520)
        //            {
        //                Div10Int += 10;
        //                DivListStr.Add(Div10Int.ToString());
        //            }
        //            else if (Div10Int == 10270 || Div10Int == 10500 || Div10Int == 10530 || Div10Int == 10880)
        //            {
        //                Div10Int += 20;
        //                DivListStr.Add(Div10Int.ToString());
        //            }
        //            else if (Div10Int == 10670)
        //            {
        //                Div10Int += 30;
        //                DivListStr.Add(Div10Int.ToString());
        //            }
        //            else if (Div10Int == 10600)
        //            {
        //                Div10Int += 70;
        //                DivListStr.Add(Div10Int.ToString());
        //            }
        //            else if (Div10Int == 10800)
        //            {
        //                Div10Int += 80;
        //                DivListStr.Add(Div10Int.ToString());
        //            }
        //        }
        //        wbsCode_cmb.DataSource = DivListStr;
        //        main_scope_description_comboBox.Text = Specialties;
        //    }
        //    else if (DivisionNum_cmb.Text == "Division no. - 11")
        //    {
        //        for (int i = 1; i < 37; i++)
        //        {
        //            if (Div11Int == 11000 || Div11Int == 11010 || Div11Int == 11020 || Div11Int == 11030 || Div11Int == 11040 || Div11Int == 11050 || Div11Int == 11060 || Div11Int == 11070 || Div11Int == 11080 || Div11Int == 11090 || Div11Int == 11100 || Div11Int == 11110 || Div11Int == 11120 || Div11Int == 11130 || Div11Int == 11140 || Div11Int == 11150 || Div11Int == 11160 || Div11Int == 11190 || Div11Int == 11450 || Div11Int == 11460 || Div11Int == 11470 || Div11Int == 11650)
        //            {
        //                Div11Int += 10;
        //                DivListStr.Add(Div11Int.ToString());
        //            }
        //            else if (Div11Int == 11170 || Div11Int == 11660 || Div11Int == 11680 || Div11Int == 11850 || Div11Int == 11280 || Div11Int == 11480)
        //            {
        //                Div11Int += 20;
        //                DivListStr.Add(Div11Int.ToString());
        //            }
        //            else if (Div11Int == 11200 || Div11Int == 11700)
        //            {
        //                Div11Int += 80;
        //                DivListStr.Add(Div11Int.ToString());
        //            }
        //            else if (Div11Int == 11400 || Div11Int == 11600)
        //            {
        //                Div11Int += 50;
        //                DivListStr.Add(Div11Int.ToString());
        //            }
        //            else if (Div11Int == 11780)
        //            {
        //                Div11Int += 70;
        //                DivListStr.Add(Div11Int.ToString());
        //            }
        //            else if (Div11Int == 11870)
        //            {
        //                Div11Int += 30;
        //                DivListStr.Add(Div11Int.ToString());
        //            }
        //            else
        //            {
        //                Div11Int += 100;
        //                DivListStr.Add(Div11Int.ToString());
        //            }
        //        }
        //        wbsCode_cmb.DataSource = DivListStr;
        //        main_scope_description_comboBox.Text = Equipment;
        //    }
        //    else if (DivisionNum_cmb.Text == "Division no. - 12")
        //    {
        //        for (int i = 1; i < 10; i++)
        //        {
        //            if (Div12Int == 12000 || Div12Int == 12050)
        //            {
        //                Div12Int += 50;
        //                DivListStr.Add(Div12Int.ToString());
        //            }
        //            else if (Div12Int == 12100)
        //            {
        //                Div12Int += 200;
        //                DivListStr.Add(Div12Int.ToString());
        //            }
        //            else
        //            {
        //                Div12Int += 100;
        //                DivListStr.Add(Div12Int.ToString());
        //            }
        //        }
        //        wbsCode_cmb.DataSource = DivListStr;
        //        main_scope_description_comboBox.Text = Furnishings;
        //    }
        //    else if (DivisionNum_cmb.Text == "Division no. - 13")
        //    {
        //        for (int i = 1; i < 30; i++)
        //        {
        //            if (Div13Int == 13000 || Div13Int == 13010 || Div13Int == 13020 || Div13Int == 13080 || Div13Int == 13090 || Div13Int == 13100 || Div13Int == 13110 || Div13Int == 13150 || Div13Int == 13175 || Div13Int == 13190 || Div13Int == 13220 || Div13Int == 13230 || Div13Int == 13000 )
        //            {
        //                Div13Int += 10;
        //                DivListStr.Add(Div13Int.ToString());
        //            }
        //            else if (Div13Int == 13030 || Div13Int == 13500 || Div13Int == 13550 || Div13Int == 13800 || Div13Int == 13850)
        //            {
        //                Div13Int += 50;
        //                DivListStr.Add(Div13Int.ToString());
        //            }
        //            else if (Div13Int == 13120)
        //            {
        //                Div13Int += 30;
        //                DivListStr.Add(Div13Int.ToString());
        //            }
        //            else if (Div13Int == 13160 || Div13Int == 13165 || Div13Int == 13170 || Div13Int == 13185)
        //            {
        //                Div13Int += 5;
        //                DivListStr.Add(Div13Int.ToString());
        //            }
        //            else if (Div13Int == 13200 || Div13Int == 13220 || Div13Int == 13240 || Div13Int == 13260 )
        //            {
        //                Div13Int += 20;
        //                DivListStr.Add(Div13Int.ToString());
        //            }
        //            else if ( Div13Int == 13280)
        //            {
        //                Div13Int += 120;
        //                DivListStr.Add(Div13Int.ToString());
        //            }
        //            else
        //            {
        //                Div13Int += 100;
        //                DivListStr.Add(Div13Int.ToString());
        //            }
        //        }
        //        wbsCode_cmb.DataSource = DivListStr;
        //        main_scope_description_comboBox.Text = Special_Construction;
        //    }
        //    else if (DivisionNum_cmb.Text == "Division no. - 14")
        //    {
        //        for (int i = 1; i < 10; i++)
        //        {
        //            if (Div14Int >= 14000)
        //            {
        //                Div14Int += 100;
        //                DivListStr.Add(Div14Int.ToString());
        //            }                   
        //        }
        //        wbsCode_cmb.DataSource = DivListStr;
        //        main_scope_description_comboBox.Text = Conveying_Systems;
        //    }
        //    else if (DivisionNum_cmb.Text == "Division no. - 15")
        //    {
        //        for (int i = 1; i < 12; i++)
        //        {
        //            if (Div15Int == 15000 || Div15Int == 15050 || Div15Int == 15900)
        //            {
        //                Div15Int += 50;
        //                DivListStr.Add(Div15Int.ToString());
        //            }
        //            else
        //            {
        //                Div15Int += 100;
        //                DivListStr.Add(Div15Int.ToString());
        //            }
        //        }
        //        wbsCode_cmb.DataSource = DivListStr;
        //        main_scope_description_comboBox.Text = Mechanical;
        //    }
        //    else if (DivisionNum_cmb.Text == "Division no. - 16")
        //    {
        //        for (int i = 1; i < 9; i++)
        //        {
        //            if (Div16Int == 16000 || Div16Int == 16050)
        //            {
        //                Div16Int += 50;
        //                DivListStr.Add(Div16Int.ToString());
        //            }
        //            else if (Div16Int==16500)
        //            {
        //                Div16Int += 200;
        //                DivListStr.Add(Div16Int.ToString());
        //            }
        //            else
        //            {
        //                Div16Int += 100;
        //                DivListStr.Add(Div16Int.ToString());
        //            }
        //        }
        //        wbsCode_cmb.DataSource = DivListStr;
        //        main_scope_description_comboBox.Text = Electrical;
        //    }
        //    else
        //    {
        //        wbsCode_cmb.DataSource = DivListStr;
        //    }
        //}
        #endregion
    }
}
