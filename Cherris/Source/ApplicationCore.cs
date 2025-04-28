using System;
using System.IO;
using System.Reflection;
using System.Threading;
using YamlDotNet.Serialization;
using System.Collections.Generic;

namespace Cherris;

public sealed class ApplicationCore
{
    private static readonly Lazy<ApplicationCore> lazyInstance = new(() => new ApplicationCore());
    private MainAppWindow? mainWindow;
    private Configuration? applicationConfig;
    private readonly List<SecondaryWindow> secondaryWindows = new();

    private const string ConfigFilePath = "Res/Cherris/Config.yaml";
    private const string LogFilePath = "Res/Cherris/Log.txt";

    public static ApplicationCore Instance => lazyInstance.Value;

    private ApplicationCore()
    {
    }

    public IntPtr GetMainWindowHandle()
    {
        return mainWindow?.Handle ?? IntPtr.Zero;
    }

    public void Run()
    {
        if (!Start())
        {
            Log.Error("ApplicationCore failed to start.");
            return;
        }

        if (mainWindow is null)
        {
            Log.Error("Main window was not initialized.");
            return;
        }

        MainLoop();

        Log.Info("Main loop exited. Application exiting.");
        Cleanup();
    }

    private bool Start()
    {
        CreateLogFile();
        SetCurrentDirectory();

        applicationConfig = LoadConfig();
        if (applicationConfig is null)
        {
            Log.Error("Failed to load configuration.");
            return false;
        }

        try
        {
            mainWindow = new MainAppWindow(
                applicationConfig.Title,
                applicationConfig.Width,
                applicationConfig.Height);

            if (!mainWindow.TryCreateWindow())
            {
                Log.Error("Failed to create main window.");
                return false;
            }

            mainWindow.Closed += OnMainWindowClosed;

            ApplyConfig();

            if (!mainWindow.InitializeWindowAndGraphics())
            {
                Log.Error("Failed to initialize window graphics.");
                return false;
            }

            mainWindow.ShowWindow();
        }
        catch (Exception ex)
        {
            Log.Error($"Error during window initialization: {ex.Message}");
            return false;
        }


        return true;
    }

    private void MainLoop()
    {
        while (mainWindow != null && mainWindow.IsOpen)
        {

            ProcessSystemMessages();


            ClickServer.Instance.Process(); // Still uses global Input
            SceneTree.Instance.Process();



            mainWindow.RenderFrame();
            RenderSecondaryWindows();


            Input.Update(); // Clears previous frame's input state for global Input

        }
    }


    private void ProcessSystemMessages()
    {
        while (NativeMethods.PeekMessage(out NativeMethods.MSG msg, IntPtr.Zero, 0, 0, NativeMethods.PM_REMOVE))
        {
            if (msg.message == NativeMethods.WM_QUIT)
            {
                Log.Info("WM_QUIT received, signaling application close.");
                mainWindow?.Close(); // Signal main window to close if not already
                break; // Exit message loop processing for this frame
            }

            NativeMethods.TranslateMessage(ref msg);
            NativeMethods.DispatchMessage(ref msg); // Sends to the correct WindowProcedure
        }
    }


    private void RenderSecondaryWindows()
    {
        var windowsToRender = new List<SecondaryWindow>(secondaryWindows);

        foreach (var window in windowsToRender)
        {
            if (window.IsOpen)
            {
                window.RenderFrame();
            }
            else
            {

                secondaryWindows.Remove(window);
            }
        }
    }

    private void OnMainWindowClosed()
    {

        Log.Info("Main window closed signal received. Closing secondary windows.");
        CloseAllSecondaryWindows();
    }

    private void Cleanup()
    {
        Log.Info("ApplicationCore Cleanup starting.");
        CloseAllSecondaryWindows();
        mainWindow?.Dispose();
        mainWindow = null;
        Log.Info("ApplicationCore Cleanup finished.");
    }

    private void CloseAllSecondaryWindows()
    {
        var windowsToClose = new List<SecondaryWindow>(secondaryWindows);
        foreach (var window in windowsToClose)
        {
            window.Close(); // Request close
            // Dispose happens when WM_NCDESTROY is processed
        }
        // Don't Clear() here, let them remove themselves on NCDESTROY
    }

    internal void RegisterSecondaryWindow(SecondaryWindow window)
    {

        if (!secondaryWindows.Contains(window))
        {
            secondaryWindows.Add(window);
            Log.Info($"Registered secondary window: {window.Title}");
        }
    }

    internal void UnregisterSecondaryWindow(SecondaryWindow window)
    {

        if (secondaryWindows.Remove(window))
        {
            Log.Info($"Unregistered secondary window: {window.Title}");
        }
    }

    private void SetRootNodeFromConfig(string scenePath)
    {
        if (string.IsNullOrEmpty(scenePath))
        {
            Log.Warning("MainScenePath is not defined in the configuration.");
            return;
        }

        try
        {
            var packedScene = new PackedScene(scenePath);
            SceneTree.Instance.RootNode = packedScene.Instantiate<Node>();
            Log.Info($"Loaded main scene: {scenePath}");
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to load main scene '{scenePath}': {ex.Message}");
            SceneTree.Instance.RootNode = new Node { Name = "ErrorRoot" };
        }
    }

    private static void CreateLogFile()
    {
        try
        {
            string? logDirectory = Path.GetDirectoryName(LogFilePath);
            if (!string.IsNullOrEmpty(logDirectory) && !Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            if (File.Exists(LogFilePath))
            {
                File.Delete(LogFilePath);
            }

            using (File.Create(LogFilePath)) { }
            Log.Info($"Log file created at {Path.GetFullPath(LogFilePath)}");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[FATAL] Failed to create log file: {ex.Message}");
            Console.ResetColor();
        }
    }

    private static void SetCurrentDirectory()
    {
        try
        {
            string? assemblyLocation = Assembly.GetEntryAssembly()?.Location;
            if (string.IsNullOrEmpty(assemblyLocation))
            {
                Log.Warning("Could not get assembly location.");
                return;
            }

            string? directoryName = Path.GetDirectoryName(assemblyLocation);
            if (string.IsNullOrEmpty(directoryName))
            {
                Log.Warning($"Could not get directory name from assembly location: {assemblyLocation}");
                return;
            }

            Environment.CurrentDirectory = directoryName;
            Log.Info($"Current directory set to: {directoryName}");
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to set current directory: {ex.Message}");
        }
    }

    private Configuration? LoadConfig()
    {
        if (!File.Exists(ConfigFilePath))
        {
            Log.Error($"Configuration file not found: {ConfigFilePath}");
            return null;
        }

        try
        {
            var deserializer = new DeserializerBuilder().Build();
            string yaml = File.ReadAllText(ConfigFilePath);
            var config = deserializer.Deserialize<Configuration>(yaml);
            Log.Info("Configuration loaded successfully.");
            return config;
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to load or parse configuration file '{ConfigFilePath}': {ex.Message}");
            return null;
        }
    }

    private void ApplyConfig()
    {
        if (applicationConfig is null)
        {
            Log.Error("Cannot apply configuration because it was not loaded.");
            return;
        }


        SetRootNodeFromConfig(applicationConfig.MainScenePath);
    }
}