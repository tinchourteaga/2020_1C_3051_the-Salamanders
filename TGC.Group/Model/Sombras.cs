﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using System.Drawing;
using System.Windows.Forms;
using TGC.Core.Direct3D;
using TGC.Core.Geometry;
using TGC.Core.Mathematica;
using TGC.Core.SceneLoader;
using TGC.Core.Shaders;
using TGC.Core.Textures;
using TGC.Core.Collision;
using TGC.Core.BoundingVolumes;

namespace TGC.Group.Model
{
    public class Sombras : ITecnicaRender
    {
        private readonly float far_plane = 3000f;
        private readonly float near_plane = 2f;

        // Shadow map
        private readonly int SHADOWMAP_SIZE = 512;
        private TgcArrow arrow;
        private Effect effect;
        private TGCVector3 g_LightDir; // direccion de la luz actual
        private TGCVector3 g_LightPos; // posicion de la luz actual (la que estoy analizando)
        private TGCMatrix g_LightView; // matriz de view del light
        private TGCMatrix g_mShadowProj; // Projection matrix for shadow map
        private Surface g_pDSShadow; // Depth-stencil buffer for rendering to shadow map
        private Texture g_pShadowMap; // Texture to which the shadow map is rendered
        private GameModel gameModel;
        private string MyShaderDir;

        public Sombras(GameModel gameModel)
        {
            MyShaderDir = gameModel.ShadersDir;
            this.gameModel = gameModel;
        }

        public void InstanciarSombras()
        {
            //Cargar Shader personalizado
            effect = TGCShaders.Instance.LoadEffect(MyShaderDir + "Sombras.fx");

            // le asigno el efecto a las mallas
            
            // Creo el shadowmap.
            // Format.R32F
            // Format.X8R8G8B8
            g_pShadowMap = new Texture(D3DDevice.Instance.Device, SHADOWMAP_SIZE, SHADOWMAP_SIZE, 1, Usage.RenderTarget, Format.R32F, Pool.Default);
            
            arrow = new TgcArrow();
            arrow.Thickness = 1f;
            arrow.HeadSize = new TGCVector2(2f, 2f);
            arrow.BodyColor = Color.Blue;
            // tengo que crear un stencilbuffer para el shadowmap manualmente
            // para asegurarme que tenga la el mismo tamano que el shadowmap, y que no tenga
            // multisample, etc etc.
            g_pDSShadow = D3DDevice.Instance.Device.CreateDepthStencilSurface(SHADOWMAP_SIZE, SHADOWMAP_SIZE, DepthFormat.D24S8, MultiSampleType.None, 0, true);
            // por ultimo necesito una matriz de proyeccion para el shadowmap, ya
            // que voy a dibujar desde el pto de vista de la luz.
            // El angulo tiene que ser mayor a 45 para que la sombra no falle en los extremos del cono de luz
            // de hecho, un valor mayor a 90 todavia es mejor, porque hasta con 90 grados es muy dificil
            // lograr que los objetos del borde generen sombras
            var aspectRatio = D3DDevice.Instance.AspectRatio;
            g_mShadowProj = TGCMatrix.PerspectiveFovLH(Geometry.DegreeToRadian(80), aspectRatio, 550, 3000);
            D3DDevice.Instance.Device.Transform.Projection = TGCMatrix.PerspectiveFovLH(Geometry.DegreeToRadian(45.0f), aspectRatio, near_plane, far_plane).ToMatrix();

            //lightLookFromModifier = camara.getPosition();
            //lightLookAtModifier = camara.getLookAt();
        }

        public void renderSombras(TGCVector3 eye, TGCVector3 target, TGCVector3 delta)
        {
            TexturesManager.Instance.clearAll();

            D3DDevice.Instance.Device.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.Black, 1.0f, 0);
            //D3DDevice.Instance.Device.BeginScene();

            g_LightPos = eye + delta;
            g_LightDir = target - g_LightPos - delta;
            //g_LightPos = personaje.getPosition() + new TGCVector3(0, -50, 0);
            //g_LightDir = personaje.getLookAt() - g_LightPos;
            g_LightDir.Normalize();

            arrow.PStart = g_LightPos ;
            arrow.PEnd = g_LightPos + g_LightDir * 0.2f;
            
