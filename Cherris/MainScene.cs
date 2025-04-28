namespace Cherris;

public class MainScene : Node
{
    // Field is readonly, assignment happens during scene loading
    private readonly Button? button;

    public override void Ready()
    {
        base.Ready();

        // Problem: 'button' might be null if scene loading failed for this specific assignment
        // Using the null-forgiving operator (!) suppresses warnings but doesn't prevent NullReferenceException
        // button!.LeftClicked += OnButtonClicked;

        // Safer approach: Check for null before subscribing
        if (button != null)
        {
            button.LeftClicked += OnButtonClicked;
            Console.WriteLine($"MainScene: Successfully subscribed to LeftClicked for button '{button.Name}' at path '{button.AbsolutePath}'");
        }
        else
        {
            // Log an error if the button reference wasn't assigned correctly by PackedScene
            Log.Error("MainScene: The 'button' field is null in Ready(). Check scene definition and PackedScene loading logs.");
        }

        // This check happens *before* the subscription attempt
        Console.WriteLine($"MainScene.Ready(): button is null? {button is null}");
    }

    private void OnButtonClicked(Button obj)
    {
        // Use Log.Info for consistency and better output control
        Log.Info($"MainScene: OnButtonClicked triggered by '{obj.Name}'!");
        Console.WriteLine($"MainScene: OnButtonClicked triggered by '{obj.Name}'!"); // Keep Console for direct feedback if needed
    }
}