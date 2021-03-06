﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TGC.Core.Mathematica;
using TGC.Core.SceneLoader;

namespace TGC.Group.Model
{
    class Nota : IInteractuable
    {
        private TgcMesh mesh;
        private GameModel gameModel;

        public Nota(TgcMesh mesh, GameModel gameModel)
        {
            this.mesh = mesh;
            this.gameModel = gameModel;
        }

        public void Interactuar(Personaje personaje)
        {
            personaje.cantidadNotas++;
            gameModel.nota.instanciarNotas(personaje.cantidadNotas);
            gameModel.agarrarPagina.escucharSonidoActual(false);
            eliminarMesh();
        }
        public TGCVector3 getPosition()
        {
            return mesh.BoundingBox.PMin;
        }

        public void Usar(Personaje personaje) { }

        private void eliminarMesh()
        {
            TGCVector3 posicionDeBorrado = new TGCVector3(0, -4000, 0);
            mesh.Move(posicionDeBorrado);
            mesh.updateBoundingBox();
            mesh.UpdateMeshTransform();
        }

    }
}
