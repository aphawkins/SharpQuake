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

using System;
using System.Linq;
using OpenTK;
using SharpQuake.Framework;
using SharpQuake.Framework.IO;
using SharpQuake.Framework.World;
using SharpQuake.Game.Data.Models;
using SharpQuake.Game.Rendering.Memory;
using SharpQuake.Game.Rendering.Textures;
using SharpQuake.Game.World;
using SharpQuake.Renderer;
using SharpQuake.Renderer.Models;
using SharpQuake.Renderer.Textures;
using SharpQuake.Rendering;

// refresh.h -- public interface to refresh functions
// gl_rmisc.c
// gl_rmain.c

namespace SharpQuake
{
	/// <summary>
	/// R_functions
	/// </summary>
	public partial class render
    {
        public refdef_t RefDef { get; } = new refdef_t();

        public bool CacheTrash { get; private set; }

        public ModelTexture NoTextureMip { get; private set; }


        public const int MAXCLIPPLANES = 11;
        public const int TOP_RANGE = 16;			// soldier uniform colors
        public const int BOTTOM_RANGE = 96;

        //
        // view origin
        //
        public Vector3 ViewUp;

        // vup
        public Vector3 ViewPn;

        // vpn
        public Vector3 ViewRight;

        // vright
        public Vector3 Origin;

        private BaseTexture[] PlayerTextures;

        // r_origin

        private int[] _LightStyleValue = new int[256]; // d_lightstylevalue  // 8.8 fraction of base light value
        private Entity _WorldEntity = new Entity( ); // r_worldentity
        private Entity _CurrentEntity; // currententity

        private int _SkyTextureNum; // skytexturenum
                                      //private Int32 _MirrorTextureNum; // mirrortexturenum	// quake texturenum, not gltexturenum

        private int _FrameCount; // r_framecount		// used for dlight push checking
       
        public Occlusion Occlusion
        {
            get;
            private set;
        }

        private int _BrushPolys; // c_brush_polys
        private int _AliasPolys; // c_alias_polys
        //private System.Boolean _IsMirror; // mirror
        //private Plane _MirrorPlane; // mirror_plane

        // Temporarily turn into property until GL stripped out of this project
        private float _glDepthMin
        {
            get
            {
                return Host.Video.Device.Desc.DepthMinimum;
            }
            set
            {
                Host.Video.Device.Desc.DepthMinimum = value;
            }
        }

        private float _glDepthMax
        {
            get
            {
                return Host.Video.Device.Desc.DepthMaximum;
            }
            set
            {
                Host.Video.Device.Desc.DepthMaximum = value;
            }
        }

        private Plane[] _Frustum = new Plane[4]; // frustum
        private bool _IsEnvMap = false; // envmap	// true during envmap command capture
        private Vector3 _ModelOrg; // modelorg
        private Vector3 _EntOrigin; // r_entorigin
        private float _ShadeLight; // shadelight
        private float _AmbientLight; // ambientlight
        private float[] _ShadeDots = anorm_dots.Values[0]; // shadedots
        private Vector3 _ShadeVector; // shadevector
        private Vector3 _LightSpot; // lightspot

        // CHANGE
        private Host Host
        {
            get;
            set;
        }

		public ParticleSystem Particles
		{
			get;
			private set;
		}

		public WarpableTextures WarpableTextures
		{
			get;
			private set;
		}

		public render( Host host )
        {
            Host = host;
			Particles = new ParticleSystem( Host.Video.Device );
			WarpableTextures = new WarpableTextures( Host.Video.Device );
		}

