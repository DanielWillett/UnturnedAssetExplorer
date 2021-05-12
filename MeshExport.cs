using UnityEngine;
using System.Text;
using System.Collections.Generic;
using SDG.Unturned;

namespace IconSenderModule
{
    /// <summary>
    /// Most of this class from: 
    /// https://wiki.unity3d.com/index.php?title=ObjExporter
    /// </summary>
    public static class ObjExporter
    {

        public static string MeshToString(MeshFilter mf, out List<Texture2D> textures, out string mtl, Asset asset, int index = -1)
        {
            Mesh m = mf.mesh;
            Material[] mats = mf.GetComponent<Renderer>().sharedMaterials;
            mtl = string.Empty;
            textures = new List<Texture2D>();
            for(int i = 0; i < mats.Length; i++)
            {
                int[] texts = mats[i].GetTexturePropertyNameIDs();
                IconSender.Log(mats.Length.ToString() + " " + texts.Length.ToString());
                for (int e = 0; e < texts.Length; e++)
                {
                    Texture tex = mats[i].GetTexture(texts[e]);
                    if (tex == null || tex.GetType() != typeof(Texture2D)) continue;
                    textures.Add((Texture2D)tex);
                }
            }
            StringBuilder sb = new StringBuilder();
            StringBuilder mtlb = new StringBuilder();

            sb.Append("mtllib ").Append("materials" + (index == -1 ? "" : "_" + index.ToString()) + ".mtl").Append("\n");
            foreach (Vector3 v in m.vertices)
            {
                sb.Append(string.Format("v {0} {1} {2}\n", v.x, v.y, v.z));
            }
            sb.Append("\n");
            foreach (Vector3 v in m.normals)
            {
                sb.Append(string.Format("vn {0} {1} {2}\n", v.x, v.y, v.z));
            }
            sb.Append("\n");
            foreach (Vector3 v in m.uv)
            {
                sb.Append(string.Format("vt {0} {1}\n", v.x, v.y));
            }
            const string z = "{0:0.000000}";
            for (int color = 0; color < m.colors32.Length; color ++)
            {
                mtlb.Append("newmtl ").Append("color" + color.ToString()).Append("\n");
                mtlb.Append($"Ka 1.000000 1.000000 1.000000").Append("\n");
                mtlb.Append($"Kd {string.Format(z, m.colors32[color].r / 256f)} {string.Format(z, m.colors32[color].g / 256f)} {string.Format(z, m.colors32[color].b / 256f)}").Append("\n");
                mtlb.Append($"Ks 0.500000 0.500000 0.500000").Append("\n");
                mtlb.Append("Ns 100.000000").Append("\n");
                mtlb.Append("Ni 1.000000").Append("\n");
                mtlb.Append("d 1.000000").Append("\n");
                mtlb.Append("illum 0").Append("\n");
            }
            for (int material = 0; material < m.subMeshCount; material++)
            {
                mtlb.Append("newmtl ").Append(mats[material].name).Append("\n");
                mtlb.Append("Ka 1.000000 1.000000 1.000000").Append("\n");
                mtlb.Append($"Kd {string.Format(z, mats[material].color.r)} {string.Format(z, mats[material].color.g)} {string.Format(z, mats[material].color.b)}").Append("\n");
                mtlb.Append("Ks 0.500000 0.500000 0.500000").Append("\n");
                mtlb.Append("Ns 100.000000").Append("\n");
                mtlb.Append("Ni 1.000000").Append("\n");
                mtlb.Append("d 1.000000").Append("\n");
                mtlb.Append("illum 0").Append("\n");
                if(textures.Count > 0)
                    mtlb.Append("map_Kd ").Append(ExtraIconInfo.TexName(asset, 0, textures[0], "png", index)).Append("\n");
                sb.Append("\n");
                sb.Append("usemtl ").Append(mats[material].name).Append("\n");
                sb.Append("usemap ").Append(mats[material].name).Append("\n");

                int[] triangles = m.GetTriangles(material);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    sb.Append(string.Format("f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}\n",
                        triangles[i] + 1, triangles[i + 1] + 1, triangles[i + 2] + 1));
                }
            }
            mtl = mtlb.ToString();
            return sb.ToString();
        }
    }
}