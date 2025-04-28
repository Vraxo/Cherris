// file: Input.cs
using System.Collections.Generic;
using System.Numerics;
using static System.Runtime.CompilerServices.RuntimeHelpers;

namespace Cherris;

public static class Input
{
    private static Vector2 _currentMousePosition = Vector2.Zero;
    private static readonly HashSet<MouseButtonCode> _currentMouseButtonsDown = [];
    private static readonly HashSet<MouseButtonCode> _previousMouseButtonsDown = [];
    private static readonly HashSet<KeyCode> _currentKeysDown = [];
    private static readonly HashSet<KeyCode> _previousKeysDown = [];
    private static float _mouseWheelMovement = 0f;

    private static readonly Dictionary<string, KeyCode> _positiveXActions = [];
    private static readonly Dictionary<string, KeyCode> _negativeXActions = [];
    private static readonly Dictionary<string, KeyCode> _positiveYActions = [];
    private static readonly Dictionary<string, KeyCode> _negativeYActions = [];


    public static void Update()
    {
        _previousMouseButtonsDown.Clear();
        foreach (var button in _currentMouseButtonsDown)
        {
            _previousMouseButtonsDown.Add(button);
        }

        _previousKeysDown.Clear();
        foreach (var key in _currentKeysDown)
        {
            _previousKeysDown.Add(key);
        }

        _mouseWheelMovement = 0f;
    }

    internal static void UpdateMouseButton(MouseButtonCode button, bool isDown)
    {
        if (isDown)
        {
            _currentMouseButtonsDown.Add(button);
        }
        else
        {
            _currentMouseButtonsDown.Remove(button);
        }
    }

    internal static void UpdateKey(KeyCode key, bool isDown)
    {
        if (isDown)
        {
            _currentKeysDown.Add(key);
        }
        else
        {
            _currentKeysDown.Remove(key);
        }
    }

    internal static void UpdateMousePosition(Vector2 position)
    {
        _currentMousePosition = position;
    }

    internal static void UpdateMouseWheel(float delta)
    {
        _mouseWheelMovement = delta;
    }


    public static bool IsActionDown(string actionName)
    {
        if (_positiveXActions.TryGetValue(actionName, out var posXKey) && IsKeyDown(posXKey)) return true;
        if (_negativeXActions.TryGetValue(actionName, out var negXKey) && IsKeyDown(negXKey)) return true;
        if (_positiveYActions.TryGetValue(actionName, out var posYKey) && IsKeyDown(posYKey)) return true;
        if (_negativeYActions.TryGetValue(actionName, out var negYKey) && IsKeyDown(negYKey)) return true;

        return false;
    }

    public static bool IsActionPressed(string actionName)
    {
        if (_positiveXActions.TryGetValue(actionName, out var posXKey) && IsKeyPressed(posXKey)) return true;
        if (_negativeXActions.TryGetValue(actionName, out var negXKey) && IsKeyPressed(negXKey)) return true;
        if (_positiveYActions.TryGetValue(actionName, out var posYKey) && IsKeyPressed(posYKey)) return true;
        if (_negativeYActions.TryGetValue(actionName, out var negYKey) && IsKeyPressed(negYKey)) return true;
        return false;
    }


    public static bool IsKeyPressed(KeyCode keyboardKey)
    {
        return _currentKeysDown.Contains(keyboardKey) && !_previousKeysDown.Contains(keyboardKey);
    }

    public static bool IsKeyReleased(KeyCode keyboardKey)
    {
        return !_currentKeysDown.Contains(keyboardKey) && _previousKeysDown.Contains(keyboardKey);
    }

    public static bool IsKeyDown(KeyCode keyboardKey)
    {
        return _currentKeysDown.Contains(keyboardKey);
    }


    public static bool IsMouseButtonPressed(MouseButtonCode button)
    {
        return _currentMouseButtonsDown.Contains(button) && !_previousMouseButtonsDown.Contains(button);
    }

    public static bool IsMouseButtonReleased(MouseButtonCode button)
    {
        return !_currentMouseButtonsDown.Contains(button) && _previousMouseButtonsDown.Contains(button);
    }

    public static bool IsMouseButtonDown(MouseButtonCode button)
    {
        return _currentMouseButtonsDown.Contains(button);
    }

    public static float GetMouseWheelMovement()
    {
        return _mouseWheelMovement;
    }

    public static Vector2 MousePosition => _currentMousePosition;

    public static Vector2 WorldMousePosition => _currentMousePosition;

    //public static MouseCursorCode Cursor
    //{
    //
    //    set { }
    //}

    public static Vector2 GetVector(string negativeX, string positiveX, string negativeY, string positiveY, float deadzone = -1.0f)
    {
        float x = 0.0f;
        float y = 0.0f;

        if (_positiveXActions.TryGetValue(positiveX, out var posXKey) && IsKeyDown(posXKey)) x += 1.0f;
        if (_negativeXActions.TryGetValue(negativeX, out var negXKey) && IsKeyDown(negXKey)) x -= 1.0f;
        if (_positiveYActions.TryGetValue(positiveY, out var posYKey) && IsKeyDown(posYKey)) y += 1.0f;
        if (_negativeYActions.TryGetValue(negativeY, out var negYKey) && IsKeyDown(negYKey)) y -= 1.0f;

        var vector = new Vector2(x, y);

        if (deadzone < 0.0f)
        {
            return vector.LengthSquared() > 0 ? Vector2.Normalize(vector) : Vector2.Zero;
        }
        else
        {
            float length = vector.Length();
            if (length < deadzone)
            {
                return Vector2.Zero;
            }
            else
            {
                var normalized = vector / length;
                float mappedLength = (length - deadzone) / (1.0f - deadzone);
                return normalized * mappedLength;
            }
        }
    }

    public static void AddActionKey(string actionName, KeyCode key, bool isPositiveX = false, bool isNegativeX = false, bool isPositiveY = false, bool isNegativeY = false)
    {
        if (isPositiveX) _positiveXActions[actionName] = key;
        if (isNegativeX) _negativeXActions[actionName] = key;
        if (isPositiveY) _positiveYActions[actionName] = key;
        if (isNegativeY) _negativeYActions[actionName] = key;
    }

    public static void SetupDefaultActions()
    {
        AddActionKey("UiUp", KeyCode.UpArrow, isNegativeY: true);
        AddActionKey("UiUp", KeyCode.W, isNegativeY: true);
        AddActionKey("UiDown", KeyCode.DownArrow, isPositiveY: true);
        AddActionKey("UiDown", KeyCode.S, isPositiveY: true);
        AddActionKey("UiLeft", KeyCode.LeftArrow, isNegativeX: true);
        AddActionKey("UiLeft", KeyCode.A, isNegativeX: true);
        AddActionKey("UiRight", KeyCode.RightArrow, isPositiveX: true);
        AddActionKey("UiRight", KeyCode.D, isPositiveX: true);
        AddActionKey("UiAccept", KeyCode.Enter, isPositiveX: true);
        AddActionKey("UiAccept", KeyCode.Space, isPositiveX: true);
        AddActionKey("UiCancel", KeyCode.Escape, isPositiveX: true);
    }
}