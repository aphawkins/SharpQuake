/// <copyright>
///
/// SharpQuakeEvolved changes by optimus-code, 2019
/// 
/// Based on SharpQuake (Quake Rewritten in C# by Yury Kiselev, 2010.)
///
/// Copyright (C) 1996-1997 Id Software, Inc.
///
/// This program is free software; you can redistribute it and/or
/// modify it under the terms of the GNU General Public License
/// as published by the Free Software Foundation; either version 2
/// of the License, or (at your option) any later version.
///
/// This program is distributed in the hope that it will be useful,
/// but WITHOUT ANY WARRANTY; without even the implied warranty of
/// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
///
/// See the GNU General Public License for more details.
///
/// You should have received a copy of the GNU General Public License
/// along with this program; if not, write to the Free Software
/// Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
/// </copyright>

namespace SharpQuake.Game.Data.Models
{
    using SharpQuake.Framework;
    using SharpQuake.Framework.World;
    using SharpQuake.Game.Rendering.Textures;
    using OpenTK;

    public class ModelData
    {
        public string Name
        {
            get;
            set;
        }

        // bmodels and sprites don't cache normally
        public bool IsLoadRequired
        {
            get;
            set;
        }

        public ModelType Type
        {
            get;
            set;
        }

        public int FrameCount
        {
            get;
            set;
        }

        public SyncType SyncType
        {
            get;
            set;
        }

        public EntityFlags Flags
        {
            get;
            set;
        }

        //
        // volume occupied by the model graphics
        //
        public Vector3 BoundsMin
        {
            get;
            set;
        }

        public Vector3 BoundsMax
        {
            get;
            set;
        }

        public float Radius
        {
            get;
            set;
        }

        //
        // solid volume for clipping 
        //
        public bool ClipBox
        {
            get;
            set;
        }

        public Vector3 ClipMin
        {
            get;
            set;
        }

        public Vector3 ClipMax
        {
            get;
            set;
        }

        //
        // additional model data
        //

        // only access through Mod_Extradata
        public CacheUser Cache
        {
            get;
            set;
        }

        protected ModelTexture NoTexture
        {
            get;
            set;
        }

        protected byte[] Buffer
        {
            get;
            set;
        }

        public ModelData(ModelTexture noTexture)
        {
            NoTexture = noTexture;
        }

        public virtual void Clear()
        {
            Name = null;
            IsLoadRequired = false;
            Type = 0;
            FrameCount = 0;
            SyncType = 0;
            Flags = 0;
            BoundsMin = Vector3.Zero;
            BoundsMax = Vector3.Zero;
            Radius = 0;
            ClipBox = false;
            ClipMin = Vector3.Zero;
            ClipMax = Vector3.Zero;
            Cache = null;
        }

        public virtual void CopyFrom(ModelData src)
        {
            Name = src.Name;
            IsLoadRequired = src.IsLoadRequired;
            Type = src.Type;
            FrameCount = src.FrameCount;
            SyncType = src.SyncType;
            Flags = src.Flags;
            BoundsMin = src.BoundsMin;
            BoundsMax = src.BoundsMax;
            Radius = src.Radius;
            ClipBox = src.ClipBox;
            ClipMin = src.ClipMin;
            ClipMax = src.ClipMax;

            Cache = src.Cache;
        }
    } //model_t;
}
