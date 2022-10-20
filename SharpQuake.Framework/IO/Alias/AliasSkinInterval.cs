﻿using System;
using System.Runtime.InteropServices;

namespace SharpQuake.Framework.IO.Alias
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct daliasskininterval_t
    {
        public float interval;

        public static int SizeInBytes = Marshal.SizeOf(typeof(daliasskininterval_t));
    } // daliasskininterval_t;
}
