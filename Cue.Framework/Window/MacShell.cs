using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Cue.Framework.Window;

/// <summary>
/// The Mac's versions of what <see cref="WindowsShell"/> does on Windows: its own Open and Save dialogs and alerts
/// (through AppleScript, run by osascript), the Trash (through Foundation's file manager, so Put Back works), and
/// keeping the Mac awake (through caffeinate).
/// </summary>
public static partial class MacShell
{
    /// <summary>
    /// <paramref name="text"/> as an AppleScript string, in quotes, with its quotes and backslashes escaped.
    /// </summary>
    public static string Quote(string text) => "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    /// <summary>
    /// The extensions in Windows-style <paramref name="patterns"/> ("*.wav;*.cue" gives wav and cue); none for "*.*".
    /// </summary>
    public static IReadOnlyList<string> Extensions(string patterns) =>
        patterns
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(pattern => pattern.TrimStart('*', '.'))
            .Where(extension => extension.Length > 0 && extension != "*")
            .ToList();

    /// <summary>
    /// The AppleScript asking for a file to open of one of <paramref name="extensions"/> (any, if none), starting in
    /// <paramref name="folder"/>, and giving its POSIX path.
    /// </summary>
    public static string ChooseFileScript(string title, IReadOnlyList<string> extensions, string? folder)
    {
        var script = new StringBuilder($"POSIX path of (choose file with prompt {Quote(title)}");
        if (extensions.Count > 0)
            script.Append(" of type {").Append(string.Join(", ", extensions.Select(Quote))).Append('}');
        if (folder is not null && Directory.Exists(folder))
            script.Append(" default location (POSIX file ").Append(Quote(folder)).Append(')');
        return script.Append(')').ToString();
    }

    /// <summary>
    /// The AppleScript asking where to save, starting with <paramref name="fileName"/> in <paramref name="folder"/>,
    /// and giving the POSIX path. The Mac's dialog asks before replacing a file there.
    /// </summary>
    public static string ChooseFileNameScript(string title, string fileName, string? folder)
    {
        var script = new StringBuilder($"POSIX path of (choose file name with prompt {Quote(title)} default name {Quote(fileName)}");
        if (folder is not null && Directory.Exists(folder))
            script.Append(" default location (POSIX file ").Append(Quote(folder)).Append(')');
        return script.Append(')').ToString();
    }

    /// <summary>
    /// Asks for a file to open; its path, or null if none was chosen.
    /// </summary>
    [SupportedOSPlatform("macos")]
    public static string? AskForFile(string title, string patterns, string? folder) =>
        Run(ChooseFileScript(title, Extensions(patterns), folder)) is { Length: > 0 } path ? path : null;

    /// <summary>
    /// Asks where to save, offering <paramref name="types"/>. The Mac's dialog has no list of types, so the type is
    /// the one whose extension was typed, or the first, whose extension goes on if none was.
    /// </summary>
    [SupportedOSPlatform("macos")]
    public static (string Path, int Type)? AskWhereToSave(string title, IReadOnlyList<(string Description, string Pattern)> types, string fileName, string? folder)
    {
        var first = types[0].Pattern.TrimStart('*', '.');
        var suggested = Path.HasExtension(fileName) ? fileName : $"{fileName}.{first}";
        if (Run(ChooseFileNameScript(title, suggested, folder)) is not { Length: > 0 } path)
            return null;
        return TypeOf(path, types);
    }

    /// <summary>
    /// <paramref name="path"/> with the type its extension says, from <paramref name="types"/>; the first type, with
    /// its extension put on, if it has none of theirs.
    /// </summary>
    public static (string Path, int Type) TypeOf(string path, IReadOnlyList<(string Description, string Pattern)> types)
    {
        var extension = Path.GetExtension(path).TrimStart('.');
        for (var i = 0; i < types.Count; i++)
        {
            if (Extensions(types[i].Pattern).Contains(extension, StringComparer.OrdinalIgnoreCase))
                return (path, i);
        }
        return (Path.HasExtension(path) ? path : path + "." + types[0].Pattern.TrimStart('*', '.'), 0);
    }

