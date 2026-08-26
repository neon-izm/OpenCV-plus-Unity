namespace OpenCvSharp.Internal;

/// <summary>
/// Marks a static method that may be marshaled to a native function pointer.
/// IL2CPP (iOS / Android / etc.) requires this attribute by name; the declaring
/// namespace does not matter. Kept local so the managed layer does not depend on
/// UnityEngine.AOT for the attribute type alone.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class MonoPInvokeCallbackAttribute : Attribute
{
    public MonoPInvokeCallbackAttribute(Type type)
    {
        Type = type;
    }

    public Type Type { get; }
}
