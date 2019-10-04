using Bunifu.Framework.UI;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GSG_Builders.RevisionedUI.Reusable_codes {
    class SelectImage : Fields_reusable_base_class {
        public static string ImagePath;

        public void SelectAnImage( BunifuImageButton item_image , string picPathStr ) {
            string flash_drive = "";
            var drives = DriveInfo.GetDrives( ).Where( drive => drive.IsReady && drive.DriveType == DriveType.Removable );

            foreach( var item in drives )
            {
                flash_drive = item.Name;
            }

            OpenFileDialog Dialog = new OpenFileDialog( );
           
            if( Dialog.ShowDialog( ) == DialogResult.OK )
            {
                ImagePath = Dialog.FileName.ToString( );
                picPathStr = ImagePath;
                item_image.ImageLocation = ImagePath;
            }
            else
            {
                ImagePath = flash_drive + @"THESIS\GSG_Builders\GSG_Builders\Resources\iconfinder_image_add_103590_128_x_128.png";              
            }
            //to set a default path when file dialog is opened
            //  Dialog.InitialDirectory = @"D:\My Files\ECD Pics";
            //   Dialog.Filter = "JPG Files(*.jpg)|*.jpg|PNG Files(*.png)|*.png|All Files(*.*)|*.*";
        }
    }
}
