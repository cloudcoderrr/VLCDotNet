using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace VLCDotNet
{
    /// <summary>
    /// A managed libvlc engine instance - the root object every other wrapper is
    /// created from. One <see cref="LibVLC"/> can back many <see cref="MediaPlayer"/>
    /// and <see cref="Media"/> objects. Dispose it last, after everything created
    /// from it has been disposed.
    /// </summary>
    public sealed class LibVLC : IDisposable
    {
        private readonly LibVlcInstanceHandle _handle;
        private readonly object _sync = new object();
        private VlcFileLog? _fileLog;
        private bool _disposed;

        /// <summary>
        /// Creates a new engine. <paramref name="options"/> are passed verbatim to
        /// <c>libvlc_new</c> (e.g. <c>--no-video-title-show</c>, <c>--verbose=2</c>,
        /// <c>--avcodec-hw=any</c>). The bundled plugin directory is located and
        /// published to libvlc automatically via <see cref="VlcRuntime.Configure"/>.
        /// </summary>
        /// <exception cref="VlcException">Thrown when the native engine cannot be created.</exception>
        public LibVLC(params string[] options)
            : this(configure: true, options)
        {
        }

        /// <param name="configure">
        /// When <see langword="true"/> (default via the public ctor), locates and
        /// publishes the bundled plugin path before creating the engine.
        /// </param>
        /// <param name="options">Options passed verbatim to <c>libvlc_new</c>.</param>
        public LibVLC(bool configure, params string[] options)
        {
            if (configure)
            {
                VlcRuntime.Configure();
            }

            string[]? argv = options is { Length: > 0 } ? options : null;
            IntPtr instance = LibVlc.libvlc_new(argv?.Length ?? 0, argv);
            _handle = SafeHandleFactory.Adopt<LibVlcInstanceHandle>(instance, "libvlc instance");
        }

        /// <summary>The libvlc runtime version string (e.g. <c>3.0.24</c>).</summary>
        public static string Version => LibVlc.Utf8ToString(LibVlc.libvlc_get_version()) ?? string.Empty;

        /// <summary>The native <c>libvlc_instance_t*</c>. For advanced interop only.</summary>
        public IntPtr NativeInstance
        {
            get
            {
                ThrowIfDisposed();
                return _handle.DangerousHandle;
            }
        }

        internal LibVlcInstanceHandle Handle => _handle;

        /// <summary>Sets a human-readable application name and HTTP user agent.</summary>
        public void SetUserAgent(string name, string httpUserAgent)
        {
            ThrowIfDisposed();
            LibVlc.libvlc_set_user_agent(NativeInstance, name, httpUserAgent);
        }

        /// <summary>Sets application identifier metadata (reverse-DNS id, version, icon name).</summary>
        public void SetAppId(string id, string version, string icon)
        {
            ThrowIfDisposed();
            LibVlc.libvlc_set_app_id(NativeInstance, id, version, icon);
        }

        /// <summary>
        /// Routes libvlc log output to <paramref name="path"/> in an ABI-safe way on
        /// every platform. Replaces any previously configured file log. Returns the
        /// log sink, which is also owned (and disposed) by this engine.
        /// </summary>
        public VlcFileLog StartFileLog(string path)
        {
            ThrowIfDisposed();
            lock (_sync)
            {
                _fileLog?.Dispose();
                _fileLog = VlcFileLog.Start(NativeInstance, path);
                return _fileLog;
            }
        }

        /// <summary>Flushes any buffered file-log output to disk.</summary>
        public void FlushLog()
        {
            lock (_sync)
            {
                _fileLog?.Flush();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(LibVLC));
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
                _fileLog?.Dispose();
                _fileLog = null;
            }

            _handle.Dispose();
        }
    }
}
