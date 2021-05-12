using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using UnityEngine;

namespace IconSenderModule
{
    public class Query
    {
        public static readonly TimeSpan DefaultTimeout = new TimeSpan(0, 0, 5);
        public const string InvalidCallResponse = "INVALID CALL";
        public string URL;
        public string Parameters;
        public string FailureReply;
        private string function;
        public Query(string URL, string Parameters, string failureResponse)
        {
            this.URL = URL;
            this.Parameters = Parameters;
            this.FailureReply = failureResponse;
        }
        public Query(string URL, string function, Dictionary<string, string> Parameters, string failureResponse)
        {
            this.URL = URL;
            this.FailureReply = failureResponse;
            this.function = function;
            string p = "call=" + function;
            //build url
            for (int i = 0; i < Parameters.Count; i++)
            {
                p += "&";
                p += Uri.EscapeUriString(Parameters.Keys.ElementAt(i));
                p += "=";
                p += Uri.EscapeUriString(Parameters.Values.ElementAt(i));
            }
            this.Parameters = p;
        }
        public delegate void AsyncQueryDelegate(WebClientWithTimeout _client, out Response res);
        public void ExecuteQueryAsync(WebClientWithTimeout _client, out Response r)
        {
            while (_client.IsBusy)
                Thread.Sleep(1);
            try
            {
                string url = URL + '?' + Parameters;
                if (url.Length > 65519)
                {
                    r.Reply = "TOO LONG";
                    r.Success = false;
                    if(function != "sendLog")
                        IconSender.Log("Web Request Too long: \n" + Parameters.Substring(0, Parameters.Length > 200 ? 200 : Parameters.Length) + "...", "error");
                    return;
                }
                if (function != "sendLog")
                    IconSender.Log("Starting web request: \"" + url.Substring(0, url.Length > 200 ? 200 : url.Length) + '\"');
                r.Reply = _client.UploadString(url, "");
            }
            catch (WebException ex)
            {
                r.Reply = ex.Message;
                r.Success = false;
                if (function != "sendLog")
                    IconSender.Log("Web Request Error: " + ex.Message, "error");
                return;
            }
            r.Success = r.Reply != FailureReply && r.Reply != InvalidCallResponse;
        }
    }
    public class WebClientWithTimeout : WebClient
    {
        public TimeSpan Timeout = Query.DefaultTimeout;
        // https://stackoverflow.com/questions/1789627/how-to-change-the-timeout-on-a-net-webclient-object
        protected override WebRequest GetWebRequest(Uri address)
        {
            WebRequest w = base.GetWebRequest(address);
            w.Timeout = (int)Math.Round(Timeout.TotalMilliseconds);
            return w;
        }
    }
    public struct Response
    {
        public bool Success;
        public string Reply;

