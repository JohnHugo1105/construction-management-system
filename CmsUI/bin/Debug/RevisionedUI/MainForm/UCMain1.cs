using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GSG_Builders.RevisionedUI.MainForm {
    public partial class UCMain1 : UserControl {        
       
        public UCMain1() {
            InitializeComponent();
           
            
        }

        private void bunifuFlatButton1_Click(object sender, EventArgs e) {
            
            

        }

        private void UCmain_Load(object sender, EventArgs e) {
          
        }

        private void picB_Designing3_Click(object sender, EventArgs e) {

        }
        private void UCMain_Leave(object sender, EventArgs e) {
           
        }
       
        private void SlideShowHome() {
            PictureBox[][] picB_planningJaggedArray = new PictureBox[][] { new PictureBox[] { picB_Planning3, picB_Planning1, picB_Planning2 }, new PictureBox[] { picB_Planning1, picB_Planning3, picB_Planning2 } };
            PictureBox[][] picB_designingJaggedArray = new PictureBox[][] { new PictureBox[] { picB_Designing3, picB_Designing1, picB_Designing2 }, new PictureBox[] { picB_Designing1, picB_Designing3, picB_Designing2 } };
            PictureBox[][] picB_buildingJaggedArray = new PictureBox[][] { new PictureBox[] { picB_Building3, picB_Building1, picB_Building2 }, new PictureBox[] { picB_Building1, picB_Building3, picB_Building2 } };

            for (int j = 0; j < 2; j++)
            {
                for (int k = 0; k < 3; k++)
                {
                    PictureBox[][] picB_ALL = new PictureBox[][] { new PictureBox[] { picB_planningJaggedArray[j][k] }, new PictureBox[] { picB_designingJaggedArray[j][k] }, new PictureBox[] { picB_buildingJaggedArray[j][k] } };
                    for (int i = 0; i < picB_ALL.Length; i++)
                    {
                        foreach (var item in picB_ALL[i])
                        {
                            if (item.Visible)
                            {
                                item.Visible = false;
                            }
                        }
                    }
                }
            }

            int LoopCount = 0;
            do
            {
                while (Visible == true)
                {                    
                    if (LoopCount == 0)
                    {                      
                        bfLbl_SlideshowCaption.Text = "PLANNING";
                        bfLbl_SlideshowCaption.Visible = false;
                        bunifuTransition3.ShowSync(bfLbl_SlideshowCaption);
                        for (int i = 0; i < picB_planningJaggedArray.Length; i++)
                        {
                            PictureBox[] innderArray = picB_planningJaggedArray[i];
                            for (int j = 0; j < innderArray.Length; j++)
                            {
                                bunifuTransition1.HideSync(picB_planningJaggedArray[0][j]);
                                bunifuTransition2.ShowSync(picB_planningJaggedArray[1][j]);
                            }
                        }
                        for (int j = 0; j < 2; j++)
                        {
                            for (int k = 0; k < 3; k++)
                            {
                                PictureBox[][] picB_ALL = new PictureBox[][] { new PictureBox[] { picB_planningJaggedArray[j][k] }, new PictureBox[] { picB_designingJaggedArray[j][k] }, new PictureBox[] { picB_buildingJaggedArray[j][k] } };
                                for (int i = 0; i < picB_ALL.Length; i++)
                                {
                                    foreach (var item in picB_ALL[i])
                                    {
                                        bunifuTransition1.HideSync(item);
                                    }
                                }
                            }
                        }
                        ////for (int i = 0; i < picB_planningJaggedArray.Length; i++)
                        ////{
                        ////    PictureBox[] innderArray = picB_planningJaggedArray[i];
                        ////    for (int j = 0; j < innderArray.Length; j++)
                        ////    {
                        ////        bunifuTransition1.HideSync(picB_planningJaggedArray[0][j]);
                        ////    }
                        ////}
                        LoopCount = 1;
                    }
                    else if (LoopCount == 1)
                    {
                        bfLbl_SlideshowCaption.Text = "DESIGNING";
                        bfLbl_SlideshowCaption.Visible = false;
                        bunifuTransition3.ShowSync(bfLbl_SlideshowCaption);
                        for (int i = 0; i < picB_designingJaggedArray.Length; i++)
                        {

                            PictureBox[] innderArray = picB_designingJaggedArray[i];
                            for (int j = 0; j < innderArray.Length; j++)
                            {
                                bunifuTransition1.HideSync(picB_designingJaggedArray[0][j]);
                                bunifuTransition2.ShowSync(picB_designingJaggedArray[1][j]);
                            }
                        }
                        for (int j = 0; j < 2; j++)
                        {
                            for (int k = 0; k < 3; k++)
                            {
                                PictureBox[][] picB_ALL = new PictureBox[][] { new PictureBox[] { picB_planningJaggedArray[j][k] }, new PictureBox[] { picB_designingJaggedArray[j][k] }, new PictureBox[] { picB_buildingJaggedArray[j][k] } };
                                for (int i = 0; i < picB_ALL.Length; i++)
                                {                                   
                                    foreach (var item in picB_ALL[i])
                                    {                                       
                                        bunifuTransition1.HideSync(item);
                                    }
                                }
                            }
                        }
                      
                        LoopCount = 2;
                    }
                    else
                    {
                        bfLbl_SlideshowCaption.Text = "BUILDING";
                        bfLbl_SlideshowCaption.Visible = false;
                        bunifuTransition3.ShowSync(bfLbl_SlideshowCaption);
                        for (int i = 0; i < picB_buildingJaggedArray.Length; i++)
                        {
                            PictureBox[] innderArray = picB_buildingJaggedArray[i];
                            for (int j = 0; j < innderArray.Length; j++)
                            {
                                bunifuTransition1.HideSync(picB_buildingJaggedArray[0][j]);
                                bunifuTransition2.ShowSync(picB_buildingJaggedArray[1][j]);
                            }
                        }
                        for (int j = 0; j < 2; j++)
                        {
                            for (int k = 0; k < 3; k++)
                            {
                                PictureBox[][] picB_ALL = new PictureBox[][] { new PictureBox[] { picB_planningJaggedArray[j][k] }, new PictureBox[] { picB_designingJaggedArray[j][k] }, new PictureBox[] { picB_buildingJaggedArray[j][k] } };
                                for (int i = 0; i < picB_ALL.Length; i++)
                                {
                                    foreach (var item in picB_ALL[i])
                                    {
                                        bunifuTransition1.HideSync(item);
                                    }
                                }
                            }
                        }
                        //for (int i = 0; i < picB_buildingJaggedArray.Length; i++)
                        //{
                        //    PictureBox[] innderArray = picB_buildingJaggedArray[i];
                        //    for (int j = 0; j < innderArray.Length; j++)
                        //    {
                        //        bunifuTransition1.HideSync(picB_buildingJaggedArray[0][j]);
                        //    }
                        //}
                        LoopCount = 0;
                    }
                    Console.WriteLine(LoopCount.ToString());
                }               
            } while (Focus());
        }
        private void UCMain_Enter_1(object sender, EventArgs e) {
            SlideShowHome();
        }

        private void UCMain_VisibleChanged_1(object sender, EventArgs e) {
              
        }
    }
}
