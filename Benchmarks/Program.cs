using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Benchmarks
{
    class Metric1
    {
        [InfluxDb.Tag]
        public string Tag1;

        public double? Field1;
    }

    class Metric4
    {
        [InfluxDb.Tag]
        public string Tag1;
        [InfluxDb.Tag]
        public string Tag2;
        [InfluxDb.Tag]
        public string Tag3;
        [InfluxDb.Tag]
        public string Tag4;

        public double? Field1;
        public double? Field2;
        public double? Field3;
        public double? Field4;
    }

    class Metric16
    {
        [InfluxDb.Tag]
        public string Tag1;
        [InfluxDb.Tag]
        public string Tag2;
        [InfluxDb.Tag]
        public string Tag3;
        [InfluxDb.Tag]
        public string Tag4;
        [InfluxDb.Tag]
        public string Tag5;
        [InfluxDb.Tag]
        public string Tag6;
        [InfluxDb.Tag]
        public string Tag7;
        [InfluxDb.Tag]
        public string Tag8;
        [InfluxDb.Tag]
        public string Tag9;
        [InfluxDb.Tag]
        public string Tag10;
        [InfluxDb.Tag]
        public string Tag11;
        [InfluxDb.Tag]
        public string Tag12;
        [InfluxDb.Tag]
        public string Tag13;
        [InfluxDb.Tag]
        public string Tag14;
        [InfluxDb.Tag]
        public string Tag15;
        [InfluxDb.Tag]
        public string Tag16;

        public double? Field1;
        public double? Field2;
        public double? Field3;
        public double? Field4;
        public double? Field5;
        public double? Field6;
        public double? Field7;
        public double? Field8;
        public double? Field9;
        public double? Field10;
        public double? Field11;
        public double? Field12;
        public double? Field13;
        public double? Field14;
        public double? Field15;
        public double? Field16;
    }

    class NullBackend : InfluxDb.IBackend
    {
        public Task Send(List<InfluxDb.Point> points, TimeSpan timeout)
        {
            var res = new Task(delegate { });
            res.RunSynchronously();
            return res;
        }
    }

    public class BM_Timeseries
    {
        public BM_Timeseries()
        {
            var cfg = new InfluxDb.PublisherConfig()
            {
                MaxPointsPerBatch = -1,
                MaxPointsPerSeries = -1,
                SamplingPeriod = TimeSpan.Zero,
                SendPeriod = TimeSpan.FromMilliseconds(100),
                SendTimeout = TimeSpan.FromSeconds(10),
            };
            var pub = new InfluxDb.Publisher(new NullBackend(), cfg);
            InfluxDb.Timeseries.SetSink(pub);
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public void Push_1_0_1()
        {
            InfluxDb.Timeseries.Push(new Metric1()
            {
                Field1 = 1.0,
            });
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public void Push_1_1_1()
        {
            InfluxDb.Timeseries.Push(new Metric1()
            {
                Tag1 = "Tag1",
                Field1 = 1.0,
            });
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public void Push_4_0_1()
        {
            InfluxDb.Timeseries.Push(new Metric4()
            {
                Field1 = 1.0,
            });
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public void Push_4_0_4()
        {
            InfluxDb.Timeseries.Push(new Metric4()
            {
                Field1 = 1.0,
                Field2 = 2.0,
                Field3 = 3.0,
                Field4 = 4.0,
            });
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public void Push_4_1_1()
        {
            InfluxDb.Timeseries.Push(new Metric4()
            {
                Tag1 = "Tag1",
                Field1 = 1.0,
            });
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public void Push_4_1_4()
        {
            InfluxDb.Timeseries.Push(new Metric4()
            {
                Tag1 = "Tag1",
                Field1 = 1.0,
                Field2 = 2.0,
                Field3 = 3.0,
                Field4 = 4.0,
            });
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public void Push_4_4_1()
        {
            InfluxDb.Timeseries.Push(new Metric4()
            {
                Tag1 = "Tag1",
                Tag2 = "Tag2",
                Tag3 = "Tag3",
                Tag4 = "Tag4",
                Field1 = 1.0,
            });
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public void Push_4_4()
        {
            InfluxDb.Timeseries.Push(new Metric4()
            {
                Tag1 = "Tag1",
                Tag2 = "Tag2",
                Tag3 = "Tag3",
                Tag4 = "Tag4",
                Field1 = 1.0,
                Field2 = 2.0,
                Field3 = 3.0,
                Field4 = 4.0,
            });
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public void Push_16_0_1()
        {
            InfluxDb.Timeseries.Push(new Metric16()
            {
                Field1 = 1.0,
            });
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public void Push_16_0_4()
        {
            InfluxDb.Timeseries.Push(new Metric16()
            {
                Field1 = 1.0,
                Field2 = 2.0,
                Field3 = 3.0,
                Field4 = 4.0,
            });
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public void Push_16_0_16()
        {
            InfluxDb.Timeseries.Push(new Metric16()
            {
                Field1 = 1.0,
                Field2 = 2.0,
                Field3 = 3.0,
                Field4 = 4.0,
                Field5 = 5.0,
                Field6 = 6.0,
                Field7 = 7.0,
                Field8 = 8.0,
                Field9 = 9.0,
                Field10 = 10.0,
                Field11 = 11.0,
                Field12 = 12.0,
                Field13 = 13.0,
                Field14 = 14.0,
                Field15 = 15.0,
                Field16 = 16.0,
            });
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public void Push_16_1_1()
        {
            InfluxDb.Timeseries.Push(new Metric16()
            {
                Tag1 = "Tag1",
                Field1 = 1.0,
            });
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public void Push_16_1_4()
        {
            InfluxDb.Timeseries.Push(new Metric16()
            {
                Tag1 = "Tag1",
                Field1 = 1.0,
                Field2 = 2.0,
                Field3 = 3.0,
                Field4 = 4.0,
            });
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public void Push_16_1_16()
        {
            InfluxDb.Timeseries.Push(new Metric16()
            {
                Tag1 = "Tag1",
                Field1 = 1.0,
                Field2 = 2.0,
                Field3 = 3.0,
                Field4 = 4.0,
                Field5 = 5.0,
                Field6 = 6.0,
                Field7 = 7.0,
                Field8 = 8.0,
                Field9 = 9.0,
                Field10 = 10.0,
                Field11 = 11.0,
                Field12 = 12.0,
                Field13 = 13.0,
                Field14 = 14.0,
                Field15 = 15.0,
                Field16 = 16.0,
            });
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public void Push_16_4_1()
        {
            InfluxDb.Timeseries.Push(new Metric16()
            {
                Tag1 = "Tag1",
                Tag2 = "Tag2",
                Tag3 = "Tag3",
                Tag4 = "Tag4",
                Field1 = 1.0,
            });
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public void Push_16_4_4()
        {
            InfluxDb.Timeseries.Push(new Metric16()
            {
                Tag1 = "Tag1",
                Tag2 = "Tag2",
                Tag3 = "Tag3",
                Tag4 = "Tag4",
                Field1 = 1.0,
                Field2 = 2.0,
                Field3 = 3.0,
                Field4 = 4.0,
            });
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public void Push_16_4_16()
        {
            InfluxDb.Timeseries.Push(new Metric16()
            {
                Tag1 = "Tag1",
                Tag2 = "Tag2",
                Tag3 = "Tag3",
                Tag4 = "Tag4",
                Field1 = 1.0,
                Field2 = 2.0,
                Field3 = 3.0,
                Field4 = 4.0,
                Field5 = 5.0,
                Field6 = 6.0,
                Field7 = 7.0,
                Field8 = 8.0,
                Field9 = 9.0,
                Field10 = 10.0,
                Field11 = 11.0,
                Field12 = 12.0,
                Field13 = 13.0,
                Field14 = 14.0,
                Field15 = 15.0,
                Field16 = 16.0,
            });
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public void Push_16_16_1()
        {
            InfluxDb.Timeseries.Push(new Metric16()
            {
                Tag1 = "Tag1",
                Tag2 = "Tag2",
                Tag3 = "Tag3",
                Tag4 = "Tag4",
                Tag5 = "Tag5",
                Tag6 = "Tag6",
                Tag7 = "Tag7",
                Tag8 = "Tag8",
                Tag9 = "Tag9",
                Tag10 = "Tag10",
                Tag11 = "Tag11",
                Tag12 = "Tag12",
                Tag13 = "Tag13",
                Tag14 = "Tag14",
                Tag15 = "Tag15",
                Tag16 = "Tag16",
                Field1 = 1.0,
            });
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public void Push_16_16_4()
        {
            InfluxDb.Timeseries.Push(new Metric16()
            {
                Tag1 = "Tag1",
                Tag2 = "Tag2",
                Tag3 = "Tag3",
                Tag4 = "Tag4",
                Tag5 = "Tag5",
                Tag6 = "Tag6",
                Tag7 = "Tag7",
                Tag8 = "Tag8",
                Tag9 = "Tag9",
                Tag10 = "Tag10",
                Tag11 = "Tag11",
                Tag12 = "Tag12",
                Tag13 = "Tag13",
                Tag14 = "Tag14",
                Tag15 = "Tag15",
                Tag16 = "Tag16",
                Field1 = 1.0,
                Field2 = 2.0,
                Field3 = 3.0,
                Field4 = 4.0,
            });
        }
        
        [BenchmarkDotNet.Attributes.Benchmark]
        public void Push_16_16_16()
        {
            InfluxDb.Timeseries.Push(new Metric16()
            {
                Tag1 = "Tag1",
                Tag2 = "Tag2",
                Tag3 = "Tag3",
                Tag4 = "Tag4",
                Tag5 = "Tag5",
                Tag6 = "Tag6",
                Tag7 = "Tag7",
                Tag8 = "Tag8",
                Tag9 = "Tag9",
                Tag10 = "Tag10",
                Tag11 = "Tag11",
                Tag12 = "Tag12",
                Tag13 = "Tag13",
                Tag14 = "Tag14",
                Tag15 = "Tag15",
                Tag16 = "Tag16",
                Field1 = 1.0,
                Field2 = 2.0,
                Field3 = 3.0,
                Field4 = 4.0,
                Field5 = 5.0,
                Field6 = 6.0,
                Field7 = 7.0,
                Field8 = 8.0,
                Field9 = 9.0,
                Field10 = 10.0,
                Field11 = 11.0,
                Field12 = 12.0,
                Field13 = 13.0,
                Field14 = 14.0,
                Field15 = 15.0,
                Field16 = 16.0,
            });
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            // Host Process Environment Information:
            // BenchmarkDotNet.Core=v0.9.9.0
            // OS=Microsoft Windows NT 6.2.9200.0
            // Processor=Intel(R) Core(TM) i7 CPU 860 2.80GHz, ProcessorCount=8
            // Frequency=2742883 ticks, Resolution=364.5799 ns, Timer=TSC
            // CLR=MS.NET 4.0.30319.42000, Arch=32-bit RELEASE
            // GC=Concurrent Workstation
            // JitModules=clrjit-v4.6.1080.0
            //
            // Type=BM_Timeseries  Mode=Throughput
            //
            //         Method |     Median |    StdDev |
            // -------------- |----------- |---------- |
            //     Push_1_0_1 |  2.8130 us | 0.1131 us |
            //     Push_1_1_1 |  3.2811 us | 0.1206 us |
            //     Push_4_0_1 |  3.0862 us | 0.0600 us |
            //     Push_4_0_4 |  6.6069 us | 0.4273 us |
            //     Push_4_1_1 |  3.3217 us | 0.1388 us |
            //     Push_4_1_4 |  7.0569 us | 0.2572 us |
            //     Push_4_4_1 |  6.4179 us | 0.1084 us |
            //       Push_4_4 |  9.8008 us | 0.4382 us |
            //    Push_16_0_1 |  3.8730 us | 0.1010 us |
            //    Push_16_0_4 |  7.5832 us | 0.2618 us |
            //   Push_16_0_16 | 30.6564 us | 1.5532 us |
            //    Push_16_1_1 |  4.2413 us | 0.1595 us |
            //    Push_16_1_4 |  8.1631 us | 0.2771 us |
            //   Push_16_1_16 | 31.0156 us | 1.1613 us |
            //    Push_16_4_1 |  7.5784 us | 0.1858 us |
            //    Push_16_4_4 | 10.5826 us | 0.2947 us |
            //   Push_16_4_16 | 33.3721 us | 1.3000 us |
            //   Push_16_16_1 | 28.5236 us | 0.7836 us |
            //   Push_16_16_4 | 31.5153 us | 0.3192 us |
            //  Push_16_16_16 | 55.1654 us | 1.0638 us |
            //
            //           ===[ Conclusions ]===
            //
            // *   The number of declared tags and fields has little effect on performance.
            // *   Every tag and field that is actually set costs ~2 us.
            BenchmarkDotNet.Running.BenchmarkRunner.Run<BM_Timeseries>();
        }
    }
}
