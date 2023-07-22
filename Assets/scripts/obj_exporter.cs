using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System;
using UnityEngine.SceneManagement;

struct ObjMaterial {
    public string name;
    public string textureName;
}

public class obj_exporter : MonoBehaviour
{
    private static int vertexOffset = 0;
    private static int normalOffset = 0;
    private static int uvOffset     = 0;
    void Start()
    {

    }

    void Update() {

        if(Input.GetKeyDown(KeyCode.E)) {
            // Find GameObject called Sculpted
            GameObject sculpted = GameObject.Find("Sculpture");
            MeshToFile(sculpted.GetComponent<MeshFilter>() , "Assets", "obj_export");
            Debug.Log("Exported obj");
        }
    }

    

    // Output folder.
    private static string targetFolder = "_ExportedObj";

    private static string MeshToString(MeshFilter mf, Dictionary<string, ObjMaterial> materialList) {
        Mesh       m    = mf.sharedMesh;
        Material[] mats = mf.GetComponent<Renderer>().sharedMaterials;

        StringBuilder sb = new StringBuilder();

        string groupName = mf.name;
        if (string.IsNullOrEmpty(groupName)) {
            groupName = getRandomStr();
        }

        sb.Append("g ").Append(groupName).Append("\n");
        foreach (Vector3 lv in m.vertices) {
            Vector3 wv = mf.transform.TransformPoint(lv);

            // This is sort of ugly - inverting x-component since we're in
            // a different coordinate system than "everyone" is "used to".
            sb.Append(string.Format(
                "v {0} {1} {2}\n",
                floatToStr(-wv.x),
                floatToStr(wv.y),
                floatToStr(wv.z)
            ));
        }

        sb.Append("\n");

        foreach (Vector3 lv in m.normals) {
            Vector3 wv = mf.transform.TransformDirection(lv);

            sb.Append(string.Format(
                "vn {0} {1} {2}\n",
                floatToStr(-wv.x),
                floatToStr(wv.y),
                floatToStr(wv.z)
            ));
        }

        sb.Append("\n");

        foreach (Vector3 v in m.uv) {
            sb.Append(string.Format(
                "vt {0} {1}\n",
                floatToStr(v.x),
                floatToStr(v.y)
            ));
        }

        sb.Append("\n");
        int[] triangles = m.triangles;

        for (int i = 0; i < triangles.Length; i += 3)
        {
            int idx1 = triangles[i] + 1; // obj indices are 1-based
            int idx2 = triangles[i + 1] + 1; // obj indices are 1-based
            int idx3 = triangles[i + 2] + 1; // obj indices are 1-based

            sb.Append($"f {idx1}/{idx1}/{idx1} {idx2}/{idx2}/{idx2} {idx3}/{idx3}/{idx3}\n");
        }

        sb.Append("\n");


        vertexOffset += m.vertices.Length;
        normalOffset += m.normals.Length;
        uvOffset     += m.uv.Length;

        return sb.ToString();
    }

    private static void Clear() {
        vertexOffset = 0;
        normalOffset = 0;
        uvOffset     = 0;
    }

    private static Dictionary<string, ObjMaterial> PrepareFileWrite() {
        Clear();

        return new Dictionary<string, ObjMaterial>();
    }


    public static void MeshToFile(MeshFilter mf, string folder, string filename) {
        Dictionary<string, ObjMaterial> materialList = PrepareFileWrite();

        using (StreamWriter sw = new StreamWriter(folder + "/" + filename + ".obj")) {
            sw.Write("mtllib ./" + filename + ".mtl\n");

            sw.Write(MeshToString(mf, materialList));
        }
    }

    public static void MeshesToFile(MeshFilter[] mf, string folder, string filename) {
        Dictionary<string, ObjMaterial> materialList = PrepareFileWrite();

        using (StreamWriter sw = new StreamWriter(folder + "/" + filename + ".obj")) {
            sw.Write("mtllib ./" + filename + ".mtl\n");

            for (int i = 0; i < mf.Length; i++) {
                sw.Write(MeshToString(mf[i], materialList));
            }
        }
    }

    private static bool CreateTargetFolder() {
        try {
            Directory.CreateDirectory(targetFolder);
        } catch {
            return false;
        }

        return true;
    }


    private static string floatToStr(float number) {
        return String.Format("{0:0.######}", number);
    }

    private static string getRandomStr() {
        string s = Path.GetRandomFileName() + DateTime.Now.Millisecond.ToString();
        s = s.Replace(".", "");

        return s;
    }
}