namespace VLCDotNet
{
    /// <summary>Current state of a media player or media (<c>libvlc_state_t</c>).</summary>
    public enum VlcState
    {
        NothingSpecial = 0,
        Opening = 1,
        Buffering = 2,
        Playing = 3,
        Paused = 4,
        Stopped = 5,
        Ended = 6,
        Error = 7,
    }

    /// <summary>Kind of a media resource (<c>libvlc_media_type_t</c>).</summary>
    public enum VlcMediaType
    {
        Unknown = 0,
        File = 1,
        Directory = 2,
        Disc = 3,
        Stream = 4,
        Playlist = 5,
    }

    /// <summary>Metadata item identifiers (<c>libvlc_meta_t</c>).</summary>
    public enum VlcMeta
    {
        Title = 0,
        Artist = 1,
        Genre = 2,
        Copyright = 3,
        Album = 4,
        TrackNumber = 5,
        Description = 6,
        Rating = 7,
        Date = 8,
        Setting = 9,
        URL = 10,
        Language = 11,
        NowPlaying = 12,
        Publisher = 13,
        EncodedBy = 14,
        ArtworkURL = 15,
        TrackID = 16,
        TrackTotal = 17,
        Director = 18,
        Season = 19,
        Episode = 20,
        ShowName = 21,
        Actors = 22,
        AlbumArtist = 23,
        DiscNumber = 24,
        DiscTotal = 25,
    }

    /// <summary>Elementary stream track kind (<c>libvlc_track_type_t</c>).</summary>
    public enum VlcTrackType
    {
        Unknown = -1,
        Audio = 0,
        Video = 1,
        Text = 2,
    }

    /// <summary>Video orientation (<c>libvlc_video_orient_t</c>).</summary>
    public enum VlcVideoOrient
    {
        TopLeft = 0,
        TopRight = 1,
        BottomLeft = 2,
        BottomRight = 3,
        LeftTop = 4,
        LeftBottom = 5,
        RightTop = 6,
        RightBottom = 7,
    }

    /// <summary>Video projection (<c>libvlc_video_projection_t</c>).</summary>
    public enum VlcVideoProjection
    {
        Rectangular = 0,
        Equirectangular = 1,
        CubemapLayoutStandard = 0x100,
    }

    /// <summary>Parsing options passed to <c>libvlc_media_parse_with_options</c> (<c>libvlc_media_parse_flag_t</c>).</summary>
    [System.Flags]
    public enum VlcMediaParseFlag
    {
        ParseLocal = 0x00,
        ParseNetwork = 0x01,
        FetchLocal = 0x02,
        FetchNetwork = 0x04,
        DoInteract = 0x08,
    }

    /// <summary>Result of a parse operation (<c>libvlc_media_parsed_status_t</c>).</summary>
    public enum VlcMediaParsedStatus
    {
        Skipped = 1,
        Failed = 2,
        Timeout = 3,
        Done = 4,
    }

    /// <summary>Slave (external subtitle / audio) kind (<c>libvlc_media_slave_type_t</c>).</summary>
    public enum VlcMediaSlaveType
    {
        Subtitle = 0,
        Audio = 1,
    }

    /// <summary>Marquee (on-screen text) options (<c>libvlc_video_marquee_option_t</c>).</summary>
    public enum VlcVideoMarqueeOption
    {
        Enable = 0,
        Text = 1,
        Color = 2,
        Opacity = 3,
        Position = 4,
        Refresh = 5,
        Size = 6,
        Timeout = 7,
        X = 8,
        Y = 9,
    }

    /// <summary>Video adjust (post-processing) options (<c>libvlc_video_adjust_option_t</c>).</summary>
    public enum VlcVideoAdjustOption
    {
        Enable = 0,
        Contrast = 1,
        Brightness = 2,
        Hue = 3,
        Saturation = 4,
        Gamma = 5,
    }

    /// <summary>Logo overlay options (<c>libvlc_video_logo_option_t</c>).</summary>
    public enum VlcVideoLogoOption
    {
        Enable = 0,
        File = 1,
        X = 2,
        Y = 3,
        Delay = 4,
        Repeat = 5,
        Opacity = 6,
        Position = 7,
    }

    /// <summary>On-screen anchor position (<c>libvlc_position_t</c>).</summary>
    public enum VlcPosition
    {
        Disable = -1,
        Center = 0,
        Left = 1,
        Right = 2,
        Top = 3,
        TopLeft = 4,
        TopRight = 5,
        Bottom = 6,
        BottomLeft = 7,
        BottomRight = 8,
    }

