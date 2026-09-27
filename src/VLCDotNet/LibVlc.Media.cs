using System;
using System.Runtime.InteropServices;

namespace VLCDotNet
{
    public static partial class LibVlc
    {
        // ----- Construction (libvlc_media.h) -------------------------------

#if VLC4
        // libvlc 4.0 dropped the libvlc_instance_t* argument from every media
        // constructor (the media is bound to an instance later, by the player).

        /// <summary>Creates a media from an MRL/URL. (<c>libvlc_media_new_location</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_media_new_location(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string mrl);

        /// <summary>Creates a media from a local filesystem path. (<c>libvlc_media_new_path</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_media_new_path(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string path);

        /// <summary>Creates a media from an open file descriptor. (<c>libvlc_media_new_fd</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_media_new_fd(int fd);

        /// <summary>Creates a media with custom callbacks (4.0 struct form). (<c>libvlc_media_new_callbacks</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_media_new_callbacks(IntPtr openCbs, IntPtr opaque);

        /// <summary>Creates a media as an empty node with a given name. (<c>libvlc_media_new_as_node</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_media_new_as_node(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string name);
#else
        /// <summary>Creates a media from an MRL/URL. (<c>libvlc_media_new_location</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_media_new_location(IntPtr instance,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string mrl);

        /// <summary>Creates a media from a local filesystem path. (<c>libvlc_media_new_path</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_media_new_path(IntPtr instance,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string path);

        /// <summary>Creates a media from an open file descriptor. (<c>libvlc_media_new_fd</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_media_new_fd(IntPtr instance, int fd);

        /// <summary>Creates a media with custom read/seek callbacks. (<c>libvlc_media_new_callbacks</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_media_new_callbacks(IntPtr instance,
            VlcMediaOpenCb? openCb, VlcMediaReadCb readCb, VlcMediaSeekCb? seekCb, VlcMediaCloseCb? closeCb, IntPtr opaque);

        /// <summary>Creates a media as an empty node with a given name. (<c>libvlc_media_new_as_node</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_media_new_as_node(IntPtr instance,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string name);
#endif

        // ----- Options / lifetime ------------------------------------------

        /// <summary>Adds an option to the media. (<c>libvlc_media_add_option</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_add_option(IntPtr media,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string options);

        /// <summary>Adds an option with a specific priority flag. (<c>libvlc_media_add_option_flag</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_add_option_flag(IntPtr media,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string options, uint flags);

        /// <summary>Increments the media reference count. (<c>libvlc_media_retain</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_retain(IntPtr media);

        /// <summary>Decrements the media reference count, freeing it at zero. (<c>libvlc_media_release</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_release(IntPtr media);

        /// <summary>Returns the media MRL (const char*, caller must <see cref="libvlc_free"/>). (<c>libvlc_media_get_mrl</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_media_get_mrl(IntPtr media);

        /// <summary>Duplicates a media object. (<c>libvlc_media_duplicate</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_media_duplicate(IntPtr media);

        // ----- Metadata -----------------------------------------------------

        /// <summary>Reads a metadata element (const char*, caller must <see cref="libvlc_free"/>). (<c>libvlc_media_get_meta</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_media_get_meta(IntPtr media, VlcMeta meta);

        /// <summary>Sets a metadata element (in memory only). (<c>libvlc_media_set_meta</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_set_meta(IntPtr media, VlcMeta meta,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string value);

#if VLC4
        /// <summary>Saves in-memory metadata back to the media (4.0 adds the instance). (<c>libvlc_media_save_meta</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_media_save_meta(IntPtr instance, IntPtr media);
#else
        /// <summary>Saves in-memory metadata back to the media. (<c>libvlc_media_save_meta</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_media_save_meta(IntPtr media);
#endif

        // ----- State / stats / structure -----------------------------------

#if !VLC4
        /// <summary>Returns the current media state. (<c>libvlc_media_get_state</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern VlcState libvlc_media_get_state(IntPtr media);
#endif

        /// <summary>Fills a <see cref="VlcMediaStats"/> structure. Returns non-zero on success. (<c>libvlc_media_get_stats</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_media_get_stats(IntPtr media, out VlcMediaStats stats);

        /// <summary>Returns the media's sub-item list, or NULL. (<c>libvlc_media_subitems</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_media_subitems(IntPtr media);

#if !VLC4
        /// <summary>Returns the media's event manager. (<c>libvlc_media_event_manager</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_media_event_manager(IntPtr media);
#endif

