namespace SharpQuake.Framework.IO.Sprite
{
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SpriteInterval
    {
        public float interval;

        public static int SizeInBytes = Marshal.SizeOf(typeof(SpriteInterval));
    }
}