        /// <summary>
        /// R_Init
        /// </summary>
        public void Initialise( )
        {            
            for ( var i = 0; i < _Frustum.Length; i++ )
                _Frustum[i] = new Plane( );

            Host.Commands.Add( "timerefresh", TimeRefresh_f );
            //Cmd.Add("envmap", Envmap_f);
            //Cmd.Add("pointfile", ReadPointFile_f);

            if ( Host.Cvars.NoRefresh == null )
            {
                Host.Cvars.NoRefresh = Host.CVars.Add( "r_norefresh", false );
                Host.Cvars.DrawEntities = Host.CVars.Add( "r_drawentities", true );
                Host.Cvars.DrawViewModel = Host.CVars.Add( "r_drawviewmodel", true );
                Host.Cvars.Speeds = Host.CVars.Add( "r_speeds", false );
                Host.Cvars.FullBright = Host.CVars.Add( "r_fullbright", false );
                Host.Cvars.LightMap = Host.CVars.Add( "r_lightmap", false );
                Host.Cvars.Shadows = Host.CVars.Add( "r_shadows", false );
                //_MirrorAlpha = Host.CVars.Add( "r_mirroralpha", "1" );
                Host.Cvars.WaterAlpha = Host.CVars.Add( "r_wateralpha", 1f );
                Host.Cvars.Dynamic = Host.CVars.Add( "r_dynamic", true );
                Host.Cvars.NoVis = Host.CVars.Add( "r_novis", false );

                Host.Cvars.glFinish = Host.CVars.Add( "gl_finish", false );
                Host.Cvars.glClear = Host.CVars.Add( "gl_clear", 0f );
                Host.Cvars.glCull = Host.CVars.Add( "gl_cull", true );
                Host.Cvars.glTexSort = Host.CVars.Add( "gl_texsort", true );
                Host.Cvars.glSmoothModels = Host.CVars.Add( "gl_smoothmodels", true );
                Host.Cvars.glAffineModels = Host.CVars.Add( "gl_affinemodels", false );
                Host.Cvars.glPolyBlend = Host.CVars.Add( "gl_polyblend", true );
                Host.Cvars.glFlashBlend = Host.CVars.Add( "gl_flashblend", true );
                Host.Cvars.glPlayerMip = Host.CVars.Add( "gl_playermip", 0 );
                Host.Cvars.glNoColors = Host.CVars.Add( "gl_nocolors", false );
                Host.Cvars.glKeepTJunctions = Host.CVars.Add( "gl_keeptjunctions", false );
                Host.Cvars.glReportTJunctions = Host.CVars.Add( "gl_reporttjunctions", false );
                Host.Cvars.glDoubleEyes = Host.CVars.Add( "gl_doubleeys", true );
            }

            if ( Host.Video.Device.Desc.SupportsMultiTexture )
                Host.CVars.Set( "gl_texsort", 0.0f );

			Particles.InitParticles( );
			Particles.InitParticleTexture( );

            // reserve 16 textures
            PlayerTextures = new BaseTexture[16];

            for ( var i = 0; i < PlayerTextures.Length; i++ )
            {
                PlayerTextures[i] = BaseTexture.FromDynamicBuffer( Host.Video.Device, "_PlayerTexture{i}", new ByteArraySegment( new byte[512 * 256 * 4] ), 512, 256, false, false );
            }

            TextureChains = new TextureChains();
            Occlusion = new Occlusion( Host, TextureChains );
        }

        // R_InitTextures
        public void InitTextures( )
        {
            // create a simple checkerboard texture for the default
            NoTextureMip = new ModelTexture( );
            NoTextureMip.pixels = new byte[(16 * 16) + (8 * 8) + (4 * 4) + (2 * 2)];
            NoTextureMip.width = NoTextureMip.height = 16;
            var offset = 0;
            NoTextureMip.offsets[0] = offset;
            offset += 16 * 16;
            NoTextureMip.offsets[1] = offset;
            offset += 8 * 8;
            NoTextureMip.offsets[2] = offset;
            offset += 4 * 4;
            NoTextureMip.offsets[3] = offset;

            var dest = NoTextureMip.pixels;
            for ( var m = 0; m < 4; m++ )
            {
                offset = NoTextureMip.offsets[m];
                for ( var y = 0; y < ( 16 >> m ); y++ )
                    for ( var x = 0; x < ( 16 >> m ); x++ )
                    {
                        if ( ( y < ( 8 >> m ) ) ^ ( x < ( 8 >> m ) ) )
                            dest[offset] = 0;
                        else
                            dest[offset] = 0xff;

                        offset++;
                    }
            }
        }

        /// <summary>
        /// R_RenderView
        /// r_refdef must be set before the first call
        /// </summary>
        public void RenderView( )
        {
            if ( Host.Cvars.NoRefresh.Get<bool>() )
                return;

            if ( _WorldEntity.model == null || Host.Client.cl.worldmodel == null )
                Utilities.Error( "R_RenderView: NULL worldmodel" );

            double time1 = 0;
            if ( Host.Cvars.Speeds.Get<bool>( ) )
            {
                Host.Video.Device.Finish( );
                time1 = Timer.GetFloatTime( );
                _BrushPolys = 0;
                _AliasPolys = 0;
            }

            //_IsMirror = false;

            if ( Host.Cvars.glFinish.Get<bool>() )
                Host.Video.Device.Finish( );

            Clear( );

            // render normal view

            RenderScene( );
            DrawViewModel( );
            DrawWaterSurfaces( );

            // render mirror view
            //Mirror();

            PolyBlend( );

            if ( Host.Cvars.Speeds.Get<bool>() )
            {
                var time2 = Timer.GetFloatTime( );
                ConsoleWrapper.Print( "{0,3} ms  {1,4} wpoly {2,4} epoly\n", (int) ( ( time2 - time1 ) * 1000 ), _BrushPolys, _AliasPolys );
            }
        }

