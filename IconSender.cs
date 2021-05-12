using SDG.Framework.Modules;
using SDG.Unturned;
using System;
using System.IO;
using UnityEngine;

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
        public readonly string ItemsPath = directoryBase + @"Items\";
        public readonly string ItemMeshesPath = directoryBase + @"Meshes\Items\";
        public readonly string VehicleMeshesPath = directoryBase + @"Meshes\Vehicles\";
        public readonly string FillInMeshesPath = directoryBase + @"Meshes\{0}\";
        public readonly string VehiclePath = directoryBase + @"Vehicles\";
        public readonly string MapPath = directoryBase + @"Map\";
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
        public WebInterface IWeb;
        public StreamWriter Writer;
        public ushort lastAsset = 0;
        public void initialize()
        {
            try
            {
                if (!Directory.Exists(ExtraIconInfo.directoryBase))
                    Directory.CreateDirectory(ExtraIconInfo.directoryBase);
                Writer = File.CreateText(ExtraIconInfo.directoryBase);
                I = this;
                Level.onLevelLoaded += LoadedLevel;
                this.Log("Initializing");
                IWeb = new WebInterface();
                IAsyncResult r = IWeb.PingAndSendAsync();
                r.AsyncWaitHandle.WaitOne();
                this.Log("Initialized");
                ChatManager.onChatted += ChatProcess;
            } catch (Exception ex)
            {
                this.Log(ex.ToString(), "error");
            }
        }

        private void ChatProcess(SteamPlayer player, EChatMode mode, ref Color chatted, ref bool isRich, string text, ref bool isVisible)
        {
            this.Log("[" + player.playerID.playerName + "]: \"" + text + "\"", "chat");
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
                        IWeb.SaveAllItems();
                        this.Log("tried to save all items.");
                    }
                    if (command.Length == 2 && ushort.TryParse(command[1], out ushort id))
                    {
                        if(cmd.StartsWith("saveitemmodel"))
                        {
                            ItemAsset asset = (ItemAsset)Assets.find(EAssetType.ITEM, id);
                            if (asset != null)
                                IWeb.SaveItemAsset(asset);
                            else
                                this.Log("count find asset", "warning");
                            this.Log("tried to save item");
                        } else if(cmd.StartsWith("saveitem"))
                        {
                            ItemAsset asset = (ItemAsset)Assets.find(EAssetType.ITEM, id);
                            if (asset != null)
                                IWeb.SaveItem(asset);
                            else
                                this.Log("count find asset", "warning");
                            this.Log("tried to save item");
                        } else if (cmd.StartsWith("savevehiclemodel"))
                        {
                            VehicleAsset asset = (VehicleAsset)Assets.find(EAssetType.VEHICLE, id);
                            if (asset != null)
                                IWeb.SaveVehicleAsset(asset);
                            else
                                this.Log("count find asset", "warning");
                            this.Log("tried to save item");
                        } else if (cmd.StartsWith("savevehicle"))
                        {
                            VehicleAsset asset = (VehicleAsset)Assets.find(EAssetType.VEHICLE, id);
                            if (asset != null)
                                IWeb.SaveVehicle(asset);
                            else
                                this.Log("count find asset", "warning");
                            this.Log("tried to save vehicle");
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
            IWeb?.Dispose();
            Writer?.Close();
        }

        /*
         *  I USE A WEB SERVER AS A LOG FOR THIS THAT I ALREADY HAD SET UP.
         *  THERE IS ALSO A TEXT FILE LOG BUT IS NOT READABLE UNTIL SHUTDOWN
         */

        public void Log(string info, string severity = "info", bool dummy = false)
        {
            IWeb?.Log(info, severity);
            Debug.Log(info);
            Writer?.WriteLine($"[{severity.ToUpper()}] {info}");
        }
        public static void Log(string info, string severity = "info") => I?.Log(info, severity);
    }
}
