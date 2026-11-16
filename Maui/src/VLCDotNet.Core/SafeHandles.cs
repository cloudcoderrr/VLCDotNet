using System;
using System.Runtime.InteropServices;

namespace VLCDotNet
{
    /// <summary>
    /// Base <see cref="SafeHandle"/> for the reference-counted libvlc objects.
    /// Each derived type overrides <c>ReleaseHandle</c> with the matching
    /// <c>libvlc_*_release</c> call, so a handle is always released exactly once -
    /// deterministically on <see cref="SafeHandle.Dispose()"/> or, as a safety
    /// net, by the finalizer if a wrapper is leaked.
    /// </summary>
    public abstract class VlcSafeHandle : SafeHandle
    {
        protected VlcSafeHandle()
            : base(IntPtr.Zero, ownsHandle: true)
        {
        }

        public override bool IsInvalid => handle == IntPtr.Zero;

        /// <summary>The raw native pointer. Only valid while the handle is open.</summary>
        internal IntPtr DangerousHandle => handle;
    }

    /// <summary>Owns a <c>libvlc_instance_t*</c>.</summary>
    public sealed class LibVlcInstanceHandle : VlcSafeHandle
    {
        protected override bool ReleaseHandle()
        {
            LibVlc.libvlc_release(handle);
            return true;
        }
    }

    /// <summary>Owns a <c>libvlc_media_t*</c>.</summary>
    public sealed class MediaHandle : VlcSafeHandle
    {
        protected override bool ReleaseHandle()
        {
            LibVlc.libvlc_media_release(handle);
            return true;
        }
    }

    /// <summary>Owns a <c>libvlc_media_player_t*</c>.</summary>
    public sealed class MediaPlayerHandle : VlcSafeHandle
    {
        protected override bool ReleaseHandle()
        {
            LibVlc.libvlc_media_player_release(handle);
            return true;
        }
    }

    /// <summary>Owns a <c>libvlc_media_list_t*</c>.</summary>
    public sealed class MediaListHandle : VlcSafeHandle
    {
        protected override bool ReleaseHandle()
        {
            LibVlc.libvlc_media_list_release(handle);
            return true;
        }
    }

    /// <summary>Owns a <c>libvlc_equalizer_t*</c>.</summary>
    public sealed class EqualizerHandle : VlcSafeHandle
    {
        protected override bool ReleaseHandle()
        {
            LibVlc.libvlc_audio_equalizer_release(handle);
            return true;
        }
    }

    internal static class SafeHandleFactory
    {
        /// <summary>Wraps a freshly created native pointer, throwing if it is NULL.</summary>
        internal static T Adopt<T>(IntPtr ptr, string what)
            where T : VlcSafeHandle, new()
        {
            if (ptr == IntPtr.Zero)
            {
                throw new VlcException($"Failed to create {what}");
            }

            var handle = new T();
            Marshal.InitHandle(handle, ptr);
            return handle;
        }
    }
}
