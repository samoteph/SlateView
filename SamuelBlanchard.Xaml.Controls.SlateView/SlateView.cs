using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;


// The Templated Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234235

namespace SamuelBlanchard.Xaml.Controls.BlurPixelView
{
    public sealed class SlateView : Control
    {
        private CanvasAnimatedControl canvas;
        private CanvasBitmap canvasBitmap;

        public SlateView()
        {
            this.DefaultStyleKey = typeof(SlateView);
        }

        /// <summary>
        /// Au format BGRA8
        /// </summary>

        public Size PixelSize
        {
            get;
            private set;
        } = new Size(0, 0);

        public int PixelWidth
        {
            get
            {
                return (int)this.PixelSize.Width;
            }
        }

        public int PixelHeight
        {
            get
            {
                return (int)this.PixelSize.Height;
            }
        }

        public byte[] Pixels
        {
            get;
            private set;
        } = null;

        private bool isScreenDirty = false;

        public CanvasImageInterpolation ImageInterpolation
        {
            get;
            set;
        } = CanvasImageInterpolation.NearestNeighbor;

        public Thickness ImageMargin
        {
            get;
            set;
        } = new Thickness(0);

        public ShowElements ElementShown
        {
            get;
            set;
        } = ShowElements.ImageAndBackground;

        /// <summary>
        /// Set a pixel
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="r"></param>
        /// <param name="g"></param>
        /// <param name="b"></param>
        /// <param name="a"></param>

        public bool SetPixel(int x, int y, byte r, byte g, byte b, byte a = 0xFF)
        {
            var pixels = this.Pixels;
            var h = this.PixelHeight;
            var w = this.PixelWidth;

            if(pixels == null)
            {
                return false;
            }

            if(x >= w || x < 0)
            {
                return false;
            }

            if (y >= h || y < 0)
            {
                return false;
            }

            var p = ((h * y) + x) * 4;

            // bgra format
            pixels[p + 0] = b;
            pixels[p + 1] = g;
            pixels[p + 2] = r;
            pixels[p + 3] = a;

            this.InvalidatePixels();

            return true;
        }

        /// <summary>
        /// Get a pixel
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="r"></param>
        /// <param name="g"></param>
        /// <param name="b"></param>
        /// <param name="a"></param>
        /// <returns></returns>

        public bool GetPixel(int x, int y, out byte r, out byte g, out byte b, out byte a)
        {
            var pixels = this.Pixels;
            var h = this.PixelHeight;
            var w = this.PixelWidth;

            if (pixels == null || (x >= w || x < 0) || (y >= h || y < 0))
            {
                r = 0;
                g = 0;
                b = 0;
                a = 0;
                return false;
            }

            var p = ((h * y) + x) * 4;

            // bgra format
            b = pixels[p + 0];
            g = pixels[p + 1];
            r = pixels[p + 2];
            a = pixels[p + 3];

            return true;
        }

        public void ClearPixels(byte r = 0x00, byte g = 0x00, byte b = 0x00, byte a = 0xFF)
        {
            var count = this.Pixels.Length / 4;

            int p = 0;
            for (int x = 0; x < count; x++)
            {
                this.Pixels[p + 0] = b; // B
                this.Pixels[p + 1] = g; // G
                this.Pixels[p + 2] = r; // R
                this.Pixels[p + 3] = a; // A
                p += 4;
            }
        }

        public void InvalidatePixels()
        {
            //Debug.WriteLine("Invalidate");
            this.isScreenDirty = true;
        }

        public double BlurEffectAmount
        {
            get;
            set;
        } = 5;

        public EffectOptimization BlurEffectOptimization
        {
            get;
            set;
        } = EffectOptimization.Speed;

        public EffectBorderMode BlurEffectBorderMode
        {
            get;
            set;
        } = EffectBorderMode.Hard;

        public bool AllowBlur
        {
            get;
            set;
        } = true;

        public double BackgroundImageOpacity
        {
            get;
            set;
        } = 0.25;

        /// <summary>
        /// Effect Blur
        /// </summary>

        GaussianBlurEffect blurEffect = null;

        private void Canvas_CreateResources(CanvasAnimatedControl sender, Microsoft.Graphics.Canvas.UI.CanvasCreateResourcesEventArgs args)
        {
            if(blurEffect != null)
            {
                blurEffect.Dispose();
            }

            blurEffect = new GaussianBlurEffect();

            this.CreateResources?.Invoke(sender, args);
        }

        private void Canvas_Unloaded(object sender, RoutedEventArgs e)
        {
            if (blurEffect != null)
            {
                blurEffect.Dispose();
            }
        }

        /// <summary>
        /// Source of the image (for Binding use)
        /// </summary>

        public Uri SourceUri
        {
            get { return (Uri)GetValue(SourceUriProperty); }
            set { SetValue(SourceUriProperty, value); }
        }

