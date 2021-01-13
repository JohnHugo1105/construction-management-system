using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GSG_Builders.RevisionedUI.Database_connection;
//using MySql.Data.MySqlClient;
using System.Windows.Forms;
using System.Data.SqlClient;
using System.Data;

namespace GSG_Builders.RevisionedUI.Reusable_codes {
    class Autocomplete_source: Fields_reusable_base_class {

        #region Methods w/ or w/o return type
        /* this is to get the main project primarykey/id that will be use in the method Autocomplete_sub_project to filter only the sub projects under only the main project*/
       

        /* getting the projects from database and table main_project and setting it as the autocomplete source of the control */
 
        /* getting the projects from database and table main_project and setting it as the autocomplete source of the control */
        public void Autocomplete_project(ComboBox project_name_comboBox ) {
            project_name_comboBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            project_name_comboBox.AutoCompleteSource = AutoCompleteSource.CustomSource;
            Collection = new AutoCompleteStringCollection();
            //test
            using( var con = new SqlConnection( MS_SQL_SERVER_connection.Get_connection_string( ) ) )
            {
                using( SqlCommand cmd = new SqlCommand( stored_procedures_list.select_records , con ) )
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Connection.Open( );
                    cmd.Parameters.Add( table_parameter , SqlDbType.VarChar ).Value = main_project_table;
                    cmd.Parameters.Add( col_select , SqlDbType.VarChar ).Value = "project_name";
                    cmd.Parameters.Add( other_query , SqlDbType.VarChar ).Value = "ORDER BY main_project_id ASC";

                    SqlDataReader reader = cmd.ExecuteReader( );
                    while( reader.Read( ) )
                    {
                        if( !reader.IsDBNull( 0 ) )
                        {
                            project_name_comboBox.Items.Add( reader.GetString( 0 ) );
                            Collection.Add( reader.GetString( 0 ) );
                        }                        
                    }
                    project_name_comboBox.AutoCompleteCustomSource = Collection;
                }
            }
            #region old codes
            //cmd = new MySqlCommand();
            //cmd.Connection = MySQL_connection.GetConnection();
            //cmd.Connection.Open();
            //cmd.CommandText = "SELECT project_name FROM"+main_project_database_table+ "ORDER BY main_project_id ASC";
            //MySqlDataReader reader = cmd.ExecuteReader();
            //while (reader.Read())
            //{
            //    project_name_comboBox.Items.Add(reader.GetString(0));
            //    Collection.Add(reader.GetString(0));
            //}
            //project_name_comboBox.AutoCompleteCustomSource = Collection;
            #endregion
        }
        /* getting the projects from database and table sub_project and setting it as the autocomplete source of the control */
        public void Autocomplete_sub_project(ComboBox main_project_name_comboBox, ComboBox sub_project_name_comboBox) {
            sub_project_name_comboBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            sub_project_name_comboBox.AutoCompleteSource = AutoCompleteSource.CustomSource;
            Collection = new AutoCompleteStringCollection();

            using( var con = new SqlConnection( MS_SQL_SERVER_connection.Get_connection_string( ) ) )
            {
                using( SqlCommand cmd = new SqlCommand( stored_procedures_list.select_records , con ) )
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Connection.Open( );
                    cmd.Parameters.Add( table_parameter , SqlDbType.VarChar ).Value =sub_project_table ;
                    cmd.Parameters.Add( col_select , SqlDbType.VarChar ).Value = "sub_project_name";
                    cmd.Parameters.Add( other_query , SqlDbType.VarChar ).Value = "ORDER BY main_project_id ASC";

                    SqlDataReader reader = cmd.ExecuteReader( );
                    while( reader.Read( ) )
                    {
                        if( !reader.IsDBNull( 0 ) )
                        {
                            sub_project_name_comboBox.Items.Add( reader.GetString( 0 ) );
                            Collection.Add( reader.GetString( 0 ) );
                        }                       
                    }
                    sub_project_name_comboBox.AutoCompleteCustomSource = Collection;
                }
            }
           #region old codes
                //cmd = new MySqlCommand();
                //cmd.Connection = MySQL_connection.GetConnection();
                //cmd.Connection.Open();
                //cmd.CommandText = "SELECT sub_project_name FROM" + sub_project_database_table + " WHERE main_project_id='" + Main_project_id(main_project_name_comboBox.Text) + "' ORDER BY sub_project_id ASC";
                //MySqlDataReader reader = cmd.ExecuteReader();
                //while (reader.Read())
                //{
                //    sub_project_name_comboBox.Items.Add(reader.GetString(0));
                //    Collection.Add(reader.GetString(0));
                //}
                //sub_project_name_comboBox.AutoCompleteCustomSource = Collection;
                #endregion
            }
        /* getting the main scope descriptions from database and table bill_mat and setting it as the autocomplete source of the control */
        public void Autocomplete_main_scope_description(ComboBox main_scope_description_comboBox) {            

            List<string> main_scope_description_list = new List<string>( );/* created a string list for the default/standard main scope */

            List<string> main_scope_description_list_on_database = new List<string>( );

            main_scope_description_list.Add( "General Requirements" );
            main_scope_description_list.Add( "Site Construction" );
            main_scope_description_list.Add( "Concrete" );
            main_scope_description_list.Add( "Masonry" );
            main_scope_description_list.Add( "Metals" );
            main_scope_description_list.Add( "Wood and Plastics" );
            main_scope_description_list.Add( "Thermal and Moisture Protection" );
            main_scope_description_list.Add( "Doors and Windows" );
            main_scope_description_list.Add( "Finishes" );
            main_scope_description_list.Add( "Specialties" );
            main_scope_description_list.Add( "Equipment" );
            main_scope_description_list.Add( "Furnishings" );
            main_scope_description_list.Add( "Special Construction" );
            main_scope_description_list.Add( "Conveying Systems" );
            main_scope_description_list.Add( "Mechanical" );
            main_scope_description_list.Add( "Electrical" );
            /* ends here */


            using( var con = new SqlConnection( MS_SQL_SERVER_connection.Get_connection_string( ) ) )
            {
                using( SqlCommand cmd = new SqlCommand( stored_procedures_list.select_records , con ) )
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Connection.Open( );
                    cmd.Parameters.Add( table_parameter , SqlDbType.VarChar ).Value = bill_mat_table;
                    cmd.Parameters.Add( col_select , SqlDbType.VarChar ).Value = "DISTINCT  main_scope_description";
                    cmd.Parameters.Add( other_query , SqlDbType.VarChar ).Value = "";

                    SqlDataReader reader = cmd.ExecuteReader( );

                    while( reader.Read( ) )
                    {
                        if( !reader.IsDBNull(0) )
                        {
                            main_scope_description_list_on_database.Add( reader.GetString( 0 ) );
                        }                        
                    }

                    var resultDiff = main_scope_description_list_on_database.Except( main_scope_description_list ).ToList( );
                    foreach( var item in resultDiff )
                    {
                        main_scope_description_comboBox.Items.Add( item );// add the result values from database then add to the items of main scope description
                        main_scope_description_comboBox.AutoCompleteCustomSource.Add( item );// adding items on the collection for the autocomplete process       
                    }
                }
            }
            #region old codes
            //cmd = new MySqlCommand();
            //cmd.Connection = MySQL_connection.GetConnection();
            //cmd.Connection.Open();         
            //cmd.CommandText = "SELECT DISTINCT  main_scope_description FROM" + bill_mat_database_table;
            //MySqlDataReader reader = cmd.ExecuteReader();

            //while (reader.Read())
            //{
            //    main_scope_description_list_on_database.Add( reader.GetString( 0 ) );           
            //}

            //var resultDiff = main_scope_description_list_on_database.Except( main_scope_description_list ).ToList();
            //foreach( var item in resultDiff )
            //{
            //    main_scope_description_comboBox.Items.Add( item );// add the result values from database then add to the items of main scope description
            //    main_scope_description_comboBox.AutoCompleteCustomSource.Add( item );// adding items on the collection for the autocomplete process       
            //}            
            #endregion
        }

        /* getting the sub scope descriptions from database and table bill_mat and setting it as the autocomplete source of the control */
        public void Autocomplete_sub_scope_description ( ComboBox sub_scope_description_comboBox ) {
            sub_scope_description_comboBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            sub_scope_description_comboBox.AutoCompleteSource = AutoCompleteSource.CustomSource;
            Collection = new AutoCompleteStringCollection( );

            using( var con = new SqlConnection( MS_SQL_SERVER_connection.Get_connection_string( ) ) )
            {
                using( SqlCommand cmd = new SqlCommand( stored_procedures_list.select_records , con ) )
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Connection.Open( );
                    cmd.Parameters.Add( table_parameter , SqlDbType.VarChar ).Value = bill_mat_table;
                    cmd.Parameters.Add( col_select , SqlDbType.VarChar ).Value = "DISTINCT sub_scope_description";
                    cmd.Parameters.Add( other_query , SqlDbType.VarChar ).Value = "";

                    SqlDataReader reader = cmd.ExecuteReader( );
                   while( reader.Read( ) )
                    {
                        if( !reader.IsDBNull(0) )
                        {
                            sub_scope_description_comboBox.Items.Add( reader.GetString( 0 ) );
                            Collection.Add( reader.GetString( 0 ) );
                        }                      
                    }
                    sub_scope_description_comboBox.AutoCompleteCustomSource = Collection;
                }
            }
            #region old codes
            //cmd = new MySqlCommand( );
            //cmd.Connection = MySQL_connection.GetConnection( );
            //cmd.Connection.Open( );
            //cmd.CommandText = "SELECT DISTINCT sub_scope_description FROM" + bill_mat_database_table;
            //MySqlDataReader reader = cmd.ExecuteReader( );
            //while( reader.Read( ) )
            //{
            //    sub_scope_description_comboBox.Items.Add( reader.GetString( 0 ) );
            //    Collection.Add( reader.GetString( 0 ) );
            //}
            //sub_scope_description_comboBox.AutoCompleteCustomSource = Collection;
            #endregion
        }
        public void Autocomplete_sub_scope_description_not_standard ( ComboBox sub_scopre_description_comboBox , ComboBox main_scope_description_comboBox ) {
       //     sub_scopre_description_comboBox.AutoCompleteMode = AutoCompleteMode.Suggest;
            sub_scopre_description_comboBox.AutoCompleteSource = AutoCompleteSource.CustomSource;
            Collection = new AutoCompleteStringCollection( );
            sub_scopre_description_comboBox.Items.Clear( );
            Collection.Clear( );

            using( var con = new SqlConnection( MS_SQL_SERVER_connection.Get_connection_string( ) ) )
            {
                using( SqlCommand cmd = new SqlCommand( stored_procedures_list.select_records , con ) )
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Connection.Open( );
                    cmd.Parameters.Add( table_parameter , SqlDbType.VarChar ).Value = bill_mat_table;
                    cmd.Parameters.Add( col_select , SqlDbType.VarChar ).Value = "DISTINCT sub_scope_description";
                    cmd.Parameters.Add( other_query , SqlDbType.VarChar ).Value = "WHERE main_scope_description='" + main_scope_description_comboBox.Text.Replace( "'" , "''" ) + "'";

                    SqlDataReader reader = cmd.ExecuteReader( );
                   while( reader.Read( ) )
                    {
                        if( !reader.IsDBNull(0) )
                        {
                            sub_scopre_description_comboBox.Items.Add( reader.GetString( 0 ) );
                            Collection.Add( reader.GetString( 0 ) );
                        }                        
                    }
                    sub_scopre_description_comboBox.AutoCompleteCustomSource = Collection;
                }
            }
            #region old codes
            //cmd = new MySqlCommand( );
            //cmd.Connection = MySQL_connection.GetConnection( );
            //cmd.Connection.Open( );
            //cmd.CommandText = "SELECT DISTINCT sub_scope_description FROM" + bill_mat_database_table + "WHERE main_scope_description='" + main_scope_description_comboBox.Text.Replace( "'" , "''" ) + "'";
            //MySqlDataReader reader = cmd.ExecuteReader( );
            //while( reader.Read( ) )
            //{
            //    sub_scopre_description_comboBox.Items.Add( reader.GetString( 0 ) );
            //    Collection.Add( reader.GetString( 0 ) );
            //}
            //sub_scopre_description_comboBox.AutoCompleteCustomSource = Collection;
            #endregion
        }

        public string[ ] Autocomplete_sub_scope_selection_get_items ( ComboBox sub_scope_description_comboBox , ComboBox division_number , ComboBox main_scope_description_comboBox ) {

            List<string> DivListStr = new List<string>( );
            string[ ] Div1_arraylist = new string[ 9 ];
            string[ ] Div2_arraylist = new string[ 12 ];
            string[ ] Div3_arraylist = new string[ 9 ];
            string[ ] Div4_arraylist = new string[ 8 ];
            string[ ] Div5_arraylist = new string[ 10 ];
            string[ ] Div6_arraylist = new string[ 7 ];
            string[ ] Div7_arraylist = new string[ 10 ];
            string[ ] Div8_arraylist = new string[ 10 ];
            string[ ] Div9_arraylist = new string[ 10 ];
            string[ ] Div10_arraylist = new string[ 24 ];
            string[ ] Div11_arraylist = new string[ 36 ];
            string[ ] Div12_arraylist = new string[ 9 ];
            string[ ] Div13_arraylist = new string[ 29 ];
            string[ ] Div14_arraylist = new string[ 9 ];
            string[ ] Div15_arraylist = new string[ 11 ];
            string[ ] Div16_arraylist = new string[ 8 ];
            string[ ] main_array_value_holder = new string[ 1 ];

            if( division_number.Text == "Division no. - 1" )
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

                int count = 0;

                foreach( var item in DivListStr )
                {
                    Div1_arraylist[ count ] = item;
                    count++;
                }
                main_array_value_holder = Div1_arraylist;

            }
            else if( division_number.Text == "Division no. - 2" )
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

                int count = 0;

                foreach( var item in DivListStr )
                {
                    Div2_arraylist[ count ] = item;
                    count++;
                }
                main_array_value_holder = Div2_arraylist;

            }
            else if( division_number.Text == "Division no. - 3" )
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

                int count = 0;

                foreach( var item in DivListStr )
                {
                    Div3_arraylist[ count ] = item;
                    count++;
                }
                main_array_value_holder = Div3_arraylist;

            }
            else if( division_number.Text == "Division no. - 4" )
            {
                DivListStr.Add( "Basic Masonry Materials and Methods" );
                DivListStr.Add( "Masonry Units" );
                DivListStr.Add( "Stone" );
                DivListStr.Add( "Refractories" );
                DivListStr.Add( "Corrosion-Resistant Masonry" );
                DivListStr.Add( "Simulated Masonry" );
                DivListStr.Add( "Masonry Assemblies" );
                DivListStr.Add( "Masonry Restoration and Cleaning" );

                int count = 0;

                foreach( var item in DivListStr )
                {
                    Div4_arraylist[ count ] = item;
                    count++;
                }
                main_array_value_holder = Div4_arraylist;


            }
            else if( division_number.Text == "Division no. - 5" )
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

                int count = 0;

                foreach( var item in DivListStr )
                {
                    Div5_arraylist[ count ] = item;
                    count++;
                }
                main_array_value_holder = Div5_arraylist;

            }
            else if( division_number.Text == "Division no. - 6" )
            {
                DivListStr.Add( "Basic Wood and Plastic Materials and Methods" );
                DivListStr.Add( "Rough Carpentry" );
                DivListStr.Add( "Finish Carpentry" );
                DivListStr.Add( "Architectural Woodwork" );
                DivListStr.Add( "Structural Plastics" );
                DivListStr.Add( "Plastic Fabrications" );
                DivListStr.Add( "Wood and Plastic Restoration and Cleaning" );

                int count = 0;

                foreach( var item in DivListStr )
                {
                    Div6_arraylist[ count ] = item;
                    count++;
                }
                main_array_value_holder = Div6_arraylist;

            }
            else if( division_number.Text == "Division no. - 7" )
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

                int count = 0;

                foreach( var item in DivListStr )
                {
                    Div7_arraylist[ count ] = item;
                    count++;
                }
                main_array_value_holder = Div7_arraylist;

            }
            else if( division_number.Text == "Division no. - 8" )
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

                int count = 0;

                foreach( var item in DivListStr )
                {
                    Div8_arraylist[ count ] = item;
                    count++;
                }
                main_array_value_holder = Div8_arraylist;
            }
            else if( division_number.Text == "Division no. - 9" )
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

                int count = 0;

                foreach( var item in DivListStr )
                {
                    Div9_arraylist[ count ] = item;
                    count++;
                }
                main_array_value_holder = Div9_arraylist;
            }
            else if( division_number.Text == "Division no. - 10" )
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

                int count = 0;

                foreach( var item in DivListStr )
                {
                    Div10_arraylist[ count ] = item;
                    count++;
                }
                main_array_value_holder = Div10_arraylist;

            }
            else if( division_number.Text == "Division no. - 11" )
            {
                DivListStr.Add( "Maintenance Equipment" ); DivListStr.Add( "Security and Vault Equipment" ); DivListStr.Add( "Teller and Service Equipment" ); DivListStr.Add( "Ecclesiastical Equipment" ); DivListStr.Add( "Library Equipment" ); DivListStr.Add( "Theater and Stage Equipment" ); DivListStr.Add( "Instrumental Equipment" ); DivListStr.Add( "Registration Equipment" );
                DivListStr.Add( "Checkroom Equipment" ); DivListStr.Add( "Mercantile Equipment" ); DivListStr.Add( "Commercial Laundry and Dry Cleaning Equipment" ); DivListStr.Add( "Vending Equipment" ); DivListStr.Add( "Audio-Visual Equipment" ); DivListStr.Add( "Vehicle Service Equipment" ); DivListStr.Add( "Parking Control Equipment" );
                DivListStr.Add( "Loading Dock Equipment" ); DivListStr.Add( "Solid Waste Handling Equipment" ); DivListStr.Add( "Detention Equipment" ); DivListStr.Add( "Water Supply and Treatment Equipment" ); DivListStr.Add( "Hydraulic Gates and Valves" ); DivListStr.Add( "Fluid Waste Treatment and Disposal Equipment" ); DivListStr.Add( "Food Service Equipment" );
                DivListStr.Add( "Residential Equipment" ); DivListStr.Add( "Unit Kitchens" ); DivListStr.Add( "Darkroom Equipment" ); DivListStr.Add( "Athletic, Recreational, and Therapeutic Equipment" ); DivListStr.Add( "Industrial and Process Equipment" ); DivListStr.Add( "Laboratory Equipment" ); DivListStr.Add( "Planetarium Equipment" );
                DivListStr.Add( "Observatory Equipment" ); DivListStr.Add( "Office Equipment" ); DivListStr.Add( "Medical Equipment" ); DivListStr.Add( "Mortuary Equipment" ); DivListStr.Add( "Navigation Equipment" ); DivListStr.Add( "Agricultural Equipment" ); DivListStr.Add( "Exhibit Equipment" );

                int count = 0;

                foreach( var item in DivListStr )
                {
                    Div11_arraylist[ count ] = item;
                    count++;
                }
                main_array_value_holder = Div11_arraylist;

            }
            else if( division_number.Text == "Division no. - 12" )
            {
                DivListStr.Add( "Fabrics" ); DivListStr.Add( "Art" ); DivListStr.Add( "Manufactured Casework" ); DivListStr.Add( "Furnishings and Accessories" ); DivListStr.Add( "Furniture" );
                DivListStr.Add( "Multiple Seating" ); DivListStr.Add( "Systems Furniture" ); DivListStr.Add( "Interior Plants and Planters" ); DivListStr.Add( "Furnishings Restoration and Repair" );

                int count = 0;

                foreach( var item in DivListStr )
                {
                    Div12_arraylist[ count ] = item;
                    count++;
                }
                main_array_value_holder = Div12_arraylist;

            }
            else if( division_number.Text == "Division no. - 13" )
            {
                DivListStr.Add( "Air-Supported Structures" ); DivListStr.Add( "Building Modules" ); DivListStr.Add( "Special Purpose Rooms" ); DivListStr.Add( "Sound, Vibration, and Seismic Control" ); DivListStr.Add( "Radiation Protection" ); DivListStr.Add( "Lightning Protection" ); DivListStr.Add( "Cathodic Protection" );
                DivListStr.Add( "Pre-Engineered Structures" ); DivListStr.Add( "Swimming Pools" ); DivListStr.Add( "Aquariums" ); DivListStr.Add( "Aquatic Park Facilities" ); DivListStr.Add( "Tubs and Pools" ); DivListStr.Add( "Ice Rinks" ); DivListStr.Add( "Kennels and Animal Shelters" );
                DivListStr.Add( "Site-Constructed Incinerators" ); DivListStr.Add( "Storage Tanks" ); DivListStr.Add( "Filter Underdrains and Media" ); DivListStr.Add( "Digester Covers and Appurtenances" ); DivListStr.Add( "Oxygenation Systems" ); DivListStr.Add( "Sludge Conditioning Systems" ); DivListStr.Add( "Hazardous Material Remediation" );
                DivListStr.Add( "Measurement and Control Instrumentation" ); DivListStr.Add( "Recording Instrumentation" ); DivListStr.Add( "Transportation Control Instrumentation" ); DivListStr.Add( "Solar and Wind Energy Equipment" ); DivListStr.Add( "Security Access and Surveillance" ); DivListStr.Add( "Building Automation and Control" ); DivListStr.Add( "Detection and Alarm" );
                DivListStr.Add( "Fire Suppression" );

                int count = 0;

                foreach( var item in DivListStr )
                {
                    Div13_arraylist[ count ] = item;
                    count++;
                }
                main_array_value_holder = Div13_arraylist;

            }
            else if( division_number.Text == "Division no. - 14" )
            {
                DivListStr.Add( "Dumbwaiters" ); DivListStr.Add( "Elevators" ); DivListStr.Add( "Escalators and Moving Walks" ); DivListStr.Add( "Lifts" ); DivListStr.Add( "Material Handling" ); DivListStr.Add( "Hoists and Cables" ); DivListStr.Add( "Turntables" ); DivListStr.Add( "Scaffolding" ); DivListStr.Add( "Transportation" );

                int count = 0;

                foreach( var item in DivListStr )
                {
                    Div14_arraylist[ count ] = item;
                    count++;
                }
                main_array_value_holder = Div14_arraylist;

            }
            else if( division_number.Text == "Division no. - 15" )
            {
                DivListStr.Add( "Basic Mechanical Materials and Methods" ); DivListStr.Add( "Building Service Piping" ); DivListStr.Add( "Process Piping" ); DivListStr.Add( "Fire Protection Piping" ); DivListStr.Add( "Plumbing Fixtures and Equipment" ); DivListStr.Add( "Heat-Generation Equipment" );
                DivListStr.Add( "Refrigeration Equipment" ); DivListStr.Add( "Heating, Ventilating, and Air Conditioning Equipment" ); DivListStr.Add( "Air Distribution" ); DivListStr.Add( "HVAC Instrumentation and Controls" ); DivListStr.Add( "Testing, Adjusting, and Balancing" );

                int count = 0;

                foreach( var item in DivListStr )
                {
                    Div15_arraylist[ count ] = item;
                    count++;
                }
                main_array_value_holder = Div15_arraylist;

            }
            else if( division_number.Text == "Division no. - 16" )
            {
                DivListStr.Add( "Basic Electrical Materials and Methods" ); DivListStr.Add( "Wiring Methods" ); DivListStr.Add( "Electrical Power" ); DivListStr.Add( "Transmission and Distribution" ); DivListStr.Add( "Low-Voltage Distribution" ); DivListStr.Add( "Lighting" ); DivListStr.Add( "Communications" );
                DivListStr.Add( "Sound and Video" );

                int count = 0;

                foreach( var item in DivListStr )
                {
                    Div16_arraylist[ count ] = item;
                    count++;
                }
                main_array_value_holder = Div16_arraylist;

            }
            else
            {
                main_scope_description_comboBox.Enabled = true;
            }
            return main_array_value_holder;
        }
        /* getting the item descriptions from database and table bill_mat and setting it as the autocomplete source of the control */
        public void Autocomplete_item_description(TextBox item_description_textbox) {
            item_description_textbox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            item_description_textbox.AutoCompleteSource = AutoCompleteSource.CustomSource;
            Collection = new AutoCompleteStringCollection();

            using( var con = new SqlConnection( MS_SQL_SERVER_connection.Get_connection_string( ) ) )
            {
                using( SqlCommand cmd = new SqlCommand( stored_procedures_list.select_records , con ) )
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Connection.Open( );
                    cmd.Parameters.Add( table_parameter , SqlDbType.VarChar ).Value = bill_mat_table;
                    cmd.Parameters.Add( col_select , SqlDbType.VarChar ).Value = "DISTINCT item_description";
                    cmd.Parameters.Add( other_query , SqlDbType.VarChar ).Value = "";

                    SqlDataReader reader = cmd.ExecuteReader( );
                  while( reader.Read( ) )
                    {
                        if( !reader.IsDBNull(0) )
                        {
                            Collection.Add( reader.GetString( 0 ) );
                            item_description_textbox.AutoCompleteCustomSource = Collection;
                        }                        
                    }                    
                }
            }
            #region old codes
            //cmd = new MySqlCommand();
            //cmd.Connection = MySQL_connection.GetConnection();
            //cmd.Connection.Open();
            //cmd.CommandText = "SELECT DISTINCT item_description FROM" + bill_mat_database_table;
            //MySqlDataReader reader = cmd.ExecuteReader();
            //while (reader.Read())
            //{               
            //    Collection.Add(reader.GetString(0));
            //    item_description_textbox.AutoCompleteCustomSource = Collection;
            //}
            #endregion
        }

        public void Autocomplete_main_project( TextBox project_textBox ) {
            project_textBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            project_textBox.AutoCompleteSource = AutoCompleteSource.CustomSource;
            Collection = new AutoCompleteStringCollection( );

            using( var con = new SqlConnection( MS_SQL_SERVER_connection.Get_connection_string( ) ) )
            {
                using( SqlCommand cmd = new SqlCommand( stored_procedures_list.select_records , con ) )
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Connection.Open( );
                    cmd.Parameters.Add( table_parameter , SqlDbType.VarChar ).Value = main_project_table;
                    cmd.Parameters.Add( col_select , SqlDbType.VarChar ).Value = "DISTINCT project_name";
                    cmd.Parameters.Add( other_query , SqlDbType.VarChar ).Value = "";

                    SqlDataReader reader = cmd.ExecuteReader( );
                    while( reader.Read( ) )
                    {
                        if( !reader.IsDBNull( 0 ) )
                        {
                            Collection.Add( reader.GetString( 0 ) );
                        }      
                    }
                    Collection.Add( "Warehouse" );
                    project_textBox.AutoCompleteCustomSource = Collection;
                }
            }
            #region old codes
            //cmd = new MySqlCommand( );
            //cmd.Connection = MySQL_connection.GetConnection( );
            //cmd.Connection.Open( );
            //cmd.CommandText = "SELECT DISTINCT project_name FROM" + main_project_database_table;
            //MySqlDataReader reader = cmd.ExecuteReader( );
            //while( reader.Read( ) )
            //{
            //    Collection.Add( reader.GetString( 0 ) );
               
            //}
            //Collection.Add( "Warehouse" );
            //project_textBox.AutoCompleteCustomSource = Collection;
            #endregion
        }

        public void Autocomplete_unit_measurement (ComboBox unit_comboBox ) {
            unit_comboBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            unit_comboBox.AutoCompleteSource = AutoCompleteSource.CustomSource;
            Collection = new AutoCompleteStringCollection( );

            using( var con = new SqlConnection( MS_SQL_SERVER_connection.Get_connection_string( ) ) )
            {
                using( SqlCommand cmd = new SqlCommand( stored_procedures_list.select_records , con ) )
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Connection.Open( );
                    cmd.Parameters.Add( table_parameter , SqlDbType.VarChar ).Value = bill_mat_table;
                    cmd.Parameters.Add( col_select , SqlDbType.VarChar ).Value = "DISTINCT unit";
                    cmd.Parameters.Add( other_query , SqlDbType.VarChar ).Value = "";

                    SqlDataReader reader = cmd.ExecuteReader( );
                    while( reader.Read( ) )
                    {
                        if( !reader.IsDBNull( 0 ) )
                        {
                            unit_comboBox.Items.Add( reader.GetString( 0 ) );
                            unit_comboBox.AutoCompleteCustomSource.Add( reader.GetString( 0 ) );
                        }                       
                    }
                }
            }
            #region old codes
            //cmd = new MySqlCommand( );
            //cmd.Connection = MySQL_connection.GetConnection( );
            //cmd.Connection.Open( );
            //cmd.CommandText = "SELECT DISTINCT unit FROM" + bill_mat_database_table;
            //MySqlDataReader reader = cmd.ExecuteReader( );
            //while( reader.Read( ) )
            //{
            //    unit_comboBox.Items.Add( reader.GetString( 0 ) );
            //    unit_comboBox.AutoCompleteCustomSource.Add( reader.GetString( 0 ) );
            //}          
            #endregion
        }

        public void Autocomplete_wbs_code_not_standard (ComboBox wbsCode_comboBox, ComboBox sub_scope_description_comboBox ) {
            wbsCode_comboBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            wbsCode_comboBox.AutoCompleteSource = AutoCompleteSource.CustomSource;
            Collection = new AutoCompleteStringCollection( );
          //  wbsCode_comboBox.Items.Clear( );
            Collection.Clear( );

            using( var con = new SqlConnection( MS_SQL_SERVER_connection.Get_connection_string( ) ) )
            {
                using( SqlCommand cmd = new SqlCommand( stored_procedures_list.select_records , con ) )
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Connection.Open( );
                    cmd.Parameters.Add( table_parameter , SqlDbType.VarChar ).Value = bill_mat_table;
                    cmd.Parameters.Add( col_select , SqlDbType.VarChar ).Value = "DISTINCT wbs_code";
                    cmd.Parameters.Add( other_query , SqlDbType.VarChar ).Value = "WHERE sub_scope_description='" + sub_scope_description_comboBox.Text.Replace( "'" , "''" ) + "'";

                    SqlDataReader reader = cmd.ExecuteReader( );
                    while( reader.Read( ) )
                    {
                        //  wbsCode_comboBox.Items.Add( reader.GetString( 0 ) );
                        if( !reader.IsDBNull( 0 ) )
                        {
                            Collection.Add( reader.GetString( 0 ) );
                            wbsCode_comboBox.Text = reader.GetString( 0 );
                        }                      
                    }
                    wbsCode_comboBox.AutoCompleteCustomSource = Collection;
                }
            }
            #region old codes
            //cmd = new MySqlCommand( );
            //cmd.Connection = MySQL_connection.GetConnection( );
            //cmd.Connection.Open( );
            //cmd.CommandText = "SELECT DISTINCT wbs_code FROM" + bill_mat_database_table + "WHERE sub_scope_description='" + sub_scope_description_comboBox.Text.Replace( "'" , "''" ) + "'";
            //MySqlDataReader reader = cmd.ExecuteReader( );
            //while( reader.Read( ) )
            //{
            //  //  wbsCode_comboBox.Items.Add( reader.GetString( 0 ) );
            //    Collection.Add( reader.GetString( 0 ) );
            //    wbsCode_comboBox.Text = reader.GetString( 0 );
            //}
            //wbsCode_comboBox.AutoCompleteCustomSource = Collection;
            #endregion
        }
        #endregion
    }
}
