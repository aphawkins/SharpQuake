namespace SharpQuake.Framework.IO.Sprite
{
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SpriteFrameType
    {
        public Framework.SpriteFrameType type;
    }
}
