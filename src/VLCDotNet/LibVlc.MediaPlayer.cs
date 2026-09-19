using System;
using System.Runtime.InteropServices;

namespace VLCDotNet
{
    public static partial class LibVlc
    {
        // ----- Lifecycle (libvlc_media_player.h) ---------------------------

        /// <summary>Creates a new player bound to an instance. (<c>libvlc_media_player_new</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_media_player_new(IntPtr instance);

        /// <summary>Creates a new player with a media already set. (<c>libvlc_media_player_new_from_media</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_media_player_new_from_media(IntPtr media);

        /// <summary>Releases (decrements ref count of) a player. (<c>libvlc_media_player_release</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_player_release(IntPtr player);

        /// <summary>Increments the player reference count. (<c>libvlc_media_player_retain</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_player_retain(IntPtr player);

        /// <summary>Sets the media the player will use. (<c>libvlc_media_player_set_media</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_player_set_media(IntPtr player, IntPtr media);

        /// <summary>Returns the player's current media, or NULL. (<c>libvlc_media_player_get_media</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_media_player_get_media(IntPtr player);

        /// <summary>Returns the player's event manager. (<c>libvlc_media_player_event_manager</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_media_player_event_manager(IntPtr player);

        /// <summary>Returns non-zero if the player is currently playing. (<c>libvlc_media_player_is_playing</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_media_player_is_playing(IntPtr player);

        /// <summary>Starts playback. Returns 0 on success, -1 on error. (<c>libvlc_media_player_play</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_media_player_play(IntPtr player);

        /// <summary>Pauses or resumes playback. (<c>libvlc_media_player_set_pause</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_player_set_pause(IntPtr player, int doPause);

        /// <summary>Toggles pause. (<c>libvlc_media_player_pause</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_player_pause(IntPtr player);

        /// <summary>Stops playback. (<c>libvlc_media_player_stop</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_player_stop(IntPtr player);

        /// <summary>Sets a renderer (e.g. Chromecast) to use. (<c>libvlc_media_player_set_renderer</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_media_player_set_renderer(IntPtr player, IntPtr renderer);

        // ----- Custom video output callbacks -------------------------------

        /// <summary>Installs decode-to-memory video callbacks. (<c>libvlc_video_set_callbacks</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_video_set_callbacks(IntPtr player,
            VlcVideoLockCb @lock, VlcVideoUnlockCb? unlock, VlcVideoDisplayCb? display, IntPtr opaque);

        /// <summary>Sets a fixed decoded video format. (<c>libvlc_video_set_format</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_video_set_format(IntPtr player,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string chroma, uint width, uint height, uint pitch);