        /// <summary>
        /// R_RemoveEfrags
        /// Call when removing an object from the world or moving it to another position
        /// </summary>
        public void RemoveEfrags( Entity ent )
        {
            var ef = ent.efrag;

            while ( ef != null )
            {
                var leaf = ef.leaf;
                while ( true )
                {
                    var walk = leaf.efrags;
                    if ( walk == null )
                        break;
                    if ( walk == ef )
                    {
                        // remove this fragment
                        leaf.efrags = ef.leafnext;
                        break;
                    }
                    else
                        leaf = ( MemoryLeaf ) (object) walk.leafnext;
                }

                var old = ef;
                ef = ef.entnext;

                // put it on the free list
                old.entnext = Host.Client.cl.free_efrags;
                Host.Client.cl.free_efrags = old;
            }

            ent.efrag = null;
        }

        /// <summary>
        /// R_TranslatePlayerSkin
        /// Translates a skin texture by the per-player color lookup
        /// </summary>
        public void TranslatePlayerSkin(int playernum )
        {
            Host.Video.Device.DisableMultitexture( );

            var top = Host.Client.cl.scores[playernum].colors & 0xf0;
            var bottom = ( Host.Client.cl.scores[playernum].colors & 15 ) << 4;

            var translate = new byte[256];
            for ( var i = 0; i < 256; i++ )
                translate[i] = (byte) i;

            for ( var i = 0; i < 16; i++ )
            {
                if ( top < 128 )	// the artists made some backwards ranges.  sigh.
                    translate[TOP_RANGE + i] = (byte) ( top + i );
                else
                    translate[TOP_RANGE + i] = (byte) ( top + 15 - i );

                if ( bottom < 128 )
                    translate[BOTTOM_RANGE + i] = (byte) ( bottom + i );
                else
                    translate[BOTTOM_RANGE + i] = (byte) ( bottom + 15 - i );
            }

            //
            // locate the original skin pixels
            //
            _CurrentEntity = Host.Client.Entities[1 + playernum];
            var model = _CurrentEntity.model;
            if ( model == null )
                return;		// player doesn't have a model yet
            if ( model.Type != ModelType.mod_alias )
                return; // only translate skins on alias models

            var paliashdr = Host.Model.GetExtraData( model );
            var s = paliashdr.skinwidth * paliashdr.skinheight;
            if ( ( s & 3 ) != 0 )
                Utilities.Error( "R_TranslateSkin: s&3" );

            byte[] original;
            if ( _CurrentEntity.skinnum < 0 || _CurrentEntity.skinnum >= paliashdr.numskins )
            {
                ConsoleWrapper.Print( "({0}): Invalid player skin #{1}\n", playernum, _CurrentEntity.skinnum );
                original = (byte[] ) paliashdr.texels[0];// (byte *)paliashdr + paliashdr.texels[0];
            }
            else
                original = (byte[] ) paliashdr.texels[_CurrentEntity.skinnum];

            var inwidth = paliashdr.skinwidth;
            var inheight = paliashdr.skinheight;

            // because this happens during gameplay, do it fast
            // instead of sending it through gl_upload 8
            var maxSize = Host.Cvars.glMaxSize.Get<int>();
            PlayerTextures[playernum].TranslateAndUpload( original, translate, inwidth, inheight, maxSize, maxSize, (int) Host.Cvars.glPlayerMip.Get<int>() );
        }

