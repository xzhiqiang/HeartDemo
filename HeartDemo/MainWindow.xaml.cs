using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using Device = SharpDX.Direct3D11.Device;
using Factory = SharpDX.DXGI.Factory1;
using PixelFormat = SharpDX.Direct2D1.PixelFormat;
using SolidColorBrush = SharpDX.Direct2D1.SolidColorBrush;
using AlphaMode = SharpDX.Direct2D1.AlphaMode;
using SharpDX.Direct3D11;
using System.Windows.Input;
namespace HeartDemo
{
    public partial class MainWindow : Window
    {
        private const int CanvasWidth = 640;
        private const int CanvasHeight = 480;
        private const double CanvasCenterX = CanvasWidth / 2.0;
        private const double CanvasCenterY = CanvasHeight / 2.0;
        private const double ImageEnlarge = 11;

        private Device device;
        private SwapChain swapChain;
        private RenderTarget renderTarget;
        private SolidColorBrush brush;
        private DispatcherTimer timer;
        private Heart heart;
        private List<Particle> particles = new List<Particle>();
        private Random random = new Random();
        private int renderFrame = 0;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeDirectX();
            InitializeTimer();
            heart = new Heart();
        }

        private void InitializeDirectX()
        {
            var width = (int)DxImage.ActualWidth;
            var height = (int)DxImage.ActualHeight;

            var swapChainDescription = new SwapChainDescription
            {
                BufferCount = 2, // 使用双缓冲避免闪烁
                ModeDescription = new ModeDescription(width, height, new Rational(60, 1), Format.B8G8R8A8_UNorm),
                Usage = Usage.RenderTargetOutput,
                OutputHandle = new WindowInteropHelper(this).Handle,
                SampleDescription = new SampleDescription(1, 0),
                IsWindowed = true
            };

            device = new Device(SharpDX.Direct3D.DriverType.Hardware, DeviceCreationFlags.BgraSupport);
            var factory = new Factory();
            swapChain = new SwapChain(factory, device, swapChainDescription);

            using (var backBuffer = swapChain.GetBackBuffer<Surface>(0))
            {
                renderTarget = new RenderTarget(
                    new SharpDX.Direct2D1.Factory(),
                    backBuffer,
                    new RenderTargetProperties(new PixelFormat(Format.Unknown, AlphaMode.Premultiplied)));
            }

            brush = new SolidColorBrush(renderTarget, new RawColor4(1.0f, 0.75f, 0.8f, 1.0f)); // 粉色
        }

        private void InitializeTimer()
        {
            timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            timer.Tick += OnRendering;
            timer.Start();
        }

        private void OnRendering(object sender, EventArgs e)
        {
            renderTarget.BeginDraw();
            renderTarget.Clear(new RawColor4(0, 0, 0, 1));
            heart.Render(renderTarget, brush, renderFrame);
            RenderParticles(renderTarget, brush);
            renderTarget.EndDraw();
            swapChain.Present(1, PresentFlags.None);
            renderFrame++;
            UpdateParticles();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            brush.Dispose();
            renderTarget.Dispose();
            swapChain.Dispose();
            device.Dispose();
            timer.Stop();
        }

        private void UpdateParticles()
        {
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var particle = particles[i];
                particle.Update();
                if (particle.Life <= 0)
                {
                    particles.RemoveAt(i);
                }
            }