        // Using a DependencyProperty as the backing store for SourceUri.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SourceUriProperty =
            DependencyProperty.Register("SourceUri", typeof(Uri), typeof(SlateView), new PropertyMetadata(null, OnSourceUriChange));

        private static async void OnSourceUriChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var me = d as SlateView;
            await me.LoadImage((Uri)e.NewValue);
        }

        protected override void OnApplyTemplate()
        {
            if (this.canvas == null)
            {
                this.canvas = this.GetTemplateChild("CanvasControl") as CanvasAnimatedControl;

                if (this.canvas != null)
                {
                    this.canvas.Draw += Canvas_Draw;

                    this.canvas.CreateResources += Canvas_CreateResources;

                    this.canvas.Unloaded += Canvas_Unloaded;
                    this.canvas.Update += this.Update;
                }
            }

            base.OnApplyTemplate();
        }

        //
        // Summary:
        //     Hook this event to create any resources needed for your drawing.
        public event TypedEventHandler<CanvasAnimatedControl, CanvasCreateResourcesEventArgs> CreateResources;
        //
        // Summary:
        //     Hook this event to draw the contents of the control.
        public event TypedEventHandler<ICanvasAnimatedControl, CanvasAnimatedDrawEventArgs> DrawStart;
        public event TypedEventHandler<ICanvasAnimatedControl, CanvasAnimatedDrawEventArgs> DrawBackground;
        public event TypedEventHandler<ICanvasAnimatedControl, CanvasAnimatedDrawEventArgs> DrawForeground;
        public event TypedEventHandler<ICanvasAnimatedControl, CanvasAnimatedDrawEventArgs> DrawStop;

        // Summary:
        //     Hook this event to update any data, as necessary, for your app's animation.
        public event TypedEventHandler<ICanvasAnimatedControl, CanvasAnimatedUpdateEventArgs> Update;

        private void Canvas_Draw(ICanvasAnimatedControl sender, CanvasAnimatedDrawEventArgs args)
        {
            this.DrawStart?.Invoke(sender, args);

            var bmp = this.canvasBitmap;

            bool isScreenDirty = this.isScreenDirty;

            var pixels = this.Pixels;
            var pixelWidth = this.PixelWidth;
            var pixelHeight = this.PixelHeight;
            var interpolation = this.ImageInterpolation;

            if(pixels == null || pixelWidth == 0 || pixelHeight == 0)
            {
                return;
            }

            if (isScreenDirty == true)
            {
                if (bmp == null || bmp.SizeInPixels.Width != pixelWidth || bmp.SizeInPixels.Height != pixelHeight)
                {
                    bmp = CanvasBitmap.CreateFromBytes(sender, pixels, pixelWidth, pixelHeight, Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized);
                    this.canvasBitmap = bmp;
                }

                bmp.SetPixelBytes(pixels);

                this.isScreenDirty = false;
            }

            var elementShown = this.ElementShown;

            // possible if IsDirty is fixed in the middle of the call of Draw method
            if (bmp != null)
            {
                // Draw Background
                if (elementShown == ShowElements.Background || elementShown == ShowElements.ImageAndBackground)
                {
                    var rectBack = GetBackRect(sender.Size, pixelWidth, pixelHeight);

                    ICanvasImage image = bmp;

                    if (AllowBlur)
                    {
                        // Set image to blur.
                        blurEffect.Source = bmp;
                        // Set blur amount from slider control.
                        blurEffect.BlurAmount = (float)this.BlurEffectAmount;
                        // Explicitly set optimization mode to highest quality, since we are using big blur amount values.
                        blurEffect.Optimization = this.BlurEffectOptimization;
                        // This prevents the blur effect from wrapping around.
                        blurEffect.BorderMode = this.BlurEffectBorderMode;
                        // Draw blurred image on top of the unaltered one. It will be masked by the radial gradient
                        // thus showing a transparent hole in the middle, and properly overlaying the alpha values.
                        image = blurEffect;
                    }

                    args.DrawingSession.DrawImage(image, new Rect(0, 0, sender.Size.Width, sender.Size.Height), rectBack, (float)BackgroundImageOpacity, interpolation);

                    this.DrawBackground?.Invoke(sender, args);
                }

                // Draw Image
                if (elementShown == ShowElements.Image || elementShown == ShowElements.ImageAndBackground)
                {
                    var rectFront = GetFrontRect(sender.Size, this.ImageMargin, pixelWidth, pixelHeight);
                    args.DrawingSession.DrawImage(bmp, rectFront, new Rect(0, 0, pixelWidth, pixelHeight), 1f, interpolation);

                    this.DrawForeground?.Invoke(sender, args);
                }
            }

            this.DrawStop?.Invoke(sender, args);
        }

