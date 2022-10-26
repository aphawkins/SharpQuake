namespace SharpQuake.Framework.IO.Alias
{
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct AliasGroup
    {
        public int numframes;
        public TriVertex bboxmin;	// lightnormal isn't used
        public TriVertex bboxmax;	// lightnormal isn't used

        public static int SizeInBytes = Marshal.SizeOf(typeof(AliasGroup));
    } // daliasgroup_t;
}
