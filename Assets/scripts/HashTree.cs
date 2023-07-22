using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class HashTree
{
    public Dictionary<string, HashTreeNode> hashTree = new Dictionary<string, HashTreeNode>();
    public Dictionary<string, List<string>> hashTreeChildren = new Dictionary<string, List<string>>();
    public HashTreeNode root = null;

    // in this implementation we keep track of all the nodes in a dictionary
    // when a node is added, we check the value of the node's hash and if it's already in the dictionary we don't add it

    public void setRoot(HashTreeNode troot) {
        root = troot;
        hashTree[troot.hash] = troot;
    }

    public List<HashTreeNode> getChildren(HashTreeNode node) {
        List<HashTreeNode> result = new List<HashTreeNode>();
        foreach(string childHash in hashTreeChildren[node.hash]) {
            result.Add(hashTree[childHash]);
        }
        return result;
    }

    public void addChild(HashTreeNode parent, HashTreeNode child) {
        if(!hashTree.ContainsKey(parent.hash)) {
            Debug.Log("Adding children parent reinit " + parent.hash);
            hashTree[parent.hash] = parent;
            hashTreeChildren[parent.hash] = new List<string>();
        }

        if(!hashTree.ContainsKey(child.hash)) {
            Debug.Log("Adding children reinit " + child.hash);
            hashTree[child.hash] = child;
            hashTreeChildren[child.hash] = new List<string>();
        }

        if(!hashTreeChildren[parent.hash].Contains(child.hash)) {
            Debug.Log("Adding children " + parent.hash + "  " + child.hash);
            hashTreeChildren[parent.hash].Add(child.hash);
            Debug.Log("Children added " + hashTreeChildren[parent.hash].Contains(child.hash));
        }

        Debug.Log("Number of children " + hashTreeChildren[parent.hash].Count);
    }

    public void addNewRoot(HashTreeNode node) {
        if(root != null) {
            addChild(node, root);
        }
        setRoot(node);
    }
    
    public void addNodeNoRelatives(HashTreeNode node) {
        if(!hashTree.ContainsKey(node.hash)) {
            Debug.Log("Adding children node " + node.hash);
            hashTree[node.hash] = node;
            hashTreeChildren[node.hash] = new List<string>();
        }
    }

    public void saveToFile(string filename_root) {
        // write the hashTree dictionary and the hashTreeNode dictionary to file

        Debug.Log("Saving to file");
        string filename_hash_tree_children = filename_root + ".hash_children";

        using (StreamWriter sw = new StreamWriter(filename_hash_tree_children))
        {
            foreach(KeyValuePair<string, List<string>> entry in hashTreeChildren) {
                Debug.Log("Writing children " + entry.Key);
                Debug.Log("Writing children " + entry.Key + " " + entry.Value.Count);
                string linetowrite = $"{entry.Key}: {string.Join(",", entry.Value)}";
                Debug.Log(linetowrite);
                sw.WriteLine(linetowrite);
            }
        }

        string filename_hash_tree_nodes = filename_root + ".hash_nodes";

        using (StreamWriter sw = new StreamWriter(filename_hash_tree_nodes))
        {
            foreach(KeyValuePair<string, HashTreeNode> entry in hashTree) {
                string hashTreeNodeToString = entry.Value.ToString();
                sw.WriteLine(hashTreeNodeToString);
            }
        }

        using (StreamWriter sw = new StreamWriter(filename_root)) {
            sw.WriteLine(root.hash);
            sw.WriteLine(filename_hash_tree_children);
            sw.WriteLine(filename_hash_tree_nodes);
        }

    }

    public void readFromFile(string filename_root) {
        string filename_hash_tree_nodes = filename_root + ".hash_nodes";

        using (StreamReader sr = new StreamReader(filename_hash_tree_nodes))
        {
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                HashTreeNode node = HashTreeNode.FromString(line);
                Debug.Log("Adding node " + node.hash);
                addNodeNoRelatives(node);
            }
        }

        string filename_hash_tree_children = filename_root + ".hash_children";

        using (StreamReader sr = new StreamReader(filename_hash_tree_children))
        {
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                Debug.Log("parsing children line " + line);
                string[] split = line.Split(": ");
                string hash = split[0];
                string[] children = split[1].Split(',');
                if(children.Length > 0 && children[0].Length > 1) {
                    foreach(string child in children) {
                        addChild(hashTree[hash], hashTree[child]);
                    }
                }
            }
        }

        using (StreamReader sr = new StreamReader(filename_root))
        {
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                root = hashTree[line];
                break;
            }
        }
    }
}