        /// <summary>
        /// R_NewMap
        /// </summary>
        public void NewMap( )
        {
            for ( var i = 0; i < 256; i++ )
                _LightStyleValue[i] = 264;		// normal light value

            _WorldEntity.Clear( );
            _WorldEntity.model = Host.Client.cl.worldmodel;

            // clear out efrags in case the level hasn't been reloaded
            // FIXME: is this one short?
            for ( var i = 0; i < Host.Client.cl.worldmodel.NumLeafs; i++ )
                Host.Client.cl.worldmodel.Leaves[i].efrags = null;

            Occlusion.ViewLeaf = null;
			Particles.ClearParticles( );

            BuildLightMaps( );

            // identify sky texture
            _SkyTextureNum = -1;
            //_MirrorTextureNum = -1;
            var world = Host.Client.cl.worldmodel;
            for ( var i = 0; i < world.NumTextures; i++ )
            {
                if ( world.Textures[i] == null )
                    continue;
                if ( world.Textures[i].name != null )
                {
                    if ( world.Textures[i].name.StartsWith( "sky" ) )
                        _SkyTextureNum = i;
                    //if( world.textures[i].name.StartsWith( "window02_1" ) )
                    //    _MirrorTextureNum = i;
                }
                world.Textures[i].texturechain = null;
            }
        }

        /// <summary>
        /// R_PolyBlend
        /// </summary>
        private void PolyBlend( )
        {
            if ( !Host.Cvars.glPolyBlend.Get<bool>() )
                return;

            if ( Host.View.Blend.A == 0 )
                return;

            Host.Video.Device.Graphics.PolyBlend( Host.View.Blend );
        }

        /// <summary>
        /// R_Mirror
        /// </summary>
        //private void Mirror()
        //{
        //    if( !_IsMirror )
        //        return;

        //    _BaseWorldMatrix = _WorldMatrix;

        //    var d = Vector3.Dot( _RefDef.vieworg, _MirrorPlane.normal ) - _MirrorPlane.dist;
        //    _RefDef.vieworg += _MirrorPlane.normal * -2 * d;

        //    d = Vector3.Dot( ViewPn, _MirrorPlane.normal );
        //    ViewPn += _MirrorPlane.normal * -2 * d;

        //    _RefDef.viewangles = new Vector3( ( Single ) ( Math.Asin( ViewPn.Z ) / Math.PI * 180.0 ),
        //        ( Single ) ( Math.Atan2( ViewPn.Y, ViewPn.X ) / Math.PI * 180.0 ),
        //        -_RefDef.viewangles.Z );

        //    var ent = Host.Client.ViewEntity;
        //    if( Host.Client.NumVisEdicts < ClientDef.MAX_VISEDICTS )
        //    {
        //        Host.Client.VisEdicts[Host.Client.NumVisEdicts] = ent;
        //        Host.Client.NumVisEdicts++;
        //    }

        //    _glDepthMin = 0.5f;
        //    _glDepthMax = 1;
        //    GL.DepthRange( _glDepthMin, _glDepthMax );
        //    GL.DepthFunc( DepthFunction.Lequal );

        //    RenderScene();
        //    DrawWaterSurfaces();

        //    _glDepthMin = 0;
        //    _glDepthMax = 0.5f;
        //    GL.DepthRange( _glDepthMin, _glDepthMax );
        //    GL.DepthFunc( DepthFunction.Lequal );

        //    // blend on top
        //    GL.Enable( EnableCap.Blend );
        //    GL.MatrixMode( MatrixMode.Projection );
        //    if( _MirrorPlane.normal.Z != 0 )
        //        GL.Scale( 1f, -1, 1 );
        //    else
        //        GL.Scale( -1f, 1, 1 );
        //    GL.CullFace( CullFaceMode.Front );
        //    GL.MatrixMode( MatrixMode.Modelview );

        //    GL.LoadMatrix( ref _BaseWorldMatrix );

        //    GL.Color4( 1, 1, 1, _MirrorAlpha.Value );
        //    var s = Host.Client.cl.worldmodel.textures[_MirrorTextureNum].texturechain;
        //    for( ; s != null; s = s.texturechain )
        //        RenderBrushPoly( s );
        //    Host.Client.cl.worldmodel.textures[_MirrorTextureNum].texturechain = null;
        //    GL.Disable( EnableCap.Blend );
        //    GL.Color4( 1f, 1, 1, 1 );
        //}

