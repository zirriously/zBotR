using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace zBotR
{
    public class TwitchLookup
    {
        private string _clientID;
        public TwitchLookup(string clientID)
        {
            _clientID = clientID;
        }

        public Task<string> TwitchRequest(string url)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.Headers.Add("Client-ID", _clientID);
            req.Timeout = 2000;
            req.KeepAlive = true;

            var webResponse = (HttpWebResponse)req.GetResponse();

            if (webResponse.StatusCode == HttpStatusCode.OK)
            {
                var sr = new StreamReader(webResponse.GetResponseStream() ?? throw new InvalidOperationException());
                string strResponse = sr.ReadToEnd();
                sr.Close();
                return Task.FromResult(strResponse);
            }
            return null;
        }
    }
}