    /// <summary>
    /// Shows an alert, and waits for it to be put away.
    /// </summary>
    [SupportedOSPlatform("macos")]
    public static void Alert(string title, string message) =>
        Run($"display alert {Quote(title)} message {Quote(message.Length > 2000 ? message[..2000] + "..." : message)} as critical");

    /// <summary>
    /// Runs <paramref name="script"/>; what it printed, without the line end, or null if it failed or was cancelled.
    /// </summary>
    static string? Run(string script)
    {
        try
        {
            var start = new ProcessStartInfo("/usr/bin/osascript") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            start.ArgumentList.Add("-e");
            start.ArgumentList.Add(script);
            using var process = Process.Start(start);
            if (process is null)
                return null;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? output.TrimEnd('\n', '\r') : null;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return null;
        }
    }

    static Process? _caffeinate;

    /// <summary>
    /// Keeps the Mac and its screen awake for as long as the app runs, or lets them sleep again.
    /// </summary>
    [SupportedOSPlatform("macos")]
    public static bool KeepAwake(bool awake)
    {
        try
        {
            if (awake)
            {
                if (_caffeinate is { HasExited: false })
                    return true;
                var start = new ProcessStartInfo("/usr/bin/caffeinate") { UseShellExecute = false };
                // The display and the system awake, until told otherwise or the app's gone.
                foreach (var argument in new[] { "-d", "-i", "-w", Environment.ProcessId.ToString() })
                    start.ArgumentList.Add(argument);
                _caffeinate = Process.Start(start);
                return _caffeinate is not null;
            }
            if (_caffeinate is { HasExited: false })
                _caffeinate.Kill();
            _caffeinate?.Dispose();
            _caffeinate = null;
            return true;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    /// Moves a file to the Trash, so it can be put back. Returns why it couldn't, or null once it's gone.
    /// </summary>
    [SupportedOSPlatform("macos")]
    public static unsafe string? MoveToTrash(string path)
    {
        // What Foundation hands back is released with this pool, whichever thread this is on.
        nint pool = 0;
        try
        {
            pool = objc_autoreleasePoolPush();
            var manager = Send(Class("NSFileManager"), Selector("defaultManager"));
            var url = Send(Class("NSURL"), Selector("fileURLWithPath:"), NSString(Path.GetFullPath(path)));
            nint error = 0;
            var moved = SendBool(manager, Selector("trashItemAtURL:resultingItemURL:error:"), url, 0, &error);
            if (moved)
                return null;
            var reason = error != 0 ? FromNSString(Send(error, Selector("localizedDescription"))) : null;
            return reason ?? "macOS couldn't move it to the Trash";
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            return "There's no Trash here";
        }
        finally
        {
            if (pool != 0)
                objc_autoreleasePoolPop(pool);
        }
    }

    // Foundation through the Objective-C runtime: a class by name, a selector by name, and messages to objects.
    const string OBJC = "/usr/lib/libobjc.A.dylib";

    static nint Class(string name) => objc_getClass(name);

    static nint Selector(string name) => sel_registerName(name);

    static nint NSString(string text) => Send(Class("NSString"), Selector("stringWithUTF8String:"), text);

    static string? FromNSString(nint text) => text == 0 ? null : Marshal.PtrToStringUTF8(Send(text, Selector("UTF8String")));

    static unsafe bool SendBool(nint receiver, nint selector, nint a, nint b, nint* c) => SendBoolNative(receiver, selector, a, b, c) != 0;

    [LibraryImport(OBJC)]
    private static partial nint objc_autoreleasePoolPush();

    [LibraryImport(OBJC)]
    private static partial void objc_autoreleasePoolPop(nint pool);

    [LibraryImport(OBJC, StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint objc_getClass(string name);

    [LibraryImport(OBJC, StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint sel_registerName(string name);

    [LibraryImport(OBJC, EntryPoint = "objc_msgSend")]
    private static partial nint Send(nint receiver, nint selector);

    [LibraryImport(OBJC, EntryPoint = "objc_msgSend")]
    private static partial nint Send(nint receiver, nint selector, nint argument);

    [LibraryImport(OBJC, EntryPoint = "objc_msgSend", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint Send(nint receiver, nint selector, string argument);

    [LibraryImport(OBJC, EntryPoint = "objc_msgSend")]
    private static unsafe partial byte SendBoolNative(nint receiver, nint selector, nint a, nint b, nint* c);
}