        /// <summary>
        /// R_DrawViewModel
        /// </summary>
        private void DrawViewModel( )
        {
            if ( !Host.Cvars.DrawViewModel.Get<bool>() )
                return;

            if ( Host.ChaseView.IsActive )
                return;

            if ( _IsEnvMap )
                return;

            if ( !Host.Cvars.DrawEntities.Get<bool>( ) )
                return;

            if ( Host.Client.cl.HasItems( QItemsDef.IT_INVISIBILITY ) )
                return;

            if ( Host.Client.cl.stats[QStatsDef.STAT_HEALTH] <= 0 )
                return;

            _CurrentEntity = Host.Client.ViewEnt;
            if ( _CurrentEntity.model == null )
                return;

            var j = LightPoint( ref _CurrentEntity.origin );

            if ( j < 24 )
                j = 24;		// allways give some light on gun
            _AmbientLight = j;
            _ShadeLight = j;

            // add dynamic lights
            for ( var lnum = 0; lnum < ClientDef.MAX_DLIGHTS; lnum++ )
            {
                var dl = Host.Client.DLights[lnum];
                if ( dl.radius == 0 )
                    continue;
                if ( dl.die < Host.Client.cl.time )
                    continue;

                var dist = _CurrentEntity.origin - dl.origin;
                var add = dl.radius - dist.Length;
                if ( add > 0 )
                    _AmbientLight += add;
            }

            // hack the depth range to prevent view model from poking into walls
            Host.Video.Device.SetDepth( _glDepthMin, _glDepthMin + (0.3f * ( _glDepthMax - _glDepthMin )) );
			DrawAliasModel( _CurrentEntity );
            Host.Video.Device.SetDepth( _glDepthMin, _glDepthMax );
        }

        /// <summary>
        /// R_RenderScene
        /// r_refdef must be set before the first call
        /// </summary>
        private void RenderScene( )
        {
            SetupFrame( );

            SetFrustum( );

            SetupGL( );

            Occlusion.MarkLeaves( );	// done here so we know if we're in water

            DrawWorld( );		// adds entities to the list

            Host.Sound.ExtraUpdate( );	// don't let sound get messed up if going slow

            DrawEntitiesOnList( );

            Host.Video.Device.DisableMultitexture( );

            RenderDlights( );

			Particles.DrawParticles( Host.Client.cl.time, Host.Client.cl.oldtime, Host.Server.Gravity, Origin, ViewUp, ViewRight, ViewPn );

#if GLTEST
	        Test_Draw ();
#endif
        }

        /// <summary>
        /// R_DrawEntitiesOnList
        /// </summary>
        private void DrawEntitiesOnList( )
        {
            if ( !Host.Cvars.DrawEntities.Get<bool>( ) )
                return;

            for ( var i = 0; i < Host.Client.NumVisEdicts; i++ )
            {
                _CurrentEntity = Host.Client.VisEdicts[i];

                switch ( _CurrentEntity.model.Type )
                {
                    case ModelType.mod_alias:
						_CurrentEntity.useInterpolation = Host.Cvars.AnimationBlend.Get<bool>( );
						DrawAliasModel( _CurrentEntity );
                        break;

                    case ModelType.mod_brush:
                        DrawBrushModel( _CurrentEntity );
                        break;

                    default:
                        break;
                }
            }

            // draw sprites seperately, because of alpha blending

            for ( var i = 0; i < Host.Client.NumVisEdicts; i++ )
            {
                _CurrentEntity = Host.Client.VisEdicts[i];

                switch ( _CurrentEntity.model.Type )
                {
                    case ModelType.mod_sprite:
                        DrawSpriteModel( _CurrentEntity );
                        break;
                }
            }
        }

        /// <summary>
        /// R_DrawSpriteModel
        /// </summary>
        private void DrawSpriteModel( Entity e )
        {
            // don't even bother culling, because it's just a single
            // polygon without a surface cache
            var frame = GetSpriteFrame( e );
            var psprite = ( msprite_t ) e.model.cache.data; // Uze: changed from _CurrentEntity to e

            Vector3 v_forward, right, up;
            if ( psprite.type == SpriteType.Oriented )
            {
                // bullet marks on walls
                MathLib.AngleVectors( ref e.angles, out v_forward, out right, out up ); // Uze: changed from _CurrentEntity to e
            }
            else
            {	// normal sprite
                up = ViewUp;// vup;
                right = ViewRight;// vright;
            }

            var texture = Host.Model.SpriteTextures.Where( t => ( ( Renderer.OpenGL.Textures.GLTextureDesc ) t.Desc ).TextureNumber == frame.gl_texturenum ).FirstOrDefault();

            Host.Video.Device.Graphics.DrawSpriteModel( texture, frame, up, right, e.origin );
        }

