using System;
using System.Collections.Generic;

namespace VLCDotNet
{
    /// <summary>
    /// A managed libvlc media player. Owns the native <c>libvlc_media_player_t</c>
    /// and releases it (after stopping playback and detaching every event and
    /// video callback) on <see cref="Dispose"/>. Exposes transport, timing,
    /// audio, track, snapshot, video-output and event APIs.
    /// </summary>
    public sealed partial class MediaPlayer : IDisposable
    {
        private readonly LibVLC _libvlc;
        private readonly MediaPlayerHandle _handle;
        private readonly object _sync = new object();
        private Media? _media;
        private Equalizer? _equalizer;
        private bool _disposed;

        /// <summary>Creates a player bound to an engine, with no media set.</summary>
        public MediaPlayer(LibVLC libvlc)
        {
            _libvlc = libvlc ?? throw new ArgumentNullException(nameof(libvlc));
            IntPtr ptr = LibVlc.libvlc_media_player_new(libvlc.NativeInstance);
            _handle = SafeHandleFactory.Adopt<MediaPlayerHandle>(ptr, "media player");
            AttachEvents();
        }

        /// <summary>Creates a player with <paramref name="media"/> already set.</summary>
        public MediaPlayer(Media media)
        {
            if (media is null)
            {
                throw new ArgumentNullException(nameof(media));
            }

            _libvlc = media.LibVLC;
            IntPtr ptr = LibVlc.libvlc_media_player_new_from_media(media.NativePointer);
            _handle = SafeHandleFactory.Adopt<MediaPlayerHandle>(ptr, "media player");
            _media = media;
            AttachEvents();
        }

        /// <summary>The engine this player belongs to.</summary>
        public LibVLC LibVLC => _libvlc;

        /// <summary>The native <c>libvlc_media_player_t*</c>. For advanced interop only.</summary>
        public IntPtr NativePointer
        {
            get
            {
                ThrowIfDisposed();
                return _handle.DangerousHandle;
            }
        }

        /// <summary>Gets or sets the media this player will play.</summary>
        public Media? Media
        {
            get => _media;
            set
            {
                ThrowIfDisposed();
                _media = value;
                LibVlc.libvlc_media_player_set_media(NativePointer, value?.NativePointer ?? IntPtr.Zero);
            }
        }

        /// <summary><see langword="true"/> while the player is actively playing.</summary>
        public bool IsPlaying
        {
            get
            {
                ThrowIfDisposed();
                return LibVlc.libvlc_media_player_is_playing(NativePointer) != 0;
            }
        }

        /// <summary>The current playback state.</summary>
        public VlcState State
        {
            get
            {
                ThrowIfDisposed();
                return LibVlc.libvlc_media_player_get_state(NativePointer);
            }
        }

        /// <summary><see langword="true"/> when the media can be paused.</summary>
        public bool CanPause
        {
            get
            {
                ThrowIfDisposed();
                return LibVlc.libvlc_media_player_can_pause(NativePointer) != 0;
            }
        }

        /// <summary><see langword="true"/> when the media is seekable.</summary>
        public bool IsSeekable
        {
            get
            {
                ThrowIfDisposed();
                return LibVlc.libvlc_media_player_is_seekable(NativePointer) != 0;
            }
        }

        /// <summary>Number of active video outputs.</summary>
        public uint VideoOutputCount
        {
            get
            {
                ThrowIfDisposed();
                return LibVlc.libvlc_media_player_has_vout(NativePointer);
            }
        }

        // ----- Transport ---------------------------------------------------

        /// <summary>Starts (or resumes) playback. Returns <see langword="true"/> on success.</summary>
        public bool Play()
        {
            ThrowIfDisposed();
            return LibVlc.libvlc_media_player_play(NativePointer) == 0;
        }

        /// <summary>Sets <paramref name="media"/> and starts playback.</summary>
        public bool Play(Media media)
        {
            Media = media;
            return Play();
        }

