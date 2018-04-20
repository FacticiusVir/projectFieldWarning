using GlmSharp;
using SharpVk;
using SharpVk.Shanq;
using SharpVk.Shanq.GlmSharp;
using SharpVk.Spirv;
using System;
using System.Linq;
using Tectonic;

namespace FieldWarning
{
    public class MeshStage
        : RenderStage
    {
        private class StageState
            : IRenderStageState
        {
            public IMeshHandle Mesh;
            public ShaderModule VertexShader;
            public ShaderModule FragmentShader;
            public PipelineLayout PipelineLayout;
            public DescriptorPool DescriptorPool;
            public DescriptorSetLayout DescriptorSetLayout;
            public DescriptorSet DescriptorSet;
            public VulkanBuffer StateBuffer;

            public Device Device { get; set; }

            public void Dispose()
            {
                this.PipelineLayout?.Dispose();

                this.FragmentShader?.Dispose();

                this.VertexShader?.Dispose();

                this.DescriptorSetLayout?.Dispose();

                this.DescriptorPool?.Dispose();

                this.StateBuffer?.Release();
            }
        }

        private struct UniformState
        {
            public mat4 World;
            public mat4 View;
            public mat4 Projection;
            public mat4 InverseTransposeWorldView;
        }

        public IMeshHandle Mesh { get; set; }

        public override IRenderStageState Initialise(Device device, VulkanBufferManager bufferManager, IHandleCreator handleCreator)
        {
            vec3.Ones.Length.ToString();

            var vertexShader = device.CreateVertexModule(shanq => from input in shanq.GetInput<Vertex>()
                                                                  from ubo in shanq.GetBinding<UniformState>(0)
                                                                  let transform = ubo.Projection * ubo.View * ubo.World
                                                                  let normal4 = ubo.InverseTransposeWorldView * new vec4(input.Normal, 1)
                                                                  select new VertexOutput
                                                                  {
                                                                      Normal = new vec3(normal4.x, normal4.y, normal4.z) / normal4.w,
                                                                      Position = transform * new vec4(input.Position, 1)
                                                                  });

            var fragmentShader = device.CreateFragmentModule(shanq => from input in shanq.GetInput<FragmentInput>()
                                                                      let brightness = input.Normal.y
                                                                      select new FragmentOutput
                                                                      {
                                                                          Colour = new vec4(brightness, brightness, brightness, 1)
                                                                      });

            var stateBuffer = bufferManager.CreateBuffer<UniformState>(1, BufferUsageFlags.TransferDestination | BufferUsageFlags.UniformBuffer, MemoryPropertyFlags.DeviceLocal);

            this.UpdateState(stateBuffer);

            var descriptorPool = device.CreateDescriptorPool(1, new DescriptorPoolSize(DescriptorType.UniformBuffer, 1));

            var descriptorSetLayout = device.CreateDescriptorSetLayout(new DescriptorSetLayoutBinding
            {
                Binding = 0,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.UniformBuffer,
                StageFlags = ShaderStageFlags.AllGraphics
            });

            var descriptorSet = descriptorPool.AllocateDescriptorSet(descriptorSetLayout);

            descriptorSet.WriteDescriptorSet(0, 0, DescriptorType.UniformBuffer, new DescriptorBufferInfo
            {
                Buffer = stateBuffer.Buffer,
                Offset = 0,
                Range = Constants.WholeSize
            });

            var pipelineLayout = device.CreatePipelineLayout(descriptorSetLayout, null);

            return new StageState
            {
                Device = device,
                PipelineLayout = pipelineLayout,
                FragmentShader = fragmentShader,
                VertexShader = vertexShader,
                DescriptorPool = descriptorPool,
                DescriptorSetLayout = descriptorSetLayout,
                DescriptorSet = descriptorSet,
                StateBuffer = stateBuffer,
                Mesh = this.Mesh
            };
        }

        public override void Update(IRenderStageState state)
        {
            this.UpdateState(((StageState)state).StateBuffer);
        }

        private void UpdateState(VulkanBuffer stateBuffer)
        {
            float rotation = (float)(DateTime.UtcNow.Millisecond + DateTime.UtcNow.Second * 1000);

            rotation /= 1000f;

            var world = mat4.Rotate(rotation, vec3.UnitY);
            var view = mat4.LookAt(new vec3(0, 10, -15), vec3.Zero, vec3.UnitY);

            var uniformState = new UniformState
            {
                World = world,
                View = view,
                Projection = mat4.Perspective((float)Math.PI / 4f, 1.25f, 0.1f, 100),
                InverseTransposeWorldView = (world * view).Inverse.Transposed
            };

            uniformState.Projection[1, 1] *= -1;

            stateBuffer.Update(uniformState);
        }

        public override bool IsValid(IRenderStageState state)
        {
            return ((StageState)state).Mesh == this.Mesh;
        }

        public override IDisposable Bind(IRenderStageState state, RenderPass renderPass, CommandBuffer commandBuffer, Extent2D targetExtent)
        {
            var stageState = (StageState)state;

            if (stageState.Mesh == null)
            {
                return null;
            }

            this.UpdateState(stageState.StateBuffer);

            var pipeline = stageState.Device.CreateGraphicsPipeline(null,
                                                                    new[]
                                                                    {
                                                                        new PipelineShaderStageCreateInfo
                                                                        {
                                                                            Stage = ShaderStageFlags.Vertex,
                                                                            Module = stageState.VertexShader,
                                                                            Name = "main"
                                                                        },
                                                                        new PipelineShaderStageCreateInfo
                                                                        {
                                                                            Stage = ShaderStageFlags.Fragment,
                                                                            Module = stageState.FragmentShader,
                                                                            Name = "main"
                                                                        }
                                                                    },
                                                                    stageState.Mesh.PipelineVertexInputState,
                                                                    new PipelineInputAssemblyStateCreateInfo { Topology = PrimitiveTopology.TriangleList },
                                                                    new PipelineRasterizationStateCreateInfo
                                                                    {
                                                                        PolygonMode = PolygonMode.Fill,
                                                                        LineWidth = 1,
                                                                        CullMode = CullModeFlags.Back,
                                                                        FrontFace = FrontFace.CounterClockwise
                                                                    },
                                                                    stageState.PipelineLayout,
                                                                    renderPass,
                                                                    0,
                                                                    null,
                                                                    -1,
                                                                    viewportState: new PipelineViewportStateCreateInfo
                                                                    {
                                                                        Viewports = new[] { new Viewport(0, 0, targetExtent.Width, targetExtent.Height, 0, 1) },
                                                                        Scissors = new[] { new Rect2D(targetExtent) }
                                                                    },
                                                                    multisampleState: new PipelineMultisampleStateCreateInfo
                                                                    {
                                                                        SampleShadingEnable = false,
                                                                        RasterizationSamples = SampleCountFlags.SampleCount1,
                                                                        MinSampleShading = 1
                                                                    },
                                                                    colorBlendState: new PipelineColorBlendStateCreateInfo
                                                                    {
                                                                        Attachments = new[]
                                                                        {
                                                                            new PipelineColorBlendAttachmentState
                                                                            {
                                                                                ColorWriteMask = ColorComponentFlags.R
                                                                                                    | ColorComponentFlags.G
                                                                                                    | ColorComponentFlags.B
                                                                                                    | ColorComponentFlags.A,
                                                                                BlendEnable = false,
                                                                                SourceColorBlendFactor = BlendFactor.One,
                                                                                DestinationColorBlendFactor = BlendFactor.Zero,
                                                                                ColorBlendOp = BlendOp.Add,
                                                                                SourceAlphaBlendFactor = BlendFactor.One,
                                                                                DestinationAlphaBlendFactor = BlendFactor.Zero,
                                                                                AlphaBlendOp = BlendOp.Add
                                                                            }
                                                                        },
                                                                        LogicOpEnable = false,
                                                                        LogicOp = LogicOp.Copy,
                                                                        BlendConstants = (0, 0, 0, 0)
                                                                    },
                                                                    depthStencilState: new PipelineDepthStencilStateCreateInfo
                                                                    {
                                                                        DepthTestEnable = true,
                                                                        DepthWriteEnable = true,
                                                                        DepthCompareOp = CompareOp.Less,
                                                                        DepthBoundsTestEnable = false,
                                                                        MinDepthBounds = 0,
                                                                        MaxDepthBounds = 1,
                                                                        StencilTestEnable = false
                                                                    });

            commandBuffer.BindDescriptorSets(PipelineBindPoint.Graphics, stageState.PipelineLayout, 0, stageState.DescriptorSet, null);

            commandBuffer.BindPipeline(PipelineBindPoint.Graphics, pipeline);

            stageState.Mesh.BindBuffers(commandBuffer);

            commandBuffer.DrawIndexed((uint)stageState.Mesh.IndexCount, 1, 0, 0, 0);

            return pipeline;
        }

        private struct VertexOutput
        {
            [Location(0)]
            public vec3 Normal;

            [BuiltIn(BuiltIn.Position)]
            public vec4 Position;
        }

        private struct FragmentInput
        {
            [Location(0)]
            public vec3 Normal;
        }

        private struct FragmentOutput
        {
            [Location(0)]
            public vec4 Colour;
        }

        private struct Vertex
        {
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