        public Response(bool Success, string Reply)
        {
            this.Success = Success;
            this.Reply = Reply;
        }
    }
    public class WebInterface : IDisposable
    {
        private WebClientWithTimeout _client;
        private const string URL = "http://localhost:8080/";
        public WebInterface()
        {
            _client = new WebClientWithTimeout();
            _client.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
        }
        public IAsyncResult PingAsync()
        {
            return BasicQueryAsync("ping", new Dictionary<string, string> { { "dt", DateTime.UtcNow.ToString("o") } },
                "No time provided.", new AsyncCallback(WebCallbacks.Ping));
        }
        public IAsyncResult PingAndSendAsync()
        {
            return BasicQueryAsync("ping", new Dictionary<string, string> { { "dt", DateTime.UtcNow.ToString("o") } },
                "No time provided.", new AsyncCallback(WebCallbacks.PingAndSend));
        }
        public IAsyncResult BasicQueryAsync(string function, Dictionary<string, string> data, string failureResponse, AsyncCallback callback)
        {
            Query q = new Query(URL, function, data, failureResponse);
            Query.AsyncQueryDelegate caller = new Query.AsyncQueryDelegate(q.ExecuteQueryAsync);
            return caller.BeginInvoke(_client, out _, callback, caller);
        }
        public void BasicQueryAsync(string data, string failureResponse, AsyncCallback callback)
        {
            Query q = new Query(URL, data, failureResponse);
            Query.AsyncQueryDelegate caller = new Query.AsyncQueryDelegate(q.ExecuteQueryAsync);
            caller.BeginInvoke(_client, out _, callback, caller);
        }
        public Response BasicQuerySync(string data, string failureResponse)
        {
            while (_client.IsBusy)
                Thread.Sleep(1);
            Response r = new Response();
            try
            {
                string url = URL + '?' + data;
                if (url.Length > 65519)
                {
                    r.Reply = "TOO LONG";
                    r.Success = false;
                    //IconSender.Log("Web Request Too long: \n" + data.Substring(0, 200) + "...", "error");
                    return r;
                }
                //IconSender.Log("Starting web request: \"" + url.Substring(0, url.Length > 200 ? 200 : url.Length) + '\"');
                r.Reply = _client.UploadString(url, "");
            }
            catch (WebException ex)
            {
                r.Reply = ex.Message;
                r.Success = false;
                //IconSender.Log("Web Request Error: " + ex.Message, "error");
                return r;
            }
            r.Success = r.Reply != failureResponse && r.Reply != Query.InvalidCallResponse;
            return r;
        }
        public Response BasicQuerySync(string function, Dictionary<string, string> data, string failureResponse)
        {
            string Parameters = "call=" + function;
            //build url
            for (int i = 0; i < data.Count; i++)
            {
                Parameters += "&";
                Parameters += Uri.EscapeUriString(data.Keys.ElementAt(i));
                Parameters += "=";
                Parameters += Uri.EscapeUriString(data.Values.ElementAt(i));
            }
            while (_client.IsBusy)
                Thread.Sleep(1);
            Response r = new Response();
            try
            {
                string url = URL + '?' + Parameters;
                if (url.Length > 65519)
                {
                    r.Reply = "TOO LONG";
                    r.Success = false;
                    //IconSender.Log("Web Request Too long: \n" + Parameters.Substring(0, Parameters.Length > 100 ? 100 : Parameters.Length) + "...", "error");
                    return r;
                }
                //IconSender.Log("Starting web request: \"" + url.Substring(0, url.Length > 200 ? 200 : url.Length) + '\"');
                r.Reply = _client.UploadString(url, "");
            }
            catch (WebException ex)
            {
                r.Reply = ex.Message;
                r.Success = false;
                //IconSender.Log("Web Request Error: " + ex.Message, "error");
                return r;
            }
            r.Success = r.Reply != failureResponse && r.Reply != Query.InvalidCallResponse;
            return r;
        }
        public void Dispose()
        {
            _client.Dispose();
            GC.SuppressFinalize(this);
        }
        public void SetHeldItem(ushort id)
        {
            if (Provider.clients.Count > 0)
            {
                SteamPlayer player = Provider.clients[0];
                player.player.inventory.tryAddItem(new Item(id, true), true, true);
                IconSender.Log("gave item " + id.ToString());
            }
        }
        public void SendHeldItemAsync()
        {
            try
            {
                if (Provider.clients.Count > 0)
                {
                    IconSender.Log("players found in clients");
                    SteamPlayer player = Provider.clients[0];
                    IconSender.Log("rendering");
                    ItemJar item = player.player.inventory.getItem(player.player.equipment.equippedPage, player.player.inventory.getIndex(player.player.equipment.equippedPage, player.player.equipment.equipped_x, player.player.equipment.equipped_y));
                    if(item != null)
                    {
                        IconSender.Log(item.item.id.ToString());
                        SaveItem(item);
                    }
                } else
                {
                    IconSender.Log("No players in Provider.clients");
                }
            } catch (Exception ex)
            {
                IconSender.Log(ex.ToString(), "error");
            }
        }
        public void SaveAllItems()
        {
            ChatManager.say("Starting save... this will take a few minutes.", UnityEngine.Color.green);
            List<ItemAsset> assets = new List<ItemAsset>(Assets.find(EAssetType.ITEM).Cast<ItemAsset>());
            assets.Sort(delegate (ItemAsset a, ItemAsset b) 
            {
                return a.id.CompareTo(b.id);
            });
            IconSender.I.lastAsset = assets[assets.Count - 1].id;
            foreach(ItemAsset asset in assets)
                SaveItem(asset, true);
        }
        public void SaveItem(ItemJar itemAsset, bool sendingAll = false)
        {
            if (itemAsset == null) return;
            ExtraIconInfo extraItemIconInfo = new ExtraIconInfo
            {
                asset = itemAsset.interactableItem.asset,
                sendingAll = sendingAll
            };
            ItemTool.getIcon(itemAsset.item.id, 0, 100, itemAsset.item.state, itemAsset.interactableItem.asset, null, string.Empty, string.Empty, itemAsset.size_x * 512, itemAsset.size_y * 512, false, true, new ItemIconReady(extraItemIconInfo.onItemIconReady));
            if (!sendingAll)
                ChatManager.say("Saved " + itemAsset.interactableItem.asset.itemName, UnityEngine.Color.red);
        }
        public void SaveItem(ItemAsset itemAsset, bool sendingAll = false)
        {
            if (itemAsset == null) return;
            ExtraIconInfo extraItemIconInfo = new ExtraIconInfo
            {
                asset = itemAsset,
                sendingAll = sendingAll
            };
            ItemTool.getIcon(itemAsset.id, 0, 100, itemAsset.getState(), itemAsset, null, string.Empty, string.Empty, itemAsset.size_x * 512, itemAsset.size_y * 512, false, true, new ItemIconReady(extraItemIconInfo.onItemIconReady));
            if(!sendingAll)
                ChatManager.say("Saved " + itemAsset.itemName, UnityEngine.Color.red);
        }
        public void SaveItemAsset(ItemAsset asset, bool savingAll = false)
        {
            Transform item = ItemTool.getItem(asset.id, 0, 100, asset.getState(), false, null);
            SaveAsset(item, asset, savingAll);
        }
        public void SaveVehicleAsset(VehicleAsset asset, bool savingAll = false)
        {
            Transform vehicle = VehicleTool.getVehicle(asset.id, 0, 0, asset, null);
            SaveAsset(vehicle, asset, savingAll);
        }
        public void SaveAsset(Transform item, Asset asset, bool savingAll = false)
        {
            string basePath = string.Empty;
            if (asset is ItemAsset)
                basePath = ExtraIconInfo.ItemMeshesPath;
            else if (asset is VehicleAsset)
                basePath = ExtraIconInfo.VehicleMeshesPath;
            else
                basePath = string.Format(ExtraIconInfo.FillInMeshesPath, asset.GetType().ToString());
            if (!Directory.Exists(basePath + asset.id.ToString() + @"\"))
                Directory.CreateDirectory(basePath + asset.id.ToString() + @"\");
            MeshFilter[] meshes = item.GetComponentsInChildren<MeshFilter>();
            MeshFilter mesh = null;
            if (meshes.Length == 1)
            {
                mesh = meshes[0];
                if (mesh == null)
                {
                    if (asset is ItemAsset)
                        Log(((ItemAsset)asset).itemName + " doesn't have a mesh.");
                    else if (asset is VehicleAsset)
                        Log(((VehicleAsset)asset).vehicleName + " doesn't have a mesh.");
                    else
                        Log(((VehicleAsset)asset).name + " doesn't have a mesh.");
                }
                try
                {
                    File.WriteAllText(basePath + asset.id.ToString() + @"\" + asset.id.ToString() + ".obj", ObjExporter.MeshToString(mesh, out List<Texture2D> textures, out string mtl, asset));
                    if (mtl != string.Empty)
                        File.WriteAllText(basePath + asset.id.ToString() + @"\materials.mtl", mtl);
                    if (textures != null)
                    {
                        for (int i = 0; i < textures.Count; i++)
                        {
                            try
                            {
                                File.WriteAllBytes(basePath + asset.id.ToString() + @"\" + ExtraIconInfo.TexName(asset, i, textures[i]), textures[i].EncodeToPNG());
                            }
                            catch (ArgumentException)
                            {
                                // Reading unreadable textures: https://fargesportfolio.com/unity-texture-texture2d-rendertexture/
                                RenderTexture rt = RenderTexture.GetTemporary(textures[i].width, textures[i].height);
                                Texture2D newText = new Texture2D(textures[i].width, textures[i].height, TextureFormat.ARGB32, false)
                                {
                                    name = textures[i].name
                                };
                                RenderTexture currentActiveRT = RenderTexture.active;
                                RenderTexture.active = rt;
                                Graphics.Blit(textures[i], rt);
                                newText.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false);
                                newText.Apply(false);
                                RenderTexture.active = currentActiveRT;
                                RenderTexture.ReleaseTemporary(rt);
                                UnityEngine.Object.Destroy(textures[i]);
                                byte[] png = newText.EncodeToPNG();
                                if (png == null)
                                    IconSender.Log("png byte array is null", "info");
                                else
                                    File.WriteAllBytes(basePath + asset.id.ToString() + @"\" + ExtraIconInfo.TexName(asset, i, newText), png);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log(asset.id + ".obj failed to write.", "error");
                    Log(ex.ToString(), "error");
                }
                if (!savingAll)
                {
                    if (asset is ItemAsset)
                        ChatManager.say("Saved " + ((ItemAsset)asset).itemName, Color.red);
                    else if (asset is VehicleAsset)
                        ChatManager.say("Saved " + ((VehicleAsset)asset).vehicleName, Color.red);
                    else
                        ChatManager.say("Saved " + ((VehicleAsset)asset).name, Color.red);
                }
            } else
            {
                for (int m = 0; m < meshes.Length; m++)
                {
                    MeshFilter meshFilter = meshes[m];
                    if (meshFilter == null)
                    {
                        if (m == meshes.Length - 1)
                        {
                            if (asset is ItemAsset)
                                Log(((ItemAsset)asset).itemName + " doesn't have a mesh.");
                            else if (asset is VehicleAsset)
                                Log(((VehicleAsset)asset).vehicleName + " doesn't have a mesh.");
                            else
                                Log(((VehicleAsset)asset).name + " doesn't have a mesh.");
                        }
                        else continue;
                    }
                    try
                    {
                        File.WriteAllText(basePath + asset.id.ToString() + @"\" + asset.id.ToString() + "_" + m.ToString() + ".obj", ObjExporter.MeshToString(meshFilter, out List<Texture2D> textures, out string mtl, asset, m));
                        if (mtl != string.Empty)
                            File.WriteAllText(basePath + asset.id.ToString() + @"\materials_" + m.ToString() + ".mtl", mtl);
                        if (textures != null)
                        {
                            for (int i = 0; i < textures.Count; i++)
                            {
                                try
                                {
                                    File.WriteAllBytes(basePath + asset.id.ToString() + @"\" + ExtraIconInfo.TexName(asset, i, textures[i]), textures[i].EncodeToPNG());
                                }
                                catch (ArgumentException)
                                {
                                    // Reading unreadable textures: https://fargesportfolio.com/unity-texture-texture2d-rendertexture/
                                    RenderTexture rt = RenderTexture.GetTemporary(textures[i].width, textures[i].height);
                                    Texture2D newText = new Texture2D(textures[i].width, textures[i].height, TextureFormat.ARGB32, false)
                                    {
                                        name = textures[i].name
                                    };
                                    RenderTexture currentActiveRT = RenderTexture.active;
                                    RenderTexture.active = rt;
                                    Graphics.Blit(textures[i], rt);
                                    newText.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false);
                                    newText.Apply(false);
                                    RenderTexture.active = currentActiveRT;
                                    RenderTexture.ReleaseTemporary(rt);
                                    UnityEngine.Object.Destroy(textures[i]);
                                    byte[] png = newText.EncodeToPNG();
                                    if (png == null)
                                        IconSender.Log("png byte array is null", "info");
                                    else
                                        File.WriteAllBytes(basePath + asset.id.ToString() + @"\" + ExtraIconInfo.TexName(asset, i, newText), png);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log(asset.id + ".obj failed to write.", "error");
                        Log(ex.ToString(), "error");
                    }
                }
                if (!savingAll)
                {
                    if (asset is ItemAsset)
                        ChatManager.say("Saved " + ((ItemAsset)asset).itemName, Color.red);
                    else if (asset is VehicleAsset)
                        ChatManager.say("Saved " + ((VehicleAsset)asset).vehicleName, Color.red);
                    else
                        ChatManager.say("Saved " + ((VehicleAsset)asset).name, Color.red);
                }
            }
        }

        public void SaveTerrain()
        {
            //LevelAsset asset = Level.getAsset();
            IEnumerable assets = Assets.find(EAssetType.OBJECT).Cast<ObjectAsset>();
        }
        public Dictionary<string, string> GetPlayerListParams()
        {
            string names = string.Empty;
            string ids = string.Empty;
            string teams = string.Empty;
            for (int i = 0; i < Provider.clients.Count; i++)
            {
                if (i != 0)
                {
                    names += ',';
                    ids += ',';
                    teams += ',';
                }
                names += Uri.EscapeUriString(Provider.clients[i].playerID.playerName);
                ids += Provider.clients[i].playerID.steamID.m_SteamID.ToString();
                teams += Provider.clients[i].player.quests.groupID.m_SteamID.ToString();
            }
            Dictionary<string, string> Parameters = new Dictionary<string, string> {
                { "server", "warfare" },
                { "names", "_" + names },
                { "ids", "_" + ids },
                { "teams", "_" + teams },
            };
            return Parameters;
        }
        public IAsyncResult SendPlayerListAsync()
        {
            return BasicQueryAsync("sendPlayerList", GetPlayerListParams(), "FAILURE", new AsyncCallback(WebCallbacks.SendPlayerList));
        }
        internal void Log(string info, string severity = "info")
        {
            try
            {
                BasicQuerySync("sendLog", new Dictionary<string, string> { { "log", Uri.EscapeUriString(info) }, { "severity", Uri.EscapeUriString(severity) } }, "FAILURE");
            } catch (Exception ex)
            {
                BasicQueryAsync("sendLog", new Dictionary<string, string> { { "log", Uri.EscapeUriString(ex.ToString()) }, { "severity", "error" } }, "FAILURE", new AsyncCallback(WebCallbacks.Log));
            }
        }
        internal void SaveVehicle(VehicleAsset asset)
        {
            if (asset == null) return;
            ExtraIconInfo extraItemIconInfo = new ExtraIconInfo();
            extraItemIconInfo.asset = asset;
            VehicleTool.getIcon(asset.id, 0, asset, null, 2048, 2048, true, new VehicleIconReady(extraItemIconInfo.onVehicleIconReady));
            ChatManager.say("Saved " + asset.vehicleName, UnityEngine.Color.red);
        }
    }

    public enum EResponseFromAsyncSocketEvent : byte
    {
        NO_DISCORD_ID_BAN,
        NO_STEAM_ID_BAN,
        NO_REASON_BAN,
        NO_ARGS_BAN,
        NO_REASON_OR_DISCORD_ID_BAN
    }
    public enum ECall : byte
    {
        SEND_PLAYER_LIST,
        SEND_PLAYER_JOINED,
        SEND_PLAYER_LEFT,
        GET_PLAYER_LIST,
        GET_USERNAME,
        PING_SERVER,
        SEND_PLAYER_LOCATION_DATA,
        PLAYER_KILLED,
        INVOKE_BAN,
        SEND_VEHICLE_DATA,
        SEND_ITEM_DATA,
        SEND_SKIN_DATA,
        REPORT_VEHICLE_ERROR,
        REPORT_ITEM_ERROR,
        REPORT_SKIN_ERROR
    }
}
