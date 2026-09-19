using System;
using System.Runtime.InteropServices;

namespace VLCDotNet
{
    public static partial class LibVlc
    {
        // ----- libvlc_media_discoverer.h -----------------------------------

        /// <summary>Creates a media discoverer by service name. (<c>libvlc_media_discoverer_new</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_media_discoverer_new(IntPtr instance,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

        /// <summary>Starts a media discoverer. Returns 0 on success. (<c>libvlc_media_discoverer_start</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_media_discoverer_start(IntPtr discoverer);

        /// <summary>Stops a media discoverer. (<c>libvlc_media_discoverer_stop</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_discoverer_stop(IntPtr discoverer);

        /// <summary>Releases a media discoverer. (<c>libvlc_media_discoverer_release</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_discoverer_release(IntPtr discoverer);

        /// <summary>Returns the media list produced by a discoverer. (<c>libvlc_media_discoverer_media_list</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_media_discoverer_media_list(IntPtr discoverer);

        /// <summary>Returns non-zero when the discoverer is running. (<c>libvlc_media_discoverer_is_running</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_media_discoverer_is_running(IntPtr discoverer);

        /// <summary>Gets the list of discovery services for a category. (<c>libvlc_media_discoverer_list_get</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern nuint libvlc_media_discoverer_list_get(IntPtr instance, VlcMediaDiscovererCategory category, out IntPtr services);

        /// <summary>Releases a discovery service list. (<c>libvlc_media_discoverer_list_release</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_discoverer_list_release(IntPtr services, nuint count);

        // ----- libvlc_renderer_discoverer.h --------------------------------

        /// <summary>Creates a renderer discoverer by service name. (<c>libvlc_renderer_discoverer_new</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_renderer_discoverer_new(IntPtr instance,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

        /// <summary>Releases a renderer discoverer. (<c>libvlc_renderer_discoverer_release</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_renderer_discoverer_release(IntPtr discoverer);

        /// <summary>Starts a renderer discoverer. Returns 0 on success. (<c>libvlc_renderer_discoverer_start</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_renderer_discoverer_start(IntPtr discoverer);

        /// <summary>Stops a renderer discoverer. (<c>libvlc_renderer_discoverer_stop</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_renderer_discoverer_stop(IntPtr discoverer);

        /// <summary>Returns the renderer discoverer event manager. (<c>libvlc_renderer_discoverer_event_manager</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_renderer_discoverer_event_manager(IntPtr discoverer);

        /// <summary>Gets the list of available renderer discovery services. (<c>libvlc_renderer_discoverer_list_get</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern nuint libvlc_renderer_discoverer_list_get(IntPtr instance, out IntPtr services);

        /// <summary>Releases a renderer discovery service list. (<c>libvlc_renderer_discoverer_list_release</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_renderer_discoverer_list_release(IntPtr services, nuint count);

        /// <summary>Holds (retains) a renderer item. (<c>libvlc_renderer_item_hold</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_renderer_item_hold(IntPtr item);

        /// <summary>Releases a renderer item. (<c>libvlc_renderer_item_release</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_renderer_item_release(IntPtr item);

        /// <summary>Returns a renderer item name (const char*). (<c>libvlc_renderer_item_name</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_renderer_item_name(IntPtr item);

        /// <summary>Returns a renderer item type (const char*). (<c>libvlc_renderer_item_type</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_renderer_item_type(IntPtr item);

        /// <summary>Returns renderer item capability flags. (<c>libvlc_renderer_item_flags</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_renderer_item_flags(IntPtr item);
    }
}
