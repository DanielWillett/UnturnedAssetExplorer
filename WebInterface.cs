using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace IconSenderModule
{
    public class WebInterface
    {
        public WebInterface()
        {
            //_client = new WebClientWithTimeout();
            //_client.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
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
            if (!sendingAll || !File.Exists(ExtraIconInfo.ItemsPath + itemAsset.id.ToString() + ".png"))
            {
                ItemTool.getIcon(itemAsset.id, 0, 100, itemAsset.getState(), itemAsset, null, string.Empty, string.Empty, itemAsset.size_x * 512, itemAsset.size_y * 512, false, true, new ItemIconReady(extraItemIconInfo.onItemIconReady));
                if (!sendingAll)
                    ChatManager.say("Saved " + itemAsset.itemName, Color.red);
            }
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
            string basePath;
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
                    if (asset is ItemAsset asset2)
                        IconSender.Log(asset2.itemName + " doesn't have a mesh.");
                    else if (asset is VehicleAsset asset1)
                        IconSender.Log(asset1.vehicleName + " doesn't have a mesh.");
                    else if (asset is ObjectAsset asset3)
                        IconSender.Log(asset3.objectName + " doesn't have a mesh.");
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
                    if (asset is ItemAsset asset1)
                        ChatManager.say("Saved " + asset1.itemName, Color.red);
                    else if (asset is VehicleAsset asset2)
                        ChatManager.say("Saved " + asset2.vehicleName, Color.red);
                    else if (asset is ObjectAsset asset3)
                        ChatManager.say("Saved " + asset3.objectName, Color.red);
                    else
                        ChatManager.say("Saved " + asset.name, Color.red);
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
                            if (asset is ItemAsset asset1)
                                IconSender.Log(asset1.itemName + " doesn't have a mesh.");
                            else if (asset is VehicleAsset asset2)
                                IconSender.Log(asset2.vehicleName + " doesn't have a mesh.");
                            else if (asset is ObjectAsset asset3)
                                IconSender.Log(asset3.objectName + " doesn't have a mesh.");
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
                    if (asset is ItemAsset asset1)
                        ChatManager.say("Saved " + asset1.itemName, Color.red);
                    else if (asset is VehicleAsset asset2)
                        ChatManager.say("Saved " + asset2.vehicleName, Color.red);
                    else
                        ChatManager.say("Saved " + asset.name, Color.red);
                }
            }
        }
        public void WriteTexture(Texture2D texture, string path, bool destroyinput = false)
        {
            byte[] png;
            try
            {
                png = texture.EncodeToPNG();
                if (png == null)
                {
                    IconSender.Log("png byte array is null of path: " + path, "info");
                    goto rt;
                }
                else if (png.Length == 0)
                {
                    IconSender.Log("png byte array is empty: " + texture.width.ToString() + ',' + texture.height.ToString() + " of path: " + path, "info");
                    goto rt;
                }
                else
                    File.WriteAllBytes(path, png);
                if (destroyinput)
                    UnityEngine.Object.Destroy(texture);
                return;
            }
            catch (ArgumentException)
            {
                goto rt;
            }
            rt:
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
            if (destroyinput)
                UnityEngine.Object.Destroy(texture);
            png = newText.EncodeToPNG();
            if (png == null)
                IconSender.Log("png byte array is null of path: " + path, "info");
            else if (png.Length == 0)
                IconSender.Log("png byte array is empty: " + newText.width.ToString() + ',' + newText.height.ToString() + " of path: " + path, "info");
            else
                File.WriteAllBytes(path, png);
            UnityEngine.Object.Destroy(newText);
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
            string dir = ExtraIconInfo.TerrainPath + Level.level.name + '\\';
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            if (LevelGround.terrain != null)
                WriteRenderTexture(LevelGround.terrain.terrainData.heightmapTexture, dir + "heightmap_primary.png");
            if (LevelGround.terrain2 != null)
                WriteRenderTexture(LevelGround.terrain2.terrainData.heightmapTexture, dir + "heightmap_secondary.png");
            foreach (GroundMaterial material in LevelGround._materials)
            {
                if (material != null && material.layer != null)
                {
                    Texture2D texture = material.layer.diffuseTexture;
                    if (texture != null)
                    {
                        string name = texture.name + "_DIF";
                        if (File.Exists(dir + texture.name + "_DIF.png"))
                        {
                            int copyid = 1;
                            while (File.Exists(dir + texture.name + "_DIF" + '_' + copyid.ToString() + ".png"))
                            {
                                copyid++;
                            }
                            name = texture.name + "_DIF" + '_' + copyid.ToString() + ".png";
                        }
                        WriteTexture(texture, dir + name + "_DIF.png");
                    }
                    texture = material.layer.normalMapTexture;
                    if (texture != null)
                    {
                        string name = texture.name + "_N";
                        if (File.Exists(dir + texture.name + "_N.png"))
                        {
                            int copyid = 1;
                            while (File.Exists(dir + texture.name + "_N" + '_' + copyid.ToString() + ".png"))
                            {
                                copyid++;
                            }
                            name = texture.name + "_N" + '_' + copyid.ToString() + ".png";
                        }
                        WriteTexture(texture, dir + name + "_N.png");
                    }
                    texture = material.layer.maskMapTexture;
                    if (texture != null)
                    {
                        string name = texture.name + "_MASK";
                        if (File.Exists(dir + texture.name + "_MASK.png"))
                        {
                            int copyid = 1;
                            while (File.Exists(dir + texture.name + "_MASK" + '_' + copyid.ToString() + ".png"))
                            {
                                copyid++;
                            }
                            name = texture.name + "_MASK" + '_' + copyid.ToString() + ".png";
                        }
                        WriteTexture(texture, dir + name + "_MASK.png");
                    }
                }
            }
            if(TryGetFromLook(look, out Terrain terrain, RayMasks.GROUND | RayMasks.GROUND2))
            {
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
        public void SaveAllAttachments()
        {
            try
            {
                Asset[] assets = Assets.find(EAssetType.ITEM);
                List<ItemGripAsset> grips = new List<ItemGripAsset>() { null };
                List<ItemBarrelAsset> barrels = new List<ItemBarrelAsset>() { null };
                List<ItemMagazineAsset> magazines = new List<ItemMagazineAsset>() { null };
                List<ItemSightAsset> sights = new List<ItemSightAsset>() { null };
                List<ItemTacticalAsset> tacticals = new List<ItemTacticalAsset>() { null };
                List<ItemGunAsset> guns = new List<ItemGunAsset>();
                for (int i = 0; i < assets.Length; i++)
                {
                    Asset asset = assets[i];
                    if (asset is ItemGunAsset gun)
                        guns.Add(gun);
                    else if (asset is ItemGripAsset grip)
                        grips.Add(grip);
                    else if (asset is ItemBarrelAsset barrel)
                        barrels.Add(barrel);
                    else if (asset is ItemMagazineAsset magazine)
                        magazines.Add(magazine);
                    else if (asset is ItemSightAsset sight)
                        sights.Add(sight);
                    else if (asset is ItemTacticalAsset tactical)
                        tacticals.Add(tactical);
                }
                IconSender.Log($"{assets.Length} assets: {grips.Count} grips, {barrels.Count} barrels, {magazines.Count} magazines, {sights.Count} sights, {tacticals.Count} tacticals, {guns.Count} guns.");
                this.total = 0;
                this.totalRendered = 0;
                this.start = DateTime.Now;
                for (int i = 0; i < guns.Count; i++)
                {
                    ItemGunAsset gun = guns[i];
                    for (int s = 0; s < sights.Count; s++)
                    {
                        ItemSightAsset sight = sights[s];
                        if (IsAttachmentValid(sight, gun))
                        {
                            for (int g = 0; g < grips.Count; g++)
                            {
                                ItemGripAsset grip = grips[g];
                                if (IsAttachmentValid(grip, gun))
                                {
                                    for (int b = 0; b < barrels.Count; b++)
                                    {
                                        ItemBarrelAsset barrel = barrels[b];
                                        if (IsAttachmentValid(barrel, gun))
                                        {
                                            for (int m = 0; m < magazines.Count; m++)
                                            {
                                                ItemMagazineAsset magazine = magazines[m];
                                                if (IsAttachmentValid(magazine, gun))
                                                {
                                                    for (int t = 0; t < tacticals.Count; t++)
                                                    {
                                                        ItemTacticalAsset tactical = tacticals[t];
                                                        if (IsAttachmentValid(tactical, gun))
                                                        {
                                                            total++;
                                                            byte[] state = gun.getState(true);
                                                            if (sight != null)
                                                                Buffer.BlockCopy(BitConverter.GetBytes(sight.id), 0, state, 0, 2);
                                                            else
                                                            {
                                                                state[0] = 0;
                                                                state[1] = 0;
                                                            }
                                                            if (tactical != null)
                                                                Buffer.BlockCopy(BitConverter.GetBytes(tactical.id), 0, state, 2, 2);
                                                            else
                                                            {
                                                                state[2] = 0;
                                                                state[3] = 0;
                                                            }
                                                            if (grip != null)
                                                                Buffer.BlockCopy(BitConverter.GetBytes(grip.id), 0, state, 4, 2);
                                                            else
                                                            {
                                                                state[4] = 0;
                                                                state[5] = 0;
                                                            }
                                                            if (barrel != null)
                                                                Buffer.BlockCopy(BitConverter.GetBytes(barrel.id), 0, state, 6, 2);
                                                            else
                                                            {
                                                                state[6] = 0;
                                                                state[7] = 0;
                                                            }
                                                            if (magazine != null)
                                                                Buffer.BlockCopy(BitConverter.GetBytes(magazine.id), 0, state, 8, 2);
                                                            else
                                                            {
                                                                state[8] = 0;
                                                                state[9] = 0;
                                                            }
                                                            state[10] = magazine == null ? (byte)0 : magazine.amount;
                                                            state[11] = 2;
                                                            state[12] = 100;
                                                            state[13] = 100;
                                                            state[14] = 100;
                                                            state[15] = 100;
                                                            state[16] = 100;
                                                            string path = SAVELOC + $"{gun.id}\\{BitConverter.ToUInt16(state, 0)}_{BitConverter.ToUInt16(state, 2)}_{BitConverter.ToUInt16(state, 4)}_{BitConverter.ToUInt16(state, 6)}_{BitConverter.ToUInt16(state, 8)}.png";
                                                            if (!File.Exists(path))
                                                                ItemTool.getIcon(gun.id, 0, gun.qualityMax, state, gun,
                                                                    null, string.Empty, string.Empty,
                                                                    gun.size_x * 128, gun.size_y * 128, false, true,
                                                                    (txt) => OnReady(gun.id, state, txt));
                                                            else --total;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                IconSender.Log(ex.ToString(), "ERROR");
            }
        }
        private bool IsAttachmentValid(ItemCaliberAsset attachment, ItemGunAsset gun)
        {
            if (attachment == null) return true;
            if (attachment.type == EItemType.SIGHT && !gun.hasSight) return false;
            if (attachment.type == EItemType.GRIP && !gun.hasGrip) return false;
            if (attachment.type == EItemType.BARREL && !gun.hasBarrel) return false;
            if (attachment.type == EItemType.TACTICAL && !gun.hasTactical) return false;
            if (attachment.calibers.Length == 0) return true;
            ushort[] calibers = attachment.type == EItemType.MAGAZINE ? gun.magazineCalibers : gun.attachmentCalibers;
            for (int i = 0; i < attachment.calibers.Length; ++i)
            {
                for (int j = 0; j < calibers.Length; ++j)
                {
                    if (attachment.calibers[i] == calibers[j])
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        const string SAVELOC = @"C:\UnturnedExport\Attachments\";
        private int totalRendered = 0;
        private int total = 0;
        private DateTime start;
        private void OnReady(ushort itemID, byte[] state, Texture2D texture)
        {
            if (!Directory.Exists(SAVELOC + $"{itemID}\\"))
                Directory.CreateDirectory(SAVELOC + $"{itemID}\\");
            WriteTexture(texture, SAVELOC + $"{itemID}\\{BitConverter.ToUInt16(state, 0)}_{BitConverter.ToUInt16(state, 2)}_{BitConverter.ToUInt16(state, 4)}_{BitConverter.ToUInt16(state, 6)}_{BitConverter.ToUInt16(state, 8)}.png", true);
            UnityEngine.Object.Destroy(texture);
            totalRendered++;
            if (totalRendered >= total - 1)
            {
                IconSender.Log($"{totalRendered} / {total}");
                IconSender.Log("Done with " + total.ToString() + " renders.");
            }
            else if (totalRendered % 100 == 0)
            {
                TimeSpan estTimeRemaining = TimeSpan.FromSeconds((DateTime.Now - start).TotalSeconds / (totalRendered / (float)total));
                IconSender.Log($"({itemID}) -> {totalRendered} / {total} -> {(totalRendered * 100f / total):N4}% -> [~{estTimeRemaining.TotalDays:N0}:{estTimeRemaining.Hours:N0}:{estTimeRemaining.Minutes:N0} remaining]");
            }
        }
        public static ushort awaitingSkin = 0;
        public static void SetHeldSkin(PlayerEquipment equipment, ushort skin)
        {
            awaitingSkin = skin;
            ushort oldid = equipment.asset.sharedSkinLookupID;
            bool set = false;
            if (!equipment.channel.owner.itemSkins.ContainsKey(oldid))
            {
                set = true;
                equipment.channel.owner.itemSkins.Add(oldid, -1);
            }
            equipment.ReceiveEquip(equipment.equippedPage, equipment.equipped_x, equipment.equipped_y, equipment.asset.GUID, 100, equipment.state, equipment.useable.GetNetId());
            awaitingSkin = 0;
            if (set)
            {
                equipment.channel.owner.itemSkins.Remove(oldid);
            }
        }
        public static void ResetHeldSkin(PlayerEquipment equipment)
        {
            awaitingSkin = 0;
            equipment.ReceiveEquip(equipment.equippedPage, equipment.equipped_x, equipment.equipped_y, equipment.asset.GUID, 100, equipment.state, equipment.useable.GetNetId());
        }

        public static void LoopContentBundles()
        {
            List<string> children = new List<string>();
            foreach (KeyValuePair<string, RootContentDirectory> kvp in Assets.rootContentDirectories)
            {
                IconSender.Log("Root Directory: " + kvp.Value.name);
                FieldInfo info =
                    typeof(RootContentDirectory).GetField("assetBundle",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                if (info.GetValue(kvp.Value) is AssetBundle bundle)
                {
                    string[] files = bundle.GetAllAssetNames();
                    IconSender.Log("Files: " + files.Length);
                    for (int i = 0; i < files.Length; i++)
                        IconSender.Log(files[i]);
                }
                Recurse(kvp.Value, children, kvp.Value);
                children.Clear();
            }
        }
        static void Recurse(ContentDirectory dir, List<string> children, RootContentDirectory root)
        {
            bool isRoot = dir == root;
            int index = children.Count;
            if (!isRoot) children.Add(dir.name);
            string p = root.name + "\\";
            if (!isRoot) p += string.Join("\\", children) + "\\";
            string[] paths = new string[4]
            {
                ExtraIconInfo.directoryBase + $"Content\\{p}Textures\\",
                ExtraIconInfo.directoryBase + $"Content\\{p}Meshes\\",
                ExtraIconInfo.directoryBase + $"Content\\{p}Materials\\",
                ExtraIconInfo.directoryBase + $"Content\\{p}"
            };
            if (!Directory.Exists(paths[3]))
                Directory.CreateDirectory(paths[3]);

            IconSender.Log(dir.name);
            string p2 = string.Join("/", children) + "/";
            foreach (ContentFile file in dir.files)
            {
                string p3 = p2 + file.name + Path.GetExtension(file.file);
                IconSender.Log(p3);
                if (file.guessedType == typeof(Texture2D))
                {
                    Texture2D t2d = root.loadAsset<Texture2D>(p3);
                    if (t2d == null)
                    {
                        IconSender.Log("Unable to export file " + p3 + " as Texture2D");
                        continue;
                    }
                    if (!Directory.Exists(paths[0]))
                        Directory.CreateDirectory(paths[0]);
                    string ext = ".png";
                    string name = Path.GetFileNameWithoutExtension(file.file);
                    string path = paths[0] + name;
                    int addon = -1;
                    while (File.Exists(path + (addon == -1 ? string.Empty : "_" + addon.ToString()) + ext))
                    {
                        addon++;
                    }

                    path = path + (addon == -1 ? string.Empty : "_" + addon.ToString()) + ext;

                    IconSender.I.Sender.WriteTexture(t2d, path);
                }
                else if (file.guessedType == typeof(Mesh))
                {
                    Mesh mesh = root.loadAsset<Mesh>(p3);
                    if (mesh == null)
                    {
                        IconSender.Log("Unable to export file " + p3 + " as Mesh");
                        continue;
                    }
                    if (!Directory.Exists(paths[1]))
                        Directory.CreateDirectory(paths[1]);
                    string ext = ".obj";
                    string name = Path.GetFileNameWithoutExtension(file.file);
                    string path = paths[1] + name;
                    int addon = -1;
                    while (File.Exists(path + (addon == -1 ? string.Empty : "_" + addon.ToString()) + ext))
                    {
                        addon++;
                    }

                    path = path + (addon == -1 ? string.Empty : "_" + addon.ToString()) + ext;
                    string val = ObjExporter.MeshToString(mesh, true, null);
                    File.WriteAllText(path, val);
                }
                else if (file.guessedType == typeof(Material))
                {
                    Material mat = root.loadAsset<Material>(p3);
                    if (mat == null)
                    {
                        IconSender.Log("Unable to export file " + p3 + " as Material");
                        continue;
                    }
                    if (!Directory.Exists(paths[2]))
                        Directory.CreateDirectory(paths[2]);
                    string dirname = Path.GetFileNameWithoutExtension(file.file);
                    string dirpath = paths[2] + dirname;
                    int diraddon = -1;
                    while (Directory.Exists(dirpath + (diraddon == -1 ? string.Empty : "_" + diraddon.ToString()) + "\\"))
                    {
                        diraddon++;
                    }
                    dirpath = dirpath + (diraddon == -1 ? string.Empty : "_" + diraddon.ToString()) + "\\";
                    Directory.CreateDirectory(dirpath);
                    int[] @is = mat.GetTexturePropertyNameIDs();
                    for (int i = 0; i < @is.Length; i++)
                    {
                        if (mat.GetTexture(@is[i]) is Texture2D t2d)
                        {
                            string ext = ".png";
                            string name = "T_" + t2d.name + "_" + @is[i].ToString();
                            string path = dirpath + name;
                            int addon = -1;
                            while (File.Exists(path + (addon == -1 ? string.Empty : "_" + addon.ToString()) + ext))
                            {
                                addon++;
                            }
                            path = path + (addon == -1 ? string.Empty : "_" + addon.ToString()) + ext;

                            IconSender.I.Sender.WriteTexture(t2d, path);
                        }
                    }
                }
                else
                {
                    IconSender.Log("Unable to export file " + p3 + " of type " + (file.guessedType?.Name ?? "null"));
                }
            }
            foreach (ContentDirectory dir2 in dir.directories.Values)
            {
                Recurse(dir2, children, root);
            }
            if (!isRoot && index < children.Count)
                children.RemoveAt(index);
        }
    }
}
