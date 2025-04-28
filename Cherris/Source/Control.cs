using System;
using System.Collections.Generic;
using System.Numerics;
using Vortice.Mathematics;

namespace Cherris;

public class Control : ClickableRectangle
{
    private bool wasFocusedLastFrame = false;
    private readonly Dictionary<string, float> actionHoldTimes = [];
    private bool fieldDisabled = false;
    private bool fieldFocused = false;
    private static bool _verboseControlInputLog = false;


    public bool Focusable { get; set; } = true;
    public bool Navigable { get; set; } = true;
    public bool RapidNavigation { get; set; } = true;
    public string? FocusNeighborTop { get; set; }
    public string? FocusNeighborBottom { get; set; }
    public string? FocusNeighborLeft { get; set; }
    public string? FocusNeighborRight { get; set; }
    public string? FocusNeighborNext { get; set; }
    public string? FocusNeighborPrevious { get; set; }
    public string AudioBus { get; set; } = "Master";
    public Sound? FocusGainedSound { get; set; }

    private const float InitialDelay = 0.5f;
    private const float RepeatInterval = 0.1f;

    public bool Disabled
    {
        get => fieldDisabled;
        set
        {
            if (value == fieldDisabled)
            {
                return;
            }

            fieldDisabled = value;
            WasDisabled?.Invoke(this);
            if (value && fieldFocused)
            {
                Focused = false;
            }
        }
    }

    public bool Focused
    {
        get => fieldFocused;
        set
        {
            if (fieldFocused == value)
            {
                return;
            }
            fieldFocused = value;
            FocusChanged?.Invoke(this);

            if (fieldFocused)
            {
                FocusGained?.Invoke(this);

                FocusGainedSound?.Play(AudioBus);
            }
            else
            {
                actionHoldTimes.Clear();
            }
        }
    }

    public string ThemeFile
    {
        set
        {
            OnThemeFileChanged(value);
        }
    }


    public delegate void Event(Control control);
    public event Event? FocusChanged;
    public event Event? FocusGained;
    public event Event? WasDisabled;
    public event Event? ClickedOutside;


    public override void Process()
    {
        base.Process();


        if (Disabled)
        {

            if (Focused) Focused = false;
            return;
        }


        WindowNode? ownerWindow = GetOwningWindowNode();
        if (ownerWindow == null)
        {
            if (Navigable && Focused && wasFocusedLastFrame)
            {
                HandleArrowNavigation();
            }
            UpdateFocusOnOutsideClicked();
        }
        else
        {

            UpdateFocusOnOutsideClicked();
        }


        wasFocusedLastFrame = Focused;
    }


    private void HandleArrowNavigation()
    {

        var actions = new (string Action, string? Path)[]
        {
            ("UiLeft", FocusNeighborLeft),
            ("UiUp", FocusNeighborTop),
            ("UiRight", FocusNeighborRight),
            ("UiDown", FocusNeighborBottom),
            ("UiNext", FocusNeighborNext),
            ("UiPrevious", FocusNeighborPrevious)
        };


        foreach (var entry in actions)
        {
            if (string.IsNullOrEmpty(entry.Path)) continue;


            if (RapidNavigation)
            {
                if (Input.IsActionDown(entry.Action))
                {
                    if (!actionHoldTimes.ContainsKey(entry.Action))
                    {
                        actionHoldTimes[entry.Action] = 0f;
                    }

                    actionHoldTimes[entry.Action] += Time.Delta;
                    float holdTime = actionHoldTimes[entry.Action];

                    bool shouldNavigate = (holdTime <= Time.Delta + float.Epsilon) ||
                        (holdTime >= InitialDelay && (holdTime - InitialDelay) % RepeatInterval < Time.Delta);

                    if (shouldNavigate)
                    {
                        NavigateToControl(entry.Path, entry.Action, holdTime);
                    }
                }
                else
                {
                    actionHoldTimes[entry.Action] = 0f;
                }
            }
            else
            {
                if (Input.IsActionPressed(entry.Action))
                {
                    NavigateToControl(entry.Path, entry.Action, 0f);
                }
            }
        }
    }

    private void NavigateToControl(string controlPath, string action, float holdTime)
    {
        var neighbor = GetNodeOrNull<Control>(controlPath);


        if (neighbor is null)
        {
            Log.Error($"[Control] [{Name}] NavigateToControl: Could not find '{controlPath}'.");
            return;
        }


        if (neighbor.Disabled)
        {
            return;
        }


        if (RapidNavigation)
        {

            if (neighbor.GetOwningWindowNode() == null)
            {
                neighbor.actionHoldTimes[action] = holdTime;
            }
        }


        neighbor.Focused = true;
        Focused = false;
    }


    private void UpdateFocusOnOutsideClicked()
    {
        bool isPressed;
        WindowNode? ownerWindow = GetOwningWindowNode();


        if (ownerWindow != null)
        {

            isPressed = ownerWindow.IsLocalMouseButtonPressed(MouseButtonCode.Left);
        }
        else
        {

            isPressed = Input.IsMouseButtonPressed(MouseButtonCode.Left);
        }


        if (!IsMouseOver() && isPressed)
        {

            if (ownerWindow == null && Focused)
            {
                Focused = false;
                ClickedOutside?.Invoke(this);
            }

            else if (ownerWindow != null)
            {
                ClickedOutside?.Invoke(this);
            }
        }
    }

    protected virtual void HandleClickFocus()
    {

        WindowNode? ownerWindow = GetOwningWindowNode();
        if (ownerWindow == null && Focusable && IsMouseOver())
        {

            if (Input.IsMouseButtonPressed(MouseButtonCode.Left) || Input.IsMouseButtonPressed(MouseButtonCode.Right))
            {
                Focused = true;
            }
        }

    }


    protected virtual void OnThemeFileChanged(string themeFile) { }


    public override bool IsMouseOver()
    {
        Vector2 mousePosition;
        Rect bounds;
        WindowNode? ownerWindow = GetOwningWindowNode();


        var origin = Origin;
        var scaledSize = ScaledSize;


        if (ownerWindow != null)
        {

            mousePosition = ownerWindow.LocalMousePosition;


            var localPos = Position;


            bounds = new Rect(localPos.X - origin.X, localPos.Y - origin.Y, scaledSize.X, scaledSize.Y);


            Log.Info($"Control '{Name}' (Win: {ownerWindow.Name}): LocalMouse={mousePosition}, LocalPos={localPos}, Origin={origin}, ScaledSize={scaledSize}, Bounds={bounds}", _verboseControlInputLog);
        }
        else
        {

            mousePosition = Input.MousePosition;


            var globalPos = GlobalPosition;


            bounds = new Rect(globalPos.X - origin.X, globalPos.Y - origin.Y, scaledSize.X, scaledSize.Y);


            Log.Info($"Control '{Name}' (MainWin): GlobalMouse={mousePosition}, GlobalPos={globalPos}, Origin={origin}, ScaledSize={scaledSize}, Bounds={bounds}", _verboseControlInputLog);
        }



        bool contains = bounds.Contains(mousePosition);
        if (_verboseControlInputLog && contains != wasHoveredLastFrame) Log.Info($"Control '{Name}' IsMouseOver changed to {contains}");
        wasHoveredLastFrame = contains;
        return contains;
    }

    private bool wasHoveredLastFrame = false;
}