using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Linq;




namespace DicomImageViewer
{
    public enum ImageBitsPerPixel { Eight, Sixteen, TwentyFour };
    public enum ViewSettings { Zoom1_1, ZoomToFit };

    /// <summary>
    /// This program reads in a DICOM file and displays it on the screen. 
    /// The functionality for viewer is:
    /// o Open DICOM files created as per DICOM 3.0 standard
    /// o Open files with Explicit VR and Implicit VR Transfer Syntax
    /// o Read those files where image bit depth is 8 or 16 bits (Digital Radiography), 
    ///    or RGB images (from Ultrasound)
    /// o Read a DICOM file with just one image inside it
    /// o Read a DICONDE file also (a DICONDE file is a DICOM file with NDE - Non Destructive   
    ///    Evaluation - tags inside it)
    /// o Read older DICOM files. Earlier DICOM files don't have the preamble and prefix, and 
    ///    just contain the string 1.2.840.10008 somewhere in the beginning
    /// o Perform Window/Level operations on the image.
    /// 
    /// This viewer is not intended to:
    /// o Check whether all mandatory tags are present
    /// o Open files with VR other than Explicit and Implicit - in particular, not to open 
    ///    JPEG Lossy and Lossless files

    /// o Read a sequence of images. 
    /// </summary>
        
    public partial class MainForm : Form
    {
        DicomDecoder dd;
        List<byte> pixels8;
        List<byte> pixels88;
        List<ushort> pixels16;
        List<ushort> pixels1616;
        List<byte> pixels24; // 30 July 2010
        int imageWidth;
        int imageHeight;
        int bitDepth;
        int samplesPerPixel;  // Updated 30 July 2010
        bool imageOpened;
        double winCentre;
        double winWidth;
        bool signedImage;
        int maxPixelValue;    // Updated July 2012
        int minPixelValue;

        public MainForm()
        {
            InitializeComponent();
            dd = new DicomDecoder();
            pixels8 = new List<byte>();
            pixels88 = new List<byte>();
            pixels16 = new List<ushort>();
            pixels1616 = new List<ushort>();
            pixels24 = new List<byte>();
            imageOpened = false;
            signedImage = false;
            maxPixelValue = 0;
            minPixelValue = 65535;
        }

//        public List<byte> vrati()
        public void vrati(List<byte> pixels, int imageWidth, int imageHeight, int bitDepth)
        {
           

             
                pixels.Clear();
                pixels.AddRange(pixels8);
                imageWidth = this.imageWidth;
                imageHeight = this.imageHeight;
                bitDepth = this.bitDepth;
                    
        }
               
        private int Otsu_algorithm(byte[] input_image, int imageWidth, int imageHeight, int bitDepth)
        {
            int N = imageWidth * imageHeight;
            double var_max = 0.0, sum = 0.0, sumB = 0.0, q1 = 0.0, q2 = 0.0, µ1 = 0.0, µ2 = 0.0;
            int max_intensity = (int)Math.Pow(2, bitDepth)-1;
            int[] histogram = new int[(int)Math.Pow(2, bitDepth)];
            int i, t;
            double Sigmb;
            int value, threshold = 0;
            int max_value = -100000;



            for (i = 0; i <= max_intensity; i++)
            {
                histogram[i] = 0;
            }

            for (i = 0; i < N; i++)
            {
                value = input_image[i];
               // if (value == 0) continue;
                if (value > max_value) max_value = value;
                histogram[value] += 1;
            }

            for (i = 0; i <= max_intensity; i++)
                sum += i*histogram[i];


            for (t = 0; t <= max_intensity; t++)
            {
                q1 += histogram[t];
                
                if (q1==0.0) continue;
                q2 = N - q1;
                if (q2 == 0) continue;

                sumB += t * histogram[t];
                µ1 = sumB / q1;
                µ2 = (sum - sumB) / q2;

                Sigmb = q1 * q2 * (µ1 - µ2) * (µ1 - µ2);
                if (Sigmb > var_max) { threshold = t; var_max = Sigmb; }
            }
            return threshold;

        }

