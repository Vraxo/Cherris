namespace Cherris;

public class EntryPoint
{
    [STAThread]
    public static void Main(string[] args)
    {
        ApplicationCore.Instance.Run();
    }
}