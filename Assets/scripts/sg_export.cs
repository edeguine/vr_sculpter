using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;

public class sg_export : MonoBehaviour
{

    public static HashTreeNode box(float x, float y, float z, HashTree tree) {
        List<float> floatData = new List<float>();
        
        floatData.Add(x);
        floatData.Add(y);
        floatData.Add(z);

        HashTreeNode box = new HashTreeNode("sg_object_box", floatData);
        tree.addNodeNoRelatives(box);

        return box;
    }

    // sg_object_rotate(brushCube, 
    //        brush.transform.position.x, brush.transform.position.y, brush.transform.position.z,
    //        axisBrush.x, axisBrush.y, axisBrush.z, angleRadBrush);
    public static HashTreeNode rotate(HashTreeNode node, HashTree tree, float x, float y, float z, float axisX, float axisY, float axisZ, float angle) {
        List<float> floatData = new List<float>();
        
        floatData.Add(x);
        floatData.Add(y);
        floatData.Add(z);
        floatData.Add(axisX);
        floatData.Add(axisY);
        floatData.Add(axisZ);
        floatData.Add(angle);

        HashTreeNode rotate = new HashTreeNode("sg_object_rotate", floatData);
        tree.addNodeNoRelatives(rotate);
        tree.addChild(rotate, node);

        return rotate;
    }

    //sg_object_move(brushCube, 
    //        newPositions.x, newPositions.y, newPositions.z);

    public static HashTreeNode move(HashTreeNode node, HashTree tree, float x, float y, float z) {
        List<float> floatData = new List<float>();
        
        

        floatData.Add(x);
        floatData.Add(y);
        floatData.Add(z);

        HashTreeNode move = new HashTreeNode("sg_object_move", floatData);
        tree.addNodeNoRelatives(move);
        tree.addChild(move, node);

        Debug.Log("Moving " +move.hash + " X " + node.hash + "X");

        return move;
    }

    public static HashTreeNode scale(HashTreeNode node, HashTree tree, float x, float y, float z) {
        List<float> floatData = new List<float>();
        
        floatData.Add(x);
        floatData.Add(y);
        floatData.Add(z);

        HashTreeNode scale = new HashTreeNode("sg_object_scale", floatData);
        tree.addNodeNoRelatives(scale);
        tree.addChild(scale, node);

        Debug.Log("Scaling " + scale.hash + " X " + node.hash + "X");

        return scale;
    }

    // sg_bool_sub(firstSelected, secondSelected);
    public static HashTreeNode sub(HashTreeNode first, HashTreeNode second, HashTree tree) {
        HashTreeNode sub = new HashTreeNode("sg_bool_sub");
        tree.addNodeNoRelatives(sub);
        tree.addChild(sub, first);
        tree.addChild(sub, second);

        return sub;
    }

     public static HashTreeNode add(HashTreeNode first, HashTreeNode second, HashTree tree) {
        HashTreeNode sub = new HashTreeNode("sg_bool_add");
        tree.addNodeNoRelatives(sub);
        tree.addChild(sub, first);
        tree.addChild(sub, second);

        return sub;
    }

    public static void writeToFile(HashTreeNode newRoot, HashTree tree, string filename) {
        sg_reconstruction sgr = new sg_reconstruction();
        HashTree ttree = sgr.CopyTree(tree);
        ttree.setRoot(newRoot);
        ttree.saveToFile(filename);
    }

    public static HashTree readFromFile(string filename) {
        HashTree tree = new HashTree();
        tree.readFromFile(filename);
        return tree;
    }
}