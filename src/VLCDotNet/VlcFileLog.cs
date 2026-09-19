using System;
using System.IO;
using System.Runtime.InteropServices;

namespace VLCDotNet
{
    /// <summary>
    /// Writes libvlc log output to a text file in an ABI-safe way on every
    /// supported platform:
    /// <list type="bullet">
    ///   <item>On Windows a <see cref="VlcLogCallback"/> is installed and each
    ///   message is formatted with the C runtime <c>_vsnprintf</c>. The native
    ///   <c>va_list</c> is a plain pointer under the Win64/WinArm64 ABIs, so this
    ///   is safe, and it avoids the cross-CRT <c>FILE*</c> hazard of
    ///   <c>libvlc_log_set_file</c>.</item>
    ///   <item>On Unix-like systems (Linux, macOS, Mac Catalyst, iOS, Android) a
    ///   C <c>FILE*</c> is opened with the single system <c>libc</c> and handed to
    ///   <c>libvlc_log_set_file</c>, so libvlc formats the messages itself and no
    ///   <c>va_list</c> marshaling is required (correct even on AArch64).</item>
    /// </list>
    /// </summary>
    public sealed class VlcFileLog : IDisposable
    {
#if IOS
        private const string Libc = "__Internal";
#else
        private const string Libc = "libc";
#endif

        private static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        private readonly IntPtr _instance;
        private readonly object _sync = new object();

        private IntPtr _file;             // Unix FILE*
        private StreamWriter? _writer;    // Windows sink
        private VlcLogCallback? _callback; // kept alive for the native side
        private bool _disposed;

        private VlcFileLog(IntPtr instance)
        {
            _instance = instance;
        }

        /// <summary>
        /// Starts writing the log of <paramref name="instance"/> to
        /// <paramref name="path"/>. Pass verbosity options (e.g. <c>--verbose=2</c>)
        /// to <c>libvlc_new</c> to control how much is captured.
        /// </summary>
        public static VlcFileLog Start(IntPtr instance, string path)
        {
            if (instance == IntPtr.Zero)
            {
                throw new ArgumentException("A valid libvlc instance is required.", nameof(instance));
            }

            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("A log file path is required.", nameof(path));
            }

            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var log = new VlcFileLog(instance);

            if (IsWindows)
            {
                log._writer = new StreamWriter(path, append: false) { AutoFlush = true };
                log._callback = log.OnWindowsLog;
                LibVlc.libvlc_log_set(instance, log._callback, IntPtr.Zero);
            }
            else
            {
                log._file = fopen(path, "w");
                if (log._file == IntPtr.Zero)
                {
                    throw new IOException($"Could not open log file '{path}'.");
                }

                // Line-buffer so partial logs are visible while the test runs.
                setvbuf(log._file, IntPtr.Zero, 1 /* _IOLBF */, (UIntPtr)4096);
                LibVlc.libvlc_log_set_file(instance, log._file);
            }

            return log;
        }

        private void OnWindowsLog(IntPtr data, int level, IntPtr ctx, IntPtr fmt, IntPtr args)
        {
            string message = FormatWindows(fmt, args);

            string module = string.Empty;
            if (ctx != IntPtr.Zero)
            {
                LibVlc.libvlc_log_get_context(ctx, out IntPtr modulePtr, out _, out _);
                module = LibVlc.Utf8ToString(modulePtr) ?? string.Empty;
            }

            lock (_sync)
            {
                _writer?.WriteLine($"[{LevelName(level)}] {module}: {message}");
            }
        }

        private static string LevelName(int level) => level switch
        {
            0 => "dbg",
            2 => "note",
            3 => "warn",
            4 => "err",
            _ => level.ToString(),
        };

        private static string FormatWindows(IntPtr fmt, IntPtr args)
        {
            if (fmt == IntPtr.Zero)
            {
                return string.Empty;
            }

            const int bufferSize = 4096;
            IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                int written = _vsnprintf(buffer, (UIntPtr)bufferSize, fmt, args);
                if (written < 0)
                {
                    // Truncated: the buffer is still NUL-terminated by the CRT.
                    Marshal.WriteByte(buffer, bufferSize - 1, 0);
                }

                return LibVlc.Utf8ToString(buffer) ?? string.Empty;
            }
            catch
            {
                return LibVlc.Utf8ToString(fmt) ?? string.Empty;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        /// <summary>Flushes any buffered log output to disk.</summary>
        public void Flush()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                if (IsWindows)
                {
                    _writer?.Flush();
                }
                else if (_file != IntPtr.Zero)
                {
                    fflush(_file);
                }
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;

                // Detach the sink before tearing it down so libvlc stops writing.
                LibVlc.libvlc_log_unset(_instance);

                if (IsWindows)
                {
                    _writer?.Flush();
                    _writer?.Dispose();
                    _writer = null;
                    _callback = null;
                }
                else if (_file != IntPtr.Zero)
                {
                    fflush(_file);
                    fclose(_file);
                    _file = IntPtr.Zero;
                }
            }
        }

        // ----- C runtime imports -------------------------------------------
        // Only ever invoked on the matching OS; unused imports are never bound.

        [DllImport(Libc, CallingConvention = CallingConvention.Cdecl, EntryPoint = "fopen")]
        private static extern IntPtr fopen(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string mode);

        [DllImport(Libc, CallingConvention = CallingConvention.Cdecl, EntryPoint = "fclose")]
        private static extern int fclose(IntPtr file);

        [DllImport(Libc, CallingConvention = CallingConvention.Cdecl, EntryPoint = "fflush")]
        private static extern int fflush(IntPtr file);

        [DllImport(Libc, CallingConvention = CallingConvention.Cdecl, EntryPoint = "setvbuf")]
        private static extern int setvbuf(IntPtr file, IntPtr buffer, int mode, UIntPtr size);

        [DllImport("msvcrt", CallingConvention = CallingConvention.Cdecl, EntryPoint = "_vsnprintf")]
        private static extern int _vsnprintf(IntPtr buffer, UIntPtr count, IntPtr format, IntPtr args);
    }
}
