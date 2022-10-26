namespace SharpQuake.Framework.IO.Alias
{
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct AliasSkinGroup
    {
        public int numskins;

        public static int SizeInBytes = Marshal.SizeOf(typeof(AliasSkinGroup));
    }
}
