using System;

namespace VLCDotNet
{
    public sealed partial class MediaPlayer
    {
        // The event types we subscribe to on the player's event manager. Kept in
        // one place so Attach and Detach stay in sync.
        private static readonly VlcEventType[] SubscribedEvents =
        {
            VlcEventType.MediaPlayerOpening,
            VlcEventType.MediaPlayerPlaying,
            VlcEventType.MediaPlayerPaused,
            VlcEventType.MediaPlayerStopped,
            VlcEventType.MediaPlayerEndReached,
            VlcEventType.MediaPlayerEncounteredError,
            VlcEventType.MediaPlayerTimeChanged,
            VlcEventType.MediaPlayerPositionChanged,
            VlcEventType.MediaPlayerLengthChanged,
            VlcEventType.MediaPlayerBuffering,
            VlcEventType.MediaPlayerVout,
            VlcEventType.MediaPlayerMediaChanged,
            VlcEventType.MediaPlayerSeekableChanged,
            VlcEventType.MediaPlayerPausableChanged,
        };

        private VlcEventCallback? _eventCallback;
        private IntPtr _eventManager;

        /// <summary>Raised when the player begins opening the media.</summary>
        public event EventHandler? Opening;

        /// <summary>Raised when playback starts.</summary>
        public event EventHandler? Playing;

        /// <summary>Raised when playback is paused.</summary>
        public event EventHandler? Paused;

        /// <summary>Raised when playback stops.</summary>
        public event EventHandler? Stopped;

        /// <summary>Raised when the end of the media is reached.</summary>
        public event EventHandler? EndReached;

        /// <summary>Raised when a playback error occurs.</summary>
        public event EventHandler? EncounteredError;

        /// <summary>Raised when the current media changes.</summary>
        public event EventHandler? MediaChanged;

        /// <summary>Raised when the seekable flag changes.</summary>
        public event EventHandler? SeekableChanged;

        /// <summary>Raised when the pausable flag changes.</summary>
        public event EventHandler? PausableChanged;

        /// <summary>Raised when the playback time changes.</summary>
        public event EventHandler<MediaPlayerTimeChangedEventArgs>? TimeChanged;

        /// <summary>Raised when the playback position changes.</summary>
        public event EventHandler<MediaPlayerPositionChangedEventArgs>? PositionChanged;

        /// <summary>Raised when the media length changes.</summary>
        public event EventHandler<MediaPlayerLengthChangedEventArgs>? LengthChanged;

        /// <summary>Raised while the media is buffering.</summary>
        public event EventHandler<MediaPlayerBufferingEventArgs>? Buffering;

        /// <summary>Raised when the number of active video outputs changes.</summary>
        public event EventHandler<MediaPlayerVoutEventArgs>? Vout;

        private void AttachEvents()
        {
            _eventManager = LibVlc.libvlc_media_player_event_manager(_handle.DangerousHandle);
            if (_eventManager == IntPtr.Zero)
            {
                return;
            }

            // Stored in a field so the GC cannot collect the delegate while libvlc
            // holds the native function pointer.
            _eventCallback = OnVlcEvent;
            foreach (VlcEventType type in SubscribedEvents)
            {
                LibVlc.libvlc_event_attach(_eventManager, type, _eventCallback, IntPtr.Zero);
            }
        }

        private void DetachEvents()
        {
            if (_eventManager == IntPtr.Zero || _eventCallback is null)
            {
                return;
            }

            foreach (VlcEventType type in SubscribedEvents)
            {
                LibVlc.libvlc_event_detach(_eventManager, type, _eventCallback, IntPtr.Zero);
            }

            _eventCallback = null;
            _eventManager = IntPtr.Zero;
        }

        // Invoked on an internal libvlc thread. A managed exception must never
        // propagate back into native code (it would crash the process), so every
        // dispatch is wrapped. Handlers should be quick and marshal to the UI
        // thread themselves.
        private void OnVlcEvent(IntPtr evt, IntPtr data)
        {
            try
            {
                switch (VlcEvent.GetEventType(evt))
                {
                    case VlcEventType.MediaPlayerOpening:
                        Opening?.Invoke(this, EventArgs.Empty);
                        break;
                    case VlcEventType.MediaPlayerPlaying:
                        Playing?.Invoke(this, EventArgs.Empty);
                        break;
                    case VlcEventType.MediaPlayerPaused:
                        Paused?.Invoke(this, EventArgs.Empty);
                        break;
                    case VlcEventType.MediaPlayerStopped:
                        Stopped?.Invoke(this, EventArgs.Empty);
                        break;
                    case VlcEventType.MediaPlayerEndReached:
                        EndReached?.Invoke(this, EventArgs.Empty);
                        break;
                    case VlcEventType.MediaPlayerEncounteredError:
                        EncounteredError?.Invoke(this, EventArgs.Empty);
                        break;
                    case VlcEventType.MediaPlayerMediaChanged:
                        MediaChanged?.Invoke(this, EventArgs.Empty);
                        break;
                    case VlcEventType.MediaPlayerSeekableChanged:
                        SeekableChanged?.Invoke(this, EventArgs.Empty);
                        break;
                    case VlcEventType.MediaPlayerPausableChanged:
                        PausableChanged?.Invoke(this, EventArgs.Empty);
                        break;
                    case VlcEventType.MediaPlayerTimeChanged:
                        TimeChanged?.Invoke(this, new MediaPlayerTimeChangedEventArgs(VlcEvent.ReadNewTime(evt)));
                        break;
                    case VlcEventType.MediaPlayerPositionChanged:
                        PositionChanged?.Invoke(this, new MediaPlayerPositionChangedEventArgs(VlcEvent.ReadNewPosition(evt)));
                        break;
                    case VlcEventType.MediaPlayerLengthChanged:
                        LengthChanged?.Invoke(this, new MediaPlayerLengthChangedEventArgs(VlcEvent.ReadNewLength(evt)));
                        break;
                    case VlcEventType.MediaPlayerBuffering:
                        Buffering?.Invoke(this, new MediaPlayerBufferingEventArgs(VlcEvent.ReadNewCache(evt)));
                        break;
                    case VlcEventType.MediaPlayerVout:
                        Vout?.Invoke(this, new MediaPlayerVoutEventArgs(VlcEvent.ReadVoutCount(evt)));
                        break;
                }
            }
            catch
            {
                // Swallow: never let a managed exception cross the native boundary.
            }
        }
    }
}