        Rect GetFrontRect(Size size, Thickness margin, int pixelWidth, int pixelHeight)
        {
            var viewSize = new Size(size.Width - (margin.Left + margin.Right), size.Height - (margin.Top + margin.Bottom));

            double width;
            double height;

            if (viewSize.Width < viewSize.Height)
            {
                width = viewSize.Width;
                height = (width * pixelHeight) / pixelWidth;
                
                if(height > viewSize.Height)
                {               
                    height = viewSize.Height;
                    width = (height * pixelWidth) / pixelHeight;
                }
            }
            else
            {
                height = viewSize.Height;
                width = (height * pixelWidth) / pixelHeight;

                if (width > viewSize.Width)
                {
                    width = viewSize.Width;
                    height = (width * pixelHeight) / pixelWidth;
                }
            }

            double x = ((viewSize.Width - width) / 2) + margin.Left;
            double y = ((viewSize.Height - height) / 2) + margin.Top;

            return new Rect(x, y, width, height);
        }

        Rect GetBackRect(Size viewSize, int pixelWidth, int pixelHeight)
        {
            double width;
            double height;

            if (viewSize.Width > viewSize.Height)
            {
                width = pixelWidth;
                height = (width * viewSize.Height) / viewSize.Width;

                if(height > pixelHeight)
                {
                    height = pixelHeight;
                    width = (height * viewSize.Width) / viewSize.Height;
                }
            }
            else
            {
                height = pixelHeight;
                width = (height * viewSize.Width) / viewSize.Height;

                if(width > pixelWidth)
                {
                    width = pixelWidth;
                    height = (width * viewSize.Height) / viewSize.Width;
                }
            }

            double x = (pixelWidth - width) / 2;
            double y = (pixelHeight - height) / 2;

            return new Rect(x, y, width, height);
        }

        /// <summary>
        /// Chargmement de l'image
        /// </summary>
        /// <param name="uriImage"></param>
        /// <returns></returns>

        public async Task<bool> LoadImage(Uri uriImage)
        {
            if(uriImage == null)
            {
                return false;
            }

            try
            {
                var file = await StorageFile.GetFileFromApplicationUriAsync(uriImage);

                using (var stream = await file.OpenReadAsync())
                {
                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                    var bmp = await decoder.GetSoftwareBitmapAsync();

                    this.SetPixels(bmp);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public void SetPixels(byte[] pixels, Size size)
        {
            this.PixelSize = size;
            this.Pixels = pixels;

            this.InvalidatePixels();
        }

        public void SetPixels(byte[] pixels, int pixelWidth, int pixelHeight)
        {
            this.PixelSize = new Size(pixelWidth, pixelHeight);
            this.Pixels = pixels;

            this.InvalidatePixels();
        }

        public void SetPixels(WriteableBitmap bitmap)
        {
            var pixels = bitmap.PixelBuffer.ToArray();
            var w = bitmap.PixelWidth;
            var h = bitmap.PixelHeight;

            this.SetPixels(pixels, w, h);
        }

        public void SetPixels(VideoFrame frame)
        {
            this.SetPixels(frame.SoftwareBitmap);
        }

        public void SetPixels(SoftwareBitmap bitmap)
        {
            if (bitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 && bitmap.BitmapPixelFormat == BitmapPixelFormat.Rgba8)
            {
                throw new Exception("Unsupported file format (BGRA8 is supported) ");
            }

            byte[] imageBytes = new byte[4 * bitmap.PixelWidth * bitmap.PixelHeight];
            bitmap.CopyToBuffer(imageBytes.AsBuffer());

            this.SetPixels(imageBytes, bitmap.PixelWidth, bitmap.PixelHeight);
        }

        public WriteableBitmap GetWriteableBitmap()
        {
            var pixels = this.Pixels;
            var w = this.PixelHeight;
            var h = this.PixelWidth;

            var bmp = new WriteableBitmap(w, h);
            bmp.PixelBuffer.AsStream().Write(pixels, 0, pixels.Length);

            return bmp;
        }

        public SoftwareBitmap GetSoftwareBitmap()
        {
            var pixels = this.Pixels;
            var w = this.PixelWidth;
            var h = this.PixelHeight;

            var bmp = new SoftwareBitmap(BitmapPixelFormat.Bgra8, w, h, BitmapAlphaMode.Premultiplied);
            bmp.CopyFromBuffer(pixels.AsBuffer());

            return bmp;
        }

        public async Task<SoftwareBitmapSource> GetSoftwareBitmapSourceAsync()
        {
            var source = new SoftwareBitmapSource();
            await source.SetBitmapAsync(this.GetSoftwareBitmap());

            return source;
        }

        public void CreatePixels(int pixelWidth, int pixelHeight)
        {
            this.PixelSize = new Size(pixelWidth, pixelHeight);
            this.Pixels = new byte[(int)pixelWidth * (int)pixelHeight * 4];
            this.ClearPixels();
            this.InvalidatePixels();
        }
    }

    public enum ShowElements
    {
        None,
        ImageAndBackground,
        Image,
        Background,
    }
}
