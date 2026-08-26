#!/usr/bin/env python3
"""Apply Unity / IL2CPP adaptations to OpenCvSharp NativeMethods.cs after packaging.

Transforms the upstream file so that:
  - iOS P/Invoke targets __Internal
  - LoadLibraries skips Win32 LoadLibrary on non-Windows (iOS IsUnix is unreliable)
  - redirectError uses ExceptionHandler (not lambdas — IL2CPP-unsafe)
  - ErrorHandler* callbacks are static MonoPInvokeCallback methods
  - HandleException always surfaces native exceptions in Unity
"""
from __future__ import annotations

import re
import sys
from pathlib import Path


DLL_EXTERN_UPSTREAM = '    public const string DllExtern = "OpenCvSharpExtern";'

DLL_EXTERN_UNITY = '''    // iOS static libraries are linked into the player, so P/Invoke must target
    // `__Internal` (Unity-adaptation mirrored from the reference OpenCV-plus-Unity).
#if !UNITY_EDITOR && UNITY_IOS
    public const string DllExtern = "__Internal";
#else
    public const string DllExtern = "OpenCvSharpExtern";
#endif'''

CCTOR_UPSTREAM = '''    static NativeMethods()
    {
        LoadLibraries(WindowsLibraryLoader.Instance.AdditionalPaths);

        // call cv to enable redirecting 
        TryPInvoke();
    }'''

CCTOR_UNITY = '''    static NativeMethods()
    {
        LoadLibraries(WindowsLibraryLoader.Instance.AdditionalPaths);

        // call cv to enable redirecting 
        TryPInvoke();

        // Redirect native OpenCV errors into a managed callback so exceptions
        // propagate on every platform (Unity included), not just DOTNETCORE.
        // Must run after TryPInvoke so the native plugin is resolvable, and must
        // use ExceptionHandler's IL2CPP-safe static callback (not a lambda).
        ExceptionHandler.RegisterExceptionCallback();
    }'''

HANDLE_EXCEPTION_UPSTREAM = '''    public static void HandleException(ExceptionStatus status)
    {
#if DOTNETCORE
        // Check if there has been an exception
        if (status == ExceptionStatus.Occurred /*&& IsUnix()*/) // thrown can be 1 when unix 
        {
            ExceptionHandler.ThrowPossibleException();
        }
#else
#endif
    }'''

HANDLE_EXCEPTION_UNITY = '''    public static void HandleException(ExceptionStatus status)
    {
        // Check if there has been an exception
        if (status == ExceptionStatus.Occurred /*&& IsUnix()*/) // thrown can be 1 when unix 
        {
            ExceptionHandler.ThrowPossibleException();
        }
    }'''

# Matches both collection-expression `[]` and cs11ify'd `new string[0]` / `Array.Empty<string>()`.
LOAD_LIBRARIES_RE = re.compile(
    r'''    public static void LoadLibraries\(IEnumerable<string>\? additionalPaths = null\)
    \{
        if \(IsWasm\(\)\)
        \{
            return;
        \}

        if \(IsUnix\(\)\)
        \{
#if DOTNETCORE
            ExceptionHandler\.RegisterExceptionCallback\(\);
#endif
            return;
        \}

        var ap = \(additionalPaths is null\) \? .+? : additionalPaths\.ToArray\(\);

        /\*
        if \(Environment\.Is64BitProcess\)
            WindowsLibraryLoader\.Instance\.LoadLibrary\(DllFfmpegX64, ap\);
        else
            WindowsLibraryLoader\.Instance\.LoadLibrary\(DllFfmpegX86, ap\);
        //\*/
        WindowsLibraryLoader\.Instance\.LoadLibrary\(DllExtern, ap\);

        // Redirection of error occurred in native library 
        var zero = IntPtr\.Zero;
        var current = redirectError\(ErrorHandlerThrowException, zero, ref zero\);
        GC\.KeepAlive\(current\);
    }''',
    re.MULTILINE,
)

