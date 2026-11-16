using System;

namespace VLCDotNet
{
    /// <summary>
    /// A managed, thread-safe ordered collection of <see cref="Media"/> backed by
    /// a native <c>libvlc_media_list_t</c>. All access takes the native list lock.
    /// Owns the list and releases it on <see cref="Dispose"/>; the native list
    /// keeps its own reference on each added media.
    /// </summary>
    public sealed class MediaList : IDisposable
    {
        private readonly LibVLC _libvlc;
        private readonly MediaListHandle _handle;
        private bool _disposed;

        /// <summary>Creates an empty media list.</summary>
        public MediaList(LibVLC libvlc)
        {
            _libvlc = libvlc ?? throw new ArgumentNullException(nameof(libvlc));
            _handle = SafeHandleFactory.Adopt<MediaListHandle>(
                LibVlc.libvlc_media_list_new(libvlc.NativeInstance), "media list");
        }

        /// <summary>The native <c>libvlc_media_list_t*</c>. For advanced interop only.</summary>
        public IntPtr NativePointer
        {
            get
            {
                ThrowIfDisposed();
                return _handle.DangerousHandle;
            }
        }

        /// <summary>Number of items in the list.</summary>
        public int Count
        {
            get
            {
                ThrowIfDisposed();
                LibVlc.libvlc_media_list_lock(NativePointer);
                try
                {
                    return LibVlc.libvlc_media_list_count(NativePointer);
                }
                finally
                {
                    LibVlc.libvlc_media_list_unlock(NativePointer);
                }
            }
        }

        /// <summary>Appends a media to the list. Returns true on success.</summary>
        public bool Add(Media media)
        {
            ThrowIfDisposed();
            if (media is null)
            {
                throw new ArgumentNullException(nameof(media));
            }

            LibVlc.libvlc_media_list_lock(NativePointer);
            try
            {
                return LibVlc.libvlc_media_list_add_media(NativePointer, media.NativePointer) == 0;
            }
            finally
            {
                LibVlc.libvlc_media_list_unlock(NativePointer);
            }
        }

        /// <summary>Removes the media at <paramref name="index"/>. Returns true on success.</summary>
        public bool RemoveAt(int index)
        {
            ThrowIfDisposed();
            LibVlc.libvlc_media_list_lock(NativePointer);
            try
            {
                return LibVlc.libvlc_media_list_remove_index(NativePointer, index) == 0;
            }
            finally
            {
                LibVlc.libvlc_media_list_unlock(NativePointer);
            }
        }

        /// <summary>
        /// Returns the media at <paramref name="index"/> as a new owned wrapper
        /// (dispose it when done), or <see langword="null"/> if out of range.
        /// </summary>
        public Media? ItemAt(int index)
        {
            ThrowIfDisposed();
            LibVlc.libvlc_media_list_lock(NativePointer);
            try
            {
                IntPtr ptr = LibVlc.libvlc_media_list_item_at_index(NativePointer, index);
                // item_at_index returns a retained reference the caller must release.
                return ptr == IntPtr.Zero ? null : Media.FromNative(_libvlc, ptr, owns: true);
            }
            finally
            {
                LibVlc.libvlc_media_list_unlock(NativePointer);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MediaList));
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
