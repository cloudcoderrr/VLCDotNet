using System;
using System.Runtime.InteropServices;

namespace VLCDotNet
{
    public static partial class LibVlc
    {
        /// <summary>
        /// Reads a NUL-terminated UTF-8 <c>const char*</c> into a managed string.
        /// Returns <see langword="null"/> when <paramref name="ptr"/> is <see cref="IntPtr.Zero"/>.
        /// The native memory is not freed; use <see cref="libvlc_free"/> when the
        /// API contract says the caller owns the returned buffer.
        /// </summary>
        public static string? Utf8ToString(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
            {
                return null;
            }

            return Marshal.PtrToStringUTF8(ptr);
        }
    }
}
