namespace SharpQuake.Framework.IO.Alias
{
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Vertex
    {
        public int onseam;
        public int s;
        public int t;

        public static int SizeInBytes = Marshal.SizeOf(typeof(Vertex));
    }
}
