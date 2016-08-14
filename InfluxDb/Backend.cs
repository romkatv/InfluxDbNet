using Conditions;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace InfluxDb
{
    public class Instance
    {
        public string Endpoint { get; set; }
        public string Database { get; set; }
        public string RetentionPolicy { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }

        internal string Uri
        {
            get
            {
                Condition.Requires(Endpoint, "Endpoint").IsNotNullOrEmpty();
                Condition.Requires(Database, "Database").IsNotNullOrEmpty();
                var uri = new StringBuilder(Endpoint);
                uri.Append("?precision=u");  // timestamps are in microseconds
                Action<string, string> AddQueryParam = (name, value) =>
                {
                    Condition.Requires(name, "name").IsNotNullOrEmpty();
                    if (value == null) return;
                    uri.AppendFormat("&{0}={1}", HttpUtility.UrlEncode(name), HttpUtility.UrlEncode(value));
                };
                AddQueryParam("db", Database);
                AddQueryParam("rp", RetentionPolicy);
                AddQueryParam("u", Username);
                AddQueryParam("p", Password);
                return uri.ToString();
            }
        }
    }

    public class RestBackend : IBackend, IDisposable
    {
        static readonly Logger _log = LogManager.GetCurrentClassLogger();
        readonly HttpClient _http;

        public RestBackend(Instance instance, TimeSpan timeout)
        {
            _http = new HttpClient();
            _http.Timeout = TimeSpan.FromMilliseconds(-1);
            _http.BaseAddress = new Uri(instance.Uri);
        }

        public async Task Send(List<Point> points, TimeSpan timeout)
        {
            _log.Info("OUT: HTTP POST {0} <{1} point(s)>", _http.BaseAddress, points.Count);
            var msg = new HttpRequestMessage(HttpMethod.Post, "");
            msg.Content = new StringContent(Serializer.Serialize(points), Encoding.UTF8, "application/octet-stream");
            using (var cancel = new CancellationTokenSource(timeout))
            {
                HttpResponseMessage resp = await _http.SendAsync(msg, HttpCompletionOption.ResponseContentRead, cancel.Token);
                string content = await resp.Content.ReadAsStringAsync();
                int code = (int)resp.StatusCode;
                string log = string.Format("IN: HTTP {0} ({1}): {2}", (int)resp.StatusCode, resp.StatusCode, content);
                if (code < 200 || code >= 300)
                {
                    _log.Warn("{0}", log);
                    throw new Exception(log);
                }
                else
                {
                    _log.Info("{0}", log);
                }
            }
        }

        public void Dispose()
        {
            _http.Dispose();
        }
    }
}
