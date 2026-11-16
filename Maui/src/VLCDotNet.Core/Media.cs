using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace VLCDotNet
{
    /// <summary>How a <see cref="Media"/> string is interpreted.</summary>
    public enum FromType
    {
        /// <summary>A local filesystem path (<c>libvlc_media_new_path</c>).</summary>
        FromPath,

        /// <summary>An MRL/URL such as http(s)://, rtsp://, file:// (<c>libvlc_media_new_location</c>).</summary>
        FromLocation,
    }

    /// <summary>
    /// A managed media descriptor (a file, URL, stream or disc). Owns the native
    /// <c>libvlc_media_t</c> and releases it on <see cref="Dispose"/>. Holds a
    /// reference to its <see cref="LibVLC"/> so the engine outlives the media.
    /// </summary>
    public sealed class Media : IDisposable
    {
        private readonly LibVLC _libvlc;
        private readonly MediaHandle _handle;
        private bool _disposed;

        /// <summary>Creates a media from a local path or an MRL/URL.</summary>
        public Media(LibVLC libvlc, string pathOrUri, FromType type = FromType.FromPath)
        {
            _libvlc = libvlc ?? throw new ArgumentNullException(nameof(libvlc));
            if (string.IsNullOrEmpty(pathOrUri))
            {
                throw new ArgumentException("A path or URI is required.", nameof(pathOrUri));
            }

            IntPtr ptr = type == FromType.FromLocation
                ? LibVlc.libvlc_media_new_location(libvlc.NativeInstance, pathOrUri)
                : LibVlc.libvlc_media_new_path(libvlc.NativeInstance, pathOrUri);
            _handle = SafeHandleFactory.Adopt<MediaHandle>(ptr, "media");
        }

        /// <summary>Creates a media from an absolute URI (uses <c>libvlc_media_new_location</c>).</summary>
        public Media(LibVLC libvlc, Uri uri)
            : this(libvlc, (uri ?? throw new ArgumentNullException(nameof(uri))).AbsoluteUri, FromType.FromLocation)
        {
        }

        private Media(LibVLC libvlc, MediaHandle handle)
        {
            _libvlc = libvlc;
            _handle = handle;
        }

        /// <summary>
        /// Wraps an existing native media pointer. When <paramref name="owns"/> is
        /// <see langword="false"/> the pointer is retained so this wrapper owns a
        /// reference it can safely release.
        /// </summary>
        internal static Media FromNative(LibVLC libvlc, IntPtr mediaPtr, bool owns)
        {
            if (mediaPtr == IntPtr.Zero)
            {
                throw new VlcException("null media pointer");
            }

            if (!owns)
            {
                LibVlc.libvlc_media_retain(mediaPtr);
            }

            var handle = new MediaHandle();
            Marshal.InitHandle(handle, mediaPtr);
            return new Media(libvlc, handle);
        }

        /// <summary>The engine this media belongs to.</summary>
        public LibVLC LibVLC => _libvlc;

        /// <summary>The native <c>libvlc_media_t*</c>. For advanced interop only.</summary>
        public IntPtr NativePointer
        {
            get
            {
                ThrowIfDisposed();
                return _handle.DangerousHandle;
            }
        }

        internal MediaHandle Handle => _handle;

        /// <summary>Adds a media option (e.g. <c>:avcodec-hw=any</c>, <c>:start-time=5</c>).</summary>
        public void AddOption(string option)
        {
            ThrowIfDisposed();
            if (!string.IsNullOrEmpty(option))
            {
                LibVlc.libvlc_media_add_option(NativePointer, option);
            }
        }

        /// <summary>Media duration in milliseconds, or -1 when unknown.</summary>
        public long Duration
        {
            get
            {
                ThrowIfDisposed();
                return LibVlc.libvlc_media_get_duration(NativePointer);
            }
        }

        /// <summary>The media MRL.</summary>
        public string? Mrl
        {
            get
            {
                ThrowIfDisposed();
                IntPtr ptr = LibVlc.libvlc_media_get_mrl(NativePointer);
                try
                {
                    return LibVlc.Utf8ToString(ptr);
                }
                finally
                {
                    if (ptr != IntPtr.Zero)
                    {
                        LibVlc.libvlc_free(ptr);
                    }
                }
            }
        }

        /// <summary>The media resource type.</summary>
        public VlcMediaType Type
        {
            get
            {
                ThrowIfDisposed();
                return LibVlc.libvlc_media_get_type(NativePointer);
            }
        }

        /// <summary>The current media state.</summary>
        public VlcState State
        {
            get
            {
                ThrowIfDisposed();
                return LibVlc.libvlc_media_get_state(NativePointer);
            }
        }

        /// <summary>The parse status of the media.</summary>
        public VlcMediaParsedStatus ParsedStatus
        {
            get
            {
                ThrowIfDisposed();
                return LibVlc.libvlc_media_get_parsed_status(NativePointer);
            }
        }

        /// <summary>Reads a metadata element (returns <see langword="null"/> if absent).</summary>
        public string? GetMeta(VlcMeta meta)
        {
            ThrowIfDisposed();
            IntPtr ptr = LibVlc.libvlc_media_get_meta(NativePointer, meta);
            try
            {
                return LibVlc.Utf8ToString(ptr);
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                {
                    LibVlc.libvlc_free(ptr);
                }
            }
        }

        /// <summary>Attaches an external subtitle or audio slave by URI.</summary>
        public bool AddSlave(VlcMediaSlaveType type, uint priority, string uri)
        {
            ThrowIfDisposed();
            return LibVlc.libvlc_media_slaves_add(NativePointer, type, priority, uri) == 0;
        }

        /// <summary>
        /// Parses the media (reads duration, tracks and metadata) and waits up to
        /// <paramref name="timeoutMilliseconds"/> for completion. Returns the final
        /// parse status.
        /// </summary>
        public VlcMediaParsedStatus Parse(
            VlcMediaParseFlag flags = VlcMediaParseFlag.ParseLocal,
            int timeoutMilliseconds = 5000)
        {
            ThrowIfDisposed();
            if (LibVlc.libvlc_media_parse_with_options(NativePointer, flags, timeoutMilliseconds) != 0)
            {
                return VlcMediaParsedStatus.Failed;
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            // Poll until the parse reaches a terminal state. parse_with_options is
            // asynchronous; the native timeout bounds the work, we bound the wait.
            int budget = timeoutMilliseconds + 2000;
            while (sw.ElapsedMilliseconds < budget)
            {
                VlcMediaParsedStatus status = ParsedStatus;
                if (status is VlcMediaParsedStatus.Done
                    or VlcMediaParsedStatus.Failed
                    or VlcMediaParsedStatus.Timeout
                    or VlcMediaParsedStatus.Skipped)
                {
                    return status;
                }

                Thread.Sleep(25);
            }

            return ParsedStatus;
        }

        /// <summary>Asynchronously parses the media (see <see cref="Parse"/>).</summary>
        public Task<VlcMediaParsedStatus> ParseAsync(
            VlcMediaParseFlag flags = VlcMediaParseFlag.ParseLocal,
            int timeoutMilliseconds = 5000,
            CancellationToken cancellationToken = default) =>
            Task.Run(() => Parse(flags, timeoutMilliseconds), cancellationToken);

        /// <summary>
        /// Enumerates the elementary-stream tracks. Best called after
        /// <see cref="Parse"/> or once playback has started. The native track array
        /// is always released.
        /// </summary>
        public IReadOnlyList<MediaTrack> Tracks
        {
            get
            {
                ThrowIfDisposed();
                var list = new List<MediaTrack>();
                uint count = LibVlc.libvlc_media_tracks_get(NativePointer, out IntPtr array);
                if (array == IntPtr.Zero || count == 0)
                {
                    return list;
                }

                try
                {
                    int ptrSize = IntPtr.Size;
                    for (int i = 0; i < count; i++)
                    {
                        IntPtr trackPtr = Marshal.ReadIntPtr(array, i * ptrSize);
                        if (trackPtr == IntPtr.Zero)
                        {
                            continue;
                        }

                        var raw = Marshal.PtrToStructure<VlcMediaTrack>(trackPtr);
                        list.Add(new MediaTrack(raw));
                    }
                }
                finally
                {
                    LibVlc.libvlc_media_tracks_release(array, count);
                }

                return list;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(Media));
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _handle.Dispose();
        }
    }
}