LOAD_LIBRARIES_UNITY = '''    public static void LoadLibraries(IEnumerable<string>? additionalPaths = null)
    {
        if (IsWasm())
        {
            return;
        }

        // Prefer !IsWindows() over IsUnix(): on Unity iOS IL2CPP,
        // RuntimeInformation.IsOSPlatform(OSX) is false (iOS ≠ OSX), so IsUnix()
        // wrongly falls through into the Win32 LoadLibrary + redirectError path.
        // Unity already loads plugins; only Windows needs explicit LoadLibrary.
        if (!IsWindows())
        {
            return;
        }

        var ap = (additionalPaths is null) ? new string[0] : additionalPaths.ToArray();

        /*
        if (Environment.Is64BitProcess)
            WindowsLibraryLoader.Instance.LoadLibrary(DllFfmpegX64, ap);
        else
            WindowsLibraryLoader.Instance.LoadLibrary(DllFfmpegX86, ap);
        //*/
        WindowsLibraryLoader.Instance.LoadLibrary(DllExtern, ap);

        // Error redirection is registered once in the static constructor via
        // ExceptionHandler (IL2CPP-safe). Do not call redirectError with a
        // lambda here — that crashes IL2CPP players.
    }'''

ERROR_HANDLERS_UPSTREAM = '''    /// <summary>
    /// Custom error handler to be thrown by OpenCV
    /// </summary>
    public static readonly CvErrorCallback ErrorHandlerThrowException =
        // ReSharper disable once UnusedParameter.Local
        (status, funcName, errMsg, fileName, line, userData) => throw new OpenCVException(status, funcName, errMsg, fileName, line);

    /// <summary>
    /// Custom error handler to ignore all OpenCV errors
    /// </summary>
    // ReSharper disable UnusedParameter.Local
    public static readonly CvErrorCallback ErrorHandlerIgnorance =
        (status, funcName, errMsg, fileName, line, userData) => 0;
    // ReSharper restore UnusedParameter.Local'''

ERROR_HANDLERS_UNITY = '''    /// <summary>
    /// Custom error handler to be thrown by OpenCV
    /// </summary>
    public static readonly CvErrorCallback ErrorHandlerThrowException = ErrorHandlerThrowExceptionImpl;

    [MonoPInvokeCallback(typeof(CvErrorCallback))]
    private static int ErrorHandlerThrowExceptionImpl(
        ErrorCode status, string funcName, string errMsg, string fileName, int line, IntPtr userData)
    {
        throw new OpenCVException(status, funcName, errMsg, fileName, line);
    }

    /// <summary>
    /// Custom error handler to ignore all OpenCV errors
    /// </summary>
    public static readonly CvErrorCallback ErrorHandlerIgnorance = ErrorHandlerIgnoranceImpl;

    [MonoPInvokeCallback(typeof(CvErrorCallback))]
    private static int ErrorHandlerIgnoranceImpl(
        ErrorCode status, string funcName, string errMsg, string fileName, int line, IntPtr userData)
    {
        return 0;
    }'''


def patch(text: str) -> str:
    replacements = [
        ("DllExtern", DLL_EXTERN_UPSTREAM, DLL_EXTERN_UNITY),
        ("static ctor", CCTOR_UPSTREAM, CCTOR_UNITY),
        ("HandleException", HANDLE_EXCEPTION_UPSTREAM, HANDLE_EXCEPTION_UNITY),
        ("ErrorHandlers", ERROR_HANDLERS_UPSTREAM, ERROR_HANDLERS_UNITY),
    ]
    for name, old, new in replacements:
        if old not in text:
            raise SystemExit(f"patch-nativemethods-unity: failed to find block: {name}")
        text = text.replace(old, new, 1)

    if not LOAD_LIBRARIES_RE.search(text):
        raise SystemExit("patch-nativemethods-unity: failed to find block: LoadLibraries")
    text = LOAD_LIBRARIES_RE.sub(LOAD_LIBRARIES_UNITY, text, count=1)
    return text


def main() -> None:
    if len(sys.argv) != 2:
        raise SystemExit(f"usage: {sys.argv[0]} <NativeMethods.cs>")
    path = Path(sys.argv[1])
    original = path.read_text(encoding="utf-8")
    # Already patched (re-entrant packaging / local edits).
    if "MonoPInvokeCallback(typeof(CvErrorCallback))" in original and "!IsWindows()" in original:
        print(f"Already Unity-adapted: {path}")
        return
    path.write_text(patch(original), encoding="utf-8")
    print(f"Patched for Unity/IL2CPP: {path}")


if __name__ == "__main__":
    main()
