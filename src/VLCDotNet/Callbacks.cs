using System;
using System.Runtime.InteropServices;

namespace VLCDotNet
{
    // ----- Events -----------------------------------------------------------

    /// <summary>Event handler registered through a libvlc event manager (<c>libvlc_callback_t</c>).</summary>
    /// <param name="p_event">Pointer to the native <c>libvlc_event_t</c>; use <see cref="VlcEvent"/> helpers to read it.</param>
    /// <param name="p_data">Opaque user data supplied at registration.</param>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void VlcEventCallback(IntPtr p_event, IntPtr p_data);

    // ----- Logging ----------------------------------------------------------

    /// <summary>Log sink registered through <c>libvlc_log_set</c> (<c>libvlc_log_cb</c>).</summary>
    /// <param name="data">Opaque user data.</param>
    /// <param name="level">A <see cref="VlcLogLevel"/> value.</param>
    /// <param name="ctx">Opaque log context (module/file/line accessors).</param>
    /// <param name="fmt">printf-style format string (UTF-8).</param>
    /// <param name="args">Native <c>va_list</c>. Only a plain pointer on Windows/Apple ABIs.</param>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void VlcLogCallback(IntPtr data, int level, IntPtr ctx, IntPtr fmt, IntPtr args);

    // ----- Custom video output ---------------------------------------------

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr VlcVideoLockCb(IntPtr opaque, IntPtr planes);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void VlcVideoUnlockCb(IntPtr opaque, IntPtr picture, IntPtr planes);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void VlcVideoDisplayCb(IntPtr opaque, IntPtr picture);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate uint VlcVideoFormatCb(ref IntPtr opaque, IntPtr chroma, ref uint width, ref uint height, IntPtr pitches, IntPtr lines);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void VlcVideoCleanupCb(IntPtr opaque);

    // ----- Custom audio output ---------------------------------------------

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void VlcAudioPlayCb(IntPtr data, IntPtr samples, uint count, long pts);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void VlcAudioPauseCb(IntPtr data, long pts);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void VlcAudioResumeCb(IntPtr data, long pts);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void VlcAudioFlushCb(IntPtr data, long pts);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void VlcAudioDrainCb(IntPtr data);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int VlcAudioSetupCb(ref IntPtr opaque, IntPtr format, ref uint rate, ref uint channels);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void VlcAudioCleanupCb(IntPtr opaque);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void VlcAudioVolumeCb(IntPtr data, float volume, [MarshalAs(UnmanagedType.I1)] bool mute);

    // ----- Custom media input (imem) ---------------------------------------

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int VlcMediaOpenCb(IntPtr opaque, ref IntPtr datap, ref ulong sizep);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr VlcMediaReadCb(IntPtr opaque, IntPtr buf, UIntPtr len);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int VlcMediaSeekCb(IntPtr opaque, ulong offset);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void VlcMediaCloseCb(IntPtr opaque);
}
