namespace PrometheusExporter.Instrumentation.Mac;

using System.Runtime.InteropServices;

// ReSharper disable CommentTypo
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming
#pragma warning disable IDE1006
#pragma warning disable CA2101
#pragma warning disable CA5392
#pragma warning disable CS8981
internal static class NativeMethods
{
    //------------------------------------------------------------------------
    // Struct
    //------------------------------------------------------------------------

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct timeval
    {
        public long tv_sec;
        public long tv_usec;
    }

    [DllImport("libc")]
    public static extern int sysctlbyname([MarshalAs(UnmanagedType.LPStr)] string name, ref timeval oldp, ref int oldlen, IntPtr newp, int newlen);
}
