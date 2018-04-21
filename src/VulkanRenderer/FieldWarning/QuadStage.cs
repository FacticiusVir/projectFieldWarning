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
    public class QuadStage
        : RenderStage
    {
        private class StageState
            : IRenderStageState
        {
            public IMeshHandle Mesh;
            public ShaderModule VertexShader;
            public ShaderModule FragmentShader;
            public PipelineLayout PipelineLayout;

            public Device Device { get; set; }

            public void Dispose()
            {
                this.PipelineLayout?.Dispose();

                this.FragmentShader?.Dispose();

                this.VertexShader?.Dispose();

                this.Mesh?.Dispose();
            }
        }
        
        private readonly Vertex[] vertices =
        {
            new Vertex(new vec2(1f, 1f), new vec3(0.0f, 0.0f, 1.0f)),
            new Vertex(new vec2(1f, -1f), new vec3(0.0f, 1.0f, 0.0f)),
            new Vertex(new vec2(-1f, -1f), new vec3(1.0f, 0.0f, 0.0f)),
            new Vertex(new vec2(-1f, 1f), new vec3(1.0f, 1.0f, 1.0f))
        };

        private readonly uint[] indices = { 0, 1, 2, 2, 3, 0 };

        public override IRenderStageState Initialise(Device device, VulkanBufferManager bufferManager, IHandleCreator handleCreator)
        {
            var mesh = StaticMeshHandle.Create(bufferManager, VectorTypeLibrary.Instance, this.vertices, this.indices);

            var vertexShader = device.CreateVertexModule(shanq => from input in shanq.GetInput<Vertex>()
                                                                  select new VertexOutput
                                                                  {
                                                                      Colour = input.Colour,
                                                                      Position = new vec4(input.Position, 0, 1)
                                                                  });

            var fragmentShader = device.CreateFragmentModule(shanq => from input in shanq.GetInput<FragmentInput>()
                                                                      select new FragmentOutput
                                                                      {
                                                                          Colour = new vec4(input.Colour, 1)
                                                                      });

            var pipelineLayout = device.CreatePipelineLayout(null, null);

            return new StageState
            {
                Device = device,
                PipelineLayout = pipelineLayout,
                FragmentShader = fragmentShader,
                VertexShader = vertexShader,
                Mesh = mesh
            };
        }

        public override IDisposable Bind(IRenderStageState state, RenderPass renderPass, CommandBuffer commandBuffer, Extent2D targetExtent)
        {
            var stageState = (StageState)state;

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

            commandBuffer.BindPipeline(PipelineBindPoint.Graphics, pipeline);

            stageState.Mesh.BindBuffers(commandBuffer);

            commandBuffer.DrawIndexed((uint)this.indices.Length, 1, 0, 0, 0);

            return pipeline;
        }

        private struct VertexOutput
        {
            [Location(0)]
            public vec3 Colour;

            [BuiltIn(BuiltIn.Position)]
            public vec4 Position;
        }

        private struct FragmentInput
        {
            [Location(0)]
            public vec3 Colour;
        }

        private struct FragmentOutput
        {
            [Location(0)]
            public vec4 Colour;
        }

        private struct Vertex
        {
            public Vertex(vec2 position, vec3 colour)
            {
                this.Position = position;
                this.Colour = colour;
            }

            [Location(0)]
            public vec2 Position;

            [Location(1)]
            public vec3 Colour;
        }
    }
}