        /// <summary>Pauses or resumes playback.</summary>
        public void SetPause(bool pause)
        {
            ThrowIfDisposed();
            LibVlc.libvlc_media_player_set_pause(NativePointer, pause ? 1 : 0);
        }

        /// <summary>Toggles the pause state.</summary>
        public void Pause()
        {
            ThrowIfDisposed();
            LibVlc.libvlc_media_player_pause(NativePointer);
        }

        /// <summary>Stops playback and tears down the decoder/vout threads.</summary>
        public void Stop()
        {
            if (_disposed)
            {
                return;
            }

            LibVlc.libvlc_media_player_stop(NativePointer);
        }

        /// <summary>Shows the next video frame while paused.</summary>
        public void NextFrame()
        {
            ThrowIfDisposed();
            LibVlc.libvlc_media_player_next_frame(NativePointer);
        }

        // ----- Timing ------------------------------------------------------

        /// <summary>Current playback time in milliseconds.</summary>
        public long Time
        {
            get
            {
                ThrowIfDisposed();
                return LibVlc.libvlc_media_player_get_time(NativePointer);
            }
            set
            {
                ThrowIfDisposed();
                LibVlc.libvlc_media_player_set_time(NativePointer, value);
            }
        }

        /// <summary>Playback position in the range 0.0 - 1.0.</summary>
        public float Position
        {
            get
            {
                ThrowIfDisposed();
                return LibVlc.libvlc_media_player_get_position(NativePointer);
            }
            set
            {
                ThrowIfDisposed();
                LibVlc.libvlc_media_player_set_position(NativePointer, value);
            }
        }

        /// <summary>Media length in milliseconds.</summary>
        public long Length
        {
            get
            {
                ThrowIfDisposed();
                return LibVlc.libvlc_media_player_get_length(NativePointer);
            }
        }

        /// <summary>Playback rate (1.0 = normal).</summary>
        public float Rate
        {
            get
            {
                ThrowIfDisposed();
                return LibVlc.libvlc_media_player_get_rate(NativePointer);
            }
            set
            {
                ThrowIfDisposed();
                LibVlc.libvlc_media_player_set_rate(NativePointer, value);
            }
        }

        // ----- Audio -------------------------------------------------------

        /// <summary>Audio volume as a percentage (0 - 100, values above 100 amplify).</summary>
        public int Volume
        {
            get
            {
                ThrowIfDisposed();
                return LibVlc.libvlc_audio_get_volume(NativePointer);
            }
            set
            {
                ThrowIfDisposed();
                LibVlc.libvlc_audio_set_volume(NativePointer, value);
            }
        }

        /// <summary>Audio mute state.</summary>
        public bool Mute
        {
            get
            {
                ThrowIfDisposed();
                return LibVlc.libvlc_audio_get_mute(NativePointer) == 1;
            }
            set
            {
                ThrowIfDisposed();
                LibVlc.libvlc_audio_set_mute(NativePointer, value ? 1 : 0);
            }
        }

        /// <summary>Toggles the mute state.</summary>
        public void ToggleMute()
        {
            ThrowIfDisposed();
            LibVlc.libvlc_audio_toggle_mute(NativePointer);
        }

        // ----- Tracks ------------------------------------------------------

        /// <summary>Number of selectable audio tracks.</summary>
        public int AudioTrackCount
        {
            get
            {
                ThrowIfDisposed();
                return LibVlc.libvlc_audio_get_track_count(NativePointer);
            }
        }

        /// <summary>Current audio track id (-1 when disabled).</summary>
        public int AudioTrack
        {
            get
            {
                ThrowIfDisposed();
                return LibVlc.libvlc_audio_get_track(NativePointer);
            }
        }

        /// <summary>Selects an audio track by id. Returns <see langword="true"/> on success.</summary>
        public bool SetAudioTrack(int trackId)
        {
            ThrowIfDisposed();
            return LibVlc.libvlc_audio_set_track(NativePointer, trackId) == 0;
        }

        /// <summary>Number of subtitle (SPU) tracks.</summary>
        public int SpuCount
        {
            get
            {
                ThrowIfDisposed();
                return LibVlc.libvlc_video_get_spu_count(NativePointer);
            }
        }