        /// <summary>Returns the media duration in milliseconds. (<c>libvlc_media_get_duration</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern long libvlc_media_get_duration(IntPtr media);

        // ----- Parsing ------------------------------------------------------

#if VLC4
        // libvlc 4.0 moved preparsing to the dedicated libvlc_parser_t task API
        // (libvlc_parser.h). The test suite instead reads tracks after playback,
        // so only the lightweight "is parsed" query is bound here.

        /// <summary>Returns whether the media has been parsed. (<c>libvlc_media_is_parsed</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool libvlc_media_is_parsed(IntPtr media);
#else
        /// <summary>Parses a media asynchronously with options. (<c>libvlc_media_parse_with_options</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_media_parse_with_options(IntPtr media, VlcMediaParseFlag parseFlag, int timeout);

        /// <summary>Stops an ongoing parse operation. (<c>libvlc_media_parse_stop</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_parse_stop(IntPtr media);

        /// <summary>Returns the parse status of the media. (<c>libvlc_media_get_parsed_status</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern VlcMediaParsedStatus libvlc_media_get_parsed_status(IntPtr media);
#endif

        /// <summary>Associates opaque user data with the media. (<c>libvlc_media_set_user_data</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_set_user_data(IntPtr media, IntPtr userData);

        /// <summary>Retrieves opaque user data associated with the media. (<c>libvlc_media_get_user_data</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_media_get_user_data(IntPtr media);

        /// <summary>Returns the media resource type. (<c>libvlc_media_get_type</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern VlcMediaType libvlc_media_get_type(IntPtr media);

        // ----- Tracks -------------------------------------------------------

#if VLC4
        // libvlc 4.0 replaced the media_tracks_get array with a per-type
        // tracklist object (libvlc_media_track.h). The list must be released
        // with libvlc_media_tracklist_delete.

        /// <summary>Returns the media track list for a type. (<c>libvlc_media_get_tracklist</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_media_get_tracklist(IntPtr media, VlcTrackType type);

        /// <summary>Returns the number of tracks in a tracklist. (<c>libvlc_media_tracklist_count</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern UIntPtr libvlc_media_tracklist_count(IntPtr list);

        /// <summary>Returns the track at an index. (<c>libvlc_media_tracklist_at</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_media_tracklist_at(IntPtr list, UIntPtr index);

        /// <summary>Releases a tracklist. (<c>libvlc_media_tracklist_delete</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_tracklist_delete(IntPtr list);

        /// <summary>Retains a single track so it outlives its list. (<c>libvlc_media_track_retain</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_media_track_retain(IntPtr track);

        /// <summary>Releases a single held track. (<c>libvlc_media_track_release</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_track_release(IntPtr track);
#else
        /// <summary>Allocates and returns the array of elementary-stream tracks. (<c>libvlc_media_tracks_get</c>)</summary>
        /// <returns>The number of tracks; the array must be freed with <see cref="libvlc_media_tracks_release"/>.</returns>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern uint libvlc_media_tracks_get(IntPtr media, out IntPtr tracks);

        /// <summary>Releases a track array returned by <see cref="libvlc_media_tracks_get"/>. (<c>libvlc_media_tracks_release</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_tracks_release(IntPtr tracks, uint count);
#endif

        /// <summary>Returns a human-readable codec name for a fourcc (const char*). (<c>libvlc_media_get_codec_description</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_media_get_codec_description(VlcTrackType type, uint codec);

        // ----- Slaves (external subtitles / audio) -------------------------

        /// <summary>Adds an external slave (subtitle/audio) to the media. (<c>libvlc_media_slaves_add</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_media_slaves_add(IntPtr media, VlcMediaSlaveType type, uint priority,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string uri);

        /// <summary>Clears all external slaves attached to the media. (<c>libvlc_media_slaves_clear</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_slaves_clear(IntPtr media);

        /// <summary>Returns the array of attached slaves. (<c>libvlc_media_slaves_get</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern uint libvlc_media_slaves_get(IntPtr media, out IntPtr slaves);

        /// <summary>Releases a slave array returned by <see cref="libvlc_media_slaves_get"/>. (<c>libvlc_media_slaves_release</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_media_slaves_release(IntPtr slaves, uint count);
    }
}
