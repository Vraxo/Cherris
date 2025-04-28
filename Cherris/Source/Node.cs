using Spectre.Console;
using System.Collections.Generic; // Added for Stack<T>
using System; // Added for ArgumentException, InvalidOperationException, TypeLoadException
using System.Linq; // Added for Linq methods
using System.Reflection; // Added for reflection
using YamlDotNet.Serialization; // Added for Yaml
using YamlDotNet.Serialization.NamingConventions; // Added for Yaml Naming Conventions
using System.IO; // Added for File IO

namespace Cherris;

public class Node
{
    public enum ProcessMode
    {
        Inherit,
        Pausable,
        WhenPaused,
        Disabled,
        Always
    }

    public static Node RootNode => SceneTree.Instance.RootNode!;
    public static SceneTree Tree => SceneTree.Instance;

    public string Name { get; set; } = "";
    public Node? Parent { get; set; } = null;
    public List<Node> Children { get; set; } = [];
    public ProcessMode ProcessingMode = ProcessMode.Inherit;

    private bool fieldActive = true;

    public bool Active
    {
        get => fieldActive;

        set
        {
            if (fieldActive == value)
            {
                return;
            }

            fieldActive = value;
            ActiveChanged?.Invoke(this, fieldActive);
        }
    }

    public string AbsolutePath
    {
        get
        {
            if (Parent is null)
            {

                return "/root/";
            }

            Stack<string> pathStack = new();
            Node? current = this;


            while (current is not null && current.Parent is not null)
            {

                pathStack.Push(current.Name);
                current = current.Parent;
            }


            return $"/root/{string.Join("/", pathStack)}";
        }
    }


    public delegate void ActiveEvent(Node sender, bool active);
    public delegate void ChildEvent(Node sender, Node child);
    public event ActiveEvent? ActiveChanged;
    public event ChildEvent? ChildAdded;


    public virtual void Make() { }

    public virtual void Start() { }

    public virtual void Ready() { }

    public virtual void Free()
    {
        List<Node> childrenToDestroy = new(Children);

        foreach (Node child in childrenToDestroy)
        {
            child.Free();
        }

        Parent?.Children.Remove(this);
    }


    public virtual void ProcessBegin() { }

    public virtual void Process() { }

    public virtual void ProcessEnd() { }


    public void PrintChildren()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        string rootEmoji = NodeEmoji.GetEmojiForNodeType(this);
        Tree root = new($"{rootEmoji} [green]{Name}[/]");

        AddChildrenToTree(this, root);

        AnsiConsole.Write(root);
    }

    private static void AddChildrenToTree(Node node, IHasTreeNodes parentNode)
    {
        foreach (Node child in node.Children)
        {
            string childEmoji = NodeEmoji.GetEmojiForNodeType(child);
            TreeNode childNode = parentNode.AddNode($"{childEmoji} [blue]{child.Name}[/]");
            AddChildrenToTree(child, childNode);
        }
    }


    public virtual void Activate()
    {
        Active = true;

        foreach (Node child in Children)
        {
            child.Activate();
        }
    }

    public virtual void Deactivate()
    {
        Active = false;

        foreach (Node child in Children)
        {
            child.Deactivate();
        }
    }


    public T GetParent<T>() where T : Node
    {
        if (Parent is not null)
        {
            return (T)Parent;
        }

        return (T)this;
    }

    public T GetNode<T>(string path) where T : Node
    {
        if (string.IsNullOrEmpty(path))
        {
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));
        }

        Node? currentNode;

        if (path.StartsWith("/root"))
        {
            path = path["/root".Length..];
            currentNode = SceneTree.Instance.RootNode;

            if (path.StartsWith('/'))
            {
                path = path.Substring(1);
            }

            if (!string.IsNullOrEmpty(path))
            {
                string[] nodeNames = path.Split('/');
                foreach (var name in nodeNames)
                {
                    if (name == "..")
                    {

                        currentNode = currentNode?.Parent;
                    }
                    else
                    {
                        currentNode = currentNode?.GetChild(name);
                    }

                    if (currentNode == null)
                    {
                        throw new InvalidOperationException($"Node '{name}' not found in the scene tree.");
                    }
                }
            }
        }
        else
        {
            currentNode = this;
            string[] nodeNames = path.Split('/');
            foreach (var name in nodeNames)
            {
                if (name == "..")
                {

                    currentNode = currentNode?.Parent;
                }
                else if (name != "")
                {
                    currentNode = currentNode?.GetChild(name);
                }

                if (currentNode == null)
                {
                    throw new InvalidOperationException($"Node '{name}' not found in the scene tree.");
                }
            }
        }

        return currentNode as T ?? throw new InvalidOperationException("Node is not of the expected type.");
    }

    public T? GetNodeOrNull<T>(string path) where T : Node
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        Node? currentNode;

        if (path.StartsWith("/root"))
        {
            path = path.Substring("/root".Length);
            currentNode = SceneTree.Instance.RootNode;

            if (path.StartsWith("/"))
            {
                path = path.Substring(1);
            }

            if (!string.IsNullOrEmpty(path))
            {
                string[] nodeNames = path.Split('/');
                foreach (var name in nodeNames)
                {
                    if (name == "..")
                    {

                        currentNode = currentNode?.Parent;
                    }
                    else
                    {
                        currentNode = currentNode?.GetChildOrNull(name);
                    }

                    if (currentNode == null)
                    {
                        return null;
                    }
                }
            }
        }
        else
        {
            currentNode = this;
            string[] nodeNames = path.Split('/');
            foreach (var name in nodeNames)
            {
                if (name == "..")
                {

                    currentNode = currentNode?.Parent;
                }
                else if (name != "")
                {
                    currentNode = currentNode?.GetChildOrNull(name);
                }

                if (currentNode == null)
                {
                    return null;
                }
            }
        }

        return currentNode as T;
    }


    public T? GetChild<T>(string name) where T : Node
    {
        foreach (Node child in Children)
        {
            if (child.Name == name)
            {
                return (T)child;
            }
        }

        return null;
    }

    public T? GetChild<T>() where T : Node
    {
        foreach (Node child in Children)
        {
            if (child.GetType() == typeof(T))
            {
                return (T)child;
            }
        }

        return null;
    }

    public Node GetChild(string name)
    {
        foreach (Node child in Children)
        {
            if (child.Name == name)
            {
                return child;
            }
        }

        SceneTree.Instance.RootNode?.PrintChildren();

        throw new InvalidOperationException($"Child node with name '{name}' not found.");
    }

    public Node? GetChildOrNull(string name)
    {
        foreach (Node child in Children)
        {
            if (child.Name == name)
            {
                return child;
            }
        }

        return null;
    }


    public Node AddChild(Node node)
    {
        node.Parent = this;

        node.Make();

        Children.Add(node);
        ChildAdded?.Invoke(this, node);

        return node;
    }

    public Node AddChild(Node node, string name)
    {
        node.Parent = this;
        node.Name = name;

        node.Make();

        Children.Add(node);
        ChildAdded?.Invoke(this, node);

        return node;
    }

    public WindowNode? GetOwningWindowNode()
    {
        Node? current = this;
        while (current != null)
        {
            if (current is WindowNode windowNode)
            {
                return windowNode;
            }
            current = current.Parent;
        }
        return null;
    }
}