        /// <summary>
        /// R_GetSpriteFrame
        /// </summary>
        private mspriteframe_t GetSpriteFrame( Entity currententity )
        {
            var psprite = ( msprite_t ) currententity.model.cache.data;
            var frame = currententity.frame;

            if ( ( frame >= psprite.numframes ) || ( frame < 0 ) )
            {
                Host.Console.Print( "R_DrawSprite: no such frame {0}\n", frame );
                frame = 0;
            }

            mspriteframe_t pspriteframe;
            if ( psprite.frames[frame].type == spriteframetype_t.SPR_SINGLE )
            {
                pspriteframe = ( mspriteframe_t ) psprite.frames[frame].frameptr;
            }
            else
            {
                var pspritegroup = ( mspritegroup_t ) psprite.frames[frame].frameptr;
                var pintervals = pspritegroup.intervals;
                var numframes = pspritegroup.numframes;
                var fullinterval = pintervals[numframes - 1];
                var time = (float) Host.Client.cl.time + currententity.syncbase;

                // when loading in Mod_LoadSpriteGroup, we guaranteed all interval values
                // are positive, so we don't have to worry about division by 0
                var targettime = time - (( (int) ( time / fullinterval ) ) * fullinterval);
                int i;
                for ( i = 0; i < ( numframes - 1 ); i++ )
                {
                    if ( pintervals[i] > targettime )
                        break;
                }
                pspriteframe = pspritegroup.frames[i];
            }

            return pspriteframe;
        }

        /// <summary>
        /// R_DrawAliasModel
        /// </summary>
        private void DrawAliasModel( Entity e )
        {
            var clmodel = _CurrentEntity.model;
            var mins = _CurrentEntity.origin + clmodel.BoundsMin;
            var maxs = _CurrentEntity.origin + clmodel.BoundsMax;

            if ( Utilities.CullBox( ref mins, ref maxs, ref _Frustum ) )
                return;

            _EntOrigin = _CurrentEntity.origin;
            _ModelOrg = Origin - _EntOrigin;

            //
            // get lighting information
            //

            _AmbientLight = _ShadeLight = LightPoint( ref _CurrentEntity.origin );

            // allways give the gun some light
            if ( e == Host.Client.cl.viewent && _AmbientLight < 24 )
                _AmbientLight = _ShadeLight = 24;

            for ( var lnum = 0; lnum < ClientDef.MAX_DLIGHTS; lnum++ )
            {
                if ( Host.Client.DLights[lnum].die >= Host.Client.cl.time )
                {
                    var dist = _CurrentEntity.origin - Host.Client.DLights[lnum].origin;
                    var add = Host.Client.DLights[lnum].radius - dist.Length;
                    if ( add > 0 )
                    {
                        _AmbientLight += add;
                        //ZOID models should be affected by dlights as well
                        _ShadeLight += add;
                    }
                }
            }

            // clamp lighting so it doesn't overbright as much
            if ( _AmbientLight > 128 )
                _AmbientLight = 128;
            if ( _AmbientLight + _ShadeLight > 192 )
                _ShadeLight = 192 - _AmbientLight;

            // ZOID: never allow players to go totally black
            var playernum = Array.IndexOf( Host.Client.Entities, _CurrentEntity, 0, Host.Client.cl.maxclients );
            if ( playernum >= 1 )// && i <= cl.maxclients)
                if ( _AmbientLight < 8 )
                    _AmbientLight = _ShadeLight = 8;

            // HACK HACK HACK -- no fullbright colors, so make torches full light
            if ( clmodel.Name == "progs/flame2.mdl" || clmodel.Name == "progs/flame.mdl" )
                _AmbientLight = _ShadeLight = 256;

            _ShadeDots = anorm_dots.Values[( (int) ( e.angles.Y * ( anorm_dots.SHADEDOT_QUANT / 360.0 ) ) ) & ( anorm_dots.SHADEDOT_QUANT - 1 )];
            _ShadeLight = _ShadeLight / 200.0f;

            var an = e.angles.Y / 180.0 * Math.PI;
            _ShadeVector.X = (float) Math.Cos( -an );
            _ShadeVector.Y = (float) Math.Sin( -an );
            _ShadeVector.Z = 1;
            MathLib.Normalize( ref _ShadeVector );

            //
            // locate the proper data
            //
            var paliashdr = Host.Model.GetExtraData( _CurrentEntity.model );

            _AliasPolys += paliashdr.numtris;

            BaseAliasModel model = null;

            if ( !BaseModel.ModelPool.ContainsKey( clmodel.Name ) )
            {
                var anim = (int) ( Host.Client.cl.time * 10 ) & 3;

                var tex = Host.Model.SkinTextures.Where( t =>  ( ( Renderer.OpenGL.Textures.GLTextureDesc ) t.Desc ).TextureNumber == paliashdr.gl_texturenum[_CurrentEntity.skinnum, anim] ).FirstOrDefault();

                model = BaseAliasModel.Create( Host.Video.Device, clmodel.Name, tex );
            }
            else
                model = ( BaseAliasModel ) BaseModel.ModelPool[clmodel.Name];

            model.AliasDesc.ScaleOrigin = paliashdr.scale_origin;
            model.AliasDesc.Scale = paliashdr.scale;
            model.AliasDesc.MinimumBounds = clmodel.BoundsMin;
            model.AliasDesc.MaximumBounds = clmodel.BoundsMax;
            model.AliasDesc.Origin = e.origin;
            model.AliasDesc.EulerAngles = e.angles;
            model.AliasDesc.AliasFrame = _CurrentEntity.frame;

			model.DrawAliasModel( _ShadeLight, _ShadeVector, _ShadeDots, _LightSpot.Z, paliashdr,
				Host.RealTime, Host.Client.cl.time,
				ref e.pose1, ref e.pose2, ref e.frame_start_time, ref e.frame_interval,
				ref e.origin1, ref e.origin2, ref e.translate_start_time, ref e.angles1,
				ref e.angles2, ref e.rotate_start_time,
				 Host.Cvars.Shadows.Get<bool>( ) ,  Host.Cvars.glSmoothModels.Get<bool>( ) ,  Host.Cvars.glAffineModels.Get<bool>( ) ,
				!Host.Cvars.glNoColors.Get<bool>( ),  clmodel.Name == "progs/eyes.mdl" && Host.Cvars.glDoubleEyes.Get<bool>( ) , e.useInterpolation );
		}

