using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
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
        public IAsyncResult BasicQueryAsync(string function, Dictionary<string, string> data, string failureResponse, AsyncCallback callback)
        {
            Query q = new Query(URL, function, data, failureResponse);
            Query.AsyncQueryDelegate caller = new Query.AsyncQueryDelegate(q.ExecuteQueryAsync);
            return caller.BeginInvoke(_client, out _, callback, caller);
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
                    return r;
                }
                r.Reply = _client.UploadString(url, "");
            }
            catch (WebException ex)
            {
                r.Reply = ex.Message;
                r.Success = false;
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
        public void SaveModel(Transform item, bool saveCollision, string dir, string modelName, bool writeTransform = false, bool applyOffset = false)
        {
            MeshFilter[] meshes = item.GetComponentsInChildren<MeshFilter>();
            if(writeTransform)
            {
                try
                {
                    File.WriteAllText(dir + "transform.txt", $"{item.name},\n{item.position.x},{item.position.y},{item.position.z},\n{item.rotation.x}," +
                        $"{item.rotation.y},{item.rotation.z},{item.rotation.w},\n{item.rotation.eulerAngles.x},{item.rotation.eulerAngles.y},{item.rotation.eulerAngles.z},\n{item.localScale.x},{item.localScale.y},{item.localScale.z}");
                } catch (Exception ex)
                {
                    IconSender.Log("Couldn't write transform info: \n" + ex.ToString(), "error");
                }
            }
            try
            {
                for (int m = 0; m < meshes.Length; m++)
                {

                    MeshFilter meshFilter = meshes[m];
                    if (meshFilter == null)
                    {
                        if (m == meshes.Length - 1)
                        {
                            IconSender.Log(item.name + " doesn't have a mesh.", "warning");
                            break;
                        }
                        else continue;
                    }
                    try
                    {
                        File.WriteAllText(dir + item.name.ToString() + "_" + m.ToString() + ".obj", ObjExporter.MeshToString(meshFilter, out List<Texture2D> textures, out string mtl, null, m, modelName, applyTransform: applyOffset, tOffset: item));
                        if (mtl != string.Empty)
                            File.WriteAllText(dir + "materials_" + m.ToString() + ".mtl", mtl);
                        if (textures != null)
                        {
                            for (int i = 0; i < textures.Count; i++)
                            {
                                WriteTexture(textures[i], dir + "T_" + modelName + "_" + i.ToString() + "_" + textures[0].name + "_" + m.ToString() + ".png");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        IconSender.Log(modelName + ".obj failed to write.", "error");
                        IconSender.Log(ex.ToString(), "error");
                    }
                }
            }
            catch (Exception ex)
            {
                IconSender.Log(modelName + ".obj failed to write.", "error");
                IconSender.Log(ex.ToString(), "error");
            }
            if (!saveCollision) return;
            MeshCollider[] colliders = item.GetComponentsInChildren<MeshCollider>();
            try
            {
                for (int m = 0; m < colliders.Length; m++)
                {
                    MeshCollider meshCollider = colliders[m];
                    if (meshCollider == null)
                    {
                        if (m == colliders.Length - 1)
                        {
                            IconSender.Log(item.name + " doesn't have a mesh collider.", "warning");
                            break;
                        }
                        else continue;
                    }
                    File.WriteAllText(dir + item.name.ToString() + "_" + m.ToString() + "_COLLISION.obj", ObjExporter.MeshToString(null, out _, out string mtl, null, m, modelName, meshCollider.sharedMesh, true));
                }
            }
            catch (Exception ex)
            {
                IconSender.Log(modelName + ".obj failed to write.", "error");
                IconSender.Log(ex.ToString(), "error");
            }
        }
        public void SaveAsset(Transform item, Asset asset, bool savingAll = false)
        {
            string basePath = string.Empty;
            if (asset is ItemAsset)
                basePath = ExtraIconInfo.ItemMeshesPath;
            else if (asset is VehicleAsset)
                basePath = ExtraIconInfo.VehicleMeshesPath;
            else if (asset is ObjectAsset)
                basePath = ExtraIconInfo.ObjectMeshesPath;
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
                        IconSender.Log(((ItemAsset)asset).itemName + " doesn't have a mesh.");
                    else if (asset is VehicleAsset)
                        IconSender.Log(((VehicleAsset)asset).vehicleName + " doesn't have a mesh.");
                    else if (asset is ObjectAsset)
                        IconSender.Log(((ObjectAsset)asset).objectName + " doesn't have a mesh.");
                    else
                        IconSender.Log(asset.name + " doesn't have a mesh.");
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
                            WriteTexture(textures[i], basePath + asset.id.ToString() + @"\" + ExtraIconInfo.TexName(asset, i, textures[i]), false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    IconSender.Log(asset.id + ".obj failed to write.", "error");
                    IconSender.Log(ex.ToString(), "error");
                }
                if (!savingAll)
                {
                    if (asset is ItemAsset)
                        ChatManager.say("Saved " + ((ItemAsset)asset).itemName, Color.red);
                    else if (asset is VehicleAsset)
                        ChatManager.say("Saved " + ((VehicleAsset)asset).vehicleName, Color.red);
                    else if (asset is ObjectAsset)
                        ChatManager.say("Saved " + ((ObjectAsset)asset).objectName, Color.red);
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
                                IconSender.Log(((ItemAsset)asset).itemName + " doesn't have a mesh.");
                            else if (asset is VehicleAsset)
                                IconSender.Log(((VehicleAsset)asset).vehicleName + " doesn't have a mesh.");
                            else if (asset is ObjectAsset)
                                IconSender.Log(((ObjectAsset)asset).objectName + " doesn't have a mesh.");
                            else
                                IconSender.Log(asset.name + " doesn't have a mesh.");
                            break;
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
                                WriteTexture(textures[i], basePath + asset.id.ToString() + @"\" + ExtraIconInfo.TexName(asset, i, textures[i], meshindex: m), false);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        IconSender.Log(asset.id + ".obj failed to write.", "error");
                        IconSender.Log(ex.ToString(), "error");
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
        public void WriteTexture(Texture2D texture, string path, bool destroyinput = true)
        {
            try
            {
                byte[] png = texture.EncodeToPNG();
                if (png == null)
                    IconSender.Log("png byte array is null of path: " + path, "info");
                else if (png.Length == 0)
                    IconSender.Log("png byte array is empty: " + texture.width.ToString() + ',' + texture.height.ToString() + " of path: " + path, "info");
                else
                    File.WriteAllBytes(path, png);
                if (destroyinput)
                    UnityEngine.Object.Destroy(texture);
            }
            catch (ArgumentException)
            {
                // Reading unreadable textures: https://fargesportfolio.com/unity-texture-texture2d-rendertexture/
                RenderTexture rt = RenderTexture.GetTemporary(texture.width, texture.height);
                Texture2D newText = new Texture2D(texture.width, texture.height, TextureFormat.ARGB32, false)
                {
                    name = texture.name
                };
                RenderTexture currentActiveRT = RenderTexture.active;
                RenderTexture.active = rt;
                Graphics.Blit(texture, rt);
                newText.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false);
                newText.Apply(false);
                RenderTexture.active = currentActiveRT;
                RenderTexture.ReleaseTemporary(rt);
                if(destroyinput)
                    UnityEngine.Object.Destroy(texture);
                byte[] png = newText.EncodeToPNG();
                if (png == null)
                    IconSender.Log("png byte array is null of path: " + path, "info");
                else if (png.Length == 0)
                    IconSender.Log("png byte array is empty: " + newText.width.ToString() + ',' + newText.height.ToString() + " of path: " + path, "info");
                else
                    File.WriteAllBytes(path, png);
                UnityEngine.Object.Destroy(newText);
            }
        } 
        public void WriteRenderTexture(RenderTexture texture, string path)
        {
            Texture2D newText = new Texture2D(texture.width, texture.height, TextureFormat.ARGB32, false)
            {
                name = texture.name
            };
            RenderTexture currentActiveRT = RenderTexture.active;
            RenderTexture.active = texture;
            newText.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0, false);
            newText.Apply(false);
            RenderTexture.active = currentActiveRT;
            byte[] png = newText.EncodeToPNG();
            if (png == null)
                IconSender.Log("png byte array is null of path: " + path, "info");
            else
                File.WriteAllBytes(path, png);
        }
        public void SaveTerrain(PlayerLook look)
        {
            if(TryGetFromLook(look, out Terrain terrain, RayMasks.GROUND | RayMasks.GROUND2))
            {
                string dir = ExtraIconInfo.TerrainPath + Level.level.name + '\\';
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                WriteRenderTexture(terrain.terrainData.heightmapTexture, dir + "heightmap.png");
                IconSender.Log("Wrote heightmap.");
                foreach(TerrainLayer layer in terrain.terrainData.terrainLayers)
                {
                    {
                        Texture tex = layer.diffuseTexture;
                        if (tex == null || tex.GetType() != typeof(Texture2D))
                        {
                            IconSender.Log((tex == null ? "null" : tex.name) + " was not of type Texture2D, type: " + (tex == null ? "null" : tex.GetType().Name) + ", diffuseTexture of " + layer.name);
                        } else
                        {
                            string name = tex.name + "_DIF";
                            if (File.Exists(dir + tex.name + "_DIF.png"))
                            {
                                int copyid = 1;
                                while (File.Exists(dir + tex.name + "_DIF" + '_' + copyid.ToString() + ".png"))
                                {
                                    copyid++;
                                }
                                name = tex.name + "_DIF" + '_' + copyid.ToString() + ".png";
                            }
                            WriteTexture((Texture2D)tex, dir + name + "_DIF.png");
                        }
                    }
                    {
                        Texture tex = layer.normalMapTexture;
                        if (tex == null || tex.GetType() != typeof(Texture2D))
                        {
                            IconSender.Log((tex == null ? "null" : tex.name) + " was not of type Texture2D, type: " + (tex == null ? "null" : tex.GetType().Name) + ", normalTexture of " + layer.name);
                        } else
                        {
                            string name = tex.name + "_N";
                            if (File.Exists(dir + tex.name + "_N.png"))
                            {
                                int copyid = 1;
                                while (File.Exists(dir + tex.name + "_N" + '_' + copyid.ToString() + ".png"))
                                {
                                    copyid++;
                                }
                                name = tex.name + "_N" + '_' + copyid.ToString() + ".png";
                            }
                            WriteTexture((Texture2D)tex, dir + name + ".png");
                        }
                    }
                    {
                        Texture tex = layer.maskMapTexture;
                        if (tex == null || tex.GetType() != typeof(Texture2D))
                        {
                            IconSender.Log((tex == null ? "null" : tex.name) + " was not of type Texture2D, type: " + (tex == null ? "null" : tex.GetType().Name) + ", maskMapTexture of " + layer.name);
                        } else
                        {
                            string name = tex.name + "_MASK";
                            if (File.Exists(dir + tex.name + "_MASK.png"))
                            {
                                int copyid = 1;
                                while (File.Exists(dir + tex.name + "_MASK" + '_' + copyid.ToString() + ".png"))
                                {
                                    copyid++;
                                }
                                name = tex.name + "_MASK" + '_' + copyid.ToString() + ".png";
                            }
                            WriteTexture((Texture2D)tex, dir + name + ".png");
                        }
                    }
                }
                IconSender.Log("Wrote all textures from layers.", "info");
            } else
            {
                IconSender.Log("Couldn't get terrain", "warning");
            }
            
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
        public bool TryGetFromLook<T>(PlayerLook look, out T output, int Raymask = -1) 
        {
            Ray ray = new Ray(look.aim.position, look.aim.forward);
            RaycastHit hit;
            if (Raymask == -1 ? Physics.Raycast(ray, out hit, 4f) : Physics.Raycast(ray, out hit, 4f, Raymask))
            {
                if (hit.transform.TryGetComponent(out output))
                {
                    return true;
                }
                else return false;
            } else
            {
                output = default;
                return false;
            }
        }
        internal void SaveLookNoAsset(PlayerLook look, bool playerInteractOnly, bool saveCollision, string name = "", bool useName = false)
        {
            if (TryGetFromLook(look, out Transform i, playerInteractOnly ? RayMasks.PLAYER_INTERACT : -1))
            {
                Component[] components = i.GetComponents<Component>();
                StringBuilder componentList = new StringBuilder();
                IconSender.Log("Reading " + i.name);
                for (int c = 0; c < components.Length; c++)
                {
                    if (c != 0) componentList.Append(", ");
                    componentList.Append(components[c].name + " : " + components[c].GetType().Name);
                }
                IconSender.Log("Read components: " + componentList.ToString());
                string basePath = ExtraIconInfo.OtherMeshesPath;
                string dir;
                int copyid = 0;
                string name2 = name;
                if (!useName) name2 = i.name;
                if (!Directory.Exists(basePath + name2 + @"\"))
                {
                    Directory.CreateDirectory(basePath + name2 + @"\");
                    dir = basePath + name2 + @"\";
                }
                else
                {
                    string checkdir = basePath + name2 + '_' + copyid.ToString() + @"\";
                    while (Directory.Exists(checkdir))
                    {
                        copyid++;
                        checkdir = basePath + name2 + '_' + copyid.ToString() + @"\";
                    }
                    Directory.CreateDirectory(checkdir);
                    dir = checkdir;
                }
                string modelName = name2 + (copyid != 0 ? '_' + copyid.ToString() : "");
                SaveModel(i, saveCollision, dir, modelName, writeTransform: true);
            }
        }
        internal void SaveLook(PlayerLook look, bool playerInteractOnly)
        {
            if(TryGetFromLook(look, out Transform i, playerInteractOnly ? RayMasks.PLAYER_INTERACT : -1))
            {
                Component[] components = i.GetComponents<Component>();
                StringBuilder componentList = new StringBuilder();
                IconSender.Log("Reading " + i.name);
                ushort ushortread = 0;
                for (int c = 0; c < components.Length; c++)
                {
                    if (c != 0) componentList.Append(", ");
                    componentList.Append(components[c].name + " : " + components[c].GetType().Name);
                    if(ushortread == 0 && ushort.TryParse(components[c].name, out ushort o))
                    {
                        ushortread = o;
                    } 
                }
                IconSender.Log("Read components: " + componentList.ToString());
                if(ushortread == 0)
                {
                    IconSender.Log("Failed to get ushort from found transform.");
                    return;
                } 
                try
                {
                    ObjectAsset asset = (ObjectAsset)Assets.find(EAssetType.OBJECT, ushortread);
                    if (asset != null)
                    {
                        SaveAsset(i, asset, true);
                        IconSender.Log("Saved item " + ushortread.ToString());
                        ChatManager.say(look.player.channel.owner.playerID.steamID, "Saved " + asset.objectName + ". Position: " + i.transform.position.ToString(), Color.white, false);
                    }
                } catch
                {
                    IconSender.Log("Failed to get object from found transform.");
                }
            } else
            {
                IconSender.Log("Failed to get object from player's look.");
            }
        }
    }
}
