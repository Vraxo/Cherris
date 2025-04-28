namespace Cherris;

public class MainScene : Node
{
    private readonly Button? button;

    public override void Ready()
    {
        base.Ready();

        //button = GetNode<Button>("Button");
        button!.LeftClicked += OnButtonClicked;
    }

    private void OnButtonClicked(Button obj)
    {
        Console.WriteLine("Printed!");
    }
}