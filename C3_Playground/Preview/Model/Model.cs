﻿using C3;
using C3.Elements;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace C3_Playground.Preview.Model
{
    internal class ModelRenderer
    {
        private readonly GraphicsDevice _graphicsDevice;

        public List<Model> Models { get; init; }

        public ModelRenderer(C3Model c3Model, GraphicsDevice graphicsDevice, Texture2D texture)
        {
            _graphicsDevice = graphicsDevice;
            
            Models = new();

            if (c3Model.Meshs.Count != c3Model.Animations.Count) throw new Exception("Number of meshes does not match the number of motions");

            for (int i = 0; i < c3Model.Meshs.Count; i++)
            {
                if (c3Model.Animations[i].BoneCount > 1)
                    Models.Add(new Model(c3Model.Meshs[i], c3Model.Animations[i], _graphicsDevice, texture));
            }
        }
        public void Update(GameTime gameTime)
        {
            foreach (var model in Models)
                model.Update(gameTime);
        }
        public void Draw(GameTime gameTime, BasicEffect basicEffect)
        {
            foreach (var model in Models)
              model.Draw(gameTime, basicEffect);
            //Models[0].Draw(basicEffect, gameTime);
        }
    }

    internal class Model
    { 
        private readonly GraphicsDevice _graphicsDevice;
        private readonly C3Phy _c3Phy;
        public IndexBuffer IndexBuffer { get; init; }
        

        private VertexPositionTexture[] vertices;
        private VertexBuffer vertexBuffer;
        private bool vertexBufferDirty = true;
        public VertexBuffer VertexBuffer
        {
            get
            {
                if (vertexBufferDirty)
                {
                    vertexBuffer.SetData<VertexPositionTexture>(vertices);
                    vertexBufferDirty = false;
                    return vertexBuffer;
                }
                return vertexBuffer;
            }
        }
       
        public Skeleton Skeleton { get; init; }//TODO: Get rid of skeleton, might be overkill.

        public Motion BaseMotion { get; set; }

        public Motion? ActiveMotion { get; set; }

        public Texture2D Texture { get; set; }


        public Model(C3Phy c3Phy, C3Motion c3Motion, GraphicsDevice graphicsDevice, Texture2D texture)
        {
            _graphicsDevice = graphicsDevice;
            _c3Phy = c3Phy;
            Texture = texture;

            #region Build Geometry
            IndexBuffer = new IndexBuffer(_graphicsDevice, IndexElementSize.SixteenBits, c3Phy.Indices.Length, BufferUsage.WriteOnly);
            IndexBuffer.SetData<ushort>(c3Phy.Indices);

            vertices = new VertexPositionTexture[c3Phy.Vertices.Length];
            for (int i = 0; i < c3Phy.Vertices.Length; i++)
            {
                vertices[i] = new()
                {
                    TextureCoordinate = new Vector2(c3Phy.Vertices[i].U, c3Phy.Vertices[i].V),
                    Position = new Vector3(c3Phy.Vertices[i].Position.X, c3Phy.Vertices[i].Position.Y, c3Phy.Vertices[i].Position.Z)
                };
            }
            vertexBuffer = new VertexBuffer(_graphicsDevice, typeof(VertexPositionTexture), vertices.Length, BufferUsage.WriteOnly);
            #endregion

            //Build bone relation. 
            Skeleton = new(c3Phy);

            //Set Base Motion
            BaseMotion = new(c3Motion, c3Phy.InitMatrix);
        }

        public void Update(GameTime gameTime)
        {
            //Perform calcs on vertices. Are transforms applied to the previously calculated or base?

            //Going to update Frame each call, going to be way to fast.
            bool changed = BaseMotion.NextFrame();


            if (changed)
            {
                vertexBufferDirty = true;
                foreach (var bone in Skeleton.BoneStore)
                {
                    if (Skeleton.TryGetBoneVertices(bone.Key, out var boneVertices))
                    {
                        foreach (var vertexIdx in boneVertices)
                        {
                            if (vertexIdx.Item2 == 0) continue;
                            vertices[vertexIdx.Item1] = new VertexPositionTexture()
                            {
                                TextureCoordinate = new Vector2(_c3Phy.Vertices[vertexIdx.Item1].U, _c3Phy.Vertices[vertexIdx.Item1].V),
                                Position = CalculateVertex(new Vector3(_c3Phy.Vertices[vertexIdx.Item1].Position.X, _c3Phy.Vertices[vertexIdx.Item1].Position.Y, _c3Phy.Vertices[vertexIdx.Item1].Position.Z),
                                    BaseMotion.GetMatrix(bone.Key),
                                    vertexIdx.Item2)
                            };
                        }
                    }
                }
            }
        }

        public void Draw( GameTime gameTime, BasicEffect basicEffect)
        {

            _graphicsDevice.SetVertexBuffer(VertexBuffer);
            _graphicsDevice.Indices = IndexBuffer;

            foreach (var pass in basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, IndexBuffer.IndexCount);
            }
        }

        public Vector3 CalculateVertex(Vector3 vertex, Matrix transform, float weight)
        {
            //It appears that the weight does not have an effect...not used in the eu client. Renders incorrectly when using weight.
            var result = Vector3.Transform(vertex, transform); //Matrix.Multiply(transform, weight));
            return result;
        }
    }
}
