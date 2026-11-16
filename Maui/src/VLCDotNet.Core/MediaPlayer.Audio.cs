using System;

namespace VLCDotNet
{
    public sealed partial class MediaPlayer
    {
        private MemoryAudioSource? _audioSource;

        /// <summary>
        /// Routes decoded PCM audio to an in-memory <see cref="MemoryAudioSource"/>
        /// (bypassing the system audio device) so the audio pipeline can be
        /// verified headlessly. The player owns and disposes the returned source.
        /// </summary>
        public MemoryAudioSource EnableAudioCapture(uint sampleRate = 44100, uint channels = 2)
        {
            ThrowIfDisposed();
            lock (_sync)
            {
                _audioSource?.Dispose();
                _audioSource = new MemoryAudioSource(this, sampleRate, channels);
                return _audioSource;
            }
        }

        /// <summary>The active audio capture source, if <see cref="EnableAudioCapture"/> was called.</summary>
        public MemoryAudioSource? AudioSource => _audioSource;

        // Low-level pass-throughs used by MemoryAudioSource. The caller keeps the
        // supplied delegate alive for the player's lifetime.
        internal void SetAudioFormat(string format, uint rate, uint channels)
            => LibVlc.libvlc_audio_set_format(NativePointer, format, rate, channels);

        internal void SetAudioCallbacks(VlcAudioPlayCb play)
            => LibVlc.libvlc_audio_set_callbacks(NativePointer, play, null, null, null, null, IntPtr.Zero);

        private void DisposeAudioOutput()
        {
            MemoryAudioSource? source;
            lock (_sync)
            {
                source = _audioSource;
                _audioSource = null;
            }

            source?.Dispose();
        }
    }
}
