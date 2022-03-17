using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace unsubscription
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            chk = new CheckBox()
            {
                Content = "Subscribe from child to parent event?"
            };

            chk2 = new CheckBox()
            {
                Content = "Subscribe from parent to child event?"
            };

            btn = new Button()
            {
                Content = "Run with no Visual Elements"
            };
            btn.Click += (s, e) =>
            {
                btn.IsEnabled = false;
                btn2.IsEnabled = false;

                RunNoVisual();
            };

            btn2 = new Button()
            {
                Content = "Run with Visual Elements"
            };
            btn2.Click += (s, e) =>
            {
                btn.IsEnabled = false;
                btn2.IsEnabled = false;

                RunVisual();
            };

            DockPanel.SetDock(chk, Dock.Top);
            DockPanel.SetDock(chk2, Dock.Top);
            DockPanel.SetDock(btn, Dock.Top);
            DockPanel.SetDock(btn2, Dock.Top);
            DockPanel.SetDock(lPnl, Dock.Bottom);

            var dock = new DockPanel();
            dock.Children.Add(chk);
            dock.Children.Add(chk2);
            dock.Children.Add(btn);
            dock.Children.Add(btn2);

            graph = new MemoryGraph();
            dock.Children.Add(graph);

            Content = dock;

            graph.Run();
        }

        private CheckBox chk;
        private CheckBox chk2;
        private Button btn;
        private List<Wrapper> lArr;
        private MemoryGraph graph;
        private Panel lPnl = new Panel();
        private Button btn2;

        private event EventHandler evt;

        private async void RunNoVisual()
        {
            var stp = Stopwatch.StartNew();
            while (true)
            {
                lArr = new List<Wrapper>();
                for (int i = 0; i < 100; i++)
                {
                    lArr.Add(new Wrapper(this, chk.IsChecked == true, chk2.IsChecked == true));
                }

                Title = graph.Status;

                stp.Stop();
                await Task.Delay(250);
                stp.Restart();
            }
        }

        private async void RunVisual()
        {
            var stp = Stopwatch.StartNew();
            while (true)
            {
                lPnl.Children.Clear();
                for (int i = 0; i < 100; i++)
                {
                    var txt = new MyTextBox(this, chk.IsChecked == true);
                    txt.Text = string.Empty.PadLeft(1000, 'c');

                    if (chk2.IsChecked == true)
                        txt.PastingFromClipboard += PasteEvent;

                    lPnl.Children.Add(txt);
                }

                Title = graph.Status;

                stp.Stop();
                await Task.Delay(250);
                stp.Restart();
            }
        }

        private void PasteEvent(object? sender, RoutedEventArgs e)
        {
        }

        class MyTextBox : TextBox
        {
            public MyTextBox(MainWindow parent, bool subscribeToParent)
            {
                if (subscribeToParent)
                    parent.evt += Evt;
            }

            private void Evt(object? sender, EventArgs e)
            {
            }
        }

        class MemoryGraph : Panel
        {
            const int GRAPH_SIZE = 200;
            private long initialMemorySize;

            public string Status { get; private set; }

            ConcurrentQueue<long> usageQueue = new ConcurrentQueue<long>();

            public async void Run()
            {
                var stp = Stopwatch.StartNew();
                while (true)
                {
                    if (stp.Elapsed.TotalSeconds < 1)
                        continue;

                    var memoryUsageBeforeGC = GC.GetTotalMemory(false);
                    var memoryUsage = GC.GetTotalMemory(true);
                    double delta = double.NaN;

                    if (initialMemorySize == 0)
                        initialMemorySize = memoryUsage;

                    delta = memoryUsage - initialMemorySize;

                    if (usageQueue.Count > GRAPH_SIZE)
                        _ = usageQueue.TryDequeue(out _);

                    usageQueue.Enqueue(memoryUsage);

                    Status = $"memory delta: {delta / 1024.0 / 1024.0:0.#####Mb}; usage: {memoryUsage / 1024.0 / 1024.0:0.#Mb}; before GC: {memoryUsageBeforeGC / 1024.0 / 1024.0:0.#Mb}";

                    InvalidateVisual();
                    await Task.Delay(100);
                }
            }

            public override void Render(DrawingContext context)
            {
                base.Render(context);
                var usageQueue = this.usageQueue.ToList();

                var max = usageQueue.Max();
                var min = usageQueue.Min();

                var p = new Pen()
                {
                    Brush = Brushes.SkyBlue,
                };

                for (int i = 1; i < usageQueue.Count; i++)
                {
                    var usage = (double)usageQueue[i];
                    var y = Bounds.Height - Clamp((usage - min) / (max - min)) * Bounds.Height;
                    var x = ((double)i / GRAPH_SIZE) * Bounds.Width;

                    var pusage = (double)usageQueue[i - 1];
                    var py = Bounds.Height - Clamp((pusage - min) / (max - min)) * Bounds.Height;
                    var px = (((double)i - 1) / GRAPH_SIZE) * Bounds.Width;

                    context.DrawLine(p, new Point(px, py), new Point(x, y));
                }

                var maxMb = max / 1024.0 / 1024.0;
                var minMb = min / 1024.0 / 1024.0;

                context.DrawText(Brushes.Black, new Point(0, 0), new FormattedText($"{maxMb:0.###Mb}", Typeface.Default, 11, TextAlignment.Left, TextWrapping.NoWrap, new Size(100, 30)));
                context.DrawText(Brushes.Black, new Point(0, Bounds.Height - 15), new FormattedText($"{minMb:0.###Mb}", Typeface.Default, 11, TextAlignment.Left, TextWrapping.NoWrap, new Size(100, 30)));
            }

            private double Clamp(double v)
            {
                if (v > 1)
                    return 1;

                if (v < 0)
                    return 0;

                return v;
            }
        }

        class Wrapper
        {
            private BigArray barr;

            public Wrapper(MainWindow parent, bool subscribeToParent, bool subscribeToChild)
            {
                barr = new BigArray();

                if (subscribeToChild)
                    barr.Evt += Barr_Evt;

                if (subscribeToParent)
                    parent.evt += Parent_Evt;
            }

            private void Barr_Evt(object? sender, EventArgs e)
            {
            }

            private void Parent_Evt(object sender, EventArgs evt)
            {
            }
        }

        class BigArray
        {
            private byte[] arr;

            static readonly Random rd = new Random();

            public event EventHandler Evt;

            public BigArray()
            {
                // 1Mb array
                arr = new byte[1024 * 1024];
                rd.NextBytes(arr);
            }
        }
    }
}