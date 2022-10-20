using System;
using System.Runtime.InteropServices;

namespace SharpQuake.Framework.IO.Alias
{
	[StructLayout( LayoutKind.Sequential, Pack = 1 )]
    public struct stvert_t
    {
        public int onseam;
        public int s;
        public int t;

        public static int SizeInBytes = Marshal.SizeOf( typeof( stvert_t ) );
    } // stvert_t;

}
