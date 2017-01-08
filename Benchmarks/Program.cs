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
        long _ticks = 0;
        readonly InfluxDb.Point _metric_1_0_1;
        readonly InfluxDb.Point _metric_1_1_1;
        readonly InfluxDb.Point _metric_4_0_1;
        readonly InfluxDb.Point _metric_4_0_4;
        readonly InfluxDb.Point _metric_4_1_1;
        readonly InfluxDb.Point _metric_4_1_4;
        readonly InfluxDb.Point _metric_4_4_1;
        readonly InfluxDb.Point _metric_4_4_4;
        readonly InfluxDb.Point _metric_16_0_1;
        readonly InfluxDb.Point _metric_16_0_4;
        readonly InfluxDb.Point _metric_16_0_16;
        readonly InfluxDb.Point _metric_16_1_1;
        readonly InfluxDb.Point _metric_16_1_4;
        readonly InfluxDb.Point _metric_16_1_16;
        readonly InfluxDb.Point _metric_16_4_1;
        readonly InfluxDb.Point _metric_16_4_4;
        readonly InfluxDb.Point _metric_16_4_16;
        readonly InfluxDb.Point _metric_16_16_1;
        readonly InfluxDb.Point _metric_16_16_4;
        readonly InfluxDb.Point _metric_16_16_16;

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
            InfluxDb.Facade.Instance = new InfluxDb.Facade(pub);
            _metric_1_0_1 = Extract_1_0_1();
            _metric_1_1_1 = Extract_1_1_1();
            _metric_4_0_1 = Extract_4_0_1();
            _metric_4_0_4 = Extract_4_0_4();
            _metric_4_1_1 = Extract_4_1_1();
            _metric_4_1_4 = Extract_4_1_4();
            _metric_4_4_1 = Extract_4_4_1();
            _metric_4_4_4 = Extract_4_4_4();
            _metric_16_0_1 = Extract_16_0_1();
            _metric_16_0_4 = Extract_16_0_4();
            _metric_16_0_16 = Extract_16_0_16();
            _metric_16_1_1 = Extract_16_1_1();
            _metric_16_1_4 = Extract_16_1_4();
            _metric_16_1_16 = Extract_16_1_16();
            _metric_16_4_1 = Extract_16_4_1();
            _metric_16_4_4 = Extract_16_4_4();
            _metric_16_4_16 = Extract_16_4_16();
            _metric_16_16_1 = Extract_16_16_1();
            _metric_16_16_4 = Extract_16_16_4();
            _metric_16_16_16 = Extract_16_16_16();
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public InfluxDb.Point Extract_1_0_1()
        {
            return InfluxDb.Facade.Extract(new Metric1()
            {
                Field1 = 1.0,
            });
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public InfluxDb.Point Extract_1_1_1()
        {
            return InfluxDb.Facade.Extract(new Metric1()
            {
                Tag1 = "Tag1",
                Field1 = 1.0,
            });
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public InfluxDb.Point Extract_4_0_1()
        {
            return InfluxDb.Facade.Extract(new Metric4()
            {
                Field1 = 1.0,
            });
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public InfluxDb.Point Extract_4_0_4()
        {
            return InfluxDb.Facade.Extract(new Metric4()
            {
                Field1 = 1.0,
                Field2 = 2.0,
                Field3 = 3.0,
                Field4 = 4.0,
            });
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public InfluxDb.Point Extract_4_1_1()
        {
            return InfluxDb.Facade.Extract(new Metric4()
            {
                Tag1 = "Tag1",
                Field1 = 1.0,
            });
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public InfluxDb.Point Extract_4_1_4()
        {
            return InfluxDb.Facade.Extract(new Metric4()
            {
                Tag1 = "Tag1",
                Field1 = 1.0,
                Field2 = 2.0,
                Field3 = 3.0,
                Field4 = 4.0,
            });
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public InfluxDb.Point Extract_4_4_1()
        {
            return InfluxDb.Facade.Extract(new Metric4()
            {
                Tag1 = "Tag1",
                Tag2 = "Tag2",
                Tag3 = "Tag3",
                Tag4 = "Tag4",
                Field1 = 1.0,
            });
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public InfluxDb.Point Extract_4_4_4()
        {
            return InfluxDb.Facade.Extract(new Metric4()
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
        public InfluxDb.Point Extract_16_0_1()
        {
            return InfluxDb.Facade.Extract(new Metric16()
            {
                Field1 = 1.0,
            });
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public InfluxDb.Point Extract_16_0_4()
        {
            return InfluxDb.Facade.Extract(new Metric16()
            {
                Field1 = 1.0,
                Field2 = 2.0,
                Field3 = 3.0,
                Field4 = 4.0,
            });
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public InfluxDb.Point Extract_16_0_16()
        {
            return InfluxDb.Facade.Extract(new Metric16()
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
        public InfluxDb.Point Extract_16_1_1()
        {
            return InfluxDb.Facade.Extract(new Metric16()
            {
                Tag1 = "Tag1",
                Field1 = 1.0,
            });
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public InfluxDb.Point Extract_16_1_4()
        {
            return InfluxDb.Facade.Extract(new Metric16()
            {
                Tag1 = "Tag1",
                Field1 = 1.0,
                Field2 = 2.0,
                Field3 = 3.0,
                Field4 = 4.0,
            });
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public InfluxDb.Point Extract_16_1_16()
        {
            return InfluxDb.Facade.Extract(new Metric16()
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
        public InfluxDb.Point Extract_16_4_1()
        {
            return InfluxDb.Facade.Extract(new Metric16()
            {
                Tag1 = "Tag1",
                Tag2 = "Tag2",
                Tag3 = "Tag3",
                Tag4 = "Tag4",
                Field1 = 1.0,
            });
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public InfluxDb.Point Extract_16_4_4()
        {
            return InfluxDb.Facade.Extract(new Metric16()
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
        public InfluxDb.Point Extract_16_4_16()
        {
            return InfluxDb.Facade.Extract(new Metric16()
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
        public InfluxDb.Point Extract_16_16_1()
        {
            return InfluxDb.Facade.Extract(new Metric16()
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
        public InfluxDb.Point Extract_16_16_4()
        {
            return InfluxDb.Facade.Extract(new Metric16()
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
        public InfluxDb.Point Extract_16_16_16()
        {
            return InfluxDb.Facade.Extract(new Metric16()
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

        [BenchmarkDotNet.Attributes.Benchmark]
        public void Push_16_0_1()
        {
            Push(_metric_16_0_1);
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public void Push_16_1_1()
        {
            Push(_metric_16_1_1);
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public void Push_16_4_1()
        {
            Push(_metric_16_4_1);
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public void Push_16_16_1()
        {
            Push(_metric_16_16_1);
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public void Push_16_0_4()
        {
            Push(_metric_16_0_4);
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public void Push_16_1_4()
        {
            Push(_metric_16_1_4);
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public void Push_16_4_4()
        {
            Push(_metric_16_4_4);
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public void Push_16_16_4()
        {
            Push(_metric_16_16_4);
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public void Push_16_0_16()
        {
            Push(_metric_16_0_16);
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public void Push_16_1_16()
        {
            Push(_metric_16_1_16);
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public void Push_16_4_16()
        {
            Push(_metric_16_4_16);
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public void Push_16_16_16()
        {
            Push(_metric_16_16_16);
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public void With_16_16_16()
        {
            InfluxDb.Facade.Instance?.With(new DateTime(1), _metric_16_16_16).Dispose();
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public void With_16_0_1_Push_16_0_1()
        {
            using (InfluxDb.Facade.Instance?.With(new DateTime(1), _metric_16_0_1))
            {
                Push(_metric_16_0_1);
            }
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public void With_16_0_1_Push_16_16_16()
        {
            using (InfluxDb.Facade.Instance?.With(new DateTime(1), _metric_16_0_1))
            {
                Push(_metric_16_16_16);
            }
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public void With_16_16_16_Push_16_0_1()
        {
            using (InfluxDb.Facade.Instance?.With(new DateTime(1), _metric_16_16_16))
            {
                Push(_metric_16_0_1);
            }
        }

        [BenchmarkDotNet.Attributes.Benchmark]
        public void With_16_16_16_Push_16_16_16()
        {
            using (InfluxDb.Facade.Instance?.With(new DateTime(1), _metric_16_16_16))
            {
                Push(_metric_16_16_16);
            }
        }

        void Push(InfluxDb.Point p)
        {
            // Ensure that all timestamps are distinct.
            InfluxDb.Facade.Instance?.Push(new DateTime(++_ticks), p);
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
            // Frequency=2742886 ticks, Resolution=364.5795 ns, Timer=TSC
            // CLR=MS.NET 4.0.30319.42000, Arch=32-bit RELEASE
            // GC=Concurrent Workstation
            // JitModules=clrjit-v4.6.1586.0
            //
            // Type=BM_Timeseries  Mode=Throughput
            //
            //                       Method |         Median |      StdDev |
            // ---------------------------- |--------------- |------------ |
            //                Extract_1_0_1 |    421.2679 ns |  31.1853 ns |
            //                Extract_1_1_1 |    525.1546 ns |  50.1382 ns |
            //                Extract_4_0_1 |    614.3559 ns |  20.1191 ns |
            //                Extract_4_0_4 |  1,135.5698 ns |  50.4740 ns |
            //                Extract_4_1_1 |    740.0135 ns |  14.1497 ns |
            //                Extract_4_1_4 |  1,267.0784 ns |   9.4236 ns |
            //                Extract_4_4_1 |  1,082.5380 ns |   5.3271 ns |
            //                Extract_4_4_4 |  1,594.3719 ns |  27.3335 ns |
            //               Extract_16_0_1 |  1,325.2778 ns |   3.8002 ns |
            //               Extract_16_0_4 |  1,889.8237 ns |   8.4880 ns |
            //              Extract_16_0_16 |  3,680.8560 ns |  17.9167 ns |
            //               Extract_16_1_1 |  1,458.1226 ns |  21.0654 ns |
            //               Extract_16_1_4 |  2,001.0275 ns |  26.4922 ns |
            //              Extract_16_1_16 |  3,990.8204 ns | 459.9830 ns |
            //               Extract_16_4_1 |  1,850.6445 ns |  68.2333 ns |
            //               Extract_16_4_4 |  2,458.2831 ns | 138.0780 ns |
            //              Extract_16_4_16 |  4,531.0195 ns | 175.9461 ns |
            //              Extract_16_16_1 |  2,848.3955 ns | 122.5068 ns |
            //              Extract_16_16_4 |  3,569.7565 ns | 160.6673 ns |
            //             Extract_16_16_16 |  5,680.0102 ns | 183.4657 ns |
            //                  Push_16_0_1 |  2,654.0117 ns | 150.7405 ns |
            //                  Push_16_1_1 |  2,989.9763 ns | 102.2369 ns |
            //                  Push_16_4_1 |  3,489.3038 ns | 191.0437 ns |
            //                 Push_16_16_1 |  6,256.8976 ns | 382.5689 ns |
            //                  Push_16_0_4 |  3,215.5980 ns |  83.5083 ns |
            //                  Push_16_1_4 |  3,545.3320 ns | 226.2567 ns |
            //                  Push_16_4_4 |  4,188.2778 ns | 199.8327 ns |
            //                 Push_16_16_4 |  6,390.2381 ns | 163.0985 ns |
            //                 Push_16_0_16 |  5,043.0141 ns | 414.5678 ns |
            //                 Push_16_1_16 |  5,513.6196 ns | 156.3707 ns |
            //                 Push_16_4_16 |  6,269.1502 ns | 393.6358 ns |
            //                Push_16_16_16 |  8,433.4710 ns |  48.7041 ns |
            //                With_16_16_16 |    215.3515 ns |   8.9925 ns |
            //      With_16_0_1_Push_16_0_1 |  2,944.1091 ns | 218.6547 ns |
            //    With_16_0_1_Push_16_16_16 |  9,138.8091 ns |  71.1801 ns |
            //    With_16_16_16_Push_16_0_1 |  9,377.6416 ns | 373.5109 ns |
            //  With_16_16_16_Push_16_16_16 | 11,939.8617 ns | 116.1112 ns |
            //
            //                ===[ Legend ]===
            //
            // Extract_1_0_1 measures the performance of Timeseries.Extrac(obj) where
            // typeof(obj) has X declared tags and X declared fields, and obj has Y set
            // tags and Z set fields.
            //
            // Push_X_Y_Z measures the performance of Timeseries.Push(p) where p is the result
            // of Extract_X_Y_Z.
            //
            // Push_X_Y_Z measures the performance of Timeseries.With(p) where p is the result
            // of Extract_X_Y_Z.
            //
            // With_A_B_C_Push_X_Y_Z measures the performance of Timeseries.With(q) followed by
            // Timeseries.Push(s) where q and s are the results of Extract_A_B_C and Extract_X_Y_Z
            // respectively.
            BenchmarkDotNet.Running.BenchmarkRunner.Run<BM_Timeseries>();
        }
    }
}