        /// <summary>Current subtitle track id (-1 when disabled).</summary>
        public int Spu
        {
            get
            {
                ThrowIfDisposed();
                return LibVlc.libvlc_video_get_spu(NativePointer);
            }
        }

        /// <summary>Selects a subtitle track by id. Returns <see langword="true"/> on success.</summary>
        public bool SetSpu(int spuId)
        {
            ThrowIfDisposed();
            return LibVlc.libvlc_video_set_spu(NativePointer, spuId) == 0;
        }

        /// <summary>Current video track id (-1 when disabled).</summary>
        public int VideoTrack
        {
            get
            {
                ThrowIfDisposed();
                return LibVlc.libvlc_video_get_track(NativePointer);
            }
        }

        /// <summary>Selects a video track by id. Returns <see langword="true"/> on success.</summary>
        public bool SetVideoTrack(int trackId)
        {
            ThrowIfDisposed();
            return LibVlc.libvlc_video_set_track(NativePointer, trackId) == 0;
        }

        /// <summary>Lists the selectable audio tracks as (id, name) pairs.</summary>
        public IReadOnlyList<TrackDescription> AudioTrackDescriptions =>
            ReadTrackDescriptions(LibVlc.libvlc_audio_get_track_description(NativePointer));

        /// <summary>Lists the selectable subtitle tracks as (id, name) pairs.</summary>
        public IReadOnlyList<TrackDescription> SpuTrackDescriptions =>
            ReadTrackDescriptions(LibVlc.libvlc_video_get_spu_description(NativePointer));

        /// <summary>Attaches an external subtitle/audio slave to the player.</summary>
        public bool AddSlave(VlcMediaSlaveType type, string uri, bool select)
        {
            ThrowIfDisposed();
            return LibVlc.libvlc_media_player_add_slave(NativePointer, type, uri, select) == 0;
        }

        private IReadOnlyList<TrackDescription> ReadTrackDescriptions(IntPtr head)
        {
            ThrowIfDisposed();
            var list = new List<TrackDescription>();
            IntPtr node = head;
            while (node != IntPtr.Zero)
            {
                var desc = System.Runtime.InteropServices.Marshal.PtrToStructure<VlcTrackDescription>(node);
                list.Add(new TrackDescription(desc.Id, LibVlc.Utf8ToString(desc.Name)));
                node = desc.Next;
            }

            if (head != IntPtr.Zero)
            {
                LibVlc.libvlc_track_description_list_release(head);
            }

            return list;
        }

        // ----- Equalizer ---------------------------------------------------

        /// <summary>
        /// Applies (or clears, when <see langword="null"/>) an equalizer. The player
        /// keeps a managed reference so the equalizer is not collected while in use.
        /// </summary>
        public bool SetEqualizer(Equalizer? equalizer)
        {
            ThrowIfDisposed();
            _equalizer = equalizer;
            return LibVlc.libvlc_media_player_set_equalizer(
                NativePointer, equalizer?.NativePointer ?? IntPtr.Zero) == 0;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MediaPlayer));
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
            }

            // Order is critical to avoid native use-after-free:
            //   1. Stop playback so the decoder/vout threads stop invoking the
            //      video/display callbacks.
            //   2. Detach the event dispatcher.
            //   3. Release the player handle.
            //   4. Finally free the video-output buffer the (now idle) callbacks used.
            try
            {
                LibVlc.libvlc_media_player_stop(_handle.DangerousHandle);
            }
            catch
            {
                // best effort
            }

            DetachEvents();
            _handle.Dispose();
            DisposeVideoOutput();
            DisposeAudioOutput();

            _media = null;
            _equalizer = null;
        }
    }

    /// <summary>A selectable track as reported by libvlc (id + display name).</summary>
    public readonly struct TrackDescription
    {
        public TrackDescription(int id, string? name)
        {
            Id = id;
            Name = name;
        }

        public int Id { get; }

        public string? Name { get; }

        public override string ToString() => $"#{Id} {Name}";
    }
}
