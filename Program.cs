using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text;
using OpenTK.Core.Native;
using OpenTK.Core.Utility;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGLES1;
using OpenTK.Graphics.Vulkan;
using OpenTK.Mathematics;
using OpenTK.Platform;

namespace Blockgame_VulkanTests;

public struct Vertex
{

    public Vector2 Position;
    public Vector3 Color;

    public static VkVertexInputBindingDescription GetVertexBindingDescription()
    {
        
        VkVertexInputBindingDescription vertexBindingDescription = new VkVertexInputBindingDescription();

        vertexBindingDescription.binding = 0;
        vertexBindingDescription.stride = (uint) Marshal.SizeOf<Vertex>();
        vertexBindingDescription.inputRate = VkVertexInputRate.VertexInputRateVertex;
        
        return vertexBindingDescription;

    }

    public static VkVertexInputAttributeDescription[] GetVertexAttributeDescriptions()
    {
        
        VkVertexInputAttributeDescription[] vertexAttributeDescriptions = new VkVertexInputAttributeDescription[2];

        vertexAttributeDescriptions[0].binding = 0;
        vertexAttributeDescriptions[0].location = 0;
        vertexAttributeDescriptions[0].format = VkFormat.FormatR32g32Sfloat;
        vertexAttributeDescriptions[0].offset = 0;
        
        vertexAttributeDescriptions[1].binding = 0;
        vertexAttributeDescriptions[1].location = 1;
        vertexAttributeDescriptions[1].format = VkFormat.FormatR32g32b32Sfloat;
        vertexAttributeDescriptions[1].offset = (uint) Marshal.OffsetOf<Vertex>(nameof(Color));
        
        return vertexAttributeDescriptions;
        
    } 

}
class Program
{
    public static Vertex[] vertices =
    {
        new Vertex() { Position = (0.0f, -0.5f), Color = (1.0f, 0.0f, 0.0f) },
        new Vertex() { Position = (0.5f, 0.5f), Color = (0.0f, 1.0f, 0.0f)},
        new Vertex() { Position = (-0.5f, 0.5f), Color = (0.0f, 0.0f, 1.0f)}
    };
    
    public static bool IsRunning = true;
    
    static unsafe void Main(string[] args)
    {
        
        VKLoader.Init();
        
        ConsoleLogger consoleLogger = new ConsoleLogger();
        consoleLogger.Filter = LogLevel.Info;
        
        ToolkitOptions options = new ToolkitOptions();
        options.ApplicationName = "VulkanTest";
        options.Logger = null;
        
        Toolkit.Init(options);
        
        VulkanGraphicsApiHints contextSettings = new VulkanGraphicsApiHints();
        Renderer.Window = Toolkit.Window.Create(contextSettings);
        
        Toolkit.Window.SetTitle(Renderer.Window, "Vulkan Testing");
        Toolkit.Window.SetSize(Renderer.Window, (800, 600));
        Toolkit.Window.SetMode(Renderer.Window, WindowMode.Normal);

        ReadOnlySpan<byte> iconData = [255, 255, 255, 255, 0, 0, 0, 255, 0, 0, 0, 255, 255, 255, 255, 255];
        IconHandle icon = Toolkit.Icon.Create(2, 2, iconData);
        Toolkit.Window.SetIcon(Renderer.Window, icon);

        EventQueue.EventRaised += EventRaised;
        
        Renderer.Init();
        VkPipeline currentPipeline = Renderer.CreateGraphicsPipeline("vert.spv", "frag.spv", Vertex.GetVertexBindingDescription(), Vertex.GetVertexAttributeDescriptions());
        VkBuffer vertexBuffer = Renderer.CreateVertexBuffer(vertices);
        
        GameLogger.DebugLog("Successfully created semaphores.");
        
        while (IsRunning)
        {

            Toolkit.Window.ProcessEvents(false);
            Renderer.Wait();
            
            Renderer.BeginRenderPass(Color4.Brown);
            Renderer.BindGraphicsPipeline(currentPipeline);
            Renderer.BindVertexBuffer(vertexBuffer);
            
            Renderer.Draw();
            
            Renderer.EndRenderPass();
            
        }
        
        GameLogger.DebugLog("Destroying and closing game.");
        Renderer.DestroyAll();
        
        Toolkit.Window.Destroy(Renderer.Window);

    }

    static void EventRaised(PalHandle? handle, PlatformEventType type, EventArgs args)
    {

        switch (args)
        {
            
            case CloseEventArgs close:
                // Toolkit.Window.Destroy(close.Window);
                IsRunning = false;
                break;
            
        }
        
    }
    
}