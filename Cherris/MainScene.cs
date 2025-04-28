namespace Cherris;

public class MainScene : Node
{
    private readonly Button? button;

    public override void Ready()
    {
        base.Ready();

        //button = GetNode<Button>("Button");
        button!.LeftClicked += OnButtonClicked;

        Console.WriteLine(button is null);
    }

    public override void Process()
    {
        base.Process();

        Console.WriteLine("shiet");
    }

    private void OnButtonClicked(Button obj)
    {
        Console.WriteLine("Printed!");
    }
}