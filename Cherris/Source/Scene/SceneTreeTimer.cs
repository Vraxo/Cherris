namespace Cherris;

public class SceneTreeTimer(float waitTime)
{
    public float WaitTime { get; set; } = waitTime;
    private float timePassed = 0;

    public delegate void TimerEventHandler();
    public event TimerEventHandler? Timeout;

    public void Process()
    {
        //timePassed += Time.Delta;

        if (timePassed >= WaitTime)
        {
            Timeout?.Invoke();
            SceneTree.Instance.RemoveTimer(this);
        }
    }
}