            //arrow.updateValues();
            // Shadow maps:
            //D3DDevice.Instance.Device.EndScene(); // termino el thread anterior
            D3DDevice.Instance.Device.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.Gray, 1.0f, 0);

            //Genero el shadow map
            RenderShadowMap();

            //D3DDevice.Instance.Device.BeginScene();
            // dibujo la escena pp dicha
            D3DDevice.Instance.Device.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.Gray, 1.0f, 0);

            RenderScene2(false);

            //arrow.Render();
            //D3DDevice.Instance.Device.EndScene();
            //D3DDevice.Instance.Device.Present();
        }

        public void renderSombrasPersonaje(Personaje personaje)
        {
            TexturesManager.Instance.clearAll();

            D3DDevice.Instance.Device.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.Black, 1.0f, 0);

            g_LightPos = personaje.getPosition() + new TGCVector3(100, 10, -150);
            g_LightDir = personaje.getLookAt() - g_LightPos - new TGCVector3(-100, -10, 150);
            g_LightDir.Normalize();

            arrow.PStart = g_LightPos;
            arrow.PEnd = g_LightPos + g_LightDir * 0.2f;

            D3DDevice.Instance.Device.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.Gray, 1.0f, 0);

            RenderShadowMap();

            D3DDevice.Instance.Device.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.Gray, 1.0f, 0);

            RenderScene2(false);
        }
        public void RenderShadowMap()
        {
            // Calculo la matriz de view de la luz
            effect.SetValue("g_vLightPos", new TGCVector4(g_LightPos.X, g_LightPos.Y, g_LightPos.Z, 1));
            effect.SetValue("g_vLightDir", new TGCVector4(g_LightDir.X, g_LightDir.Y, g_LightDir.Z, 1));
            g_LightView = TGCMatrix.LookAtLH(g_LightPos, g_LightPos + g_LightDir,new TGCVector3(0, 0, 1));

            arrow.PStart = g_LightPos;
            arrow.PEnd = g_LightPos + g_LightDir * 20f;
            arrow.updateValues();

            // inicializacion standard:
            effect.SetValue("g_mProjLight", g_mShadowProj.ToMatrix());
            effect.SetValue("g_mViewLightProj", (g_LightView * g_mShadowProj).ToMatrix());

            //frustumShadow.updateVolume(TGCMatrix.FromMatrix(D3DDevice.Instance.Device.Transform.View), 
            //  TGCMatrix.FromMatrix(D3DDevice.Instance.Device.Transform.Projection));

            // Primero genero el shadow map, para ello dibujo desde el pto de vista de luz
            // a una textura, con el VS y PS que generan un mapa de profundidades.
            var pOldRT = D3DDevice.Instance.Device.GetRenderTarget(0);
            var pShadowSurf = g_pShadowMap.GetSurfaceLevel(0);
            D3DDevice.Instance.Device.SetRenderTarget(0, pShadowSurf);
            var pOldDS = D3DDevice.Instance.Device.DepthStencilSurface;
            D3DDevice.Instance.Device.DepthStencilSurface = g_pDSShadow;
            D3DDevice.Instance.Device.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.Gray, 1.0f, 0);
            //D3DDevice.Instance.Device.BeginScene();

            // Hago el render de la escena pp dicha
            effect.SetValue("g_txShadow", g_pShadowMap);
            //RenderScene(true,g_LightView, g_mShadowProj);
            RenderScene2(true);
            //Termino
            //D3DDevice.Instance.Device.EndScene();
            TextureLoader.Save("shadowmap.bmp", ImageFileFormat.Bmp, g_pShadowMap);

            // restuaro el render target y el stencil
            D3DDevice.Instance.Device.DepthStencilSurface = pOldDS;
            D3DDevice.Instance.Device.SetRenderTarget(0, pOldRT);
        }

        public void RenderScene(bool shadow, TGCMatrix viewMatrix, TGCMatrix projectionMatrix)
        {
            gameModel.Frustum.updateVolume(viewMatrix,projectionMatrix);

            var meshesQueChocanConFrustrum = gameModel.escenario.tgcScene.Meshes.FindAll
                (mesh => TgcCollisionUtils.classifyFrustumAABB(gameModel.Frustum, mesh.BoundingBox) != 
                TgcCollisionUtils.FrustumResult.OUTSIDE);
            
            foreach (var T in meshesQueChocanConFrustrum)
            {
                if (shadow)
                {
                    T.Technique = "RenderShadow";
                }
                else
                {
                    T.Technique = "RenderScene";
                }

                T.Render();
            }
   
        }


        public void RenderScene2(bool shadow)
        {
            //gameModel.Frustum.updateVolume(viewMatrix, projectionMatrix);

            var meshesQueChocanConFrustrum = gameModel.escenario.tgcScene.Meshes.FindAll
                (mesh => TgcCollisionUtils.classifyFrustumAABB(gameModel.Frustum, mesh.BoundingBox) !=
                TgcCollisionUtils.FrustumResult.OUTSIDE);

            foreach (var T in meshesQueChocanConFrustrum)
            {
                if (shadow)
                {
                    T.Technique = "RenderShadow";
                }
                else
                {
                    T.Technique = "RenderScene";
                }

                T.Render();
            }

        }


        public void SetearTecnica()
        {
            var meshesQueChocanConFrustrum = gameModel.escenario.tgcScene.Meshes.FindAll
                (mesh => TgcCollisionUtils.classifyFrustumAABB(gameModel.Frustum, mesh.BoundingBox) !=
                TgcCollisionUtils.FrustumResult.OUTSIDE);
            foreach (var T in meshesQueChocanConFrustrum)
            {
                T.Scale = new TGCVector3(1f, 1f, 1f);
                T.Effect = effect;
            }
        }

        public void AplicarRender()
        {
            //Inicio el render de la escena, para ejemplos simples. 
            //Cuando tenemos postprocesado o shaders es mejor realizar las operaciones según nuestra conveniencia.
            //PreRender();

            var device = D3DDevice.Instance.Device;

            // Capturamos las texturas de pantalla
            Surface screenRenderTarget = device.GetRenderTarget(0);
            Surface screenDepthSurface = device.DepthStencilSurface;

            // Especificamos que vamos a dibujar en una textura
            Surface surface = gameModel.renderTarget1.GetSurfaceLevel(0);
            device.SetRenderTarget(0, surface);
            device.DepthStencilSurface = gameModel.depthStencil;

            // Captura de escena en render target
            device.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.CornflowerBlue, 1.0f, 0);
            device.BeginScene();

            //Render

            if (!gameModel.estoyJugando)
            {
                gameModel.menu.renderSprite();
            }
            else
            {
                TgcMesh unPoste = gameModel.escenario.listaDePostes.OrderBy(poste => gameModel.DistanciaA2(poste)).First();
                if (!gameModel.personaje.tieneLuz && gameModel.DistanciaA2(unPoste) < 1000)
                {
                    //Se prende el farol mas cercano
                    this.renderSombras(unPoste.BoundingBox.PMin, new TGCVector3(unPoste.BoundingBox.PMin.X, 15, unPoste.BoundingBox.PMin.Z), new TGCVector3(15, 380, 15));

                }
                else
                {
                    //01158354515 --> Numero de Marce
                    //this.renderSombras(gameModel.personaje.getPosition(), gameModel.personaje.getLookAt(), new TGCVector3(100, 10, -150));
                    this.renderSombrasPersonaje(gameModel.personaje);
                    //monsterBlur.RenderMonsterBlur();
                }
            }
            device.EndScene();
            // Fin de escena


            // Especificamos que vamos a dibujar en pantalla
            device.SetRenderTarget(0, screenRenderTarget);
            device.DepthStencilSurface = screenDepthSurface;

            // Dibujado de textura en full screen quad
            device.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.Black, 1.0f, 0);
            device.BeginScene();

            gameModel.quads.FindAll(unQuad => unQuad.loEstoyUsando).ForEach(unQuad => gameModel.RenderQuad(unQuad));

            //this.OurFullScreenQuad(device, effectPosProcesado);

            gameModel.RenderFps();

            gameModel.nota.renderSprite();
            gameModel.vidaUtilVela.renderSprite();
            gameModel.velita.renderSprite();
            gameModel.vidaUtilLinterna.renderSprite();
            gameModel.linternita.renderSprite();
            //RenderAxis();
            device.EndScene();

            device.Present();

            surface.Dispose();
        }
    }
}
