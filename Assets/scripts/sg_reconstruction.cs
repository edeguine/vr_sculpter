using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System;
using SolidGeometryLib;

public class sg_reconstruction
{

    public List<IntPtr> toFree = new List<IntPtr>();

    public GameObject ReconstructFromTree(HashTreeNode root, HashTree tree) {
        IntPtr result = evaluateNode(root, tree);
        // drop the last toFree ptr because it will be freed when reconstructued is destroyed
        if(toFree.Count > 0) {
            toFree.RemoveAt(toFree.Count - 1);
        }
        GameObject reconstructued = new GameObject("Reconstructed");
        sgObject sgObject = reconstructued.AddComponent<sgObject>();
        sgObject.InitObject(result);
        return reconstructued;
    }

    public GameObject Reconstruct(string path, out HashTree resTree, out HashTreeNode resRoot) {
        HashTree tree = sg_export.readFromFile(path);
        Debug.Log("Read from file");
        PrintTree(tree, tree.root);
        resTree = tree;
        GameObject reconstructued = ReconstructFromTree(tree.root, tree);
        resRoot = resTree.root;

        Debug.Log("Reconstructed");
        return reconstructued;
    }

    public HashTree CopyTree(HashTree tree) {
        HashTree copy = new HashTree();
        
        foreach(HashTreeNode node in tree.hashTree.Values) {
            copy.addNodeNoRelatives(node);
        }

        foreach(HashTreeNode node in tree.hashTree.Values) {
            foreach(HashTreeNode child in tree.getChildren(node)) {
                copy.addChild(node, child);
                Debug.Log("Copying children");
            }
        }
        
        // set the root of copy
        if(tree.root != null) {
            copy.setRoot(copy.hashTree[tree.root.hash]);
        }

        return copy;
    }

    private IntPtr evaluateNode(HashTreeNode node, HashTree tree) {
        IntPtr x = IntPtr.Zero;
        if(node.name == "sg_object_box") {
            x = SolidGeometryLibIntegration.sg_object_box(node.floatData[0], node.floatData[1], node.floatData[2]);
        } else if(node.name == "sg_object_rotate") {
            HashTreeNode leftChild = tree.getChildren(node)[0];
            x = evaluateNode(leftChild, tree);
            SolidGeometryLibIntegration.sg_object_rotate(x, node.floatData[0], node.floatData[1], node.floatData[2], node.floatData[3], node.floatData[4], node.floatData[5], node.floatData[6]);
        } else if(node.name == "sg_object_move") {
            HashTreeNode leftChild = tree.getChildren(node)[0];
            x = evaluateNode(leftChild, tree);
            SolidGeometryLibIntegration.sg_object_move(x, node.floatData[0], node.floatData[1], node.floatData[2]);
        } else if(node.name == "sg_bool_sub") {
            HashTreeNode leftChild = tree.getChildren(node)[0];
            HashTreeNode rightChild = tree.getChildren(node)[1];
            x = myBooleanSub(evaluateNode(leftChild, tree), evaluateNode(rightChild, tree));
        } else if(node.name == "sg_bool_add") {
            HashTreeNode leftChild = tree.getChildren(node)[0];
            HashTreeNode rightChild = tree.getChildren(node)[1];
            x = myBooleanAdd(evaluateNode(leftChild, tree), evaluateNode(rightChild, tree));
        } else {
            Debug.Log("Unknown node name: X" + node.name + "X");
            Debug.Log(node.name == "sg_object_move");
            return IntPtr.Zero;
        }
        toFree.Add(x);
        return x;
    }

    private static IntPtr myBooleanSub(IntPtr first, IntPtr second) {
        IntPtr res = IntPtr.Zero;
        IntPtr subResGroup = SolidGeometryLibIntegration.sg_bool_sub(first, second);

            if (subResGroup != IntPtr.Zero)
            {
                if (SolidGeometryLibIntegration.sg_object_type(subResGroup) == 10)   //  10 - type of group
                {
                    int chCnt = SolidGeometryLibIntegration.sg_group_child_count(subResGroup);
                    Debug.Log("subResGroup has " + chCnt.ToString() + " children");
                    // just get the first group
                    for (int i = 0; i < chCnt && i < 1; i++)
                    {
                        IntPtr curCh = SolidGeometryLibIntegration.sg_group_child(subResGroup, i);
                        res = curCh;
                    }
                    SolidGeometryLibIntegration.sg_group_break(subResGroup);
                } else {
                    Debug.Log("subResGroup is not a group");
                }
            } else {
                Debug.Log("subResGroup is null");
            }
        return res;
    }

    private static IntPtr myBooleanAdd(IntPtr first, IntPtr second) {
        IntPtr res = IntPtr.Zero;
        IntPtr subResGroup = SolidGeometryLibIntegration.sg_bool_union(first, second);

            if (subResGroup != IntPtr.Zero)
            {
                if (SolidGeometryLibIntegration.sg_object_type(subResGroup) == 10)   //  10 - type of group
                {
                    int chCnt = SolidGeometryLibIntegration.sg_group_child_count(subResGroup);
                    Debug.Log("subResGroup has " + chCnt.ToString() + " children");
                    // just get the first group
                    for (int i = 0; i < chCnt && i < 1; i++)
                    {
                        IntPtr curCh = SolidGeometryLibIntegration.sg_group_child(subResGroup, i);
                        res = curCh;
                    }
                    SolidGeometryLibIntegration.sg_group_break(subResGroup);
                } else {
                    Debug.Log("subResGroup is not a group");
                }
            } else {
                Debug.Log("subResGroup is null");
            }
        return res;
    }

    public static void PrintTree(HashTree tree, HashTreeNode node, int depth = 0) {
        if(node != null ) {
            Debug.Log($"{new string('\t', depth)}{node.name}");
            foreach(HashTreeNode child in tree.getChildren(node)) {
                PrintTree(tree, child, depth + 1);
            }
        }
        
    }
}
