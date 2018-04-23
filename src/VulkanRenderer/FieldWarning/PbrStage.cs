using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpVk;
using Tectonic;

namespace FieldWarning
{
    public class PbrStage
        : RenderStage
    {
        private class Stub
            : IDisposable
        {
            public void Dispose()
            {
            }
        }

        private struct PushConstBlockMaterial
        {
            public float HasBaseColorTexture;
            public float HasMetallicRoughnessTexture;
            public float HasNormalTexture;
            public float HasOcclusionTexture;
            public float HasEmissiveTexture;
            public float MetallicFactor;
            public float RoughnessFactor;
            public float AlphaMask;
            public float AlphaMaskCutoff;
        }

        private class PbrState
            : IRenderStageState
        {
            public ShaderModule VertexShader;
            public ShaderModule FragmentShader;
            public PipelineLayout PipelineLayout;
            public DescriptorPool DescriptorPool;
            public DescriptorSetLayout[] DescriptorSetLayouts;
            public IMeshHandle SkyboxMesh;

            public IMeshHandle Mesh;

            public Device Device { get; set; }

            public void Dispose()
            {
                this.VertexShader?.Dispose();
                this.FragmentShader?.Dispose();

                this.PipelineLayout?.Dispose();

                foreach (var layout in this.DescriptorSetLayouts)
                {
                    layout.Dispose();
                }

                this.DescriptorPool?.Dispose();

                this.SkyboxMesh?.Dispose();
            }
        }

        public IMeshHandle Mesh { get; set; }

        public override IRenderStageState Initialise(Device device, VulkanBufferManager bufferManager, IHandleCreator handleCreator)
        {
            var vertexShader = LoadShaderModule(device, ".\\data\\shaders\\pbr.vert.spv");
            var fragmentShader = LoadShaderModule(device, ".\\data\\shaders\\pbr.frag.spv");

            var descriptorPool = device.CreateDescriptorPool(2, new[] { new DescriptorPoolSize(DescriptorType.UniformBuffer, 2), new DescriptorPoolSize(DescriptorType.CombinedImageSampler, 7) });

            var sceneDescriptorSetLayout = device.CreateDescriptorSetLayout(
                new[]
                {
                    new DescriptorSetLayoutBinding
                    {
                        Binding = 0,
                        DescriptorCount = 1,
                        DescriptorType = DescriptorType.UniformBuffer,
                        StageFlags = ShaderStageFlags.Vertex | ShaderStageFlags.Fragment
                    },
                    new DescriptorSetLayoutBinding
                    {
                        Binding = 1,
                        DescriptorCount = 1,
                        DescriptorType = DescriptorType.UniformBuffer,
                        StageFlags = ShaderStageFlags.Fragment
                    },
                    new DescriptorSetLayoutBinding
                    {
                        Binding = 2,
                        DescriptorCount = 1,
                        DescriptorType = DescriptorType.CombinedImageSampler,
                        StageFlags = ShaderStageFlags.Fragment
                    },
                    new DescriptorSetLayoutBinding
                    {
                        Binding = 3,
                        DescriptorCount = 1,
                        DescriptorType = DescriptorType.CombinedImageSampler,
                        StageFlags = ShaderStageFlags.Fragment
                    },
                    new DescriptorSetLayoutBinding
                    {
                        Binding = 4,
                        DescriptorCount = 1,
                        DescriptorType = DescriptorType.CombinedImageSampler,
                        StageFlags = ShaderStageFlags.Fragment
                    }
                });

            var materialDescriptorSetLayout = device.CreateDescriptorSetLayout(
                new[]
                {
                    new DescriptorSetLayoutBinding
                    {
                        Binding = 0,
                        DescriptorCount = 1,
                        DescriptorType = DescriptorType.CombinedImageSampler,
                        StageFlags = ShaderStageFlags.Fragment
                    },
                    new DescriptorSetLayoutBinding
                    {
                        Binding = 1,
                        DescriptorCount = 1,
                        DescriptorType = DescriptorType.CombinedImageSampler,
                        StageFlags = ShaderStageFlags.Fragment
                    },
                    new DescriptorSetLayoutBinding
                    {
                        Binding = 2,
                        DescriptorCount = 1,
                        DescriptorType = DescriptorType.CombinedImageSampler,
                        StageFlags = ShaderStageFlags.Fragment
                    },
                    new DescriptorSetLayoutBinding
                    {
                        Binding = 3,
                        DescriptorCount = 1,
                        DescriptorType = DescriptorType.CombinedImageSampler,
                        StageFlags = ShaderStageFlags.Fragment
                    },
                    new DescriptorSetLayoutBinding
                    {
                        Binding = 4,
                        DescriptorCount = 1,
                        DescriptorType = DescriptorType.CombinedImageSampler,
                        StageFlags = ShaderStageFlags.Fragment
                    }
                });

            var pipelineLayout = device.CreatePipelineLayout(
                new[] { sceneDescriptorSetLayout, materialDescriptorSetLayout },
                new PushConstantRange
                {
                    Offset = 0,
                    Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<PushConstBlockMaterial>(),
                    StageFlags = ShaderStageFlags.AllGraphics
                });

            return new PbrState
            {
                Device = device,
                VertexShader = vertexShader,
                FragmentShader = fragmentShader,
                DescriptorPool = descriptorPool,
                DescriptorSetLayouts = new[] { sceneDescriptorSetLayout, materialDescriptorSetLayout },
                PipelineLayout = pipelineLayout,
                Mesh = this.Mesh
            };
        }

        public override bool IsValid(IRenderStageState state)
        {
            return ((PbrState)state).Mesh == this.Mesh;
        }

        public override IDisposable Bind(IRenderStageState state, RenderPass renderPass, CommandBuffer commandBuffer, Extent2D targetExtent)
        {
            var stageState = (PbrState)state;

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
                                                                    new PipelineVertexInputStateCreateInfo
                                                                    {
                                                                        VertexBindingDescriptions = new[] { stageState.Mesh.GetBindingDescription(0) },
                                                                        VertexAttributeDescriptions = stageState.Mesh.GetAttributeDescriptions(0, 0)
                                                                    },
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

            return new DisposeGroup(pipeline);
        }

        private static ShaderModule LoadShaderModule(Device device, string filePath)
        {
            var code = LoadShaderData(filePath, out int codeSize);

            return device.CreateShaderModule(codeSize, code);
        }

        private static uint[] LoadShaderData(string filePath, out int codeSize)
        {
            var fileBytes = File.ReadAllBytes(filePath);
            var shaderData = new uint[(int)Math.Ceiling(fileBytes.Length / 4f)];

            System.Buffer.BlockCopy(fileBytes, 0, shaderData, 0, fileBytes.Length);

            codeSize = fileBytes.Length;

            return shaderData;
        }
    }
}
