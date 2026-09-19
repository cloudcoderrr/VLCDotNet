using System;
using System.Runtime.InteropServices;

namespace VLCDotNet
{
    public static partial class LibVlc
    {
        // ----- libvlc_media_list.h -----------------------------------------

        /// <summary>Creates an empty media list. (<c>libvlc_media_list_new</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_media_list_new(IntPtr instance);

        /// <summary>Releases a media list. (<c>libvlc_media_list_release</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_list_release(IntPtr list);

        /// <summary>Retains a media list. (<c>libvlc_media_list_retain</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_list_retain(IntPtr list);

        /// <summary>Associates a media descriptor with the list. (<c>libvlc_media_list_set_media</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_list_set_media(IntPtr list, IntPtr media);

        /// <summary>Returns the media descriptor associated with the list. (<c>libvlc_media_list_media</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_media_list_media(IntPtr list);

        /// <summary>Appends a media to the list (must be locked). (<c>libvlc_media_list_add_media</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_media_list_add_media(IntPtr list, IntPtr media);

        /// <summary>Inserts a media at an index (must be locked). (<c>libvlc_media_list_insert_media</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_media_list_insert_media(IntPtr list, IntPtr media, int index);

        /// <summary>Removes the media at an index (must be locked). (<c>libvlc_media_list_remove_index</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_media_list_remove_index(IntPtr list, int index);

        /// <summary>Returns the number of items (must be locked). (<c>libvlc_media_list_count</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_media_list_count(IntPtr list);

        /// <summary>Returns the item at an index (must be locked). (<c>libvlc_media_list_item_at_index</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_media_list_item_at_index(IntPtr list, int index);

        /// <summary>Returns the index of a media in the list (must be locked). (<c>libvlc_media_list_index_of_item</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_media_list_index_of_item(IntPtr list, IntPtr media);

        /// <summary>Returns non-zero if the list is read-only. (<c>libvlc_media_list_is_readonly</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_media_list_is_readonly(IntPtr list);

        /// <summary>Locks the media list. (<c>libvlc_media_list_lock</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_list_lock(IntPtr list);

        /// <summary>Unlocks the media list. (<c>libvlc_media_list_unlock</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_list_unlock(IntPtr list);

        /// <summary>Returns the media list event manager. (<c>libvlc_media_list_event_manager</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_media_list_event_manager(IntPtr list);

        // ----- libvlc_media_list_player.h ----------------------------------

        /// <summary>Creates a new media-list player. (<c>libvlc_media_list_player_new</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_media_list_player_new(IntPtr instance);

        /// <summary>Releases a media-list player. (<c>libvlc_media_list_player_release</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_list_player_release(IntPtr listPlayer);

        /// <summary>Retains a media-list player. (<c>libvlc_media_list_player_retain</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_list_player_retain(IntPtr listPlayer);

        /// <summary>Returns the media-list player event manager. (<c>libvlc_media_list_player_event_manager</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_media_list_player_event_manager(IntPtr listPlayer);

        /// <summary>Sets the underlying media player. (<c>libvlc_media_list_player_set_media_player</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_list_player_set_media_player(IntPtr listPlayer, IntPtr player);

        /// <summary>Gets the underlying media player. (<c>libvlc_media_list_player_get_media_player</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_media_list_player_get_media_player(IntPtr listPlayer);

        /// <summary>Sets the media list to play. (<c>libvlc_media_list_player_set_media_list</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_list_player_set_media_list(IntPtr listPlayer, IntPtr list);

        /// <summary>Starts playback. (<c>libvlc_media_list_player_play</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_list_player_play(IntPtr listPlayer);

        /// <summary>Toggles pause. (<c>libvlc_media_list_player_pause</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_list_player_pause(IntPtr listPlayer);

        /// <summary>Pauses or resumes playback. (<c>libvlc_media_list_player_set_pause</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_list_player_set_pause(IntPtr listPlayer, int doPause);

        /// <summary>Returns non-zero if playing. (<c>libvlc_media_list_player_is_playing</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_media_list_player_is_playing(IntPtr listPlayer);

        /// <summary>Returns the current state. (<c>libvlc_media_list_player_get_state</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern VlcState libvlc_media_list_player_get_state(IntPtr listPlayer);

        /// <summary>Plays the item at an index. (<c>libvlc_media_list_player_play_item_at_index</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_media_list_player_play_item_at_index(IntPtr listPlayer, int index);

        /// <summary>Plays a specific media item. (<c>libvlc_media_list_player_play_item</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_media_list_player_play_item(IntPtr listPlayer, IntPtr media);

        /// <summary>Stops playback. (<c>libvlc_media_list_player_stop</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_list_player_stop(IntPtr listPlayer);

        /// <summary>Plays the next item. (<c>libvlc_media_list_player_next</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_media_list_player_next(IntPtr listPlayer);

        /// <summary>Plays the previous item. (<c>libvlc_media_list_player_previous</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_media_list_player_previous(IntPtr listPlayer);

        /// <summary>Sets the playback repeat mode. (<c>libvlc_media_list_player_set_playback_mode</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_list_player_set_playback_mode(IntPtr listPlayer, VlcPlaybackMode mode);
    }
}
