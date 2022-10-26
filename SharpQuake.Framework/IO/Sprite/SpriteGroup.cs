namespace SharpQuake.Framework.IO.Sprite
{
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SpriteGroup
    {
        public int numframes;

        public static int SizeInBytes = Marshal.SizeOf(typeof(SpriteGroup));
    }
}
