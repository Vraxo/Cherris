using System;
using System.Numerics;
using Vortice.Mathematics;
using D2D = Vortice.Direct2D1;
using DW = Vortice.DirectWrite;

namespace Cherris;

public class Button : Control
{
    public enum ActionMode { Release, Press }
    public enum ClickBehavior { Left, Right, Both }

    private bool pressedLeft = false;
    private bool pressedRight = false;
    private bool wasHovered = false;
    private string displayedText = "";
    private string _text = "";
    private static bool _verboseInputLog = false;

    public Vector2 TextOffset { get; set; } = Vector2.Zero;
    public HAlignment TextHAlignment { get; set; } = HAlignment.Center;
    public VAlignment TextVAlignment { get; set; } = VAlignment.Center;
    public ButtonStylePack Themes { get; set; } = new();
    public float AvailableWidth { get; set; } = 0;
    public ActionMode LeftClickActionMode { get; set; } = ActionMode.Release;
    public ActionMode RightClickActionMode { get; set; } = ActionMode.Release;
    public bool StayPressed { get; set; } = false;
    public bool ClipText { get; set; } = false;
    public bool AutoWidth { get; set; } = false;
    public Vector2 TextMargin { get; set; } = new(10, 5);
    public string Ellipsis { get; set; } = "...";
    public ClickBehavior Behavior { get; set; } = ClickBehavior.Left;
    public Texture? Icon { get; set; } = null;
    public float IconMargin { get; set; } = 12;
    public Sound? ClickSound { get; set; }
    public Sound? HoverSound { get; set; }

    public string Text
    {
        get => _text;
        set
        {
            if (_text == value) return;
            _text = value;
            displayedText = value;

        }
    }

    public event Action<Button>? LeftClicked;
    public event Action<Button>? RightClicked;
    public event Action<Button>? MouseEntered;
    public event Action<Button>? MouseExited;

    public Button()
    {
        Size = new(100, 26);
        Offset = Vector2.Zero;
        OriginPreset = OriginPreset.None;

        WasDisabled += (button) => Themes.Current = Disabled ? Themes.Disabled : Themes.Normal;

    }

    public override void Process()
    {
        base.Process();

        if (Disabled)
        {

            if (Themes.Current != Themes.Disabled) Themes.Current = Themes.Disabled;

            if (wasHovered) { wasHovered = false; MouseExited?.Invoke(this); }
            pressedLeft = false;
            pressedRight = false;
            return;
        }


        HandleClicks();



        UpdateTheme();
    }

    protected virtual void OnEnterPressed() { }


    private void HandleKeyboardInput()
    {

        if (!Focused || GetOwningWindowNode() != null)
        {
            return;
        }

        if (!Input.IsKeyPressed(KeyCode.Enter))
        {
            return;
        }

        bool invoked = false;
        if (Behavior == ClickBehavior.Left || Behavior == ClickBehavior.Both)
        {
            LeftClicked?.Invoke(this);
            invoked = true;
        }

        if (Behavior == ClickBehavior.Right || Behavior == ClickBehavior.Both)
        {
            RightClicked?.Invoke(this);
            invoked = true;
        }

        if (invoked)
        {
            ClickSound?.Play(AudioBus);
            OnEnterPressed();
        }
    }

    private void HandleClicks()
    {
        bool isMouseOver = IsMouseOver();
        WindowNode? ownerWindow = GetOwningWindowNode();
        Log.Info($"Button '{Name}' HandleClicks. IsMouseOver={isMouseOver}. OwnerWindow={(ownerWindow?.Name ?? "None")}", _verboseInputLog);


        bool leftClickInvoked = false;
        bool rightClickInvoked = false;


        if (Behavior == ClickBehavior.Left || Behavior == ClickBehavior.Both)
        {
            leftClickInvoked = HandleSingleClick(ownerWindow, ref pressedLeft, MouseButtonCode.Left, LeftClickActionMode, LeftClicked);
        }

        if (Behavior == ClickBehavior.Right || Behavior == ClickBehavior.Both)
        {
            rightClickInvoked = HandleSingleClick(ownerWindow, ref pressedRight, MouseButtonCode.Right, RightClickActionMode, RightClicked);
        }

        HandleHover(isMouseOver);
    }

