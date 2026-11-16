using System;
using Avalonia.Controls;
using Avalonia.Platform;

namespace VLCDotNet.Avalonia
{
    /// <summary>
    /// A <see cref="NativeControlHost"/> that surfaces the child native window
    /// handle (HWND / X11 window / NSView) so a <see cref="MediaPlayer"/> can be
    /// pointed at it for hardware-accelerated output.
    /// </summary>
    internal sealed class NativeVideoHost : NativeControlHost
    {
        public Action<IPlatformHandle>? HandleCreated { get; set; }

        public Action? HandleDestroying { get; set; }

        protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
        {
            IPlatformHandle handle = base.CreateNativeControlCore(parent);
            HandleCreated?.Invoke(handle);
            return handle;
        }

        protected override void DestroyNativeControlCore(IPlatformHandle control)
        {
            HandleDestroying?.Invoke();
            base.DestroyNativeControlCore(control);
        }
    }
}
