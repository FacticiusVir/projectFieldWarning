using FileFormatWavefront;
using GlmSharp;
using glTFLoader.Schema;
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
            
            var pbrStage = new PbrStage();

            this.renderMap = this.vulkan.CreateSimpleRenderMap((1440, 960),
                                                                "Project Field Warning",
                                                                new ClearStage { ClearColour = new vec4(0, 0, 0, 1) },
                                                                pbrStage);

            var model = glTFLoader.Interface.LoadModel(".\\data\\models\\DamagedHelmet\\glTF-Embedded\\DamagedHelmet.gltf");

            var dataBuffers = model.Buffers.Select(LoadBufferData).ToArray();

            pbrStage.Mesh = this.renderMap.CreateStaticMesh<Vertex>(VectorTypeLibrary.Instance, vertices.ToArray(), indices.ToArray());
        }

        private static byte[] LoadBufferData(glTFLoader.Schema.Buffer gltfBuffer)
        {
            return Convert.FromBase64String(gltfBuffer.Uri.Substring(37));
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
                this.Uv = vec2.Zero;
            }

            [Location(0)]
            public vec3 Position;

            [Location(1)]
            public vec3 Normal;

            [Location(2)]
            public vec2 Uv;
        }
    }
}
