namespace SharpQuake.Framework.IO.Alias
{
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct AliasSkinInterval
    {
        public float interval;

        public static int SizeInBytes = Marshal.SizeOf(typeof(AliasSkinInterval));
    }
}
