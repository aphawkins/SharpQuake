namespace SharpQuake.Game.Data.Models
{
    using System;
    using SharpQuake.Framework;
    using SharpQuake.Framework.IO.Sprite;
    using SharpQuake.Framework.World;
    using SharpQuake.Game.Rendering.Textures;

    public class SpriteModelData : ModelData
    {
        public SpriteModelData(ModelTexture noTexture) : base(noTexture)
        {

        }

        public void Load(string name, byte[] buffer, Func<string, ByteArraySegment, int, int, int> onLoadSpriteTexture)
        {
            Name = name;
            Buffer = buffer;

            var pin = Utilities.BytesToStructure<Framework.IO.Sprite.Sprite>(buffer, 0);

            var version = EndianHelper.LittleLong(pin.version);

            if (version != ModelDef.SPRITE_VERSION)
            {
                Utilities.Error("{0} has wrong version number ({1} should be {2})",
                    Name, version, ModelDef.SPRITE_VERSION);
            }

            var numframes = EndianHelper.LittleLong(pin.numframes);

            var psprite = new Framework.Sprite();

            // Uze: sprite models are not cached so
            Cache = new CacheUser
            {
                data = psprite
            };

            psprite.type = (SpriteType)EndianHelper.LittleLong(pin.type);
            psprite.maxwidth = EndianHelper.LittleLong(pin.width);
            psprite.maxheight = EndianHelper.LittleLong(pin.height);
            psprite.beamlength = EndianHelper.LittleFloat(pin.beamlength);
            SyncType = (SyncType)EndianHelper.LittleLong((int)pin.synctype);
            psprite.numframes = numframes;

            var mins = BoundsMin;
            var maxs = BoundsMax;
            mins.X = mins.Y = -psprite.maxwidth / 2;
            maxs.X = maxs.Y = psprite.maxwidth / 2;
            mins.Z = -psprite.maxheight / 2;
            maxs.Z = psprite.maxheight / 2;

            //
            // load the frames
            //
            if (numframes < 1)
            {
                Utilities.Error("Mod_LoadSpriteModel: Invalid # of frames: {0}\n", numframes);
            }

            FrameCount = numframes;

            var frameOffset = Framework.IO.Sprite.Sprite.SizeInBytes;

            psprite.frames = new SpriteFrameDesc[numframes];

            for (var i = 0; i < numframes; i++)
            {
                var frametype = (Framework.SpriteFrameType)BitConverter.ToInt32(buffer, frameOffset);
                frameOffset += 4;

                psprite.frames[i].type = frametype;

                frameOffset = frametype == Framework.SpriteFrameType.SPR_SINGLE
                    ? LoadSpriteFrame(new ByteArraySegment(buffer, frameOffset), out psprite.frames[i].frameptr, i, onLoadSpriteTexture)
                    : LoadSpriteGroup(new ByteArraySegment(buffer, frameOffset), out psprite.frames[i].frameptr, i, onLoadSpriteTexture);
            }

            Type = ModelType.mod_sprite;
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns>Offset of next data block</returns>
        private int LoadSpriteFrame(ByteArraySegment pin, out object ppframe, int framenum, Func<string, ByteArraySegment, int, int, int> onLoadSpriteTexture)
        {
            var pinframe = Utilities.BytesToStructure<Framework.IO.Sprite.SpriteFrame>(pin.Data, pin.StartIndex);

            var width = EndianHelper.LittleLong(pinframe.width);
            var height = EndianHelper.LittleLong(pinframe.height);
            var size = width * height;

            var pspriteframe = new Framework.SpriteFrame();

            ppframe = pspriteframe;

            pspriteframe.width = width;
            pspriteframe.height = height;
            var orgx = EndianHelper.LittleLong(pinframe.origin[0]);
            var orgy = EndianHelper.LittleLong(pinframe.origin[1]);

            pspriteframe.up = orgy;// origin[1];
            pspriteframe.down = orgy - height;
            pspriteframe.left = orgx;// origin[0];
            pspriteframe.right = width + orgx;// origin[0];

            var name = Name + "_" + framenum.ToString();

            var index = onLoadSpriteTexture(name, new ByteArraySegment(pin.Data, pin.StartIndex + Framework.IO.Sprite.SpriteFrame.SizeInBytes), width, height);

            pspriteframe.gl_texturenum = index;

            return pin.StartIndex + Framework.IO.Sprite.SpriteFrame.SizeInBytes + size;
        }

        /// <summary>
        /// Mod_LoadSpriteGroup
        /// </summary>
        private int LoadSpriteGroup(ByteArraySegment pin, out object ppframe, int framenum, Func<string, ByteArraySegment, int, int, int> onLoadSpriteTexture)
        {
            var pingroup = Utilities.BytesToStructure<Framework.IO.Sprite.SpriteGroup>(pin.Data, pin.StartIndex);

            var numframes = EndianHelper.LittleLong(pingroup.numframes);
            var pspritegroup = new Framework.SpriteGroup
            {
                numframes = numframes,
                frames = new Framework.SpriteFrame[numframes]
            };
            ppframe = pspritegroup;
            var poutintervals = new float[numframes];
            pspritegroup.intervals = poutintervals;

            var offset = pin.StartIndex + Framework.IO.Sprite.SpriteGroup.SizeInBytes;
            for (var i = 0; i < numframes; i++, offset += SpriteInterval.SizeInBytes)
            {
                var interval = Utilities.BytesToStructure<SpriteInterval>(pin.Data, offset);
                poutintervals[i] = EndianHelper.LittleFloat(interval.interval);
                if (poutintervals[i] <= 0)
                {
                    Utilities.Error("Mod_LoadSpriteGroup: interval<=0");
                }
            }

            for (var i = 0; i < numframes; i++)
            {
                offset = LoadSpriteFrame(new ByteArraySegment(pin.Data, offset), out object tmp, (framenum * 100) + i, onLoadSpriteTexture);
                pspritegroup.frames[i] = (Framework.SpriteFrame)tmp;
            }

            return offset;
        }

        public override void Clear()
        {
            base.Clear();
        }

        public override void CopyFrom(ModelData src)
        {
            base.CopyFrom(src);

            Type = ModelType.mod_sprite;
        }
    }
}
