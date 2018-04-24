using FileFormatWavefront;
using GlmSharp;
using glTFLoader.Schema;
using SharpVk;
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

            var modelMesh = model.Meshes[0];

            var attributeAccessors = modelMesh.Primitives[0].Attributes.OrderBy(x => x.Value).Select(x => model.Accessors[x.Value]).ToArray();

            int count = attributeAccessors.First().Count;

            int attributeOffset = 0;

            var accessorInfo = attributeAccessors.Select(x =>
            {
                var bufferView = model.BufferViews[x.BufferView.Value];
                var buffer = dataBuffers[bufferView.Buffer];
                int attributeStride = GetStride(x);

                var result = (Stride: bufferView.ByteStride ?? attributeStride, Buffer: buffer, BufferOffset: bufferView.ByteOffset, AttributeOffset: attributeOffset, Format: GetFormat(x));

                attributeOffset += attributeStride;

                return result;
            }).ToArray();

            int vertexStride = attributeAccessors.Sum(GetStride);

            var vertices = new byte[vertexStride * count];

            int vertexPointer = 0;

            for (int index = 0; index < count; index++)
            {
                int offset = 0;

                for (int accessorIndex = 0; accessorIndex < attributeAccessors.Length; accessorIndex++)
                {
                    var info = accessorInfo[accessorIndex];

                    Array.ConstrainedCopy(info.Buffer, info.BufferOffset + (index * info.Stride), vertices, vertexPointer + offset, info.Stride);

                    offset += info.Stride;
                }

                vertexPointer += vertexStride;
            }

            pbrStage.Mesh = this.renderMap.CreateStaticMesh((uint)vertexStride, accessorInfo.Select(x => ((uint)x.AttributeOffset, x.Format)).ToArray(), vertices, this.indices);
        }

        private static Format GetFormat(Accessor accessor)
        {
            if (accessor.ComponentType != Accessor.ComponentTypeEnum.FLOAT)
            {
                throw new NotSupportedException();
            }

            switch (accessor.Type)
            {
                case Accessor.TypeEnum.SCALAR:
                    return Format.R32SFloat;
                case Accessor.TypeEnum.VEC2:
                    return Format.R32G32SFloat;
                case Accessor.TypeEnum.VEC3:
                    return Format.R32G32B32SFloat;
                case Accessor.TypeEnum.VEC4:
                    return Format.R32G32B32A32SFloat;
                default:
                    throw new NotSupportedException();
            }
        }

        private static int GetStride(Accessor accessor)
        {
            int stride;

            switch (accessor.ComponentType)
            {
                case Accessor.ComponentTypeEnum.BYTE:
                case Accessor.ComponentTypeEnum.UNSIGNED_BYTE:
                    stride = 1;
                    break;
                case Accessor.ComponentTypeEnum.SHORT:
                case Accessor.ComponentTypeEnum.UNSIGNED_SHORT:
                    stride = 2;
                    break;
                case Accessor.ComponentTypeEnum.UNSIGNED_INT:
                case Accessor.ComponentTypeEnum.FLOAT:
                    stride = 4;
                    break;
                default:
                    throw new NotSupportedException();
            }

            switch (accessor.Type)
            {
                case Accessor.TypeEnum.SCALAR:
                    break;
                case Accessor.TypeEnum.VEC2:
                    stride *= 2;
                    break;
                case Accessor.TypeEnum.VEC3:
                    stride *= 3;
                    break;
                case Accessor.TypeEnum.VEC4:
                case Accessor.TypeEnum.MAT2:
                    stride *= 4;
                    break;
                case Accessor.TypeEnum.MAT3:
                    stride *= 9;
                    break;
                case Accessor.TypeEnum.MAT4:
                    stride *= 16;
                    break;
                default:
                    throw new NotSupportedException();
            }

            return stride;
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
