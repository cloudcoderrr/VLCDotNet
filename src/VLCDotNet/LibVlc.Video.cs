using System;
using System.Runtime.InteropServices;

namespace VLCDotNet
{
    public static partial class LibVlc
    {
        // ----- Geometry -----------------------------------------------------

        /// <summary>Gets the video scaling factor (0 = auto/fit). (<c>libvlc_video_get_scale</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern float libvlc_video_get_scale(IntPtr player);

        /// <summary>Sets the video scaling factor (0 = auto/fit). (<c>libvlc_video_set_scale</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_video_set_scale(IntPtr player, float factor);

        /// <summary>Gets the aspect ratio (const char*, caller must <see cref="libvlc_free"/>). (<c>libvlc_video_get_aspect_ratio</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_video_get_aspect_ratio(IntPtr player);

        /// <summary>Sets the aspect ratio (e.g. "16:9"). (<c>libvlc_video_set_aspect_ratio</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_video_set_aspect_ratio(IntPtr player,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string? aspect);

        /// <summary>Gets the crop geometry (const char*, caller must <see cref="libvlc_free"/>). (<c>libvlc_video_get_crop_geometry</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_video_get_crop_geometry(IntPtr player);

        /// <summary>Sets the crop geometry (e.g. "16:9"). (<c>libvlc_video_set_crop_geometry</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_video_set_crop_geometry(IntPtr player,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string? geometry);

        /// <summary>Sets the deinterlace mode (NULL = disable). (<c>libvlc_video_set_deinterlace</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_video_set_deinterlace(IntPtr player,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string? mode);

        // ----- Teletext -----------------------------------------------------

        /// <summary>Gets the current teletext page. (<c>libvlc_video_get_teletext</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_video_get_teletext(IntPtr player);

        /// <summary>Sets the current teletext page. (<c>libvlc_video_set_teletext</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_video_set_teletext(IntPtr player, int page);

        // ----- Marquee ------------------------------------------------------

        /// <summary>Gets an integer marquee option. (<c>libvlc_video_get_marquee_int</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_video_get_marquee_int(IntPtr player, VlcVideoMarqueeOption option);

        /// <summary>Sets an integer marquee option. (<c>libvlc_video_set_marquee_int</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_video_set_marquee_int(IntPtr player, VlcVideoMarqueeOption option, int value);

        /// <summary>Sets a string marquee option (e.g. the displayed text). (<c>libvlc_video_set_marquee_string</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_video_set_marquee_string(IntPtr player, VlcVideoMarqueeOption option,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string text);

        // ----- Logo ---------------------------------------------------------

        /// <summary>Gets an integer logo option. (<c>libvlc_video_get_logo_int</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_video_get_logo_int(IntPtr player, VlcVideoLogoOption option);

        /// <summary>Sets an integer logo option. (<c>libvlc_video_set_logo_int</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_video_set_logo_int(IntPtr player, VlcVideoLogoOption option, int value);

        /// <summary>Sets a string logo option (e.g. the file path). (<c>libvlc_video_set_logo_string</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_video_set_logo_string(IntPtr player, VlcVideoLogoOption option,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string value);

        // ----- Adjust (post-processing) ------------------------------------

        /// <summary>Gets an integer adjust option. (<c>libvlc_video_get_adjust_int</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_video_get_adjust_int(IntPtr player, VlcVideoAdjustOption option);

        /// <summary>Sets an integer adjust option (e.g. Enable). (<c>libvlc_video_set_adjust_int</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_video_set_adjust_int(IntPtr player, VlcVideoAdjustOption option, int value);

        /// <summary>Gets a float adjust option. (<c>libvlc_video_get_adjust_float</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern float libvlc_video_get_adjust_float(IntPtr player, VlcVideoAdjustOption option);

        /// <summary>Sets a float adjust option (e.g. Brightness). (<c>libvlc_video_set_adjust_float</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_video_set_adjust_float(IntPtr player, VlcVideoAdjustOption option, float value);

        // ----- Viewpoint (360 video) ---------------------------------------

        /// <summary>Allocates a viewpoint structure (free with <see cref="libvlc_free"/>). (<c>libvlc_video_new_viewpoint</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_video_new_viewpoint();

        /// <summary>Updates the current 360 viewpoint. (<c>libvlc_video_update_viewpoint</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_video_update_viewpoint(IntPtr player, IntPtr viewpoint,
            [MarshalAs(UnmanagedType.I1)] bool absolute);
    }
}
