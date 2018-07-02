using FFXIV_GameSense.Properties;
using Microsoft.AspNetCore.SignalR.Client;
using Splat;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace FFXIV_GameSense
{
    class HuntsHubConnection
    {
        public HubConnection Connection { get; private set; }
        public bool Connected { get; private set; } = false;

        private const string HuntsHubEndpoint = "SignalR/HuntsHub";
        private bool IsConnecting = false;

        public HuntsHubConnection()
        {
            Connection = new HubConnectionBuilder().WithUrl(FFXIVHunts.baseUrl + HuntsHubEndpoint, o =>
            {
                o.Cookies = Login(Program.mem.GetServerId());
            }).Build();
            Connection.Closed += Connection_Closed;
        }

        private Task Connection_Closed(Exception arg)
        {
            LogHost.Default.InfoException(nameof(HuntsHubConnection) + " closed due to: ", arg);
            Connected = false;
            return Task.CompletedTask;
        }

        internal async Task<bool> Connect(Window1 w1)
        {
            if (Connected)
                await Connection.StopAsync();
            Connected = false;
            if (IsConnecting)
                return false;
            IsConnecting = true;
            while (!Connected)
            {
                try
                {
                    w1.HuntConnectionTextBlock.Dispatcher.Invoke(() => w1.HuntConnectionTextBlock.Text = "Connecting...");
                    await Connection.StartAsync();
                    Connected = true;
                }
                catch (Exception e)
                {
                    if (e is HttpRequestException && e.Message.Contains("401"))
                    {
                        LogHost.Default.WarnException("Failed to connect.", e);
                        await Connection.DisposeAsync();
                        Connection.Closed -= Connection_Closed;
                        Settings.Default.Cookies = string.Empty;
                        Settings.Default.Save();
                        Connection = new HubConnectionBuilder().WithUrl(FFXIVHunts.baseUrl + HuntsHubEndpoint, o =>
                        {
                            o.Cookies = Login(Program.mem.GetServerId());
                        }).Build();
                        Connection.Closed += Connection_Closed;
                        IsConnecting = false;
                        return true;
                    }
                    else
                    {
                        string msg = "Failed to connect. Retrying in {0} seconds.";
                        int wtime = 5000;
                        LogHost.Default.InfoException(string.Format(msg, 5), e);
                        while (wtime > 0)
                        {
                            w1.HuntConnectionTextBlock.Dispatcher.Invoke(() => w1.HuntConnectionTextBlock.Text = string.Format(msg, wtime / 1000));
                            await Task.Delay(1000);
                            wtime -= 1000;
                        }
                    }
                }
            }
            IsConnecting = false;
            return false;
        }

        internal CookieContainer Login(ushort sid)
        {
            CookieContainer cc = null;
            if (!string.IsNullOrWhiteSpace(Settings.Default.Cookies))
                cc = (CookieContainer)ByteArrayToObject(Convert.FromBase64String(Settings.Default.Cookies));
            while (!TestCC(cc))
            {
                var lif = new UI.LogInForm(sid);
                if ((bool)lif.ShowDialog() && lif.receivedCookies.Count > 0)
                    cc = lif.receivedCookies;
                if (lif.receivedCookies.Count == 0)
                    Environment.Exit(0);
            }
            return cc;
        }

        private bool TestCC(CookieContainer cc)
        {
            if (cc == null)
                return false;
            CookieCollection ccs = cc.GetCookies(new Uri(FFXIVHunts.baseUrl));
            for (int i = 0; i < ccs.Count; i++)
                if (ccs[i].Name == UI.LogInForm.TwoFactorRememberMeCookieName)
                    return true;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(FFXIVHunts.VerifiedCharactersUrl);
            request.CookieContainer = cc;
            request.AllowAutoRedirect = false;
            bool result;
            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                    result = response.StatusCode == HttpStatusCode.OK;
            }
            catch (WebException we)
            {
                using (var response = (HttpWebResponse)we.Response)
                    result = response.StatusCode == HttpStatusCode.OK;
            }
            return result;
        }

        private static object ByteArrayToObject(byte[] arrBytes)
        {
            using (var memStream = new MemoryStream())
            {
                var binForm = new BinaryFormatter();
                memStream.Write(arrBytes, 0, arrBytes.Length);
                memStream.Seek(0, SeekOrigin.Begin);
                var obj = binForm.Deserialize(memStream);
                return obj;
            }
        }
    }
}
