using System;
using System.Runtime.InteropServices;

namespace SharpQuake.Framework.IO.Alias
{
	[StructLayout( LayoutKind.Sequential )]
    public struct dtriangle_t
    {
        public int facesfront;
        [MarshalAs( UnmanagedType.ByValArray, ArraySubType = UnmanagedType.I4, SizeConst = 3 )]
        public int[] vertindex; // int vertindex[3];

        public static int SizeInBytes = Marshal.SizeOf( typeof( dtriangle_t ) );
    } // dtriangle_t;
}
