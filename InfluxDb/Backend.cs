using Conditions;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
        // Optional.
        public string RetentionPolicy { get; set; }
        // Optional.
        public string Username { get; set; }
        // Optional.
        public string Password { get; set; }

        internal string Uri
        {
            get
            {
                Condition.Requires(Endpoint, "Endpoint").IsNotNullOrEmpty();
                Condition.Requires(Database, "Database").IsNotNullOrEmpty();
                var uri = new StringBuilder(Endpoint);
                if (!Endpoint.EndsWith("/")) uri.Append('/');
                uri.Append("write");
                bool hasQuery = false;
                Action<string, string> AddQueryParam = (name, value) =>
                {
                    Condition.Requires(name, "name").IsNotNullOrEmpty();
                    if (value == null) return;
                    uri.Append(hasQuery ? '&' : '?');
                    hasQuery = true;
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

        // If proxy is not null, HTTP requests will be sent through this proxy. Must be in the form of host:port. 
        public RestBackend(Instance instance, string proxy = null)
        {
            var h = new HttpClientHandler()
            {
                Proxy = proxy == null ? null : new WebProxy(proxy),
                UseProxy = proxy != null,
            };
            _http = new HttpClient(h)
            {
                Timeout = TimeSpan.FromMilliseconds(-1),
                BaseAddress = new Uri(instance.Uri)
            };
        }

        public async Task Send(List<Point> points, TimeSpan timeout)
        {
            string req = Serializer.Serialize(points);
            if (_log.IsDebugEnabled)
            {
                string indent = "  ";
                _log.Debug("OUT: HTTP POST {0} <{1} point(s)>:\n{2}{3}",
                           _http.BaseAddress, points.Count, indent, req.Replace("\n", "\n" + indent));
            }
            else
            {
                _log.Info("OUT: HTTP POST {0} <{1} point(s)>", _http.BaseAddress, points.Count);
            }
            var msg = new HttpRequestMessage(HttpMethod.Post, "")
            {
                Content = new StringContent(req, Encoding.UTF8, "application/octet-stream")
            };
            using (var cancel = new CancellationTokenSource(timeout))
            {
                HttpResponseMessage resp = await _http.SendAsync(msg, HttpCompletionOption.ResponseContentRead, cancel.Token);
                string content = await resp.Content.ReadAsStringAsync();
                int code = (int)resp.StatusCode;
                string log = string.Format("IN: HTTP {0} {1} ({2}): {3}",
                                           _http.BaseAddress, (int)resp.StatusCode, resp.StatusCode, content);
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
