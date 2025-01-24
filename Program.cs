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

class Program
{

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
        
        GameLogger.DebugLog("Successfully created semaphores.");
        
        while (IsRunning)
        {

            Toolkit.Window.ProcessEvents(false);
            Renderer.Wait();
            
            Renderer.BeginRenderPass(Color4.Brown);
            
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