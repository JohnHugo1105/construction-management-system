using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GSG_Builders.RevisionedUI {
    public partial class Archives_loading_form : Form {
        public Archives_loading_form ( ) {
            InitializeComponent( );
           
            loading( );
        }

        private void loading ( ) {
            if( !backgroundWorker1.IsBusy )
            {
                backgroundWorker1.RunWorkerAsync( );
            }
        }        

        private void backgroundWorker1_DoWork ( object sender , DoWorkEventArgs e ) {
         
        }

        private void backgroundWorker1_ProgressChanged ( object sender , ProgressChangedEventArgs e ) {
            bunifuCircleProgressbar1.Value = e.ProgressPercentage;          
        }

        private void backgroundWorker1_RunWorkerCompleted ( object sender , RunWorkerCompletedEventArgs e ) {         
            
        }
    }
}
