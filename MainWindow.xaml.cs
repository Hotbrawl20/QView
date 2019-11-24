using libusbK;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace QView
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private UsbK USB = null;
        private Thread USBThread;
        public const long RawRGBAScreenBufferSize = 1280 * 720 * 4;
        MainWindow n;
        bool disconnected = false;
        public byte[][] CaptureBlocks = new byte[][]
        {
            new byte[RawRGBAScreenBufferSize],
        };
        public MainWindow()
        {
            InitializeComponent();
            InitializeCaptureBox(iCapture);
            n = this;
        }

        private void USBThreadMain()
        {
            while (RefreshCapture());
        }
        public delegate void ApplyDelegate(byte[] data);
        private static Action EmptyDelegate = delegate () { };


        public void Refresh()
        {
            n.Dispatcher.Invoke(DispatcherPriority.Render, EmptyDelegate);
        }
        public void ApplyRGBAInternal(byte[] data)
        {

            if (iCapture.Dispatcher.CheckAccess())
            {
                var d = new ApplyDelegate(ApplyRGBAInternal);
                iCapture.Source.Dispatcher.Invoke(d, data);
                
            }
            else
            {
                ApplyRGBAToPictureBox(iCapture, data);
                Refresh();
            }
        }

        private unsafe void ApplyRGBAToPictureBox(System.Windows.Controls.Image iCapture, byte[] data)
        {
            try
            {
                var capture = iCapture.Dispatcher.Invoke(() => iCapture.Source);
                var bmpS = capture as BitmapSource;
                Bitmap bmp = new Bitmap(
                                    bmpS.Dispatcher.Invoke(() => bmpS.PixelWidth),
                                    bmpS.Dispatcher.Invoke(() => bmpS.PixelHeight),
                                    System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                BitmapData img = bmp.LockBits(new System.Drawing.Rectangle(0, 0, 1280, 720), ImageLockMode.ReadWrite, bmp.PixelFormat);

                fixed (byte* rawData = data) unchecked
                    {
                        uint* ptr = (uint*)rawData;
                        uint* image = (uint*)img.Scan0.ToPointer();
                        uint* ptrEnd = ptr + 720 * 1280;
                        while (ptr != ptrEnd)
                        {
                            uint argb = *ptr << 8;
                            *image = ((argb & 0x0000FF00) << 8) | ((argb & 0x00FF0000) >> 8) | ((argb & 0xFF000000) >> 24) | 0xFF000000;
                            image++; ptr++;
                        }

                    }
                bmpS.Dispatcher.Invoke(() => bmp.UnlockBits(img));

                bmpS.Dispatcher.Invoke(() => iCapture.Source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                                bmp.GetHbitmap(),
                                IntPtr.Zero,
                                System.Windows.Int32Rect.Empty,
                                BitmapSizeOptions.FromWidthAndHeight(bmp.Width, bmp.Height)
                             ));
            }
            catch (Exception)
            {
                disconnected = true;
            }
           
        }

        private bool RefreshCapture()
        {

                try
                {

                    int aaaaa;
                    USB.ReadPipe(0x81, CaptureBlocks[0], CaptureBlocks[0].Length, out aaaaa, IntPtr.Zero);
                    ApplyRGBAInternal(CaptureBlocks[0]);
                
                
                }
                catch (Exception e)
                {
                    return false;
                }
            if (disconnected)
            {
                return false;
            }
            else
            {
                return true;

            }

        }

            private void InitializeCaptureBox(System.Windows.Controls.Image Box)
        {
            int w = 1280;
            int h = 720;
            Box.Source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                            new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb).GetHbitmap(),
                            IntPtr.Zero,
                            System.Windows.Int32Rect.Empty,
                            BitmapSizeOptions.FromWidthAndHeight(w, h)
                         );
            
        }

        private void connect(bool re)
        {
            try
            {
                var pat = new KLST_PATTERN_MATCH { DeviceID = @"USB\VID_057E&PID_3000" };
                var lst = new LstK(0, ref pat);
                lst.MoveNext(out var dinfo);
                this.USB = new UsbK(dinfo);
            }
            catch (Exception)
            {
                this.USB = null;
            }

            if (this.USB == null)
            {
                
                MessageBox.Show("No Switch running uLaunch detected!");
                disconnect();
            }
            else
            {
                if (!re)
                {
                    MessageBox.Show("Switch running uLaunch detected, continuing!");
                }
                miConn.IsEnabled = false;
                miReConn.IsEnabled = true;
                miDisConn.IsEnabled = true;
                USBThread = new Thread(new ThreadStart(USBThreadMain));
                USBThread.Start();
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
           
        }

        private void miConn_Click(object sender, RoutedEventArgs e)
        {
            disconnected = false;
            connect(false);

           
        }

        private void miReConn_Click(object sender, RoutedEventArgs e)
        {
            disconnect();
            connect(true);

        }

        private void miDisConn_Click(object sender, RoutedEventArgs e)
        {
            disconnect();
           

        }
        private void disconnect()
        {
            if (USBThread != null)
            {
                USBThread.Abort();
            }

            disconnected = false;
            this.USB = null;
            miConn.IsEnabled = true;
            miDisConn.IsEnabled = false;
            miReConn.IsEnabled = false;
            InitializeCaptureBox(iCapture);
        }
    }
}
