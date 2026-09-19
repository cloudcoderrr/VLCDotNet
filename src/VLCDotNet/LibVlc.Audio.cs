using System;
using System.Runtime.InteropServices;

namespace VLCDotNet
{
    public static partial class LibVlc
    {
        // ----- Audio output enumeration ------------------------------------

        /// <summary>Returns the list of available audio output modules. (<c>libvlc_audio_output_list_get</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_audio_output_list_get(IntPtr instance);

        /// <summary>Frees an audio output module list. (<c>libvlc_audio_output_list_release</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_audio_output_list_release(IntPtr list);

        /// <summary>Selects an audio output module by name. (<c>libvlc_audio_output_set</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_audio_output_set(IntPtr player,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

        /// <summary>Enumerates the output devices for the player's audio module. (<c>libvlc_audio_output_device_enum</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_audio_output_device_enum(IntPtr player);

        /// <summary>Frees an audio output device list. (<c>libvlc_audio_output_device_list_release</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_audio_output_device_list_release(IntPtr list);

        /// <summary>Configures the audio output device. (<c>libvlc_audio_output_device_set</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_audio_output_device_set(IntPtr player,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string? module,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string deviceId);

        /// <summary>Gets the current audio output device id (const char*, caller must <see cref="libvlc_free"/>). (<c>libvlc_audio_output_device_get</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_audio_output_device_get(IntPtr player);

        // ----- Volume / mute ------------------------------------------------

        /// <summary>Toggles mute. (<c>libvlc_audio_toggle_mute</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_audio_toggle_mute(IntPtr player);

        /// <summary>Gets mute state (-1 unknown, 0 off, 1 on). (<c>libvlc_audio_get_mute</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_audio_get_mute(IntPtr player);

        /// <summary>Sets mute state. (<c>libvlc_audio_set_mute</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_audio_set_mute(IntPtr player, int status);

        /// <summary>Gets the current volume (0-100+, percent). (<c>libvlc_audio_get_volume</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_audio_get_volume(IntPtr player);

        /// <summary>Sets the volume (percent). Returns 0 on success. (<c>libvlc_audio_set_volume</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_audio_set_volume(IntPtr player, int volume);

        // ----- Audio tracks -------------------------------------------------

        /// <summary>Gets the number of audio tracks. (<c>libvlc_audio_get_track_count</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_audio_get_track_count(IntPtr player);

        /// <summary>Gets the audio track description list. (<c>libvlc_audio_get_track_description</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_audio_get_track_description(IntPtr player);

        /// <summary>Gets the current audio track id. (<c>libvlc_audio_get_track</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_audio_get_track(IntPtr player);

        /// <summary>Selects an audio track by id. (<c>libvlc_audio_set_track</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_audio_set_track(IntPtr player, int track);

        /// <summary>Gets the audio channel down-mix mode. (<c>libvlc_audio_get_channel</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_audio_get_channel(IntPtr player);

        /// <summary>Sets the audio channel down-mix mode. (<c>libvlc_audio_set_channel</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_audio_set_channel(IntPtr player, int channel);

        /// <summary>Gets the audio delay in microseconds. (<c>libvlc_audio_get_delay</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern long libvlc_audio_get_delay(IntPtr player);

        /// <summary>Sets the audio delay in microseconds. (<c>libvlc_audio_set_delay</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_audio_set_delay(IntPtr player, long delay);

        // ----- Decode-to-memory audio callbacks ----------------------------

        /// <summary>Installs custom audio playback callbacks. (<c>libvlc_audio_set_callbacks</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_audio_set_callbacks(IntPtr player,
            VlcAudioPlayCb play, VlcAudioPauseCb? pause, VlcAudioResumeCb? resume,
            VlcAudioFlushCb? flush, VlcAudioDrainCb? drain, IntPtr opaque);

        /// <summary>Installs a volume-change callback for custom audio. (<c>libvlc_audio_set_volume_callback</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_audio_set_volume_callback(IntPtr player, VlcAudioVolumeCb? volume);

        /// <summary>Installs audio format setup/cleanup callbacks. (<c>libvlc_audio_set_format_callbacks</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_audio_set_format_callbacks(IntPtr player,
            VlcAudioSetupCb setup, VlcAudioCleanupCb? cleanup);

        /// <summary>Sets a fixed decoded audio format (e.g. "S16N"). (<c>libvlc_audio_set_format</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_audio_set_format(IntPtr player,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string format, uint rate, uint channels);

        // ----- Equalizer ----------------------------------------------------

        /// <summary>Gets the number of built-in equalizer presets. (<c>libvlc_audio_equalizer_get_preset_count</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern uint libvlc_audio_equalizer_get_preset_count();

        /// <summary>Gets the name of an equalizer preset (const char*). (<c>libvlc_audio_equalizer_get_preset_name</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_audio_equalizer_get_preset_name(uint index);

        /// <summary>Gets the number of equalizer bands. (<c>libvlc_audio_equalizer_get_band_count</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern uint libvlc_audio_equalizer_get_band_count();

        /// <summary>Gets the frequency of an equalizer band. (<c>libvlc_audio_equalizer_get_band_frequency</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern float libvlc_audio_equalizer_get_band_frequency(uint index);

        /// <summary>Creates a flat equalizer. (<c>libvlc_audio_equalizer_new</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_audio_equalizer_new();

        /// <summary>Creates an equalizer from a preset. (<c>libvlc_audio_equalizer_new_from_preset</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_audio_equalizer_new_from_preset(uint index);

        /// <summary>Releases an equalizer. (<c>libvlc_audio_equalizer_release</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_audio_equalizer_release(IntPtr equalizer);

        /// <summary>Sets the equalizer pre-amplification. (<c>libvlc_audio_equalizer_set_preamp</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_audio_equalizer_set_preamp(IntPtr equalizer, float preamp);

        /// <summary>Gets the equalizer pre-amplification. (<c>libvlc_audio_equalizer_get_preamp</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern float libvlc_audio_equalizer_get_preamp(IntPtr equalizer);

        /// <summary>Sets the amplification of a band. (<c>libvlc_audio_equalizer_set_amp_at_index</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_audio_equalizer_set_amp_at_index(IntPtr equalizer, float amp, uint band);

        /// <summary>Gets the amplification of a band. (<c>libvlc_audio_equalizer_get_amp_at_index</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern float libvlc_audio_equalizer_get_amp_at_index(IntPtr equalizer, uint band);

        /// <summary>Applies an equalizer to a player. (<c>libvlc_media_player_set_equalizer</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_media_player_set_equalizer(IntPtr player, IntPtr equalizer);
    }
}