    private bool HandleSingleClick(WindowNode? ownerWindow, ref bool pressedState, MouseButtonCode button, ActionMode mode, Action<Button>? handler)
    {
        bool invoked = false;
        bool mouseOver = IsMouseOver();


        bool buttonPressedThisFrame;
        bool buttonReleasedThisFrame;
        if (ownerWindow != null)
        {
            buttonPressedThisFrame = ownerWindow.IsLocalMouseButtonPressed(button);
            buttonReleasedThisFrame = ownerWindow.IsLocalMouseButtonReleased(button);
            Log.Info($"Button '{Name}' ({button}) LocalInput: Pressed={buttonPressedThisFrame}, Released={buttonReleasedThisFrame}", _verboseInputLog && (buttonPressedThisFrame || buttonReleasedThisFrame));
        }
        else
        {
            buttonPressedThisFrame = Input.IsMouseButtonPressed(button);
            buttonReleasedThisFrame = Input.IsMouseButtonReleased(button);
            Log.Info($"Button '{Name}' ({button}) GlobalInput: Pressed={buttonPressedThisFrame}, Released={buttonReleasedThisFrame}", _verboseInputLog && (buttonPressedThisFrame || buttonReleasedThisFrame));
        }


        if (mouseOver && buttonPressedThisFrame && !Disabled)
        {
            Log.Info($"Button '{Name}' ({button}) Pressed Over Control.", _verboseInputLog);
            pressedState = true;
            HandleClickFocus();

            if (mode == ActionMode.Press)
            {
                Log.Info($"Button '{Name}' ({button}) Invoking handler on Press.", _verboseInputLog);
                handler?.Invoke(this);
                ClickSound?.Play(AudioBus);
                invoked = true;
            }
        }


        if (buttonReleasedThisFrame)
        {
            Log.Info($"Button '{Name}' ({button}) Button released this frame. WasPressedState={pressedState}", _verboseInputLog);
            if (pressedState)
            {
                if (!Disabled && mouseOver && mode == ActionMode.Release)
                {
                    Log.Info($"Button '{Name}' ({button}) Invoking handler on Release.", _verboseInputLog);
                    handler?.Invoke(this);
                    ClickSound?.Play(AudioBus);
                    invoked = true;
                }


                if (!StayPressed)
                {
                    Log.Info($"Button '{Name}' ({button}) Resetting pressed state (Not StayPressed).", _verboseInputLog);
                    pressedState = false;
                }
                else
                {

                    Log.Info($"Button '{Name}' ({button}) Maintaining pressed state (StayPressed).", _verboseInputLog);
                }
            }
        }


        return invoked;
    }


    private void HandleHover(bool isMouseOver)
    {
        if (Disabled)
        {
            if (wasHovered)
            {
                wasHovered = false;
                MouseExited?.Invoke(this);
            }
            return;
        }

        if (isMouseOver)
        {
            if (!wasHovered)
            {
                Log.Info($"Button '{Name}' Mouse Entered.", _verboseInputLog);
                MouseEntered?.Invoke(this);
                HoverSound?.Play(AudioBus);
                wasHovered = true;
            }
        }
        else
        {
            if (wasHovered)
            {
                Log.Info($"Button '{Name}' Mouse Exited.", _verboseInputLog);
                wasHovered = false;
                MouseExited?.Invoke(this);
            }

            if (!StayPressed)
            {
                if (pressedLeft || pressedRight) Log.Info($"Button '{Name}' Resetting pressed state on mouse exit.", _verboseInputLog);
                pressedLeft = false;
                pressedRight = false;
            }
        }
    }