            if (renderFrame % 60 == 0)
            {
                GenerateFireworks();
            }
        }

        private void RenderParticles(RenderTarget renderTarget, SolidColorBrush brush)
        {
            foreach (var particle in particles)
            {
                brush.Color = new RawColor4((float)particle.Color.R, (float)particle.Color.G, (float)particle.Color.B, 1.0f);
                renderTarget.FillEllipse(new Ellipse(new RawVector2((float)particle.Position.X, (float)particle.Position.Y), (float)particle.Size, (float)particle.Size), brush);
            }
        }

        private void GenerateFireworks()
        {
            var points = heart.GetCurrentFramePoints(renderFrame);
            foreach (var (x, y, size, color) in points)
            {
                double angle = random.NextDouble() * 2 * Math.PI;
                double speed = random.NextDouble() * 2 + 1;
                var velocity = new Vector2((float)(speed * Math.Cos(angle)), (float)(speed * Math.Sin(angle)));
                particles.Add(new Particle(x, y, velocity, color, size));
            }
        }

        private class Heart
        {
            private readonly Random random = new Random();
            private readonly int generateFrame = 20;
            private readonly (double, double, int, (double R, double G, double B))[][] allPoints;

            public Heart()
            {
                allPoints = new (double, double, int, (double R, double G, double B))[generateFrame][];
                for (int frame = 0; frame < generateFrame; frame++)
                {
                    allPoints[frame] = Calc(frame);
                }
            }

            private (double, double, int, (double R, double G, double B))[] Calc(int generateFrame)
            {
                double ratio = 10 * Curve(generateFrame / 10.0 * Math.PI);
                int haloRadius = (int)(4 + 6 * (1 + Curve(generateFrame / 10.0 * Math.PI)));
                int haloNumber = (int)(3000 + 4000 * Math.Abs(Math.Pow(Curve(generateFrame / 10.0 * Math.PI), 2)));

                var allPoints = new (double, double, int, (double R, double G, double B))[haloNumber];

                for (int i = 0; i < haloNumber; i++)
                {
                    double t = random.NextDouble() * 2 * Math.PI;
                    var (x, y) = HeartFunction(t, 11);
                    (x, y) = Shrink(x, y, haloRadius);
                    x += random.Next(-11, 11);
                    y += random.Next(-11, 11);
                    int size = random.Next(1, 2); // 粒子大小
                    double hue = 0.95; // 粉色
                    var color = HsvToRgb(hue, 0.5, 1.0); // 动态调整为粉色
                    allPoints[i] = (x, y, size, color);
                }

                return allPoints;
            }

            private static (double, double) Shrink(double x, double y, double ratio)
            {
                double force = -1 / Math.Pow(Math.Pow(x - CanvasCenterX, 2) + Math.Pow(y - CanvasCenterY, 2), 0.6);
                double dx = ratio * force * (x - CanvasCenterX);
                double dy = ratio * force * (y - CanvasCenterY);
                return (x - dx, y - dy);
            }

            private static double Curve(double p)
            {
                return 4 * (2 * Math.Sin(4 * p)) / (2 * Math.PI);
            }

            public void Render(RenderTarget renderTarget, SolidColorBrush brush, int renderFrame)
            {
                var points = allPoints[renderFrame % generateFrame];
                foreach (var (x, y, size, color) in points)
                {
                    brush.Color = new RawColor4((float)color.R, (float)color.G, (float)color.B, 1.0f);
                    double scale = 1 + 0.5 * Math.Sin(renderFrame / 10.0);
                    renderTarget.FillEllipse(new Ellipse(new RawVector2((float)x, (float)y), (float)(size * scale), (float)(size * scale)), brush);
                }
            }

            public (double, double, int, (double R, double G, double B))[] GetCurrentFramePoints(int renderFrame)
            {
                return allPoints[renderFrame % generateFrame];
            }

            private static (double, double) HeartFunction(double t, double shrinkRatio = ImageEnlarge)
            {
                double x = 16 * Math.Pow(Math.Sin(t), 3);
                double y = -(13 * Math.Cos(t) - 5 * Math.Cos(2 * t) - 2 * Math.Cos(3 * t) - Math.Cos(4 * t));

                x *= shrinkRatio;
                y *= shrinkRatio;

                x += CanvasCenterX;
                y += CanvasCenterY;

                return (x, y);
            }
        }

        private class Particle
        {
            public Vector2 Position { get; private set; }
            public Vector2 Velocity { get; private set; }
            public double Size { get; private set; }
            public double Life { get; private set; }
            public (double R, double G, double B) Color { get; private set; }

            public Particle(double x, double y, Vector2 velocity, (double R, double G, double B) color, double size = 1.0, double life = 1.0)
            {
                Position = new Vector2((float)x, (float)y);
                Velocity = velocity;
                Size = size;
                Life = life;
                Color = color;
            }

            public void Update()
            {
                Position += Velocity;
                Life -= 0.02;
                Size *= 0.98;
            }
        }

        private static (double R, double G, double B) HsvToRgb(double h, double s, double v)
        {
            int i = (int)(h * 6);
            double f = h * 6 - i;
            double p = v * (1 - s);
            double q = v * (1 - f * s);
            double t = v * (1 - (1 - f) * s);

            switch (i % 6)
            {
                case 0: return (v, t, p);
                case 1: return (q, v, p);
                case 2: return (p, v, t);
                case 3: return (p, q, v);
                case 4: return (t, p, v);
                case 5: return (v, p, q);
                default: throw new ArgumentOutOfRangeException();
            }
        }

     
    }
}
