using FileFormatWavefront;
using GlmSharp;
using SharpVk.Shanq;
using SharpVk.Shanq.GlmSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tectonic;

namespace FieldWarning
{
    public class LifecycleService
        : GameService, IUpdatable
    {
        private readonly IUpdateLoopService updateLoop;
        private readonly IVulkanService vulkan;

        private Game game;
        private VulkanRenderMap renderMap;

        public LifecycleService(IUpdateLoopService updateLoop, IVulkanService vulkan)
        {
            this.updateLoop = updateLoop;
            this.vulkan = vulkan;
        }

        public override void Initialise(Game game)
        {
            this.game = game;
        }

        public override void Start()
        {
            this.updateLoop.Register(this, UpdateStage.Update);

            var meshStage = new MeshStage();

            var pbrStage = new PbrStage();

            this.renderMap = this.vulkan.CreateSimpleRenderMap((1440, 960),
                                                                "Project Field Warning",
                                                                new ClearStage { ClearColour = new vec4(0, 0, 0, 1) },
                                                                pbrStage);

            //var tankFile = File.ReadAllLines("./tank2.obj");

            //var group = tankFile.Model.Groups.First();

            //var vertices = new List<Vertex>();

            //var vertexLookup = new Dictionary<(int, int), uint>();

            //var indices = new List<uint>();

            //void AddIndex((int PositionIndex, int NormalIndex) faceVertex)
            //{
            //    if (!vertexLookup.TryGetValue(faceVertex, out uint index))
            //    {
            //        index = (uint)vertices.Count;

            //        var position = tankFile.Model.Vertices[faceVertex.PositionIndex];

            //        var normal = tankFile.Model.Normals[faceVertex.NormalIndex];

            //        vertices.Add(new Vertex(new vec3(position.x / 100f, position.y / 100f, position.z / 100f), new vec3(normal.x, normal.y, normal.z)));
            //    }

            //    indices.Add(index);
            //}

            //foreach (var face in group.Faces)
            //{
            //    AddIndex((face.Indices[0].vertex, face.Indices[0].normal.Value));
            //    AddIndex((face.Indices[1].vertex, face.Indices[1].normal.Value));
            //    AddIndex((face.Indices[2].vertex, face.Indices[2].normal.Value));
            //}

            var boxModel = glTFLoader.Interface.LoadModel(".\\data\\models\\DamagedHelmet\\glTF-Embedded\\DamagedHelmet.gltf");

            pbrStage.Mesh = this.renderMap.CreateStaticMesh<Vertex>(VectorTypeLibrary.Instance, vertices.ToArray(), indices.ToArray());
        }

        public override void Stop()
        {
            this.updateLoop.Deregister(this);
        }

        public void Update()
        {
            if (this.renderMap.Endpoints.Any(x => x.ShouldClose))
            {
                this.renderMap.Close();

                this.renderMap = null;

                this.game.SignalStop();
            }
            else
            {
                this.renderMap.Render();
            }
        }

        private readonly Vertex[] vertices =
        {
            new Vertex(new vec3(-0.5f, -0.5f, 0.5f)),
            new Vertex(new vec3(0.5f, -0.5f, 0.5f)),
            new Vertex(new vec3(-0.5f, 0.5f, 0.5f)),
            new Vertex(new vec3(0.5f, 0.5f, 0.5f)),
            new Vertex(new vec3(-0.5f, 0.5f, -0.5f)),
            new Vertex(new vec3(0.5f, 0.5f, -0.5f)),
            new Vertex(new vec3(-0.5f, -0.5f, -0.5f)),
            new Vertex(new vec3(0.5f, -0.5f, -0.5f))
        };

        private readonly uint[] indices = { 0, 1, 2, 2, 1, 3, 2, 3, 4, 4, 3, 5, 4, 5, 6, 6, 5, 7, 6, 7, 0, 0, 7, 1, 1, 7, 3, 3, 7, 5, 6, 0, 4, 4, 0, 2 };

        private struct Vertex
        {
            public Vertex(vec3 position)
                : this(position, position)
            {
            }

            public Vertex(vec3 position, vec3 normal)
            {
                this.Position = position;
                this.Normal = normal;
            }

            [Location(0)]
            public vec3 Position;

            [Location(1)]
            public vec3 Normal;
        }
    }
}
