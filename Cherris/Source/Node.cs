using Spectre.Console;

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
    private bool isQueuedForFree = false; // Moved from WindowNode for general use


    public bool Active
    {
        get;

        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            ActiveChanged?.Invoke(this, Active);
        }
    } = true;

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
        // Direct freeing is generally discouraged for nodes that manage external resources (like WindowNode).
        // Use QueueFree() for those. This is the final cleanup.
        if (!isQueuedForFree) // Prevent double execution if QueueFree was used
        {
            FreeInternal();
        }
    }


    public void QueueFree()
    {
        if (!isQueuedForFree)
        {
            isQueuedForFree = true;
            // Optional: Add to a SceneTree queue for processing at end of frame
        }
    }


    private void FreeInternal()
    {
        Log.Info($"FreeInternal called for '{Name}' ({GetType().Name})", this is WindowNode); // Log only for windows for now
        List<Node> childrenToDestroy = new(Children);

        foreach (Node child in childrenToDestroy)
        {
            child.QueueFree(); // Children should also be queued
            child.FreeInternal(); // Process their internal freeing immediately if needed
        }
        Children.Clear(); // Clear children list after they are processed

        Parent?.Children.Remove(this);
        Parent = null; // Break parent link

        // Specific cleanup for WindowNode moved there
    }


    public virtual void ProcessBegin() { }

    public virtual void Process()
    {
        // Process queued free requests at the start of the next frame's process
        if (isQueuedForFree)
        {
            FreeInternal();
            // Important: If FreeInternal removes the node from its parent,
            // subsequent processing (like ProcessEnd or child processing) might fail.
            // Consider adding logic to SceneTree to handle removal after the full Process cycle.
            // For now, this basic implementation might suffice if children are handled correctly.
            return; // Stop processing this node further if freed
        }
    }

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

        Node? resultNode = FindNodeInternal(path);

        if (resultNode is T typedResult)
        {
            return typedResult;
        }

        if (resultNode is null)
        {
            throw new InvalidOperationException($"Node not found at path: '{path}' starting from '{Name}'.");
        }
        else
        {
            throw new InvalidOperationException($"Node at path '{path}' is of type '{resultNode.GetType().Name}' but expected '{typeof(T).Name}'.");
        }
    }

    public T? GetNodeOrNull<T>(string path) where T : Node
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        Node? resultNode = FindNodeInternal(path);
        return resultNode as T;
    }


    private Node? FindNodeInternal(string path)
    {
        Node? currentNode;
        string remainingPath = path;

        if (remainingPath.StartsWith("/root"))
        {
            remainingPath = remainingPath.Length > 5 ? remainingPath[5..] : ""; // Handle "/root" and "/root/"
            currentNode = SceneTree.Instance.RootNode;
            if (remainingPath.StartsWith('/'))
            {
                remainingPath = remainingPath[1..];
            }
        }
        else
        {
            currentNode = this; // Start from current node for relative paths
        }

        if (string.IsNullOrEmpty(remainingPath))
        {
            return currentNode; // Path was just "/root" or empty relative path
        }

        string[] nodeNames = remainingPath.Split('/');
        foreach (var name in nodeNames)
        {
            if (string.IsNullOrEmpty(name)) continue; // Skip empty segments (e.g., "NodeA//NodeB")

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
                return null; // Node not found at this step
            }
        }
        return currentNode;
    }


    public WindowNode? GetOwningWindowNode()
    {
        Node? current = this.Parent;
        while (current != null)
        {
            if (current is WindowNode windowNode)
            {
                return windowNode;
            }
            current = current.Parent;
        }
        return null; // No WindowNode ancestor, implies it's in the main window
    }


    public T? GetChild<T>(string name) where T : Node
    {
        foreach (Node child in Children)
        {
            if (child.Name == name && child is T typedChild)
            {
                return typedChild;
            }
        }

        return null;
    }

    public T? GetChild<T>() where T : Node
    {
        foreach (Node child in Children)
        {
            if (child is T typedChild)
            {
                return typedChild;
            }
        }

        return null;
    }

    public Node GetChild(string name)
    {
        Node? child = GetChildOrNull(name);
        if (child is null)
        {
            SceneTree.Instance.RootNode?.PrintChildren();
            throw new InvalidOperationException($"Child node with name '{name}' not found in parent '{Name}'.");
        }
        return child;
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
        return AddChildInternal(node, node.Name);
    }

    public Node AddChild(Node node, string name)
    {
        node.Name = name; // Ensure name is set before Make()
        return AddChildInternal(node, name); // Pass name for potential use in AddChildInternal
    }

    private Node AddChildInternal(Node node, string nodeName)
    {
        if (node.Parent != null)
        {
            node.Parent.Children.Remove(node); // Reparent if necessary
        }

        node.Parent = this;
        node.Make(); // Call Make after parent is set

        Children.Add(node);
        ChildAdded?.Invoke(this, node);

        // Consider calling Start() or Ready() here or deferring via SceneTree if needed

        return node;
    }
}