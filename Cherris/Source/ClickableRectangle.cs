namespace Cherris;

public abstract class ClickableRectangle : Clickable
{
    public override bool IsMouseOver()
    {
        Vector2 mousePosition = Input.WorldMousePosition;

        bool isMouseOver =
            mousePosition.X > GlobalPosition.X - Origin.X &&
            mousePosition.X < GlobalPosition.X + ScaledSize.X - Origin.X &&
            mousePosition.Y > GlobalPosition.Y - Origin.Y &&
            mousePosition.Y < GlobalPosition.Y + ScaledSize.Y - Origin.Y;

        return isMouseOver;
    }
}