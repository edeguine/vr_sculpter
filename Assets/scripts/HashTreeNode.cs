using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;

public class HashTreeNode
{
    public string name { get; set; }
    public List<float> floatData { get; set; }
    public string hash { get; set; }

    public HashTreeNode(string tname, List<float> tfloatData, string thash)
    {
        name = tname;
        floatData = tfloatData;
        hash = thash;
    }

    public string ToString() {
        string result = name + " " + hash + " ";
        if(floatData != null) {
            result += string.Join(",", floatData);
        }
        return result;
    }

    public static HashTreeNode FromString(string line) {
        // format is "tname thash float1 float2 etc"
        Debug.Log("From string " + line);
        string[] split = line.Split(' ');
        string tname = split[0];
        string thash = split[1];
        List<float> tFloatData = new List<float>();
        if(split.Length > 2 && split[2].Length > 1) {
            string[] floatData = split[2].Split(',');
            foreach (string part in floatData)
            {
                tFloatData.Add(float.Parse(part));
            }
        }

        return new HashTreeNode(tname, tFloatData, thash);
    }

    public HashTreeNode(string tname, string thash)
    {
        name = tname;
        floatData = null;
        hash = thash;
    }

    public HashTreeNode(string tname)
    {
        name = tname;
        floatData = null;
        Guid myuuid = Guid.NewGuid();
        hash = myuuid.ToString();

    }

     public HashTreeNode(string tname, List<float> tFloatData)
    {
        name = tname;
        floatData = tFloatData;
        Guid myuuid = Guid.NewGuid();
        hash = myuuid.ToString();

    }
}