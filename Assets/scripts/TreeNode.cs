using System.Collections.Generic;

public class TreeNode
{
    public string name { get; set; }
    public List<float> floatData { get; set; }
    public List<TreeNode> Children { get; set; }

    public TreeNode(string tname, List<float> tfloatData)
    {
        name = tname;
        floatData = tfloatData;
        Children = new List<TreeNode>();
    }

    public TreeNode(string tname)
    {
        name = tname;
        floatData = null;
        Children = new List<TreeNode>();
    }

    public void AddChild(TreeNode node)
    {
        Children.Add(node);
    }
}
