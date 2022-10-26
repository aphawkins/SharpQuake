namespace SharpQuake.Framework.IO.Alias
{
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    public struct Triangle
    {
        public int facesfront;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.I4, SizeConst = 3)]
        public int[] vertindex; // int vertindex[3];

        public static int SizeInBytes = Marshal.SizeOf(typeof(Triangle));
    }
}
