using CloudFlare.NET;
using CloudFlareDDNS.Properties;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CloudFlareDDNS
{
    public partial class Service : ServiceBase
    {
        public Service()
        {
            InitializeComponent();
        }

        private Task Thread;
        private CancellationTokenSource CTS;
        private CloudFlareClient CF;

        protected override void OnStart(string[] args)
        {
            CF = new CloudFlareClient(new CloudFlareAuth(Settings.Default.Email, Settings.Default.APIKey));
            CTS = new CancellationTokenSource();
            Thread = Task.Run(Work, CTS.Token);
        }

        public async Task Work()
        {
            while (!CTS.IsCancellationRequested)
            {
                var wc = new WebClient();
                try
                {
                    wc.DownloadString("http://" + Settings.Default.Host);
                }
                catch
                {
                    // connection error
                    // expected error:
                    //     server is busy - not a considerale one, just wait
                    //     lost connection - no hope, connect LAN physically
                    //     ip address changed - can handle, aim of this program
                    try
                    {
                        // get the global ip address
                        var data = wc.DownloadString("http://ipip.kr");
                        var match = Regex.Match(data, @"<title>.*?(\d+\.\d+\.\d+\.\d+).*?</title>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                        var ipAddr = match.Groups[1].Value;

                        // confirm ip address changed
                        var zone = (await CF.GetAllZonesAsync(new ZoneGetParameters(Settings.Default.Host))).First();
                        var record = (await CF.GetDnsRecordsAsync(zone.Id, new DnsRecordGetParameters(DnsRecordType.A, Settings.Default.Host))).Result.First();
                        if (ipAddr != record.Content)
                        {
                            Log.Write("UPDATE " + record.Content + " TO " + ipAddr);
                            var wr = WebRequest.CreateHttp(string.Format(
                                "https://api.cloudflare.com/client/v4/zones/{0}/dns_records/{1}",
                                zone.Id,
                                record.Id));
                            wr.Method = "PUT";
                            wr.Headers.Add("X-Auth-Email", CF.Auth.Email);
                            wr.Headers.Add("X-Auth-Key", CF.Auth.Key);
                            wr.ContentType = "application/json";
                            using (var rq = new StreamWriter(wr.GetRequestStream()))
                            {
                                var json = JObject.FromObject(record);
                                json["content"] = ipAddr;
                                rq.Write(json.ToString(Formatting.None));
                            }
                            using (var rp = new StreamReader(wr.GetResponse().GetResponseStream()))
                            {
                                var json = JObject.Parse(rp.ReadToEnd());
                                json.Remove("result");
                                Log.Write("RESULT " + json.ToString(Formatting.None));
                            }
                            continue;
                        }
                        Log.Write("SERVER_BUSY");
                    }
                    catch
                    {
                        Log.Write("LOST_CONNECTION");
                    }
                }
                await Task.Delay(1000 * 60, CTS.Token);
            }
        }

        protected override void OnStop()
        {
            if (Thread != null)
            {
                CTS.Cancel();
                try
                {
                    Thread.Wait();
                }
                catch { }
                Thread.Dispose();
            }
        }
    }
}
