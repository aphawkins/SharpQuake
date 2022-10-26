namespace SharpQuake.Framework.IO.Alias
{
    using System.Runtime.InteropServices;

    public enum AliasSkinType
    {
        ALIAS_SKIN_SINGLE = 0,
        ALIAS_SKIN_GROUP
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct AiasSkinTypeStruct
    {
        public AliasSkinType type;

        public static int SizeInBytes = Marshal.SizeOf(typeof(AiasSkinTypeStruct));
    }
}
