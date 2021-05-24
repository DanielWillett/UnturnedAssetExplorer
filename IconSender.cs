using SDG.Framework.Modules;
using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IconSenderModule
{
    public class ExtraIconInfo
    {
        public Asset asset;
        public bool sendingAll = false;
        /// <summary>
        /// CHANGE ME
        /// </summary>
        public const string directoryBase = @"C:\Users\danny\OneDrive\Desktop\json\";
        public static readonly string ItemsPath = directoryBase + @"Items\";
        public static readonly string ItemMeshesPath = directoryBase + @"Meshes\Items\";
        public static readonly string VehicleMeshesPath = directoryBase + @"Meshes\Vehicles\";
        public static readonly string ObjectMeshesPath = directoryBase + @"Meshes\Objects\";
        public static readonly string OtherMeshesPath = directoryBase + @"Meshes\Other\";
        public static readonly string FillInMeshesPath = directoryBase + @"Meshes\{0}\";
        public static readonly string VehiclePath = directoryBase + @"Vehicles\";
        public static readonly string TerrainPath = directoryBase + @"Terrain\";
        public static readonly string MapPath = directoryBase + @"Map\";
        public static string TexName(Asset asset, int index, Texture texture, string ext = "png", int meshindex = -1)
        {
            return "T_" + asset.id.ToString() + "_" + index.ToString() + "_" + texture.name + (meshindex == -1 ? "" : "_" + meshindex.ToString()) + "." + ext;
        }
        public void onItemIconReady(Texture2D texture)
        {
            if (!Directory.Exists(ItemsPath))
                Directory.CreateDirectory(ItemsPath);
            try
            {
                ReadWrite.writeBytes(ItemsPath + asset.id.ToString() + ".png", false, false, texture.EncodeToPNG());
            } catch (IOException)
            {
                IconSender.Log("couldn't save " + asset.id.ToString() + ".png", "error");
            }
            UnityEngine.Object.Destroy(texture);
            if (sendingAll && IconSender.I.lastAsset == asset.id)
                ChatManager.say("Finished rendering icons.", UnityEngine.Color.green);
        }
        public void onVehicleIconReady(Texture2D texture)
        {
            if (!Directory.Exists(VehiclePath))
                Directory.CreateDirectory(VehiclePath);
            ReadWrite.writeBytes(VehiclePath + asset.id.ToString() + ".png", false, false, texture.EncodeToPNG());
            UnityEngine.Object.Destroy(texture);
        }
    }
    public class IconSender : IModuleNexus
    {
        public static IconSender I;
        public WebInterface Sender;
        public ushort lastAsset = 0;
        public void initialize()
        {
            try
            {
                if (!Directory.Exists(ExtraIconInfo.directoryBase))
                    Directory.CreateDirectory(ExtraIconInfo.directoryBase);
                if (!Directory.Exists(ExtraIconInfo.ItemsPath))
                    Directory.CreateDirectory(ExtraIconInfo.ItemsPath);
                if (!Directory.Exists(ExtraIconInfo.ItemMeshesPath))
                    Directory.CreateDirectory(ExtraIconInfo.ItemMeshesPath);
                if (!Directory.Exists(ExtraIconInfo.VehicleMeshesPath))
                    Directory.CreateDirectory(ExtraIconInfo.VehicleMeshesPath);
                if (!Directory.Exists(ExtraIconInfo.ObjectMeshesPath))
                    Directory.CreateDirectory(ExtraIconInfo.ObjectMeshesPath);
                if (!Directory.Exists(ExtraIconInfo.OtherMeshesPath))
                    Directory.CreateDirectory(ExtraIconInfo.OtherMeshesPath);
                if (!Directory.Exists(ExtraIconInfo.FillInMeshesPath))
                    Directory.CreateDirectory(ExtraIconInfo.FillInMeshesPath);
                if (!Directory.Exists(ExtraIconInfo.VehiclePath))
                    Directory.CreateDirectory(ExtraIconInfo.VehiclePath);
                if (!Directory.Exists(ExtraIconInfo.TerrainPath))
                    Directory.CreateDirectory(ExtraIconInfo.TerrainPath);
                if (!Directory.Exists(ExtraIconInfo.MapPath))
                    Directory.CreateDirectory(ExtraIconInfo.MapPath);
                I = this;
                Level.onLevelLoaded += LoadedLevel;
                Sender = new WebInterface();
                IAsyncResult r = Sender.PingAsync();
                r.AsyncWaitHandle.WaitOne();
                this.Log("Initializing");
                ChatManager.onChatted += ChatProcess;
                this.Log("Initialized");
            } catch (Exception ex)
            {
                this.Log(ex.ToString(), "error");
            }
        }

        private void ChatProcess(SteamPlayer player, EChatMode mode, ref Color chatted, ref bool isRich, string text, ref bool isVisible)
        {
            this.Log("[" + player.playerID.playerName + "]: \"" + text + "\"", "chat");
            try
            {
                if (text.Length <= 1) return;
                if (text.StartsWith("-"))
                {
                    isVisible = false;
                    string cmd = text.Substring(1);
                    if (cmd.StartsWith("save"))
                    {
                        string[] command = text.Split(' ');
                        if (cmd.StartsWith("saveallitems"))
                        {
                            Sender.SaveAllItems();
                            this.Log("tried to save all items.");
                        }
                        else if (cmd.StartsWith("savelook1"))
                        {
                            Sender.SaveLook(player.player.look, false);
                        }
                        else if (cmd.StartsWith("savelook2"))
                        {
                            Sender.SaveLook(player.player.look, true);
                        }
                        else if (cmd.StartsWith("savemodel1"))
                        {
                            string name = "";
                            if (command.Length >= 1) name = string.Join("_", command, 1, command.Length - 1);
                            Sender.SaveLookNoAsset(player.player.look, false, false, name, name != "");
                        }
                        else if (cmd.StartsWith("savemodel2"))
                        {
                            string name = "";
                            if (command.Length >= 1) name = string.Join("_", command, 1, command.Length - 1);
                            Sender.SaveLookNoAsset(player.player.look, true, false, name, name != "");
                        }
                        else if (cmd.StartsWith("savemodel1c"))
                        {
                            string name = "";
                            if (command.Length >= 1) name = string.Join("_", command, 1, command.Length - 1);
                            Sender.SaveLookNoAsset(player.player.look, false, true, name, name != "");
                        }
                        else if (cmd.StartsWith("savemodel2c"))
                        {
                            string name = "";
                            if (command.Length >= 1) name = string.Join("_", command, 1, command.Length - 1);
                            Sender.SaveLookNoAsset(player.player.look, true, true, name, name != "");
                        }
                        else if (cmd.StartsWith("saveterrain"))
                        {
                            Sender.SaveTerrain(player.player.look);
                        }
                        else if (command.Length >= 2 && ushort.TryParse(command[1], out ushort id))
                        {
                            if (cmd.StartsWith("saveitemmodel"))
                            {
                                ItemAsset asset = (ItemAsset)Assets.find(EAssetType.ITEM, id);
                                if (asset != null)
                                    Sender.SaveItemAsset(asset);
                                else
                                    this.Log("count find asset", "warning");
                                this.Log("tried to save item");
                            }
                            else if (cmd.StartsWith("saveitem"))
                            {
                                ItemAsset asset = (ItemAsset)Assets.find(EAssetType.ITEM, id);
                                if (asset != null)
                                    Sender.SaveItem(asset);
                                else
                                    this.Log("count find asset", "warning");
                                this.Log("tried to save item");
                            }
                            else if (cmd.StartsWith("savevehiclemodel"))
                            {
                                VehicleAsset asset = (VehicleAsset)Assets.find(EAssetType.VEHICLE, id);
                                if (asset != null)
                                    Sender.SaveVehicleAsset(asset);
                                else
                                    this.Log("count find asset", "warning");
                                this.Log("tried to save item");
                            }
                            else if (cmd.StartsWith("savevehicle"))
                            {
                                VehicleAsset asset = (VehicleAsset)Assets.find(EAssetType.VEHICLE, id);
                                if (asset != null)
                                    Sender.SaveVehicle(asset);
                                else
                                    this.Log("count find asset", "warning");
                                this.Log("tried to save vehicle");
                            }
                            else if (cmd.StartsWith("saveallinrange"))
                            {
                                string name = "unnamed_export";
                                if (command.Length > 2) name = string.Join(" ", command, 2, command.Length - 2);
                                GameObject[] objs = UnityEngine.Object.FindObjectsOfType<GameObject>();
                                IEnumerable<GameObject> objsWithModels = objs.Where(x => x.transform.TryGetComponent<MeshFilter>(out _) && (x.transform.position - player.player.transform.position).sqrMagnitude <= id * id);
                                IEnumerator<GameObject> e = objsWithModels.GetEnumerator();
                                List<GameObject> objsNoDup = new List<GameObject>();
                                this.Log(objsWithModels.Count().ToString() + " assets found.");
                                while (e.MoveNext())
                                    if (!objsNoDup.Contains(e.Current)) objsNoDup.Add(e.Current);
                                this.Log(objsNoDup.Count().ToString() + " found nonduplicates.");
                                int i = 0;
                                string basePath = ExtraIconInfo.OtherMeshesPath;
                                string dir;
                                int copyid = 0;
                                if (!Directory.Exists(basePath + name + @"\"))
                                {
                                    Directory.CreateDirectory(basePath + name + @"\");
                                    dir = basePath + name + @"\";
                                }
                                else
                                {
                                    string checkdir = basePath + name + '_' + copyid.ToString() + @"\";
                                    while (Directory.Exists(checkdir))
                                    {
                                        copyid++;
                                        checkdir = basePath + name + '_' + copyid.ToString() + @"\";
                                    }
                                    Directory.CreateDirectory(checkdir);
                                    dir = checkdir;
                                }
                                foreach (GameObject obj in objsNoDup)
                                {
                                    this.Log("Saving \"" + obj.name + "\". - " + i.ToString() + '/' + objsNoDup.Count);
                                    int fileid = 0;
                                    string modelName;
                                    if (!File.Exists(dir + obj.transform.name))
                                    {
                                        modelName = obj.transform.name;
                                    }
                                    else
                                    {
                                        string filename = obj.transform.name + '_' + fileid.ToString();
                                        while (File.Exists(dir + filename))
                                        {
                                            fileid++;
                                            filename = obj.transform.name + '_' + fileid.ToString();
                                        }
                                        modelName = filename;
                                    }
                                    i++;
                                    Sender.SaveModel(obj.transform, false, dir, modelName, true, true);
                                }

                                this.Log(objsNoDup.Count().ToString() + " saved assets.");
                            }
                        }
                        else
                        {
                            this.Log("not good args");
                        }
                    }
                    else if (cmd.StartsWith("give"))
                    {
                        string[] command = text.Split(' ');
                        if (command.Length == 2 && ushort.TryParse(command[1], out ushort id))
                        {
                            player.player.inventory.tryAddItem(new Item(id, true), true, true);
                            this.Log("gave item " + id.ToString());
                        }
                        else
                        {
                            this.Log("not good args");
                        }
                    }
                }
            } catch (Exception ex)
            {
                this.Log(ex.ToString(), "error");
            }
            
        }

        private void LoadedLevel(int level)
        {
            this.Log("Loaded level " + level.ToString());
        }
        public void shutdown()
        {
            this.Log("shutdown");
            ChatManager.onChatted -= ChatProcess;
            Level.onLevelLoaded -= LoadedLevel;
            Sender?.Dispose();
        }

        /*
         *  I USE A WEB SERVER AS A LOG FOR THIS THAT I ALREADY HAD SET UP.
         *  THERE IS ALSO A TEXT FILE LOG
         */

        public void Log(string info, string severity = "info", bool dummy = false)
        {
            Sender?.Log(info, severity);
            Debug.Log(info);
            TextWriter Writer = File.CreateText(ExtraIconInfo.directoryBase + "log.txt");
            Writer.WriteLine($"[{severity.ToUpper()}] {info}");
            Writer.Close();
            Writer.Dispose();
        }
        public static void Log(string info, string severity = "info") => I?.Log(info, severity);
    }
}
