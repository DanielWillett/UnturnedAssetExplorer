using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace IconSenderModule
{
    public static class WebCallbacks
    {
        internal static void Ping(IAsyncResult ar)
        {
            (ar.AsyncState as Query.AsyncQueryDelegate).EndInvoke(out Response r, ar);
            SendPingData(r);
            ar.Dispose();
        }
        private static bool SendPingData(Response r)
        {
            if (r.Success)
            {
                IconSender.Log("Connected to NodeJS server successfully. Ping: " + r.Reply + "ms.");
                if (int.TryParse(r.Reply, out int ping) && ping > 300)
                    IconSender.Log(r.Reply + "ms seems a bit high, is the connection to the Node server okay?", "warning");
            }
            else
                IconSender.Log("Failed to ping NodeJS Server!", "error");
            return r.Success;
        }
        private static void Dispose(this IAsyncResult ar)
        {
            ar.AsyncWaitHandle.Close();
            ar.AsyncWaitHandle.Dispose();
        }
        private static void GetResponse(this IAsyncResult ar, out Response r)
        {
            (ar.AsyncState as Query.AsyncQueryDelegate).EndInvoke(out r, ar);
        }
        internal static void PingAndSend(IAsyncResult ar)
        {
            ar.GetResponse(out Response r);
            if (SendPingData(r))
            {
                IAsyncResult res = IconSender.I.Sender.SendPlayerListAsync();
                res.AsyncWaitHandle.WaitOne();
            }
            ar.Dispose();
        }
        internal static void SendPlayerList(IAsyncResult ar)
        {
            ar.GetResponse(out Response r);
            if (r.Success && true)
                IconSender.Log("Sent Player List to web server.");
            ar.Dispose();
        }

        internal static void Log(IAsyncResult ar)
        {
            ar.GetResponse(out Response r);
            ar.Dispose();
        }
    }
}
