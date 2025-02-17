﻿namespace SharpQuake.Game.Data.Models
{
    using System;
    using System.Linq;
    using SharpQuake.Framework;
    using SharpQuake.Framework.World;
    using SharpQuake.Framework.IO.Alias;
    using SharpQuake.Framework.Rendering;
    using SharpQuake.Game.Rendering.Textures;
    using System.Numerics;

    public class AliasModelData : ModelData
    {
        public AliasHeader Header
        {
            get;
            private set;
        }

        private int PoseNum
        {
            get;
            set;
        }

        public TriVertex[][] PoseVerts { get; private set; } = new TriVertex[ModelDef.MAXALIASFRAMES][];

        public Vertex[] STVerts { get; private set; } = new Vertex[ModelDef.MAXALIASVERTS];

        public Triangle[] Triangles { get; private set; } = new Triangle[ModelDef.MAXALIASTRIS];

        public AliasModelData(ModelTexture noTexture) : base(noTexture)
        {

        }

        public void Load(uint[] table8to24, string name, byte[] buffer, Func<string, ByteArraySegment, AliasHeader, int> onLoadSkinTexture, Action<AliasModelData, AliasHeader> onMakeAliasModelDisplayList)
        {
            Name = name;
            Buffer = buffer;

            var pinmodel = Utilities.BytesToStructure<AliasModel>(Buffer, 0);

            var version = EndianHelper.LittleLong(pinmodel.version);

            if (version != ModelDef.ALIAS_VERSION)
            {
                Utilities.Error("{0} has wrong version number ({1} should be {2})",
                    Name, version, ModelDef.ALIAS_VERSION);
            }

            //
            // allocate space for a working header, plus all the data except the frames,
            // skin and group info
            //
            Header = new AliasHeader();

            Flags = (EntityFlags)EndianHelper.LittleLong(pinmodel.flags);

            //
            // endian-adjust and copy the data, starting with the alias model header
            //
            Header.boundingradius = EndianHelper.LittleFloat(pinmodel.boundingradius);
            Header.numskins = EndianHelper.LittleLong(pinmodel.numskins);
            Header.skinwidth = EndianHelper.LittleLong(pinmodel.skinwidth);
            Header.skinheight = EndianHelper.LittleLong(pinmodel.skinheight);

            if (Header.skinheight > ModelDef.MAX_LBM_HEIGHT)
            {
                Utilities.Error("model {0} has a skin taller than {1}", Name, ModelDef.MAX_LBM_HEIGHT);
            }

            Header.numverts = EndianHelper.LittleLong(pinmodel.numverts);

            if (Header.numverts <= 0)
            {
                Utilities.Error("model {0} has no vertices", Name);
            }

            if (Header.numverts > ModelDef.MAXALIASVERTS)
            {
                Utilities.Error("model {0} has too many vertices", Name);
            }

            Header.numtris = EndianHelper.LittleLong(pinmodel.numtris);

            if (Header.numtris <= 0)
            {
                Utilities.Error("model {0} has no triangles", Name);
            }

            Header.numframes = EndianHelper.LittleLong(pinmodel.numframes);
            var numframes = Header.numframes;
            if (numframes < 1)
            {
                Utilities.Error("Mod_LoadAliasModel: Invalid # of frames: {0}\n", numframes);
            }

            Header.size = EndianHelper.LittleFloat(pinmodel.size) * ModelDef.ALIAS_BASE_SIZE_RATIO;
            SyncType = (SyncType)EndianHelper.LittleLong((int)pinmodel.synctype);
            FrameCount = Header.numframes;

            Header.scale = EndianHelper.LittleVector(Utilities.ToVector(ref pinmodel.scale));
            Header.scale_origin = EndianHelper.LittleVector(Utilities.ToVector(ref pinmodel.scale_origin));
            Header.eyeposition = EndianHelper.LittleVector(Utilities.ToVector(ref pinmodel.eyeposition));

            //
            // load the skins
            //
            var offset = LoadAllSkins(table8to24, Header.numskins, new ByteArraySegment(buffer, AliasModel.SizeInBytes), onLoadSkinTexture);

            //
            // load base s and t vertices
            //
            var stvOffset = offset; // in bytes
            for (var i = 0; i < Header.numverts; i++, offset += Vertex.SizeInBytes)
            {
                STVerts[i] = Utilities.BytesToStructure<Vertex>(buffer, offset);

                STVerts[i].onseam = EndianHelper.LittleLong(STVerts[i].onseam);
                STVerts[i].s = EndianHelper.LittleLong(STVerts[i].s);
                STVerts[i].t = EndianHelper.LittleLong(STVerts[i].t);
            }

            //
            // load triangle lists
            //
            var triOffset = stvOffset + (Header.numverts * Vertex.SizeInBytes);
            offset = triOffset;
            for (var i = 0; i < Header.numtris; i++, offset += Triangle.SizeInBytes)
            {
                Triangles[i] = Utilities.BytesToStructure<Triangle>(buffer, offset);
                Triangles[i].facesfront = EndianHelper.LittleLong(Triangles[i].facesfront);

                for (var j = 0; j < 3; j++)
                {
                    Triangles[i].vertindex[j] = EndianHelper.LittleLong(Triangles[i].vertindex[j]);
                }
            }

            //
            // load the frames
            //
            PoseNum = 0;
            var framesOffset = triOffset + (Header.numtris * Triangle.SizeInBytes);

            Header.frames = new AliasFrameDesc[Header.numframes];

            for (var i = 0; i < numframes; i++)
            {
                var frametype = (AliasFrameType)BitConverter.ToInt32(buffer, framesOffset);
                framesOffset += 4;

                framesOffset = frametype == AliasFrameType.ALIAS_SINGLE
                    ? LoadAliasFrame(new ByteArraySegment(buffer, framesOffset), ref Header.frames[i])
                    : LoadAliasGroup(new ByteArraySegment(buffer, framesOffset), ref Header.frames[i]);
            }

            Header.numposes = PoseNum;

            Type = ModelType.mod_alias;

            // FIXME: do this right
            BoundsMin = -Vector3.One * 16.0f;
            BoundsMax = -BoundsMin;

            //
            // build the draw lists
            //
            onMakeAliasModelDisplayList(this, Header);
            //mesh.MakeAliasModelDisplayLists( mod, Header );

            //
            // move the complete, relocatable alias model to the cache
            //
            //cache = Host.Cache.Alloc( aliashdr_t.SizeInBytes * Header.frames.Length * maliasframedesc_t.SizeInBytes, null );

            //if ( cache == null )
            //    return;

            //cache.data = Header;
        }

        /// <summary>
        /// Mod_LoadAllSkins
        /// </summary>
        /// <returns>Offset of next data block in source byte array</returns>
        private int LoadAllSkins(uint[] table8to24, int numskins, ByteArraySegment data, Func<string, ByteArraySegment, AliasHeader, int> onLoadSkinTexture)
        {
            if (numskins is < 1 or > ModelDef.MAX_SKINS)
            {
                Utilities.Error("Mod_LoadAliasModel: Invalid # of skins: {0}\n", numskins);
            }

            var offset = data.StartIndex;
            var skinOffset = data.StartIndex + AiasSkinTypeStruct.SizeInBytes; //  skin = (byte*)(pskintype + 1);
            var s = Header.skinwidth * Header.skinheight;

            var pskintype = Utilities.BytesToStructure<AiasSkinTypeStruct>(data.Data, offset);

            for (var i = 0; i < numskins; i++)
            {
                if (pskintype.type == AliasSkinType.ALIAS_SKIN_SINGLE)
                {
                    FloodFillSkin(table8to24, new ByteArraySegment(data.Data, skinOffset), Header.skinwidth, Header.skinheight);

                    // save 8 bit texels for the player model to remap
                    var texels = new byte[s]; // Hunk_AllocName(s, loadname);
                    Header.texels[i] = texels;// -(byte*)pheader;
                    System.Buffer.BlockCopy(data.Data, offset + AiasSkinTypeStruct.SizeInBytes, texels, 0, s);

                    // set offset to pixel data after daliasskintype_t block...
                    offset += AiasSkinTypeStruct.SizeInBytes;

                    var name = Name + "_" + i.ToString();

                    var index = onLoadSkinTexture(name, new ByteArraySegment(data.Data, offset), Header);

                    Header.gl_texturenum[i, 0] =
                    Header.gl_texturenum[i, 1] =
                    Header.gl_texturenum[i, 2] =
                    Header.gl_texturenum[i, 3] = index;
                    // Host.DrawingContext.LoadTexture( name, Header.skinwidth,
                    //Header.skinheight, new ByteArraySegment( data.Data, offset ), true, false ); // (byte*)(pskintype + 1)

                    // set offset to next daliasskintype_t block...
                    offset += s;
                    pskintype = Utilities.BytesToStructure<AiasSkinTypeStruct>(data.Data, offset);
                }
                else
                {
                    // animating skin group.  yuck.
                    offset += AiasSkinTypeStruct.SizeInBytes;
                    var pinskingroup = Utilities.BytesToStructure<AliasSkinGroup>(data.Data, offset);
                    var groupskins = EndianHelper.LittleLong(pinskingroup.numskins);
                    offset += AliasSkinGroup.SizeInBytes;
                    Utilities.BytesToStructure<AliasSkinInterval>(data.Data, offset);

                    offset += AliasSkinInterval.SizeInBytes * groupskins;

                    pskintype = Utilities.BytesToStructure<AiasSkinTypeStruct>(data.Data, offset);
                    int j;
                    for (j = 0; j < groupskins; j++)
                    {
                        FloodFillSkin(table8to24, new ByteArraySegment(data.Data, skinOffset), Header.skinwidth, Header.skinheight);
                        if (j == 0)
                        {
                            var texels = new byte[s]; // Hunk_AllocName(s, loadname);
                            Header.texels[i] = texels;// -(byte*)pheader;
                            System.Buffer.BlockCopy(data.Data, offset, texels, 0, s);
                        }

                        var name = string.Format("{0}_{1}_{2}", Name, i, j);

                        var index = onLoadSkinTexture(name, new ByteArraySegment(data.Data, offset), Header);

                        Header.gl_texturenum[i, j & 3] = index;// //  (byte*)(pskintype)

                        offset += s;

                        pskintype = Utilities.BytesToStructure<AiasSkinTypeStruct>(data.Data, offset);
                    }
                    var k = j;
                    for (; j < 4; j++)
                    {
                        Header.gl_texturenum[i, j & 3] = Header.gl_texturenum[i, j - k];
                    }
                }
            }

            return offset;// (void*)pskintype;
        }

        /// <summary>
        /// Mod_LoadAliasFrame
        /// </summary>
        /// <returns>Offset of next data block in source byte array</returns>
        private int LoadAliasFrame(ByteArraySegment pin, ref AliasFrameDesc frame)
        {
            var pdaliasframe = Utilities.BytesToStructure<AliasFrame>(pin.Data, pin.StartIndex);

            frame.name = Utilities.GetString(pdaliasframe.name);
            frame.firstpose = PoseNum;
            frame.numposes = 1;
            frame.bboxmin.Init();
            frame.bboxmax.Init();

            for (var i = 0; i < 3; i++)
            {
                // these are byte values, so we don't have to worry about
                // endianness
                frame.bboxmin.v[i] = pdaliasframe.bboxmin.v[i];
                frame.bboxmax.v[i] = pdaliasframe.bboxmax.v[i];
            }

            var verts = new TriVertex[Header.numverts];
            var offset = pin.StartIndex + AliasFrame.SizeInBytes; //pinframe = (trivertx_t*)(pdaliasframe + 1);
            for (var i = 0; i < verts.Length; i++, offset += TriVertex.SizeInBytes)
            {
                verts[i] = Utilities.BytesToStructure<TriVertex>(pin.Data, offset);
            }
            PoseVerts[PoseNum] = verts;
            PoseNum++;

            return offset;
        }

        /// <summary>
        /// Mod_LoadAliasGroup
        /// </summary>
        /// <returns>Offset of next data block in source byte array</returns>
        private int LoadAliasGroup(ByteArraySegment pin, ref AliasFrameDesc frame)
        {
            var offset = pin.StartIndex;
            var pingroup = Utilities.BytesToStructure<AliasGroup>(pin.Data, offset);
            var numframes = EndianHelper.LittleLong(pingroup.numframes);

            frame.Init();
            frame.firstpose = PoseNum;
            frame.numposes = numframes;

            for (var i = 0; i < 3; i++)
            {
                // these are byte values, so we don't have to worry about endianness
                frame.bboxmin.v[i] = pingroup.bboxmin.v[i];
                frame.bboxmin.v[i] = pingroup.bboxmax.v[i];
            }

            offset += AliasGroup.SizeInBytes;
            var pin_intervals = Utilities.BytesToStructure<AliasInterval>(pin.Data, offset); // (daliasinterval_t*)(pingroup + 1);

            frame.interval = EndianHelper.LittleFloat(pin_intervals.interval);

            offset += numframes * AliasInterval.SizeInBytes;

            for (var i = 0; i < numframes; i++)
            {
                var tris = new TriVertex[Header.numverts];
                var offset1 = offset + AliasFrame.SizeInBytes;
                for (var j = 0; j < Header.numverts; j++, offset1 += TriVertex.SizeInBytes)
                {
                    tris[j] = Utilities.BytesToStructure<TriVertex>(pin.Data, offset1);
                }
                PoseVerts[PoseNum] = tris;
                PoseNum++;

                offset += AliasFrame.SizeInBytes + (Header.numverts * TriVertex.SizeInBytes);
            }

            return offset;
        }

        /// <summary>
        /// Mod_FloodFillSkin
        /// Fill background pixels so mipmapping doesn't have haloes - Ed
        /// </summary>
        private static void FloodFillSkin(uint[] table8To24, ByteArraySegment skin, int skinwidth, int skinheight)
        {
            var filler = new FloodFiller(skin, skinwidth, skinheight);
            filler.Perform(table8To24);
        }

        public override void Clear()
        {
            base.Clear();

            Header = null;
            PoseNum = 0;
            PoseVerts = null;
            STVerts = null;
            Triangles = null;
        }

        public override void CopyFrom(ModelData src)
        {
            base.CopyFrom(src);

            Type = ModelType.mod_alias;

            if (src is not AliasModelData)
            {
                return;
            }

            var aliasSrc = (AliasModelData)src;

            Header = aliasSrc.Header;
            PoseNum = aliasSrc.PoseNum;
            PoseVerts = aliasSrc.PoseVerts.ToArray();
            STVerts = aliasSrc.STVerts.ToArray();
            Triangles = aliasSrc.Triangles.ToArray();
        }
    }
}
