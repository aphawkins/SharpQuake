using System;
using System.Runtime.InteropServices;

namespace SharpQuake.Framework.IO.Alias
{
	[StructLayout( LayoutKind.Sequential, Pack = 1 )]
    public struct mdl_t
    {
        public int ident;
        public int version;
        public Vector3f scale;
        public Vector3f scale_origin;
        public float boundingradius;
        public Vector3f eyeposition;
        public int numskins;
        public int skinwidth;
        public int skinheight;
        public int numverts;
        public int numtris;
        public int numframes;
        public SyncType synctype;
        public int flags;
        public float size;

        public static readonly int SizeInBytes = Marshal.SizeOf( typeof( mdl_t ) );

        //static mdl_t()
        //{
        //    mdl_t.SizeInBytes = Marshal.SizeOf(typeof(mdl_t));
        //}
    } // mdl_t;
}
