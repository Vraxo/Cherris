namespace Cherris;

public class Control : ClickableRectangle
{
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

    private bool wasFocusedLastFrame = false;
    private readonly Dictionary<string, float> actionHoldTimes = [];
    private const float InitialDelay = 0.5f;
    private const float RepeatInterval = 0.1f;

    public bool Disabled
    {
        get;
        set
        {
            if (value == field)
            {
                return;
            }

            field = value;
            WasDisabled?.Invoke(this);
        }
    } = false;

    public bool Focused
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }
            field = value;
            FocusChanged?.Invoke(this);

            if (field)
            {
                FocusGained?.Invoke(this);

                if (FocusGainedSound is not null)
                {
                    FocusGainedSound?.Play(AudioBus);
                }
            }
        }
    } = false;

    public string ThemeFile
    {
        set
        {
            OnThemeFileChanged(value);
        }
    }

    // Events

    public delegate void Event(Control control);
    public event Event? FocusChanged;
    public event Event? FocusGained;
    public event Event? WasDisabled;
    public event Event? ClickedOutside;

    // Main

    public override void Process()
    {
        base.Process();

        if (Navigable && Focused && wasFocusedLastFrame)
        {
            HandleArrowNavigation();
        }

        UpdateFocusOnOutsideClicked();
        wasFocusedLastFrame = Focused;
    }

    // Navigation

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
            neighbor.actionHoldTimes[action] = holdTime;
        }

        neighbor.Focused = true;
        Focused = false;
    }

    // Focus

    private void UpdateFocusOnOutsideClicked()
    {
        if (!IsMouseOver() && Input.IsMouseButtonPressed(MouseButtonCode.Left))
        {
            Focused = false;
            ClickedOutside?.Invoke(this);
        }
    }

    protected virtual void HandleClickFocus()
    {
        if (Focusable && IsMouseOver())
        {
            Focused = true;
        }
    }

    // Other

    protected virtual void OnThemeFileChanged(string themeFile) { }
}