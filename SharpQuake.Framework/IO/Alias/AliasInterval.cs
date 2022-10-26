namespace SharpQuake.Framework.IO.Alias
{
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct AliasInterval
    {
        public float interval;

        public static int SizeInBytes = Marshal.SizeOf(typeof(AliasInterval));
    } // daliasinterval_t;
}
