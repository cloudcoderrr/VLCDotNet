using System;

namespace VLCDotNet
{
    /// <summary>
    /// A managed audio equalizer. Create one (flat or from a preset), adjust the
    /// pre-amp and per-band gains, then apply it with
    /// <see cref="MediaPlayer.SetEqualizer"/>. Owns the native equalizer and
    /// releases it on <see cref="Dispose"/>.
    /// </summary>
    public sealed class Equalizer : IDisposable
    {
        private readonly EqualizerHandle _handle;
        private bool _disposed;

        /// <summary>Creates a flat equalizer (all bands at 0 dB).</summary>
        public Equalizer()
        {
            _handle = SafeHandleFactory.Adopt<EqualizerHandle>(LibVlc.libvlc_audio_equalizer_new(), "equalizer");
        }

        /// <summary>Creates an equalizer initialised from a built-in preset.</summary>
        public Equalizer(uint presetIndex)
        {
            _handle = SafeHandleFactory.Adopt<EqualizerHandle>(
                LibVlc.libvlc_audio_equalizer_new_from_preset(presetIndex), "equalizer preset");
        }

        /// <summary>Number of built-in presets.</summary>
        public static uint PresetCount => LibVlc.libvlc_audio_equalizer_get_preset_count();

        /// <summary>Name of a built-in preset.</summary>
        public static string? GetPresetName(uint index) =>
            LibVlc.Utf8ToString(LibVlc.libvlc_audio_equalizer_get_preset_name(index));

        /// <summary>Number of frequency bands.</summary>
        public static uint BandCount => LibVlc.libvlc_audio_equalizer_get_band_count();

        /// <summary>Centre frequency of a band in Hz.</summary>
        public static float GetBandFrequency(uint index) =>
            LibVlc.libvlc_audio_equalizer_get_band_frequency(index);

        internal IntPtr NativePointer
        {
            get
            {
                ThrowIfDisposed();
                return _handle.DangerousHandle;
            }
        }

        /// <summary>Pre-amplification in dB (-20 to +20).</summary>
        public float Preamp
        {
            get
            {
                ThrowIfDisposed();
                return LibVlc.libvlc_audio_equalizer_get_preamp(NativePointer);
            }
            set
            {
                ThrowIfDisposed();
                LibVlc.libvlc_audio_equalizer_set_preamp(NativePointer, value);
            }
        }

        /// <summary>Gets the gain of a band in dB.</summary>
        public float GetAmp(uint band)
        {
            ThrowIfDisposed();
            return LibVlc.libvlc_audio_equalizer_get_amp_at_index(NativePointer, band);
        }

        /// <summary>Sets the gain of a band in dB (-20 to +20). Returns true on success.</summary>
        public bool SetAmp(float amp, uint band)
        {
            ThrowIfDisposed();
            return LibVlc.libvlc_audio_equalizer_set_amp_at_index(NativePointer, amp, band) == 0;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(Equalizer));
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _handle.Dispose();
        }
    }
}
