namespace Cherris;

public class Node2D : VisualItem
{
    public Vector2 Position { get; set; } = Vector2.Zero;
    public virtual float Rotation { get; set; } = 0;
    public OriginPreset OriginPreset { get; set; } = OriginPreset.Center;
    public bool InheritPosition { get; set; } = true;
    public bool InheritOrigin { get; set; } = false;
    public bool InheritScale { get; set; } = true;
    public HAlignment HAlignment { get; set; } = HAlignment.Center;
    public VAlignment VAlignment { get; set; } = VAlignment.Center;
    public AnchorPreset AnchorPreset { get; set; } = AnchorPreset.None;

    public Vector2 ScaledSize => Size * Scale;

    public virtual Vector2 Size
    {
        get;
        set
        {
            if (value == field)
            {
                return;
            }

            field = value;
            SizeChanged?.Invoke(this, Size);
        }
    } = new(320, 320);

    public virtual Vector2 Scale
    {
        get
        {
            return InheritScale && Parent is Node2D node2DParent
                ? node2DParent.Scale
                : (field);
        }
        set;
    } = new(1, 1);

    public virtual Vector2 GlobalPosition
    {
        get
        {
            if (Parent is Node2D parentNode)
            {
                Vector2 anchorOffset = AnchorPreset switch
                {
                    AnchorPreset.TopCenter => new Vector2(parentNode.Size.X * 0.5f, 0),
                    AnchorPreset.TopLeft => Vector2.Zero, // Handle TopLeft case
                    _ => Vector2.Zero
                };
                return InheritPosition
                    ? parentNode.GlobalPosition + anchorOffset + Position
                    : (field);
            }
            else
            {
                return Position;
            }
        }
        set
        {
            field = value;
            if (Parent is not Node2D)
            {
                Position = value;
            }
        }
    }

    public Vector2 Offset
    {
        get
        {
            if (InheritOrigin && Parent is Node2D parentNode)
            {
                return parentNode.Offset;
            }
            return field;
        }
        set;
    } = Vector2.Zero;

    public Vector2 Origin
    {
        get
        {
            float x = HAlignment switch
            {
                HAlignment.Center => Size.X * Scale.X / 2,
                HAlignment.Left => 0,
                HAlignment.Right => Size.X,
                _ => 0
            };

            float y = VAlignment switch
            {
                VAlignment.Top => 0,
                VAlignment.Center => Size.Y * Scale.Y / 2,
                VAlignment.Bottom => Size.Y * Scale.Y,
                _ => 0
            };

            Vector2 alignmentOffset = new(x, y);
            return alignmentOffset + Offset;
        }
    }

    // Events

    public event EventHandler<Vector2>? SizeChanged;

    // Methods

    public void LookAt(Vector2 targetPosition)
    {
        Vector2 direction = targetPosition - GlobalPosition;
        var angle = float.Atan2(direction.Y, direction.X) * 57.29578f;
        Rotation = angle;
    }
}

public enum AnchorPreset
{
    None,
    TopCenter,
    TopLeft // Added TopLeft preset
}