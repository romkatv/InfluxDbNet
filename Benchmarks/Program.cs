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
        public void Push_4_4_4()
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
            //     Push_1_0_1 |  2.5959 us | 0.1208 us |
            //     Push_1_1_1 |  2.8539 us | 0.0910 us |
            //     Push_4_0_1 |  2.7912 us | 0.1773 us |
            //     Push_4_0_4 |  4.3179 us | 0.1576 us |
            //     Push_4_1_1 |  2.9188 us | 0.1563 us |
            //     Push_4_1_4 |  4.4506 us | 0.1186 us |
            //     Push_4_4_1 |  3.6311 us | 0.1551 us |
            //     Push_4_4_4 |  5.3141 us | 0.1669 us |
            //    Push_16_0_1 |  3.3618 us | 0.1472 us |
            //    Push_16_0_4 |  5.2516 us | 0.1854 us |
            //   Push_16_0_16 | 10.2235 us | 0.4929 us |
            //    Push_16_1_1 |  3.7248 us | 0.0931 us |
            //    Push_16_1_4 |  5.4385 us | 0.2652 us |
            //   Push_16_1_16 | 10.2315 us | 0.3020 us |
            //    Push_16_4_1 |  4.5054 us | 0.1529 us |
            //    Push_16_4_4 |  6.1313 us | 0.2707 us |
            //   Push_16_4_16 | 10.8363 us | 0.2829 us |
            //   Push_16_16_1 |  6.9902 us | 0.1678 us |
            //   Push_16_16_4 |  8.6739 us | 0.2792 us |
            //  Push_16_16_16 | 13.0362 us | 0.5730 us |
            //
            // Note: Push_X_Y_Z measures the performance of Timeseries.Push(obj) where
            // typeof(obj) has X declared tags and X declared fields, and obj has Y set
            // tags and Z set fields.
            //
            //           ===[ Conclusions ]===
            //
            // *   The number of declared tags and fields has little effect on performance.
            // *   Timeseries.Push(obj) costs about 2 us plus 400 ns for each tag and field
            //     that is actually set.
            BenchmarkDotNet.Running.BenchmarkRunner.Run<BM_Timeseries>();
        }
    }
}
