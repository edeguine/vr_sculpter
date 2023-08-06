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
        Debug.Log("Evaluating node");
        //IntPtr result = evaluateNode(root, tree);
        Stack<HashTreeNode> stack = new Stack<HashTreeNode>();
        PostOrderIterative(root, tree, stack);
        IntPtr result = PostOrderEvaluate(stack);
        if(result == IntPtr.Zero) {
            Debug.Log("Reconstruct result is null");
        } else {
            Debug.Log("Reconstruct result is not null");
        }
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
        //PrintTree(tree, tree.root);
        resTree = tree;
        GameObject reconstructued = ReconstructFromTree(tree.root, tree);
        resRoot = tree.root;

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
                //Debug.Log("Copying children");
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

    /*
    iterativePostorder(node)
    
    s —> empty stack
    t —> output stack
    while (not s.isEmpty())
        node —> s.pop()
        t.push(node)
        
        if (node.left <> null)
            s.push(node.left)
        
        if (node.right <> null)
            s.push(node.right)
    
    while (not t.isEmpty())
        node —> t.pop()
        visit(node) 
    */
    void PostOrderIterative(HashTreeNode node, HashTree tree, Stack<HashTreeNode> s)
    {
        Stack<HashTreeNode> s1 = new Stack<HashTreeNode>();

        if (node == null)
            return;

        // push root to first stack
        s1.Push(node);

        // Run while first stack is not empty
        while (s1.Count > 0)
        {
            // Pop an item from s1 and push it to the result stack
            HashTreeNode temp = s1.Pop();
            s.Push(temp);

            // Push left and right children of removed item to s1
            if (tree.getChildren(temp).Count > 0)
                s1.Push(tree.getChildren(temp)[0]);
            if (tree.getChildren(temp).Count > 1)
                s1.Push(tree.getChildren(temp)[1]);
        }
    }

    IntPtr PostOrderEvaluate(Stack<HashTreeNode> s)
    {
        Stack<IntPtr> evaluationStack = new Stack<IntPtr>();
        while (s.Count > 0)
        {
            HashTreeNode token = s.Pop();
            if (token.name == "sg_object_box")
            {
                evaluationStack.Push(SolidGeometryLibIntegration.sg_object_box(token.floatData[0], token.floatData[1], token.floatData[2]));

            } else if (token.name == "sg_object_rotate" || token.name == "sg_object_move" || token.name == "sg_object_scale")
            {
                IntPtr leftOperand = evaluationStack.Pop();
                switch (token.name)
                {
                    case "sg_object_rotate":
                        SolidGeometryLibIntegration.sg_object_rotate(leftOperand, token.floatData[0], token.floatData[1], token.floatData[2], token.floatData[3], token.floatData[4], token.floatData[5], token.floatData[6]);
                        break;
                    case "sg_object_move":
                        SolidGeometryLibIntegration.sg_object_move(leftOperand, token.floatData[0], token.floatData[1], token.floatData[2]);
                        break;
                    case "sg_object_scale":
                        SolidGeometryLibIntegration.sg_object_scale(leftOperand, token.floatData[0], token.floatData[1], token.floatData[2]);
                        break;
                }
                evaluationStack.Push(leftOperand);
            } else if (token.name == "sg_bool_sub" || token.name == "sg_bool_add")
            {
                IntPtr rightOperand = evaluationStack.Pop();
                IntPtr leftOperand = evaluationStack.Pop();
                switch (token.name)
                {
                    case "sg_bool_sub":
                        evaluationStack.Push(myBooleanSub(leftOperand, rightOperand));
                        break;
                    case "sg_bool_add":
                        evaluationStack.Push(myBooleanAdd(leftOperand, rightOperand));
                        break;
                }
            }
        }
        return evaluationStack.Pop();
    }


    /*
        void inorderIterative(Node* root)
        {
            // create an empty stack
            stack<Node*> stack;
        
            // start from the root node (set current node to the root node)
            Node* curr = root;
        
            // if the current node is null and the stack is also empty, we are done
            while (!stack.empty() || curr != nullptr)
            {
                // if the current node exists, push it into the stack (defer it)
                // and move to its left child
                if (curr != nullptr)
                {
                    stack.push(curr);
                    curr = curr->left;
                }
                else {
                    // otherwise, if the current node is null, pop an element from the stack,
                    // print it, and finally set the current node to its right child
                    curr = stack.top();
                    stack.pop();
                    cout << curr->data << " ";
        
                    curr = curr->right;
                }
            }
        }
    
    */

    IntPtr evaluateNodeIterativeInOrderWrong(HashTreeNode root, HashTree tree) {
        Stack<HashTreeNode> stack = new Stack<HashTreeNode>();
        Dictionary<HashTreeNode, IntPtr> dataDict = new Dictionary<HashTreeNode, IntPtr>();

        HashTreeNode curr = root;

        Debug.Log("Evaluating root node");
        Debug.Log(root.hash);

        while(stack.Count > 0 || curr != null) {

            if(curr != null) {
                stack.Push(curr);
                if(tree.getChildren(curr).Count > 0) {
                    curr = tree.getChildren(curr)[0];
                } else {
                    curr = null;
                }
            } else {
                curr = stack.Pop();

                Debug.Log("Evaluating inner node");
                Debug.Log(curr.hash);

                if(curr.name == "sg_object_box") {
                    dataDict[curr] = SolidGeometryLibIntegration.sg_object_box(curr.floatData[0], curr.floatData[1], curr.floatData[2]);
                } else if(curr.name == "sg_object_rotate") {
                    IntPtr leftChild = dataDict[tree.getChildren(curr)[0]];
                    SolidGeometryLibIntegration.sg_object_rotate(leftChild, curr.floatData[0], curr.floatData[1], curr.floatData[2], curr.floatData[3], curr.floatData[4], curr.floatData[5], curr.floatData[6]);
                    dataDict[curr] = leftChild;
                } else if(curr.name == "sg_object_move") {
                    IntPtr leftChild = dataDict[tree.getChildren(curr)[0]];
                    SolidGeometryLibIntegration.sg_object_move(leftChild, curr.floatData[0], curr.floatData[1], curr.floatData[2]);
                    dataDict[curr] = leftChild;
                } else if(curr.name == "sg_bool_sub") {
                    IntPtr leftChild = dataDict[tree.getChildren(curr)[0]];
                    IntPtr rightChild = dataDict[tree.getChildren(curr)[1]];
                    dataDict[curr] = myBooleanSub(leftChild, rightChild);
                } else if(curr.name == "sg_bool_add") {
                    IntPtr leftChild = dataDict[tree.getChildren(curr)[0]];
                    IntPtr rightChild = dataDict[tree.getChildren(curr)[1]];
                    dataDict[curr] = myBooleanAdd(leftChild, rightChild);
                } else {
                    Debug.Log("Unknown node name: X" + curr.name + "X");
                    Debug.Log(curr.name == "sg_object_move");
                    return IntPtr.Zero;
                }

                toFree.Add(dataDict[curr]);

                if(tree.getChildren(curr).Count > 1) {
                    curr = tree.getChildren(curr)[1];
                } else {
                    curr = null;
                }
            }

        }

        return dataDict[curr];
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