		/// <summary>
		/// R_SetupGL
		/// </summary>
		private void SetupGL( )
        {
            Host.Video.Device.Setup3DScene( Host.Cvars.glCull.Get<bool>(), RefDef, _IsEnvMap );

            ////
            //// set up viewpoint
            ////
            //GL.MatrixMode( MatrixMode.Projection );
            //GL.LoadIdentity();
            //var x = _RefDef.vrect.x * Host.Screen.glWidth / Host.Screen.vid.width;
            //var x2 = ( _RefDef.vrect.x + _RefDef.vrect.width ) * Host.Screen.glWidth / Host.Screen.vid.width;
            //var y = ( Host.Screen.vid.height - _RefDef.vrect.y ) * Host.Screen.glHeight / Host.Screen.vid.height;
            //var y2 = ( Host.Screen.vid.height - ( _RefDef.vrect.y + _RefDef.vrect.height ) ) * Host.Screen.glHeight / Host.Screen.vid.height;

            //// fudge around because of frac screen scale
            //if( x > 0 )
            //    x--;
            //if( x2 < Host.Screen.glWidth )
            //    x2++;
            //if( y2 < 0 )
            //    y2--;
            //if( y < Host.Screen.glHeight )
            //    y++;

            //var w = x2 - x;
            //var h = y - y2;

            //if( _IsEnvMap )
            //{
            //    x = y2 = 0;
            //    w = h = 256;
            //}

            //GL.Viewport( Host.Screen.glX + x, Host.Screen.glY + y2, w, h );
            //var screenaspect = ( Single ) _RefDef.vrect.width / _RefDef.vrect.height;
            //MYgluPerspective( _RefDef.fov_y, screenaspect, 4, 4096 );

            //if( _IsMirror )
            //{
            //    if( _MirrorPlane.normal.Z != 0 )
            //        GL.Scale( 1f, -1f, 1f );
            //    else
            //        GL.Scale( -1f, 1f, 1f );
            //    GL.CullFace( CullFaceMode.Back );
            //}
            //else
            //    GL.CullFace( CullFaceMode.Front );

            //GL.MatrixMode( MatrixMode.Modelview );
            //GL.LoadIdentity();

            //GL.Rotate( -90f, 1, 0, 0 );	    // put Z going up
            //GL.Rotate( 90f, 0, 0, 1 );	    // put Z going up
            //GL.Rotate( -_RefDef.viewangles.Z, 1, 0, 0 );
            //GL.Rotate( -_RefDef.viewangles.X, 0, 1, 0 );
            //GL.Rotate( -_RefDef.viewangles.Y, 0, 0, 1 );
            //GL.Translate( -_RefDef.vieworg.X, -_RefDef.vieworg.Y, -_RefDef.vieworg.Z );

            //GL.GetFloat( GetPName.ModelviewMatrix, out _WorldMatrix );

            ////
            //// set drawing parms
            ////
            //if( _glCull.Get<Boolean>() )
            //    GL.Enable( EnableCap.CullFace );
            //else
            //    GL.Disable( EnableCap.CullFace );

            //GL.Disable( EnableCap.Blend );
            //GL.Disable( EnableCap.AlphaTest );
            //GL.Enable( EnableCap.DepthTest );
        }