    private void UpdateTheme()
    {
        if (Disabled)
        {
            if (Themes.Current != Themes.Disabled) Themes.Current = Themes.Disabled;
            return;
        }

        bool isMouseOver = IsMouseOver();
        WindowNode? ownerWindow = GetOwningWindowNode();


        bool isLeftDown;
        bool isRightDown;
        if (ownerWindow != null)
        {
            isLeftDown = ownerWindow.IsLocalMouseButtonDown(MouseButtonCode.Left);
            isRightDown = ownerWindow.IsLocalMouseButtonDown(MouseButtonCode.Right);
        }
        else
        {
            isLeftDown = Input.IsMouseButtonDown(MouseButtonCode.Left);
            isRightDown = Input.IsMouseButtonDown(MouseButtonCode.Right);
        }

        bool isPhysicallyHeldDown = isMouseOver &&
            ((Behavior == ClickBehavior.Left || Behavior == ClickBehavior.Both) && isLeftDown ||
             (Behavior == ClickBehavior.Right || Behavior == ClickBehavior.Both) && isRightDown);


        bool isEffectivelyPressed = (pressedLeft || pressedRight) || isPhysicallyHeldDown;



        var oldTheme = Themes.Current;

        if (isEffectivelyPressed)
        {
            Themes.Current = Themes.Pressed;
        }
        else if (Focused)
        {
            Themes.Current = isMouseOver ? Themes.Hover : Themes.Focused;
        }
        else if (isMouseOver)
        {
            Themes.Current = Themes.Hover;
        }
        else
        {
            Themes.Current = Themes.Normal;
        }

        if (oldTheme != Themes.Current) Log.Info($"Button '{Name}' Theme changed to {Themes.Current.GetType().Name}", _verboseInputLog);
    }

    protected override void OnThemeFileChanged(string themeFile)
    {

        Log.Warning($"OnThemeFileChanged not fully implemented for Button: {themeFile}");

    }


    public override void Draw(DrawingContext context)
    {
        if (!Visible) return;

        DrawBackground(context);

        DrawText(context);
    }

    private void DrawBackground(DrawingContext context)
    {
        var position = GlobalPosition - Origin;
        var size = ScaledSize;
        var bounds = new Rect(position.X, position.Y, size.X, size.Y);

        DrawStyledRectangle(context, bounds, Themes.Current);
    }

    private void DrawIcon(DrawingContext context)
    {
        if (Icon is null || context.RenderTarget is null)
        {
            return;
        }


        Log.Warning("DrawIcon is not implemented.");

    }

    private void DrawText(DrawingContext context)
    {
        if (Themes.Current is null) return;

        var position = GlobalPosition - Origin;
        var size = ScaledSize;


        var textLayoutRect = new Rect(
            position.X + TextMargin.X + TextOffset.X,
            position.Y + TextMargin.Y + TextOffset.Y,
            Math.Max(0, size.X - TextMargin.X * 2),
            Math.Max(0, size.Y - TextMargin.Y * 2)
        );


        DrawFormattedText(
            context,
            displayedText,
            textLayoutRect,
            Themes.Current,
            TextHAlignment,
            TextVAlignment
        );
    }


    private void ResizeToFitText()
    {

        if (!AutoWidth || Themes?.Current is null)
        {
            return;
        }


        Log.Warning("ResizeToFitText requires DirectWrite implementation.");


    }

    private void ClipDisplayedText()
    {

        if (!ClipText || string.IsNullOrEmpty(Text) || Themes?.Current is null)
        {
            displayedText = Text;
            return;
        }


        Log.Warning("ClipDisplayedText requires DirectWrite implementation.");




        displayedText = Text;
    }

    private string GetTextClippedWithEllipsis(string input)
    {

        if (input.Length > Ellipsis.Length + 5)
        {
            return input.Substring(0, input.Length - Ellipsis.Length - 2) + Ellipsis;
        }
        return input;
    }
}