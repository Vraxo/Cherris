using System;
using System.Collections.Generic;
using System.Reflection;

namespace Cherris;

public class Tween(Node creatorNode, Node.ProcessMode processMode = Node.ProcessMode.Inherit)
{
    public bool Active = true;

    private readonly List<TweenStep> steps = [];
    private readonly Node creatorNode = creatorNode;
    private readonly Node.ProcessMode processMode = processMode;

    private static readonly bool debug = false;

    public bool Stopped { get; private set; }

    public void Stop()
    {
        Stopped = true;
        Active = false;
    }

    public void TweenProperty(Node node, string propertyPath, float targetValue, float duration)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(node);

            if (string.IsNullOrEmpty(propertyPath))
            {
                throw new ArgumentException("Property path cannot be null or empty.", nameof(propertyPath));
            }

            Log.Info($"[Tween] Starting tween on {node.Name} for {propertyPath}", debug);
            
            float startValue = GetFloatValue(node, propertyPath);
            
            Log.Info($"[Tween] Start value: {startValue} ➔ Target: {targetValue} ({duration}s)", debug);

            steps.Add(new TweenStep(node, propertyPath, startValue, targetValue, duration));
        }
        catch (Exception ex)
        {
            Log.Error($"[Tween] Error starting tween: {ex}");
            Active = false;
        }
    }

    public void Update(float delta)
    {
        if (!Active || Stopped)
        {
            return;
        }

        foreach (TweenStep step in steps.ToList())
        {
            step.Elapsed += delta;
            float t = Math.Clamp(step.Elapsed / step.Duration, 0, 1);
            float currentValue = step.StartValue + (step.EndValue - step.StartValue) * t;

            Log.Info($"[Tween] Updating {step.Node.Name}.{step.PropertyPath} {currentValue:0.00} ({t:P0})", debug);

            SetFloatValueDirect(step.Node, step.PropertyPath, currentValue);

            if (step.Elapsed >= step.Duration)
            {
                Log.Info($"[Tween] Completed {step.Node.Name}.{step.PropertyPath}", debug);
                steps.Remove(step);
            }
        }

        if (steps.Count == 0)
        {
            Log.Info("[Tween] All steps completed", debug);
            Active = false;
        }
    }

    public bool ShouldProcess(bool treePaused)
    {
        var effectiveMode = processMode == Node.ProcessMode.Inherit
            ? GetEffectiveProcessMode(creatorNode)
            : processMode;

        return effectiveMode switch
        {
            Node.ProcessMode.Disabled => false,
            Node.ProcessMode.Always => true,
            Node.ProcessMode.Pausable => !treePaused,
            Node.ProcessMode.WhenPaused => treePaused,
            _ => false
        };
    }

    private static Node.ProcessMode GetEffectiveProcessMode(Node node)
    {
        Node? current = node;

        while (current != null)
        {
            if (current.ProcessingMode != Node.ProcessMode.Inherit)
            {
                return current.ProcessingMode;
            }

            current = current.Parent;
        }

        return Node.ProcessMode.Pausable;
    }

    private static float GetFloatValue(Node node, string propertyPath)
    {
        object? current = node;

        foreach (string part in propertyPath.Split('/'))
        {
            if (current is null)
            {
                throw new InvalidOperationException(
                    $"Intermediate value is null in path '{propertyPath}' on node {node.Name}");
            }

            Type type = current.GetType();
            PropertyInfo? property = type.GetProperty(part);
            FieldInfo? field = type.GetField(part);

            MemberInfo? member = property ?? (MemberInfo?)field
                ?? throw new ArgumentException($"Property or field '{part}' not found in {type.Name}");

            current = member is PropertyInfo prop
                ? prop.GetValue(current)
                : ((FieldInfo)member).GetValue(current);
        }

        return current is not null
            ? (float)current
            : throw new InvalidOperationException($"Value for path '{propertyPath}' is null on node {node.Name}");
    }

    private static void SetFloatValueDirect(Node node, string propertyPath, float value)
    {
        string[] parts = propertyPath.Split('/');
        object? current = node;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (current is null)
            {
                throw new InvalidOperationException(
                    $"Intermediate value is null in path '{propertyPath}' on node {node.Name}");
            }

            Type type = current.GetType();
            PropertyInfo? property = type.GetProperty(parts[i]);
            FieldInfo? field = type.GetField(parts[i]);

            MemberInfo? member = property ?? (MemberInfo?)field
                ?? throw new ArgumentException($"Property or field '{parts[i]}' not found in {type.Name}");

            current = member is PropertyInfo prop
                ? prop.GetValue(current)
                : ((FieldInfo)member).GetValue(current);
        }

        if (current is null)
        {
            throw new InvalidOperationException(
                $"Final target is null in path '{propertyPath}' on node {node.Name}");
        }

        Type finalType = current.GetType();
        string finalPart = parts[^1];
        PropertyInfo? finalProperty = finalType.GetProperty(finalPart);
        FieldInfo? finalField = finalType.GetField(finalPart);

        MemberInfo finalMember = finalProperty ?? (MemberInfo?)finalField
            ?? throw new ArgumentException($"Property or field '{finalPart}' not found in {finalType.Name}");

        if (finalMember is PropertyInfo targetProp)
        {
            targetProp.SetValue(current, value);
        }
        else if (finalMember is FieldInfo targetField)
        {
            targetField.SetValue(current, value);
        }
    }

    private record TweenStep(Node Node, string PropertyPath, float StartValue, float EndValue, float Duration)
    {
        public float Elapsed { get; set; }
    }
}