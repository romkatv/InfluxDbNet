using InfluxDb;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Example
{
    class Common
    {
        [Tag]
        public string Exchange { get; set; }
        [Tag]
        public string Product { get; set; }
        public double Position { get; set; }
    }

    enum Signal
    {
        Buy,
        Sell,
    }

    class OrderBook
    {
        public double? BestBid { get; set; }
        public double? BestAsk { get; set; }
    }

    class MarketData
    {
        [Tag]
        public Signal? Signal { get; set; }
        public OrderBook OrderBook { get; set; }
    }

    [Name("trades")]
    class TradeData
    {
        [Name("realized_pnl"), Sum]
        public double RealizedPnL { get; set; }
        [Ignore]
        public int Internal { get; set; }
    }

    class Program
    {
        static readonly Logger _log = LogManager.GetCurrentClassLogger();

        static void Main(string[] args)
        {
            var instance = new Instance()
            {
                Endpoint = "http://localhost:8086",
                Database = "finance",
            };
            var cfg = new PublisherConfig()
            {
                MaxPointsPerBatch = 100000,
                MaxPointsPerSeries = 10000,
                SamplingPeriod = TimeSpan.FromSeconds(1),
                SendPeriod = TimeSpan.FromSeconds(5),
                SendTimeout = TimeSpan.FromSeconds(10),
            };
            using (var backend = new RestBackend(instance))
            using (var pub = new Publisher(backend, cfg))
            {
                Facade.Instance = new Facade(pub);

                // Use `?.` when reporting statistics.
                // If `Timeseries.Instance` is null, the performance overhead will be minimal.
                using (Facade.Instance?.At(new DateTime(2016, 8, 29, 11, 53, 6, DateTimeKind.Utc)))
                using (Facade.Instance?.With(new Common() { Exchange = "Coinbase", Product = "BTCUSD", Position = 42.0 }))
                {
                    Facade.Instance?.Push(new MarketData()
                    {
                        Signal = Signal.Buy,
                        OrderBook = new OrderBook() { BestAsk = 777, BestBid = 666 }
                    });
                    Facade.Instance?.Push(new TradeData() { RealizedPnL = 1337 });
                }

                try
                {
                    // HTTP POST http://localhost:8086/write?db=finance
                    //
                    // market_data,exchange=Coinbase,product=BTCUSD,signal=Buy best_ask=777,best_bid=666,position=42 1472471586000000000
                    // trades,exchange=Coinbase,product=BTCUSD position=42,realized_pnl=1337 1472471586000000000
                    //
                    // Note: the order of fields (but not tags) may differ.
                    pub.Flush(TimeSpan.FromSeconds(10)).Wait();
                }
                catch (Exception e)
                {
                    _log.Warn(e, "Unable to flush timeseries to InfluxDb");
                }
            }
        }
    }
}