        private int Otsu_algorithm2(ushort[] input_image, int imageWidth, int imageHeight, int bitDepth)
        {
            int N = imageWidth * imageHeight;
            double var_max = 0, sum = 0, sumB = 0, q1 = 0, q2 = 0, µ1 = 0, µ2 = 0;
            int max_intensity = (int) Math.Pow(2,bitDepth)-1;
            int[] histogram = new int[(int)Math.Pow(2, bitDepth)];
            int i, t;
            double Sigmb;
            int value, threshold = 0;
            int max_value = -100000;


            for (i = 0; i <= max_intensity; i++)
            {
                histogram[i] = 0;
            }

            for (i = 0; i < N; i++)
            {
                value = input_image[i];
                //if (value == 0) continue;
                if (value > max_value) max_value = value;
                histogram[value] += 1;
            }

            for (i = 0; i <= max_intensity; i++)
//                for (i = 0; i <= max_value; i++)
                    sum += i*histogram[i];


            for (t = 0; t <= max_intensity; t++)
//                for (t = 0; t <= max_value; t++)
                {
                q1 += histogram[t];
                if (q1 == 0.0) continue;
                q2 = N - q1;
                if (q2 == 0) continue;

                sumB += t * histogram[t];
                µ1 = sumB / q1;
                µ2 = (sum - sumB) / q2;

                Sigmb = q1 * q2 * (µ1 - µ2) * (µ1 - µ2);
                if (Sigmb > var_max) { threshold = t; var_max = Sigmb; }
            }
            return threshold;

        }

