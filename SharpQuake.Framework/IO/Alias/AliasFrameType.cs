namespace SharpQuake.Framework.IO.Alias
{
    using System.Runtime.InteropServices;

    public enum AliasFrameType
    {
        ALIAS_SINGLE = 0,
        ALIAS_GROUP
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct AliasFrameTypeStruct
    {
        public AliasFrameType type;
    }
}
