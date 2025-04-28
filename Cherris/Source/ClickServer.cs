namespace Cherris;

public sealed class ClickServer
{
    public static ClickServer Instance { get; } = new();

    public int MinLayer = -1;

    private readonly List<Clickable> clickables = [];
    private const bool Debug = false;

    private ClickServer() { }

    public void Register(Clickable clickable)
    {
        clickables.Add(clickable);
    }

    public void Unregister(Clickable clickable)
    {
        clickables.Remove(clickable);
    }

    public void Process()
    {
        if (Input.IsMouseButtonPressed(MouseButtonCode.Left))
        {
            SignalClick(MouseButtonCode.Left);
        }

        if (Input.IsMouseButtonPressed(MouseButtonCode.Right))
        {
            SignalClick(MouseButtonCode.Right);
        }
    }

    public int GetHighestLayer()
    {
        int highestLayer = MinLayer;

        foreach (Clickable clickable in clickables)
        {
            if (clickable.Layer <= highestLayer)
            {
                continue;
            }

            highestLayer = clickable.Layer;
        }

        return highestLayer;
    }

    private void SignalClick(MouseButtonCode mouseButton)
    {
        List<Clickable> viableClickables = GetViableClickables();

        if (viableClickables.Count <= 0)
        {
            return;
        }

        Clickable? topClickable = GetTopClickable(viableClickables);

        if (topClickable is null)
        {
            return;
        }

        if (mouseButton == MouseButtonCode.Left)
        {
            topClickable.OnTopLeft = true;
            Log.Info($"'{topClickable.Name}' has been left clicked.", Debug);
        }
        else
        {
            topClickable.OnTopRight = true;
            Log.Info($"'{topClickable.Name}' has been right clicked.", Debug);
        }
    }

    private List<Clickable> GetViableClickables()
    {
        List<Clickable> viableClickables = [];

        foreach (Clickable clickable in clickables)
        {
            if (!IsMouseOverNode2D(clickable))
            {
                continue;
            }

            viableClickables.Add(clickable);
        }

        Log.Info($"{viableClickables.Count} viable clickables.", Debug);

        return viableClickables;
    }

    private Clickable? GetTopClickable(List<Clickable> viableClickables)
    {
        Clickable? topClickable = null;
        int highestLayer = MinLayer;

        foreach (Clickable clickable in viableClickables)
        {
            if (clickable.Layer < highestLayer)
            {
                continue;
            }

            highestLayer = clickable.Layer;
            topClickable = clickable;
        }

        Log.Info($"The highest layer is {viableClickables.Count}.", Debug);

        return topClickable;
    }

    private static bool IsMouseOverNode2D(Node2D node)
    {
        Vector2 mousePosition = Input.WorldMousePosition;

        bool isMouseOver =
            mousePosition.X > node.GlobalPosition.X - node.Origin.X &&
            mousePosition.X < node.GlobalPosition.X + node.ScaledSize.X - node.Origin.X &&
            mousePosition.Y > node.GlobalPosition.Y - node.Origin.Y &&
            mousePosition.Y < node.GlobalPosition.Y + node.ScaledSize.Y - node.Origin.Y;

        return isMouseOver;
    }
}