        private void bnOpen_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "All DICOM Files(*.*)|*.*";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                if (ofd.FileName.Length > 0)
                {
                    Cursor = Cursors.WaitCursor;
                    ReadAndDisplayDicomFile(ofd.FileName, ofd.SafeFileName);
                    imageOpened = true;
                    Cursor = Cursors.Default;
                }
                ofd.Dispose();
            }
        }

        private void ReadAndDisplayDicomFile(string fileName, string fileNameOnly)
        {
            dd.DicomFileName = fileName;
            
            byte[] pixel88 = new byte[dd.width * dd.height];
            ushort[] pixel16 = new ushort[dd.width * dd.height];
            TypeOfDicomFile typeOfDicomFile = dd.typeofDicomFile;

            if (typeOfDicomFile == TypeOfDicomFile.Dicom3File ||
                typeOfDicomFile == TypeOfDicomFile.DicomOldTypeFile)
            {
                imageWidth = dd.width;
                imageHeight = dd.height;
                bitDepth = dd.bitsAllocated;
                winCentre = dd.windowCentre;
                winWidth = dd.windowWidth;
                samplesPerPixel = dd.samplesPerPixel;
                signedImage = dd.signedImage;

                label1.Visible = true;
                label2.Visible = true;
                label3.Visible = true;
                label4.Visible = true;
                bnSave.Enabled = true;
                bnTags.Enabled = true;
                bnResetWL.Enabled = true;
                label2.Text = imageWidth.ToString() + " X " + imageHeight.ToString();
                if (samplesPerPixel == 1)
                    label4.Text = bitDepth.ToString() + " bit";
                else
                    label4.Text = bitDepth.ToString() + " bit, " + samplesPerPixel +
                        " samples per pixel";

                imagePanelControl.NewImage = true;
                Text = "DICOM Image Viewer: " + fileNameOnly;

                if (samplesPerPixel == 1 && bitDepth == 8)
                {
                    pixels8.Clear();
                    pixels16.Clear();
                    pixels24.Clear();
                    dd.GetPixels8(ref pixels8);

                    // This is primarily for debugging purposes, 
                    //  to view the pixel values as ascii data.
                 /*   if (true)
                    {
                        System.IO.StreamWriter file = new System.IO.StreamWriter(
                                   "D:\\imageSigned.txt");

                        for (int ik = 0; ik < pixels8.Count; ++ik)
                        {
                            file.Write(pixels8[ik] + "  ");
                            //if (pixels8[ik] > 125) pixels8[ik]=255;
                            //else pixels8[ik] = 0;
                            pixel88[ik] = pixels8[ik];
                        }
                        int threshold=this.Otsu_algorithm (pixel88, imageWidth, imageHeight, bitDepth);
                        for (int ik = 0; ik < pixels8.Count; ++ik)
                        {
                           // threshold = 255 / 2-20;
                          //  if (pixels8[ik] > threshold) pixels8[ik]=255;
                           // else pixels8[ik] = 0;
                        }    

                        file.Close();
                    }
                    */
                    minPixelValue = pixels8.Min();
                    maxPixelValue = pixels8.Max();

                    // Bug fix dated 24 Aug 2013 - for proper window/level of signed images
                    // Thanks to Matias Montroull from Argentina for pointing this out.
                    if (dd.signedImage)
                    {
                        winCentre -= char.MinValue;
                    }

                    if (Math.Abs(winWidth) < 0.001)
                    {
                        winWidth = maxPixelValue - minPixelValue;
                    }

                    if ((winCentre == 0) ||
                        (minPixelValue > winCentre) || (maxPixelValue < winCentre))
                    {
                        winCentre = (maxPixelValue + minPixelValue) / 2;
                    }

                    histogramGraphControl.SetParametersHistogram8(ref pixels8, imageWidth, imageHeight, bitDepth);
                    imagePanelControl.SetParameters(ref pixels8, imageWidth, imageHeight,
                        winWidth, winCentre, samplesPerPixel, true, this);
                }

                if (samplesPerPixel == 1 && bitDepth == 16)
                {
                    pixels16.Clear();
                    pixels8.Clear();
                    pixels24.Clear();
                    dd.GetPixels16(ref pixels16);

                   // System.IO.StreamWriter file = new System.IO.StreamWriter("D:\\imageSigned.txt");

                       /* for (int ik = 0; ik < pixels16.Count; ++ik)
                        {
                      //      file.Write(pixels16[ik] + "\n  ");
                            pixel16[ik] = pixels16[ik];
                        }
                      
                    int threshold=this.Otsu_algorithm2 (pixel16, imageWidth, imageHeight, bitDepth);
                     //   System.IO.StreamWriter file2 = new System.IO.StreamWriter(
                       //                                  "D:\\Threshold.txt");
                        for (int ik = 0; ik < pixels16.Count; ++ik)
                        {
                    //        file2.Write(threshold + "\n  ");
                            
                        }
                    threshold = threshold/4;
                      
                    for (int ik = 0; ik < pixels16.Count; ++ik)
                        {
                            //threshold = 200;
                          //  if (pixels16[ik] > threshold) pixels16[ik]=65535;
                          //  else pixels16[ik] = 0;
                        }    

                    */
                    

                    /*
                    // This is primarily for debugging purposes, 
                    //  to view the pixel values as ascii data.
                    //if (true)
                    //{
                        System.IO.StreamWriter file = new System.IO.StreamWriter(
                                   "C:\\imageSigned.txt");

                        for (int ik = 0; ik < pixels16.Count; ++ik)
                            file.Write(pixels16[ik] + "  ");

                        file.Close();
                    //}
                    */
                    minPixelValue = pixels16.Min();
                    maxPixelValue = pixels16.Max();

                    // Bug fix dated 24 Aug 2013 - for proper window/level of signed images
                    // Thanks to Matias Montroull from Argentina for pointing this out.
                    if (dd.signedImage)
                    {
                        winCentre -= short.MinValue;
                    }

                    if (Math.Abs(winWidth) < 0.001)
                    {
                        winWidth = maxPixelValue - minPixelValue;
                    }

                    if ((winCentre == 0) ||
                        (minPixelValue > winCentre) || (maxPixelValue < winCentre))
                    {
                        winCentre = (maxPixelValue + minPixelValue) / 2;
                    }

                    imagePanelControl.Signed16Image = dd.signedImage;

                    histogramGraphControl.SetParametersHistogram16(ref pixels16, imageWidth, imageHeight, bitDepth);
                    imagePanelControl.SetParameters(ref pixels16, imageWidth, imageHeight,
                        winWidth, winCentre, true, this);
                }

                if (samplesPerPixel == 3 && bitDepth == 8)
                {
                    // This is an RGB colour image
                    pixels8.Clear();
                    pixels16.Clear();
                    pixels24.Clear();
                    dd.GetPixels24(ref pixels24);

                    // This code segment is primarily for debugging purposes, 
                    //    to view the pixel values as ascii data.
                    //if (true)
                    //{
                    //    System.IO.StreamWriter file = new System.IO.StreamWriter(
                    //                      "C:\\image24.txt");

                    //    for (int ik = 0; ik < pixels24.Count; ++ik)
                    //        file.Write(pixels24[ik] + "  ");

                    //    file.Close();
                    //}

                    imagePanelControl.SetParameters(ref pixels24, imageWidth, imageHeight,
                        winWidth, winCentre, samplesPerPixel, true, this);
                }
            }
            else 
            {
                if (typeOfDicomFile == TypeOfDicomFile.DicomUnknownTransferSyntax)
                {
                    MessageBox.Show("Sorry, I can't read a DICOM file with this Transfer Syntax.",
                        "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    MessageBox.Show("Sorry, I can't open this file. " + 
                        "This file does not appear to contain a DICOM image.",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                Text = "DICOM Image Viewer: ";
                // Show a plain grayscale image instead
                pixels8.Clear();
                pixels16.Clear();
                pixels24.Clear();
                samplesPerPixel = 1;

                imageWidth = imagePanelControl.Width - 25;   // 25 is a magic number
                imageHeight = imagePanelControl.Height - 25; // Same magic number
                int iNoPix = imageWidth * imageHeight;

                for (int i = 0; i < iNoPix; ++i)
                {
                    pixels8.Add(240);// 240 is the grayvalue corresponding to the Control colour
                }
                winWidth = 256;
                winCentre = 127;
                imagePanelControl.SetParameters(ref pixels8, imageWidth, imageHeight,
                    winWidth, winCentre, samplesPerPixel, true, this);
                imagePanelControl.Invalidate();
                label1.Visible = false;
                label2.Visible = false;
                label3.Visible = false;
                label4.Visible = false;
                bnSave.Enabled = false;
                bnTags.Enabled = false;
                bnResetWL.Enabled = false;
            }
        }

        private void bnTags_Click(object sender, EventArgs e)
        {
            if (imageOpened == true)
            {
                List<string> str = dd.dicomInfo;

                DicomTagsForm dtg = new DicomTagsForm();
                dtg.SetString(ref str);
                dtg.ShowDialog();

                imagePanelControl.Invalidate();
            }
            else
                MessageBox.Show("Load a DICOM file before viewing tags!", "Information", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void bnSave_Click(object sender, EventArgs e)
        {
            if (imageOpened == true)
            {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.Filter = "PNG Files(*.png)|*.png";

                if (sfd.ShowDialog() == DialogResult.OK)
                    imagePanelControl.SaveImage(sfd.FileName);
            }
            else
                MessageBox.Show("Load a DICOM file before saving!", "Information", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

            imagePanelControl.Invalidate();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            label1.Visible = false;
            label2.Visible = false;
            label3.Visible = false;
            label4.Visible = false;
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            pixels8.Clear();
            pixels16.Clear();
            if (imagePanelControl != null) imagePanelControl.Dispose();
        }

        private void bnResetWL_Click(object sender, EventArgs e)
        {
            if ((pixels8.Count > 0) || (pixels16.Count > 0) || (pixels24.Count > 0))
            {
                imagePanelControl.ResetValues();
                if (bitDepth == 8)
                {
                    if (samplesPerPixel == 1)
                        imagePanelControl.SetParameters(ref pixels8, imageWidth, imageHeight,
                            winWidth, winCentre, samplesPerPixel, false, this);
                    else // samplesPerPixel == 3
                        imagePanelControl.SetParameters(ref pixels24, imageWidth, imageHeight,
                        winWidth, winCentre, samplesPerPixel, false, this);
                }

                if (bitDepth == 16)
                    imagePanelControl.SetParameters(ref pixels16, imageWidth, imageHeight,
                        winWidth, winCentre, false, this);
            }
            else
                MessageBox.Show("Load a DICOM file before resetting!", "Information", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public void UpdateWindowLevel(int winWidth, int winCentre, ImageBitsPerPixel bpp)
        {
            int winMin = Convert.ToInt32(winCentre - 0.5 * winWidth);
            int winMax = winMin + winWidth;
            this.windowLevelControl.SetWindowWidthCentre(winMin, winMax, winWidth, winCentre, bpp, signedImage);
        }

        public void UpdateHistogram(int winWidth, int winCentre, ImageBitsPerPixel bpp)
        {
            int winMin = Convert.ToInt32(winCentre - 0.5 * winWidth);
            int winMax = winMin + winWidth;
            this.histogramGraphControl.SetWindowWidthCentre(winMin, winMax, winWidth, winCentre, bpp, signedImage);
        }

        private void viewSettingsCheckedChanged(object sender, EventArgs e)
        {
            if (rbZoom1_1.Checked)
            {
                imagePanelControl.viewSettings = ViewSettings.Zoom1_1;

                if (bitDepth == 8)
                {
                    pixels8.Clear();
                    pixels8.AddRange(pixels88);
                }
                if (bitDepth == 16)
                {
                    pixels16.Clear();
                    pixels16.AddRange(pixels1616);
                }

            }
            else
            {
                imagePanelControl.viewSettings = ViewSettings.ZoomToFit;
            }

            imagePanelControl.viewSettingsChanged = true;
            imagePanelControl.Invalidate();
        }

        private void HistogramControl_Load(object sender, EventArgs e)
        {

        }

        private void groupBox1_Enter(object sender, EventArgs e)
        {
            byte[] pixel88 = new byte[dd.width * dd.height];
            ushort[] pixel16 = new ushort[dd.width * dd.height];
           
            
            if (radioButton1.Checked)
            {
 //               imagePanelControl.viewSettings = ViewSettings.Zoom1_1;

                if (bitDepth == 8)
                {
                    pixels88.Clear();
                    pixels88.AddRange(pixels8);
                    for (int ik = 0; ik < pixels8.Count; ++ik)
                    {
                        pixel88[ik] = pixels8[ik];
                    }
                    int threshold = this.Otsu_algorithm(pixel88, imageWidth, imageHeight, bitDepth);
                  //  threshold = threshold / 4;
                    for (int ik = 0; ik < pixels8.Count; ++ik)
                    {
                        // threshold = 255 / 2-20;
                        if (pixels8[ik] > threshold) pixels8[ik] = 255;
                        else pixels8[ik] = 0;
                    }
                }

                if (bitDepth == 16)
                {
                    pixels1616.Clear();
                    pixels1616.AddRange(pixels16);
                    for (int ik = 0; ik < pixels16.Count; ++ik)
                    {
                        pixel16[ik] = pixels16[ik];
                    }
                    int threshold = this.Otsu_algorithm2(pixel16, imageWidth, imageHeight, bitDepth);
                 //   threshold = threshold / 4;
                    for (int ik = 0; ik < pixels16.Count; ++ik)
                    {
                        
                        if (pixels16[ik] > threshold) pixels16[ik] = 65535;
                        else pixels16[ik] = 0;
                    }
                }

            }
            else //(!radioButton2.Checked)
            {
               // imagePanelControl.viewSettings = ViewSettings.ZoomToFit;
/*                for (int ik = 0; ik < pixels8.Count; ++ik)
                {
                    pixels8[ik] = pixel88[ik];
                }*/
            }

            imagePanelControl.viewSettingsChanged = true;
            imagePanelControl.Invalidate();

        }
    }
}