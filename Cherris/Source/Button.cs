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
            return;
        }


        HandleClicks();
        HandleKeyboardInput();

    }

    protected virtual void OnEnterPressed() { }

    private void HandleKeyboardInput()
    {


        bool enterPressed;
        var owningWindowNode = GetOwningWindowNode();

        if (owningWindowNode != null)
        {
            enterPressed = owningWindowNode.IsLocalKeyPressed(KeyCode.Enter);
        }
        else
        {
            enterPressed = Input.IsKeyPressed(KeyCode.Enter);
        }

        if (!Focused || !enterPressed)
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
        bool leftClickInvoked = false;
        bool rightClickInvoked = false;


        if (Behavior == ClickBehavior.Left || Behavior == ClickBehavior.Both)
        {
            leftClickInvoked = HandleSingleClick(ref pressedLeft, MouseButtonCode.Left, LeftClickActionMode, LeftClicked);
        }

        if (Behavior == ClickBehavior.Right || Behavior == ClickBehavior.Both)
        {
            rightClickInvoked = HandleSingleClick(ref pressedRight, MouseButtonCode.Right, RightClickActionMode, RightClicked);
        }

        HandleHover(isMouseOver);

        UpdateTheme(isMouseOver, pressedLeft || pressedRight);
    }

    private bool HandleSingleClick(ref bool pressedState, MouseButtonCode button, ActionMode mode, Action<Button>? handler)
    {
        bool invoked = false;
        bool mouseOver = IsMouseOver();
        bool buttonPressedThisFrame;
        bool buttonReleasedThisFrame;

        var owningWindowNode = GetOwningWindowNode();

        if (owningWindowNode != null)
        {
            buttonPressedThisFrame = owningWindowNode.IsLocalMouseButtonPressed(button);
            buttonReleasedThisFrame = owningWindowNode.IsLocalMouseButtonReleased(button);
        }
        else
        {
            buttonPressedThisFrame = Input.IsMouseButtonPressed(button);
            buttonReleasedThisFrame = Input.IsMouseButtonReleased(button);
        }


        if (mouseOver && buttonPressedThisFrame && !Disabled)
        {
            pressedState = true;
            HandleClickFocus();

            if (mode == ActionMode.Press)
            {
                handler?.Invoke(this);
                ClickSound?.Play(AudioBus);
                invoked = true;
            }
        }


        if (buttonReleasedThisFrame)
        {
            if (pressedState)
            {
                if (!Disabled && mouseOver && mode == ActionMode.Release)
                {
                    handler?.Invoke(this);
                    ClickSound?.Play(AudioBus);
                    invoked = true;
                }


                if (!StayPressed)
                {
                    pressedState = false;
                }
                else if (!mouseOver && mode == ActionMode.Release)
                {

                    pressedState = false;
                }
                else if (mode == ActionMode.Press && !mouseOver)
                {


                    pressedState = false;
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
                MouseEntered?.Invoke(this);
                HoverSound?.Play(AudioBus);
                wasHovered = true;
            }
        }
        else
        {
            if (wasHovered)
            {
                wasHovered = false;
                MouseExited?.Invoke(this);
            }
        }
    }

    private void UpdateTheme(bool isMouseOver, bool isPressedForStayPressed)
    {
        if (Disabled)
        {
            Themes.Current = Themes.Disabled;
            return;
        }

        bool isLeftDown;
        bool isRightDown;
        var owningWindowNode = GetOwningWindowNode();

        if (owningWindowNode != null)
        {
            isLeftDown = (Behavior == ClickBehavior.Left || Behavior == ClickBehavior.Both) && owningWindowNode.IsLocalMouseButtonDown(MouseButtonCode.Left);
            isRightDown = (Behavior == ClickBehavior.Right || Behavior == ClickBehavior.Both) && owningWindowNode.IsLocalMouseButtonDown(MouseButtonCode.Right);
        }
        else
        {
            isLeftDown = (Behavior == ClickBehavior.Left || Behavior == ClickBehavior.Both) && Input.IsMouseButtonDown(MouseButtonCode.Left);
            isRightDown = (Behavior == ClickBehavior.Right || Behavior == ClickBehavior.Both) && Input.IsMouseButtonDown(MouseButtonCode.Right);
        }

        bool isPhysicallyHeldDown = isMouseOver && (isLeftDown || isRightDown);


        if (isPressedForStayPressed && StayPressed)
        {
            Themes.Current = Themes.Pressed;
        }
        else if (isPhysicallyHeldDown)
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

        if (input.Length > Ellipsis.Length)
        {
            return input.Substring(0, input.Length - Ellipsis.Length) + Ellipsis;
        }
        return input;
    }
}