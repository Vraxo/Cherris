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
    private readonly Stack<SecondaryWindow> modalStack = new();

    private const string ConfigFilePath = "Res/Cherris/Config.yaml";
    private const string LogFilePath = "Res/Cherris/Log.txt";
    private static readonly bool _verboseModalInputLog = false;

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

            ClickServer.Instance.Process();
            SceneTree.Instance.Process();

            mainWindow.RenderFrame();
            RenderSecondaryWindows();

            Input.Update();
        }
    }

    private void ProcessSystemMessages()
    {
        while (NativeMethods.PeekMessage(out NativeMethods.MSG msg, IntPtr.Zero, 0, 0, NativeMethods.PM_REMOVE))
        {
            if (msg.message == NativeMethods.WM_QUIT)
            {
                Log.Info("WM_QUIT received, signaling application close.");
                mainWindow?.Close();
                break;
            }

            bool processMessage = true;

            if (modalStack.Count > 0)
            {
                var topModal = modalStack.Peek();
                if (topModal != null && topModal.Handle != IntPtr.Zero)
                {
                    bool isMessageForTopModalWindow = (msg.hwnd == topModal.Handle);

                    if (!isMessageForTopModalWindow)
                    {
                        bool isMessageForAnyAncestorModal = false;
                        IntPtr ancestor = NativeMethods.GetAncestor(msg.hwnd, NativeMethods.GA_ROOT);
                        foreach (var modal in modalStack)
                        {
                            if (ancestor == modal.Handle)
                            {
                                isMessageForAnyAncestorModal = true;
                                break;
                            }
                        }

                        if (!isMessageForAnyAncestorModal)
                        {
                            bool isMouseKey = (msg.message >= NativeMethods.WM_MOUSEFIRST && msg.message <= NativeMethods.WM_MOUSELAST ||
                                              msg.message >= NativeMethods.WM_KEYFIRST && msg.message <= NativeMethods.WM_KEYLAST);

                            if (isMouseKey)
                            {
                                if (_verboseModalInputLog)
                                {
                                    Log.Info($"Modal Filter: Discarding msg=0x{msg.message:X} for hwnd={msg.hwnd}. TopModal='{topModal.Title}' ({topModal.Handle}). Reason: Not for TopModal or Ancestor.");
                                }
                                processMessage = false;
                            }
                        }
                    }

                    if (processMessage && _verboseModalInputLog)
                    {
                        Log.Info($"Modal Filter: Processing msg=0x{msg.message:X} for hwnd={msg.hwnd}. TopModal='{topModal.Title}' ({topModal.Handle}). Reason: Is for TopModal or Ancestor.");
                    }
                }
            }

            if (processMessage)
            {
                NativeMethods.TranslateMessage(ref msg);
                NativeMethods.DispatchMessage(ref msg);
            }
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
        modalStack.Clear();
        Log.Info("ApplicationCore Cleanup finished.");
    }

    private void CloseAllSecondaryWindows()
    {
        var windowsToClose = new List<SecondaryWindow>(secondaryWindows);

        foreach (var window in windowsToClose)
        {
            if (window.IsOpen)
            {
                window.Close();
            }
        }
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

            if (modalStack.Contains(window))
            {
                Log.Warning($"Window '{window.Title}' was potentially in modal stack during unregistration. Ensure UnregisterModal was called.");
            }
        }
    }

    internal void RegisterModal(SecondaryWindow window)
    {
        if (!modalStack.Contains(window))
        {
            Log.Info($"Pushing modal window '{window.Title}' ({window.Handle}) to stack.");
            modalStack.Push(window);

            NativeMethods.BringWindowToTop(window.Handle);
            NativeMethods.SetActiveWindow(window.Handle);
        }
    }

    internal void UnregisterModal(SecondaryWindow window)
    {
        if (modalStack.Count > 0 && modalStack.Peek() == window)
        {
            Log.Info($"Popping modal window '{window.Title}' ({window.Handle}) from stack.");
            modalStack.Pop();

            ActivateTopWindow();
        }
        else if (modalStack.Contains(window))
        {
            Log.Error($"Attempted to unregister modal window '{window.Title}' which is not on top of the stack!");

            var tempList = new List<SecondaryWindow>(modalStack);
            tempList.Remove(window);
            modalStack.Clear();
            for (int i = tempList.Count - 1; i >= 0; i--) modalStack.Push(tempList[i]);
            ActivateTopWindow();
        }
    }

    private void ActivateTopWindow()
    {
        IntPtr hwndToActivate = IntPtr.Zero;
        string windowTitle = "None";

        if (modalStack.Count > 0)
        {
            hwndToActivate = modalStack.Peek().Handle;
            windowTitle = modalStack.Peek().Title;
            Log.Info($"Activating next modal window: {windowTitle} ({hwndToActivate})");
        }
        else if (mainWindow != null && mainWindow.Handle != IntPtr.Zero)
        {
            hwndToActivate = mainWindow.Handle;
            windowTitle = mainWindow.Title;
            Log.Info($"Activating main window: {windowTitle} ({hwndToActivate})");
        }

        if (hwndToActivate != IntPtr.Zero && NativeMethods.IsWindow(hwndToActivate))
        {
            NativeMethods.BringWindowToTop(hwndToActivate);
            NativeMethods.SetActiveWindow(hwndToActivate);
            Log.Info($"Called SetActiveWindow/BringWindowToTop for '{windowTitle}' ({hwndToActivate}).");
        }
        else
        {
            Log.Warning($"Could not activate top window. Target HWND {hwndToActivate} is invalid or zero.");
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