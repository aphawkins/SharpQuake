namespace SharpQuake.Framework.IO.Alias
{
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct AliasFrame
    {
        public TriVertex bboxmin;	// lightnormal isn't used
        public TriVertex bboxmax;	// lightnormal isn't used
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] name; // char[16]	// frame name from grabbing

        public static int SizeInBytes = Marshal.SizeOf(typeof(AliasFrame));
    }
}