        /// <summary>Sets video format via setup/cleanup callbacks. (<c>libvlc_video_set_format_callbacks</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_video_set_format_callbacks(IntPtr player,
            VlcVideoFormatCb setup, VlcVideoCleanupCb? cleanup);

        // ----- Native window handles ---------------------------------------

        /// <summary>Sets the macOS/iOS NSView/UIView drawable. (<c>libvlc_media_player_set_nsobject</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_player_set_nsobject(IntPtr player, IntPtr drawable);

        /// <summary>Gets the macOS/iOS drawable. (<c>libvlc_media_player_get_nsobject</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_media_player_get_nsobject(IntPtr player);

        /// <summary>Sets the X11 window id used for video output. (<c>libvlc_media_player_set_xwindow</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_player_set_xwindow(IntPtr player, uint drawable);

        /// <summary>Gets the X11 window id used for video output. (<c>libvlc_media_player_get_xwindow</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern uint libvlc_media_player_get_xwindow(IntPtr player);

        /// <summary>Sets the Win32/WinRT HWND used for video output. (<c>libvlc_media_player_set_hwnd</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_player_set_hwnd(IntPtr player, IntPtr drawable);

        /// <summary>Gets the Win32/WinRT HWND used for video output. (<c>libvlc_media_player_get_hwnd</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_media_player_get_hwnd(IntPtr player);

        // ----- Timing -------------------------------------------------------

        /// <summary>Returns the current media length in milliseconds. (<c>libvlc_media_player_get_length</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern long libvlc_media_player_get_length(IntPtr player);

        /// <summary>Returns the current playback time in milliseconds. (<c>libvlc_media_player_get_time</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern long libvlc_media_player_get_time(IntPtr player);

        /// <summary>Sets the playback time in milliseconds. (<c>libvlc_media_player_set_time</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_player_set_time(IntPtr player, long time);

        /// <summary>Returns the playback position in the range 0.0-1.0. (<c>libvlc_media_player_get_position</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern float libvlc_media_player_get_position(IntPtr player);

        /// <summary>Sets the playback position in the range 0.0-1.0. (<c>libvlc_media_player_set_position</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_player_set_position(IntPtr player, float position);

        /// <summary>Returns non-zero when the media is seekable. (<c>libvlc_media_player_is_seekable</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_media_player_is_seekable(IntPtr player);

        /// <summary>Returns non-zero when the media can be paused. (<c>libvlc_media_player_can_pause</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_media_player_can_pause(IntPtr player);

        /// <summary>Returns non-zero if playback will start (loading). (<c>libvlc_media_player_will_play</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_media_player_will_play(IntPtr player);

        /// <summary>Displays the next video frame while paused. (<c>libvlc_media_player_next_frame</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_player_next_frame(IntPtr player);

        /// <summary>Returns the playback rate. (<c>libvlc_media_player_get_rate</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern float libvlc_media_player_get_rate(IntPtr player);

        /// <summary>Sets the playback rate. Returns 0 on success. (<c>libvlc_media_player_set_rate</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_media_player_set_rate(IntPtr player, float rate);

        /// <summary>Returns the current player state. (<c>libvlc_media_player_get_state</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern VlcState libvlc_media_player_get_state(IntPtr player);

        /// <summary>Returns the number of active video outputs. (<c>libvlc_media_player_has_vout</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern uint libvlc_media_player_has_vout(IntPtr player);

        // ----- Titles / chapters -------------------------------------------

        /// <summary>Sets the title index. (<c>libvlc_media_player_set_title</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_player_set_title(IntPtr player, int title);

        /// <summary>Gets the current title index. (<c>libvlc_media_player_get_title</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_media_player_get_title(IntPtr player);

        /// <summary>Gets the number of titles. (<c>libvlc_media_player_get_title_count</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_media_player_get_title_count(IntPtr player);

        /// <summary>Sets the chapter index. (<c>libvlc_media_player_set_chapter</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_player_set_chapter(IntPtr player, int chapter);

        /// <summary>Gets the current chapter index. (<c>libvlc_media_player_get_chapter</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_media_player_get_chapter(IntPtr player);

        /// <summary>Gets the number of chapters. (<c>libvlc_media_player_get_chapter_count</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_media_player_get_chapter_count(IntPtr player);

        /// <summary>Navigates a DVD/BD menu. (<c>libvlc_media_player_navigate</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_player_navigate(IntPtr player, uint navigate);

        /// <summary>Sets how the video title is shown on screen. (<c>libvlc_media_player_set_video_title_display</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_player_set_video_title_display(IntPtr player, VlcPosition position, uint timeout);

        // ----- Snapshot -----------------------------------------------------

        /// <summary>Takes a snapshot of the given video output into a file. (<c>libvlc_video_take_snapshot</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_video_take_snapshot(IntPtr player, uint num,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string filepath, uint width, uint height);

        /// <summary>Gets the pixel dimensions of a video output. (<c>libvlc_video_get_size</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_video_get_size(IntPtr player, uint num, out uint px, out uint py);

        // ----- Video tracks -------------------------------------------------

        /// <summary>Gets the number of available video tracks. (<c>libvlc_video_get_track_count</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_video_get_track_count(IntPtr player);

        /// <summary>Gets the video track description list. (<c>libvlc_video_get_track_description</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_video_get_track_description(IntPtr player);

        /// <summary>Gets the current video track id. (<c>libvlc_video_get_track</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_video_get_track(IntPtr player);

        /// <summary>Selects a video track by id. (<c>libvlc_video_set_track</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_video_set_track(IntPtr player, int track);

        // ----- Subtitles (SPU) ---------------------------------------------

        /// <summary>Gets the current subtitle track id. (<c>libvlc_video_get_spu</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_video_get_spu(IntPtr player);

        /// <summary>Gets the number of subtitle tracks. (<c>libvlc_video_get_spu_count</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_video_get_spu_count(IntPtr player);

        /// <summary>Gets the subtitle track description list. (<c>libvlc_video_get_spu_description</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_video_get_spu_description(IntPtr player);

        /// <summary>Selects a subtitle track by id. (<c>libvlc_video_set_spu</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_video_set_spu(IntPtr player, int spu);

        /// <summary>Gets the subtitle delay in microseconds. (<c>libvlc_video_get_spu_delay</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern long libvlc_video_get_spu_delay(IntPtr player);

        /// <summary>Sets the subtitle delay in microseconds. (<c>libvlc_video_set_spu_delay</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_video_set_spu_delay(IntPtr player, long delay);

        // ----- Slaves (external subtitle/audio at player level) ------------

        /// <summary>Adds an external slave (subtitle/audio) to the player. (<c>libvlc_media_player_add_slave</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_media_player_add_slave(IntPtr player, VlcMediaSlaveType type,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string uri, [MarshalAs(UnmanagedType.I1)] bool select);

        // ----- Track description list release ------------------------------

        /// <summary>Releases a track description linked list. (<c>libvlc_track_description_list_release</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_track_description_list_release(IntPtr list);
    }
}
