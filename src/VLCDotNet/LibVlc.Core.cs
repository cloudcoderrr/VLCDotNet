using System;
using System.Runtime.InteropServices;

namespace VLCDotNet
{
    public static partial class LibVlc
    {
        private const CallingConvention Cc = CallingConvention.Cdecl;

        // ----- Error handling (libvlc.h) -----------------------------------

        /// <summary>Returns the last human-readable error message, or NULL. (<c>libvlc_errmsg</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_errmsg();

        /// <summary>Clears the last LibVLC error for the current thread. (<c>libvlc_clearerr</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_clearerr();

        // ----- Instance (libvlc.h) -----------------------------------------

        /// <summary>Creates and initializes a libvlc instance. (<c>libvlc_new</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_new(int argc,
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPUTF8Str)] string[]? argv);

        /// <summary>Decrements the reference count and destroys the instance when it reaches zero. (<c>libvlc_release</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_release(IntPtr instance);

        /// <summary>Increments the instance reference count. (<c>libvlc_retain</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_retain(IntPtr instance);

        /// <summary>Tries to start a user interface for the instance. (<c>libvlc_add_intf</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_add_intf(IntPtr instance,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string? name);

        /// <summary>Sets a human-readable application name and HTTP user agent. (<c>libvlc_set_user_agent</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_set_user_agent(IntPtr instance,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string? name,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string? http);

        /// <summary>Sets application identifier metadata. (<c>libvlc_set_app_id</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_set_app_id(IntPtr instance,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string? id,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string? version,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string? icon);

        // ----- Version (libvlc.h) ------------------------------------------

        /// <summary>Returns the libvlc version string (const char*). (<c>libvlc_get_version</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_get_version();

        /// <summary>Returns the compiler used to build libvlc (const char*). (<c>libvlc_get_compiler</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_get_compiler();

        /// <summary>Returns the VCS changeset libvlc was built from (const char*). (<c>libvlc_get_changeset</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_get_changeset();

        /// <summary>Frees memory that was allocated and returned by a libvlc function. (<c>libvlc_free</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_free(IntPtr ptr);

        // ----- Events (libvlc.h) -------------------------------------------

        /// <summary>Registers an event handler on an event manager. (<c>libvlc_event_attach</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern int libvlc_event_attach(IntPtr eventManager, VlcEventType eventType,
            VlcEventCallback callback, IntPtr userData);

        /// <summary>Unregisters an event handler. (<c>libvlc_event_detach</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_event_detach(IntPtr eventManager, VlcEventType eventType,
            VlcEventCallback callback, IntPtr userData);

        /// <summary>Returns the human-readable name of an event type (const char*). (<c>libvlc_event_type_name</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_event_type_name(VlcEventType eventType);

        // ----- Logging (libvlc.h) ------------------------------------------

        /// <summary>Extracts module/file/line context from a log entry. (<c>libvlc_log_get_context</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_log_get_context(IntPtr ctx, out IntPtr module, out IntPtr file, out uint line);

        /// <summary>Extracts object name/header/id from a log entry. (<c>libvlc_log_get_object</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_log_get_object(IntPtr ctx, out IntPtr name, out IntPtr header, out UIntPtr id);

        /// <summary>Removes the logging callback (restores default). (<c>libvlc_log_unset</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_log_unset(IntPtr instance);

        /// <summary>Installs a logging callback. (<c>libvlc_log_set</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_log_set(IntPtr instance, VlcLogCallback callback, IntPtr data);

        /// <summary>Sends log messages to the given C <c>FILE*</c> stream. (<c>libvlc_log_set_file</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_log_set_file(IntPtr instance, IntPtr stream);

        // ----- Filters / modules (libvlc.h) --------------------------------

        /// <summary>Returns a linked list of available audio filters (module descriptions). (<c>libvlc_audio_filter_list_get</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_audio_filter_list_get(IntPtr instance);

        /// <summary>Returns a linked list of available video filters (module descriptions). (<c>libvlc_video_filter_list_get</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern IntPtr libvlc_video_filter_list_get(IntPtr instance);

        /// <summary>Releases a module description linked list. (<c>libvlc_module_description_list_release</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern void libvlc_module_description_list_release(IntPtr list);

        /// <summary>Returns the libvlc-global clock in microseconds. (<c>libvlc_clock</c>)</summary>
        [DllImport(Lib, CallingConvention = Cc, ExactSpelling = true)]
        public static extern long libvlc_clock();
    }
}