        /// <summary>
        /// R_SetFrustum
        /// </summary>
        private void SetFrustum( )
        {
            if ( RefDef.fov_x == 90 )
            {
                // front side is visible
                _Frustum[0].normal = ViewPn + ViewRight;
                _Frustum[1].normal = ViewPn - ViewRight;

                _Frustum[2].normal = ViewPn + ViewUp;
                _Frustum[3].normal = ViewPn - ViewUp;
            }
            else
            {
                // rotate VPN right by FOV_X/2 degrees
                MathLib.RotatePointAroundVector( out _Frustum[0].normal, ref ViewUp, ref ViewPn, -( 90 - (RefDef.fov_x / 2) ) );
                // rotate VPN left by FOV_X/2 degrees
                MathLib.RotatePointAroundVector( out _Frustum[1].normal, ref ViewUp, ref ViewPn, 90 - (RefDef.fov_x / 2) );
                // rotate VPN up by FOV_X/2 degrees
                MathLib.RotatePointAroundVector( out _Frustum[2].normal, ref ViewRight, ref ViewPn, 90 - (RefDef.fov_y / 2) );
                // rotate VPN down by FOV_X/2 degrees
                MathLib.RotatePointAroundVector( out _Frustum[3].normal, ref ViewRight, ref ViewPn, -( 90 - (RefDef.fov_y / 2) ) );
            }

            for ( var i = 0; i < 4; i++ )
            {
                _Frustum[i].type = PlaneDef.PLANE_ANYZ;
                _Frustum[i].dist = Vector3.Dot( Origin, _Frustum[i].normal );
                _Frustum[i].signbits = (byte) SignbitsForPlane( _Frustum[i] );
            }
        }

        private int SignbitsForPlane( Plane p )
        {
            // for fast box on planeside test
            var bits = 0;
            if ( p.normal.X < 0 )
                bits |= 1 << 0;
            if ( p.normal.Y < 0 )
                bits |= 1 << 1;
            if ( p.normal.Z < 0 )
                bits |= 1 << 2;
            return bits;
        }

        /// <summary>
        /// R_SetupFrame
        /// </summary>
        private void SetupFrame( )
        {
            // don't allow cheats in multiplayer
            if ( Host.Client.cl.maxclients > 1 )
                Host.CVars.Set( "r_fullbright", false );

            AnimateLight( );

            _FrameCount++;

            // build the transformation matrix for the given view angles
            Origin = RefDef.vieworg;

            MathLib.AngleVectors( ref RefDef.viewangles, out ViewPn, out ViewRight, out ViewUp );

            // current viewleaf
            Occlusion.SetupFrame( ref Origin );
            Host.View.SetContentsColor( Occlusion.ViewLeaf.contents );
            Host.View.CalcBlend( );

            CacheTrash = false;
            _BrushPolys = 0;
            _AliasPolys = 0;
        }

        /// <summary>
        /// R_Clear
        /// </summary>
        private void Clear( )
        {
            Host.Video.Device.Clear( Host.Video.glZTrick, Host.Cvars.glClear.Get<float>( ) );
        }

        /// <summary>
        /// R_TimeRefresh_f
        /// For program optimization
        /// </summary>
        private void TimeRefresh_f( CommandMessage msg )
        {
            //GL.DrawBuffer(DrawBufferMode.Front);
            Host.Video.Device.Finish( );

            var start = Timer.GetFloatTime( );
            for ( var i = 0; i < 128; i++ )
            {
                RefDef.viewangles.Y = (float) ( i / 128.0 * 360.0 );
                RenderView( );
                MainWindow.Instance.Present( );
            }

            Host.Video.Device.Finish( );
            var stop = Timer.GetFloatTime( );
            var time = stop - start;
            Host.Console.Print( "{0:F} seconds ({1:F1} fps)\n", time, 128 / time );

            //GL.DrawBuffer(DrawBufferMode.Back);
            Host.Screen.EndRendering( );
        }
    }
}
