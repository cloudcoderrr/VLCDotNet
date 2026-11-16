using System;

namespace VLCDotNet
{
    /// <summary>
    /// Error raised when a libvlc operation fails. The message includes the last
    /// libvlc error string for the current thread when one is available.
    /// </summary>
    public sealed class VlcException : Exception
    {
        public VlcException(string message)
            : base(Compose(message))
        {
        }

        public VlcException(string message, Exception inner)
            : base(Compose(message), inner)
        {
        }

        private static string Compose(string message)
        {
            string? native = LibVlc.Utf8ToString(LibVlc.libvlc_errmsg());
            LibVlc.libvlc_clearerr();
            return string.IsNullOrEmpty(native) ? message : $"{message} ({native})";
        }
    }
}
