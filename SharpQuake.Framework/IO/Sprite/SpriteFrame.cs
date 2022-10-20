using System;
using System.Runtime.InteropServices;

namespace SharpQuake.Framework.IO.Sprite
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct dspriteframe_t
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public int[] origin; // [2];
        public int width;
        public int height;

        public static int SizeInBytes = Marshal.SizeOf(typeof(dspriteframe_t));
    } // dspriteframe_t;
}