    /// <summary>Menu navigation actions (<c>libvlc_navigate_mode_t</c>).</summary>
    public enum VlcNavigateMode
    {
        Activate = 0,
        Up = 1,
        Down = 2,
        Left = 3,
        Right = 4,
        Popup = 5,
    }

    /// <summary>Stereo down-mix mode (<c>libvlc_audio_output_channel_t</c>).</summary>
    public enum VlcAudioOutputChannel
    {
        Error = -1,
        Stereo = 1,
        RStereo = 2,
        Left = 3,
        Right = 4,
        Dolbys = 5,
    }

    /// <summary>Log verbosity level (<c>libvlc_log_level</c>).</summary>
    public enum VlcLogLevel
    {
        Debug = 0,
        Notice = 2,
        Warning = 3,
        Error = 4,
    }

    /// <summary>Playback repeat mode for the media-list player (<c>libvlc_playback_mode_t</c>).</summary>
    public enum VlcPlaybackMode
    {
        Default = 0,
        Loop = 1,
        Repeat = 2,
    }

    /// <summary>Discoverer category (<c>libvlc_media_discoverer_category_t</c>).</summary>
    public enum VlcMediaDiscovererCategory
    {
        Devices = 0,
        Lan = 1,
        Podcasts = 2,
        LocalDirs = 3,
    }

    /// <summary>Dialog severity (<c>libvlc_dialog_question_type</c>).</summary>
    public enum VlcDialogQuestionType
    {
        Normal = 0,
        Warning = 1,
        Critical = 2,
    }

    /// <summary>
    /// Event identifiers raised by libvlc objects (<c>libvlc_event_e</c>).
    /// Values match the native header exactly, including the group base offsets.
    /// </summary>
    public enum VlcEventType
    {
        // Media (0x000)
        MediaMetaChanged = 0x000,
        MediaSubItemAdded,
        MediaDurationChanged,
        MediaParsedChanged,
        MediaFreed,
        MediaStateChanged,
        MediaSubItemTreeAdded,

        // MediaPlayer (0x100)
        MediaPlayerMediaChanged = 0x100,
        MediaPlayerNothingSpecial,
        MediaPlayerOpening,
        MediaPlayerBuffering,
        MediaPlayerPlaying,
        MediaPlayerPaused,
        MediaPlayerStopped,
        MediaPlayerForward,
        MediaPlayerBackward,
        MediaPlayerEndReached,
        MediaPlayerEncounteredError,
        MediaPlayerTimeChanged,
        MediaPlayerPositionChanged,
        MediaPlayerSeekableChanged,
        MediaPlayerPausableChanged,
        MediaPlayerTitleChanged,
        MediaPlayerSnapshotTaken,
        MediaPlayerLengthChanged,
        MediaPlayerVout,
        MediaPlayerScrambledChanged,
        MediaPlayerESAdded,
        MediaPlayerESDeleted,
        MediaPlayerESSelected,
        MediaPlayerCorked,
        MediaPlayerUncorked,
        MediaPlayerMuted,
        MediaPlayerUnmuted,
        MediaPlayerAudioVolume,
        MediaPlayerAudioDevice,
        MediaPlayerChapterChanged,

        // MediaList (0x200)
        MediaListItemAdded = 0x200,
        MediaListWillAddItem,
        MediaListItemDeleted,
        MediaListWillDeleteItem,
        MediaListEndReached,

        // MediaListView (0x300)
        MediaListViewItemAdded = 0x300,
        MediaListViewWillAddItem,
        MediaListViewItemDeleted,
        MediaListViewWillDeleteItem,

        // MediaListPlayer (0x400)
        MediaListPlayerPlayed = 0x400,
        MediaListPlayerNextItemSet,
        MediaListPlayerStopped,

        // Discoverer / Renderer (0x500)
        MediaDiscovererStarted = 0x500,
        MediaDiscovererEnded,
        RendererDiscovererItemAdded,
        RendererDiscovererItemDeleted,

        // VLM (0x600)
        VlmMediaAdded = 0x600,
        VlmMediaRemoved,
        VlmMediaChanged,
        VlmMediaInstanceStarted,
        VlmMediaInstanceStopped,
        VlmMediaInstanceStatusInit,
        VlmMediaInstanceStatusOpening,
        VlmMediaInstanceStatusPlaying,
        VlmMediaInstanceStatusPause,
        VlmMediaInstanceStatusEnd,
        VlmMediaInstanceStatusError,
    }
}
