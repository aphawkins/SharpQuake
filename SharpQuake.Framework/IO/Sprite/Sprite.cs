namespace SharpQuake.Framework.IO.Sprite
{
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Sprite
    {
        public int ident;
        public int version;
        public int type;
        public float boundingradius;
        public int width;
        public int height;
        public int numframes;
        public float beamlength;
        public SyncType synctype;

        public static int SizeInBytes = Marshal.SizeOf(typeof(Sprite));
    }
}
