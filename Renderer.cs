using System.Runtime.InteropServices;
using System.Text;
using OpenTK.Graphics;
using OpenTK.Graphics.Vulkan;
using OpenTK.Mathematics;
using OpenTK.Platform;

namespace Blockgame_VulkanTests;

public unsafe class Renderer
{
    
    private static VkInstance _instance;
    private static VkPhysicalDevice _physicalDevice = VkPhysicalDevice.Zero;
    private static VkDevice _device;
    private static VkResult _result;
        
    private static VkQueue _graphicsQueue;
    private static VkQueue _presentQueue;

    private static VkSwapchainKHR _swapchain = VkSwapchainKHR.Zero;
    private static VkSurfaceFormatKHR _swapchainSurfaceFormat;
    private static List<VkImage> _swapchainImages = new();
    private static List<VkImageView> _swapchainImageViews = new();
    private static List<VkFramebuffer> _swapchainFramebuffers = new();
        
    private static VkRenderPass _renderPass;
    private static VkPipelineLayout _pipelineLayout;
    private static VkPipeline _graphicsPipeline;
        
    private static VkCommandPool _commandPool;
    private static VkCommandBuffer _commandBuffer;
        
    private static VkSemaphore _imageAvailableSemaphore;
    private static VkSemaphore _renderFinishedSemaphore;
    private static VkFence _inFlightFence;
    
    private static List<VkSemaphore> _imageAvailableSemaphores = new();
    private static List<VkSemaphore> _renderFinishedSemaphores = new();
    private static List<VkFence> _imageAvailableFences = new();

    private static VkSurfaceKHR _surface;
    private static VkExtent2D _swapchainSurfaceExtent;
    private static VkViewport _viewport;
    private static VkRect2D _scissorRect;

    private static uint _imageIndex;
    private static uint? _graphicsFamilyIndex;
    private static uint? _presentFamilyIndex;
    
    public static WindowHandle Window;

    public static void Draw()
    {

        Vk.CmdDraw(_commandBuffer, 6, 1, 0, 0);

    }

    public static void SetScissor(int x, int y, uint width, uint height)
    {

        _scissorRect.offset = new VkOffset2D(x, y);
        _scissorRect.extent = new VkExtent2D(width, height);

    }
    public static void SetViewport(float x, float y, float width, float height)
    {

        _viewport.x = x;
        _viewport.y = y;
        _viewport.width = width;
        _viewport.height = height;

    }

    public static void DestroyAll()
    {
        
        Vk.DeviceWaitIdle(_device);
                
        foreach (VkFramebuffer framebuffer in _swapchainFramebuffers)
        {
            Vk.DestroyFramebuffer(_device, framebuffer, null);    
        }
        foreach (VkImageView imageView in _swapchainImageViews)
        {
            Vk.DestroyImageView(_device, imageView, null);
        }
        Vk.DestroySwapchainKHR(_device, _swapchain, null);
        Vk.DestroySurfaceKHR(_instance, _surface, null);    
        // Vk.DestroyShaderModule(_device, _vertexShaderModule, null);
        // Vk.DestroyShaderModule(_device, _fragmentShaderModule, null);
        Vk.DestroyPipeline(_device, _graphicsPipeline, null);
        Vk.DestroyPipelineLayout(_device, _pipelineLayout, null);
        Vk.DestroyRenderPass(_device, _renderPass, null);
        fixed (VkCommandBuffer* commandBufferPtr = &_commandBuffer) Vk.FreeCommandBuffers(_device, _commandPool, 1, commandBufferPtr);
        Vk.DestroyCommandPool(_device, _commandPool, null);
        Vk.DestroySemaphore(_device, _imageAvailableSemaphore, null);
        Vk.DestroySemaphore(_device, _renderFinishedSemaphore, null);
        Vk.DestroyFence(_device, _inFlightFence, null);
        Vk.DestroyDevice(_device, null);
        Vk.DestroyInstance(_instance, null);
        
    }
    public static void EndRenderPass()
    {
        
        Vk.CmdEndRenderPass(_commandBuffer);
        _result = Vk.EndCommandBuffer(_commandBuffer);
        if (_result != VkResult.Success) GameLogger.ThrowError($"Failed to record the command buffer with message {_result.ToString()}");
            
        VkSubmitInfo submitInfo = new VkSubmitInfo();
        submitInfo.sType = VkStructureType.StructureTypeSubmitInfo;

        submitInfo.waitSemaphoreCount = 1;
        fixed (VkSemaphore* imageAvailableSemaphorePtr = &_imageAvailableSemaphore) submitInfo.pWaitSemaphores = imageAvailableSemaphorePtr;
        VkPipelineStageFlagBits bits = VkPipelineStageFlagBits.PipelineStageColorAttachmentOutputBit;
        submitInfo.pWaitDstStageMask = &bits;

        submitInfo.commandBufferCount = 1;
        fixed (VkCommandBuffer* commandBufferPtr = &_commandBuffer) submitInfo.pCommandBuffers = commandBufferPtr;

        submitInfo.signalSemaphoreCount = 1;
        fixed (VkSemaphore* renderFinishedSemaphorePtr = &_renderFinishedSemaphore) submitInfo.pSignalSemaphores = renderFinishedSemaphorePtr;
            
        _result = Vk.QueueSubmit(_graphicsQueue, 1, &submitInfo, _inFlightFence);
        if (_result != VkResult.Success) GameLogger.ThrowError($"Failed to submit the command buffer with message {_result.ToString()}"); 
            
        VkPresentInfoKHR presentInfo = new VkPresentInfoKHR();
        presentInfo.sType = VkStructureType.StructureTypePresentInfoKhr;
        presentInfo.waitSemaphoreCount = 1;
        fixed (VkSemaphore* renderFinishedSemaphorePtr = &_renderFinishedSemaphore) presentInfo.pWaitSemaphores = renderFinishedSemaphorePtr;
            
        presentInfo.swapchainCount = 1;
        fixed (VkSwapchainKHR* swapchainPtr = &_swapchain) presentInfo.pSwapchains = swapchainPtr;
        fixed (uint* imageIndexPtr = &_imageIndex) presentInfo.pImageIndices = imageIndexPtr;
        
        presentInfo.pResults = null;
        Vk.QueuePresentKHR(_presentQueue, &presentInfo);
        
    }

    public static void BeginRenderPass(Color4<Rgba> clearColor)
    {
        
        fixed (uint* imageIndexPtr = &_imageIndex) _result = Vk.AcquireNextImageKHR(_device, _swapchain, ulong.MaxValue, _imageAvailableSemaphore, VkFence.Zero, imageIndexPtr);
        if (_result != VkResult.Success)
        {
            if (_result == VkResult.SuboptimalKhr || _result == VkResult.ErrorOutOfDateKhr)
            {
                RecreateSwapchain();
            }
            else
            {
                GameLogger.ThrowError($"Failed to acquire next image with message {_result.ToString()}");
            }
        }
    
        _result = Vk.ResetCommandBuffer(_commandBuffer, 0);
        if (_result != VkResult.Success) GameLogger.ThrowError($"Failed to reset the command buffer with message {_result.ToString()}");

        VkCommandBufferBeginInfo commandBufferBeginInfo = new VkCommandBufferBeginInfo();
        commandBufferBeginInfo.sType = VkStructureType.StructureTypeCommandBufferBeginInfo;
        commandBufferBeginInfo.pNext = null;
        commandBufferBeginInfo.flags = 0;
        commandBufferBeginInfo.pInheritanceInfo = null;
    
        _result = Vk.BeginCommandBuffer(_commandBuffer, &commandBufferBeginInfo);
        if (_result != VkResult.Success) GameLogger.ThrowError("Failed to begin recording the command buffer.");

        VkRenderPassBeginInfo renderPassBeginInfo = new VkRenderPassBeginInfo();
        renderPassBeginInfo.sType = VkStructureType.StructureTypeRenderPassBeginInfo;
        renderPassBeginInfo.renderPass = _renderPass;
        renderPassBeginInfo.framebuffer = _swapchainFramebuffers[(int)_imageIndex];
        renderPassBeginInfo.renderArea.offset = new VkOffset2D(0, 0);
        renderPassBeginInfo.renderArea.extent = _swapchainSurfaceExtent;
        
        VkClearValue color = default;
        color.color.float32[0] = (float)Math.Pow(clearColor.X, 2.2);
        color.color.float32[1] = (float)Math.Pow(clearColor.Y, 2.2);
        color.color.float32[2] = (float)Math.Pow(clearColor.Z, 2.2);
        color.color.float32[3] = (float)Math.Pow(clearColor.W, 2.2);
        renderPassBeginInfo.clearValueCount = 1;
        renderPassBeginInfo.pClearValues = &color;
        
        Vk.CmdBeginRenderPass(_commandBuffer, &renderPassBeginInfo, VkSubpassContents.SubpassContentsInline);
        Vk.CmdBindPipeline(_commandBuffer, VkPipelineBindPoint.PipelineBindPointGraphics, _graphicsPipeline);
        
        fixed (VkViewport* viewportPtr = &_viewport) Vk.CmdSetViewport(_commandBuffer, 0, 1, viewportPtr);
        fixed (VkRect2D* scissorRectPtr = &_scissorRect) Vk.CmdSetScissor(_commandBuffer, 0, 1, scissorRectPtr);
        
    }
    public static void Wait()
    {

        fixed (VkFence* inFlightFencePtr = &_inFlightFence)
        {
            Vk.WaitForFences(_device, 1, inFlightFencePtr, 1, ulong.MaxValue);
            Vk.ResetFences(_device, 1, inFlightFencePtr);
        }

    }

    public static void RecreateSwapchain()
    {
        
        Vk.DeviceWaitIdle(_device);
        if (_swapchain != VkSwapchainKHR.Zero)
        {
            
            foreach (VkFramebuffer framebuffer in _swapchainFramebuffers)
            {
                Vk.DestroyFramebuffer(_device, framebuffer, null);
            }

            foreach (VkImageView imageView in _swapchainImageViews)
            {
                Vk.DestroyImageView(_device, imageView, null);
            }
            
            _swapchainFramebuffers.Clear();
            _swapchainImageViews.Clear();
            _swapchainImages.Clear();
            
            Vk.DestroySwapchainKHR(_device, _swapchain, null);
            
        }
    
        // Get details of swapchain support
        VkSurfaceCapabilitiesKHR swapchainCapabilities = new VkSurfaceCapabilitiesKHR();
        List<VkSurfaceFormatKHR> swapchainSupportedFormats = new();
        List<VkPresentModeKHR> swapchainPresentModesSupported = new();
        
        // Get values pertaining to swapchain support.
        // Console.WriteLine("hi");
        Vk.GetPhysicalDeviceSurfaceCapabilitiesKHR(_physicalDevice, _surface, &swapchainCapabilities);

        uint formatCount = 0;
        Vk.GetPhysicalDeviceSurfaceFormatsKHR(_physicalDevice, _surface, &formatCount, null);
        if (formatCount != 0)
        {
            VkSurfaceFormatKHR* surfaceSupportedFormatsPtr = stackalloc VkSurfaceFormatKHR[(int)formatCount];
            Vk.GetPhysicalDeviceSurfaceFormatsKHR(_physicalDevice, _surface, &formatCount, surfaceSupportedFormatsPtr);
            for (int i = 0; i < formatCount; i++)
            {
                swapchainSupportedFormats.Add(surfaceSupportedFormatsPtr[i]);
            }
        }

        uint presentModeCount = 0;
        Vk.GetPhysicalDeviceSurfacePresentModesKHR(_physicalDevice, _surface, &presentModeCount, null);
        if (presentModeCount != 0)
        {
            VkPresentModeKHR* surfaceSupportedPresentModesPtr = stackalloc VkPresentModeKHR[(int)presentModeCount];
            Vk.GetPhysicalDeviceSurfacePresentModesKHR(_physicalDevice, _surface, &presentModeCount, surfaceSupportedPresentModesPtr);
            for (int i = 0; i < presentModeCount; i++)
            {
                swapchainPresentModesSupported.Add(surfaceSupportedPresentModesPtr[i]);
            }
        }
        
        if (swapchainPresentModesSupported.Count == 0 && swapchainSupportedFormats.Count == 0) GameLogger.ThrowError("Swapchain extensions are not supported.");
        
        // Choose necessary swapchain surface formats and capabilities.
        VkSurfaceFormatKHR swapchainSurfaceFormat = swapchainSupportedFormats[0];
        foreach (VkSurfaceFormatKHR format in swapchainSupportedFormats)
        {
            if (format.format == VkFormat.FormatB8g8r8a8Srgb && format.colorSpace == VkColorSpaceKHR.ColorspaceSrgbNonlinearKhr)
            {
                swapchainSurfaceFormat = format;
                break;
            }
        }

        // we can enumerate like we did surface format if we wanted, but PresentModeFifoKhr is always available.
        VkPresentModeKHR swapchainSurfacePresentMode = VkPresentModeKHR.PresentModeFifoKhr;

        Toolkit.Window.GetFramebufferSize(Window, out Vector2i framebufferSize);
        _swapchainSurfaceExtent.width = (uint) Math.Clamp(framebufferSize.X, swapchainCapabilities.minImageExtent.width, swapchainCapabilities.maxImageExtent.width);
        _swapchainSurfaceExtent.height = (uint) Math.Clamp(framebufferSize.Y, swapchainCapabilities.minImageExtent.height, swapchainCapabilities.maxImageExtent.height);

        uint imageCount = swapchainCapabilities.minImageCount + 1;
        if (swapchainCapabilities.maxImageCount > 0 && imageCount > swapchainCapabilities.maxImageCount) imageCount = swapchainCapabilities.maxImageCount;

        VkSwapchainCreateInfoKHR swapchainCreateInfo = new VkSwapchainCreateInfoKHR();
        swapchainCreateInfo.sType = VkStructureType.StructureTypeSwapchainCreateInfoKhr;
        swapchainCreateInfo.surface = _surface;
        swapchainCreateInfo.minImageCount = imageCount;
        swapchainCreateInfo.imageFormat = swapchainSurfaceFormat.format;
        swapchainCreateInfo.imageColorSpace = swapchainSurfaceFormat.colorSpace;
        swapchainCreateInfo.imageExtent = _swapchainSurfaceExtent;
        swapchainCreateInfo.imageArrayLayers = 1;
        swapchainCreateInfo.imageUsage = VkImageUsageFlagBits.ImageUsageColorAttachmentBit;
        
        uint* queueFamilyIndices = stackalloc uint[] { _graphicsFamilyIndex.Value, _presentFamilyIndex.Value };
        if (_graphicsFamilyIndex.Value != _presentFamilyIndex.Value)
        {
            swapchainCreateInfo.imageSharingMode = VkSharingMode.SharingModeConcurrent;
            swapchainCreateInfo.queueFamilyIndexCount = 2;
            swapchainCreateInfo.pQueueFamilyIndices = queueFamilyIndices;
        }
        else
        {
            swapchainCreateInfo.imageSharingMode = VkSharingMode.SharingModeExclusive;
            swapchainCreateInfo.queueFamilyIndexCount = 0;
            swapchainCreateInfo.pQueueFamilyIndices = null;
        }

        swapchainCreateInfo.preTransform = swapchainCapabilities.currentTransform;
        swapchainCreateInfo.compositeAlpha = VkCompositeAlphaFlagBitsKHR.CompositeAlphaOpaqueBitKhr;
        swapchainCreateInfo.presentMode = swapchainSurfacePresentMode;
        swapchainCreateInfo.clipped = 1;
        swapchainCreateInfo.oldSwapchain = VkSwapchainKHR.Zero;
        
        fixed (VkSwapchainKHR* swapchainPtr = &_swapchain) _result = Vk.CreateSwapchainKHR(_device, &swapchainCreateInfo, null, swapchainPtr);
        if (_result != VkResult.Success) GameLogger.ThrowError($"Failed to create swapchain with message {_result.ToString()}");
        GameLogger.DebugLog("Successfully created the swapchain.");
        
        uint swapchainImageCount = 0;
        Vk.GetSwapchainImagesKHR(_device, _swapchain, &swapchainImageCount, null);
        VkImage* swapchainImagesPtr = stackalloc VkImage[(int)swapchainImageCount];
        Vk.GetSwapchainImagesKHR(_device, _swapchain, &swapchainImageCount, swapchainImagesPtr);
        for (int i = 0; i < swapchainImageCount; i++)
        {
            _swapchainImages.Add(swapchainImagesPtr[i]);
        }

        // Create swapchain image views.
        VkImageView* swapchainImageViewsPtr = stackalloc VkImageView[_swapchainImages.Count];
        for (int i = 0; i < _swapchainImages.Count; i++)
        {
            
            VkImageViewCreateInfo imageViewCreateInfo = new VkImageViewCreateInfo();
            imageViewCreateInfo.sType = VkStructureType.StructureTypeImageViewCreateInfo;
            imageViewCreateInfo.image = _swapchainImages[i];
            imageViewCreateInfo.viewType = VkImageViewType.ImageViewType2d;
            imageViewCreateInfo.format = swapchainSurfaceFormat.format;
            imageViewCreateInfo.components.r = VkComponentSwizzle.ComponentSwizzleIdentity;
            imageViewCreateInfo.components.g = VkComponentSwizzle.ComponentSwizzleIdentity;
            imageViewCreateInfo.components.b = VkComponentSwizzle.ComponentSwizzleIdentity;
            imageViewCreateInfo.components.a = VkComponentSwizzle.ComponentSwizzleIdentity;
            imageViewCreateInfo.subresourceRange.aspectMask = VkImageAspectFlagBits.ImageAspectColorBit;
            imageViewCreateInfo.subresourceRange.baseMipLevel = 0;
            imageViewCreateInfo.subresourceRange.levelCount = 1;
            imageViewCreateInfo.subresourceRange.baseArrayLayer = 0;
            imageViewCreateInfo.subresourceRange.layerCount = 1;
            
            _result = Vk.CreateImageView(_device, &imageViewCreateInfo, null, &swapchainImageViewsPtr[i]);    
            if (_result != VkResult.Success) GameLogger.ThrowError($"Failed to create image view with message {_result.ToString()}");
            GameLogger.DebugLog($"Successfully created image view.");
            
        }
        
        for (int i = 0; i < _swapchainImages.Count; i++)
        {
            _swapchainImageViews.Add(swapchainImageViewsPtr[i]);
        }
        
        // Create the framebuffers.
        for (int i = 0; i < _swapchainImageViews.Count; i++)
        {
            
            VkFramebufferCreateInfo framebufferCreateInfo = new VkFramebufferCreateInfo();
            framebufferCreateInfo.sType = VkStructureType.StructureTypeFramebufferCreateInfo;
            framebufferCreateInfo.renderPass = _renderPass;
            framebufferCreateInfo.attachmentCount = 1;
            VkImageView attachments = _swapchainImageViews[i];
            framebufferCreateInfo.pAttachments = &attachments;
            framebufferCreateInfo.width = _swapchainSurfaceExtent.width;
            framebufferCreateInfo.height = _swapchainSurfaceExtent.height;
            framebufferCreateInfo.layers = 1;

            VkFramebuffer frameBuffer;
            _result = Vk.CreateFramebuffer(_device, &framebufferCreateInfo, null, &frameBuffer);
            if (_result != VkResult.Success) GameLogger.ThrowError($"Failed to create framebuffer with message {_result.ToString()}");
            _swapchainFramebuffers.Add(frameBuffer);
            GameLogger.DebugLog("Successfully created framebuffer.");
            
        }
        
        SetScissor(0, 0, _swapchainSurfaceExtent.width, _swapchainSurfaceExtent.height);
        SetViewport(0.0f, 0.0f, _swapchainSurfaceExtent.width, _swapchainSurfaceExtent.height);
        
    }
    public static void Init()
    {
        
        // Setup for creating an instance.
        // TODO: potentially offload this to a method.
        VkApplicationInfo appInfo = new VkApplicationInfo();
        appInfo.sType = VkStructureType.StructureTypeApplicationInfo;
        appInfo.pApplicationName = (byte*)Marshal.StringToHGlobalAnsi("Vulkan Testing");
        appInfo.applicationVersion = Vk.MAKE_API_VERSION(1, 0, 0, 0);
        appInfo.pEngineName = (byte*)Marshal.StringToHGlobalAnsi("No Engine");
        appInfo.engineVersion = Vk.MAKE_API_VERSION(1, 0, 0, 0);
        appInfo.apiVersion = Vk.VK_API_VERSION_1_0;

        VkInstanceCreateInfo vkInstanceCreateInfo = new VkInstanceCreateInfo();
        vkInstanceCreateInfo.sType = VkStructureType.StructureTypeInstanceCreateInfo;
        vkInstanceCreateInfo.pApplicationInfo = &appInfo;

        ReadOnlySpan<string> toolkitExtensions = Toolkit.Vulkan.GetRequiredInstanceExtensions();
        byte** toolkitExtensionPtrs = stackalloc byte*[toolkitExtensions.Length];
        for (int i = 0; i < toolkitExtensions.Length; i++)
        {
            toolkitExtensionPtrs[i] = (byte*)Marshal.StringToHGlobalAnsi(toolkitExtensions[i]);
        }

        ReadOnlySpan<string> requestedValidationLayers = [ "VK_LAYER_KHRONOS_validation" ];
        byte** requestedValidationLayersPtrs = stackalloc byte*[requestedValidationLayers.Length];
        for (int i = 0; i < requestedValidationLayers.Length; i++)
        {
            requestedValidationLayersPtrs[i] = (byte*)Marshal.StringToHGlobalAnsi(requestedValidationLayers[i]);
        }
        
        uint layerCount = 0;
        bool foundValidationLayers = false;
        Vk.EnumerateInstanceLayerProperties(&layerCount, null);
        VkLayerProperties* layerProperties = stackalloc VkLayerProperties[(int)layerCount];
        Vk.EnumerateInstanceLayerProperties(&layerCount, layerProperties);
        for (int i = 0; i < layerCount; i++)
        {
            ReadOnlySpan<byte> layerName = layerProperties[i].layerName;
            layerName = layerName.Slice(0, layerName.IndexOf((byte)0));
            string layerNameString = Encoding.UTF8.GetString(layerName);
            // Console.WriteLine(layerNameString);
            if (layerNameString == requestedValidationLayers[0])
            {
                foundValidationLayers = true;
                break;
            }

        }
        if (!foundValidationLayers) GameLogger.ThrowError("Could not find the validation layer.");
        GameLogger.DebugLog($"Found validation layer {requestedValidationLayers[0]}.");

        vkInstanceCreateInfo.enabledLayerCount = (uint)requestedValidationLayers.Length;
        vkInstanceCreateInfo.ppEnabledLayerNames = requestedValidationLayersPtrs;
        
        vkInstanceCreateInfo.enabledExtensionCount = (uint)toolkitExtensions.Length;
        vkInstanceCreateInfo.ppEnabledExtensionNames = toolkitExtensionPtrs;

        uint instanceExtensionCount = 0;
        Vk.EnumerateInstanceExtensionProperties(null, &instanceExtensionCount, null);
        VkExtensionProperties* instanceExtensionProperties = stackalloc VkExtensionProperties[(int)instanceExtensionCount];
        Vk.EnumerateInstanceExtensionProperties(null, &instanceExtensionCount, instanceExtensionProperties);
        GameLogger.DebugLog("Supported extensions:");
        for (int i = 0; i < instanceExtensionCount; i++)
        {

            ReadOnlySpan<byte> name = instanceExtensionProperties[i].extensionName;
            name = name.Slice(0, name.IndexOf((byte)0));
            Console.WriteLine($"\t{Encoding.UTF8.GetString(name)}");

        }

        fixed (VkInstance* instancePtr = &_instance) _result = Vk.CreateInstance(&vkInstanceCreateInfo, null, instancePtr);
        if (_result != VkResult.Success) GameLogger.ThrowError($"Failed to create a Vulkan instance with error {_result.ToString()}");
        GameLogger.DebugLog("Created the Vulkan instance.");
        
        VKLoader.SetInstance(_instance);
        
        // Create the window surface.
        _result = Toolkit.Vulkan.CreateWindowSurface(_instance, Window, null, out _surface);
        if (_result != VkResult.Success) GameLogger.ThrowError($"Failed to create a window surface with error {_result.ToString()}");
        
        // Create and select a physical device; your GPU.
        uint physicalDeviceCount = 0;
        Vk.EnumeratePhysicalDevices(_instance, &physicalDeviceCount, null);
        if (physicalDeviceCount == 0) GameLogger.ThrowError("There are no devices that support Vulkan.");
        VkPhysicalDevice* physicalDevices = stackalloc VkPhysicalDevice[(int)physicalDeviceCount];
        Vk.EnumeratePhysicalDevices(_instance, &physicalDeviceCount, physicalDevices);
        
        // Check if the devices are suitable for our use case.
        // We can have a more complex method of determining
        // what our physical device will be, based on parameters
        // like if it is integrated or not, but for now we will
        // just pick the very first one that supports our needs.
        _physicalDevice = physicalDevices[0];

        uint extensionCount = 0;
        Vk.EnumerateDeviceExtensionProperties(_physicalDevice, null, &extensionCount, null);
        VkExtensionProperties* availableExtensions = stackalloc VkExtensionProperties[(int)extensionCount];
        Vk.EnumerateDeviceExtensionProperties(_physicalDevice, null, &extensionCount, availableExtensions);

        List<string> requiredExtensions = [ "VK_KHR_swapchain" ];
        int extensionsAvailable = 0;
        for (int i = 0; i < extensionCount; i++)
        {
            
            ReadOnlySpan<byte> name = availableExtensions[i].extensionName;
            name = name.Slice(0, name.IndexOf((byte)0));
            string stringName = Encoding.UTF8.GetString(name);
            if (requiredExtensions.Contains(stringName)) extensionsAvailable++;

        }

        if (requiredExtensions.Count != extensionsAvailable)
        {
            
            GameLogger.DebugLog("The following required extensions are not available:", SeverityType.Error);
            foreach (string requiredExtension in requiredExtensions)
            {
                Console.WriteLine($"\t{requiredExtension}");
            }
            GameLogger.ThrowError("There exists required extensions that are not available.");
            
        }
        
        // Find queue families that are needed.
        // uint? graphicsFamilyIndex = null;
        // uint? presentFamilyIndex = null;

        uint queueFamilyCount = 0;
        Vk.GetPhysicalDeviceQueueFamilyProperties(_physicalDevice, &queueFamilyCount, null);
        VkQueueFamilyProperties* queueFamilyProperties = stackalloc VkQueueFamilyProperties[(int)queueFamilyCount];
        Vk.GetPhysicalDeviceQueueFamilyProperties(_physicalDevice, &queueFamilyCount, queueFamilyProperties);
        for (uint i = 0; i < queueFamilyCount; i++)
        {

            int presentSupport = 0;
            Vk.GetPhysicalDeviceSurfaceSupportKHR(_physicalDevice, i, _surface, &presentSupport);
            if (presentSupport == 1) _presentFamilyIndex = i;
            
            if (((uint)queueFamilyProperties[i].queueFlags & (uint)VkQueueFlagBits.QueueGraphicsBit) == 1)
            {
                _graphicsFamilyIndex = i;
            }

        }
        
        if (_graphicsFamilyIndex == null) GameLogger.ThrowError("No graphics family could be found.");
        
        // Stuff for creating the logical device.
        
        // Determine the amount of queues per queue family.
        List<uint> uniqueQueueFamilies = [ _graphicsFamilyIndex.Value, _presentFamilyIndex.Value ];

        List<VkDeviceQueueCreateInfo> queueCreateInfos = new();
        float queuePriority = 1.0f;
        foreach (uint queueFamilyIndex in uniqueQueueFamilies)
        {

            VkDeviceQueueCreateInfo queueCreateInfo = new VkDeviceQueueCreateInfo();
            queueCreateInfo.sType = VkStructureType.StructureTypeDeviceQueueCreateInfo;
            queueCreateInfo.queueFamilyIndex = queueFamilyIndex;
            queueCreateInfo.queueCount = 1;
            queueCreateInfo.pQueuePriorities = &queuePriority;
            queueCreateInfos.Add(queueCreateInfo);

        }
        
        // Specify the device features.
        VkPhysicalDeviceFeatures deviceFeatures = new VkPhysicalDeviceFeatures(); // do stuff later
        
        // Creating the logical device.
        VkDeviceCreateInfo deviceCreateInfo = new VkDeviceCreateInfo();
        deviceCreateInfo.sType = VkStructureType.StructureTypeDeviceCreateInfo;
        deviceCreateInfo.queueCreateInfoCount = (uint) queueCreateInfos.Count;
        VkDeviceQueueCreateInfo* queueCreateInfoPtrs = stackalloc VkDeviceQueueCreateInfo[queueCreateInfos.Count];
        for (int i = 0; i < queueCreateInfos.Count; i++)
        {
            queueCreateInfoPtrs[i] = queueCreateInfos[i];
        }
        deviceCreateInfo.pQueueCreateInfos = queueCreateInfoPtrs;
        deviceCreateInfo.pEnabledFeatures = &deviceFeatures;
        deviceCreateInfo.enabledExtensionCount = (uint) requiredExtensions.Count;
        byte** requiredDeviceExtensionPtrs = stackalloc byte*[requiredExtensions.Count];
        for (int i = 0; i < requiredExtensions.Count; i++)
        {
            requiredDeviceExtensionPtrs[i] = (byte*)Marshal.StringToHGlobalAnsi(requiredExtensions[i]);
        }
        deviceCreateInfo.ppEnabledExtensionNames = requiredDeviceExtensionPtrs;
        deviceCreateInfo.enabledLayerCount = 0;
        
        // this is where we would do stuff for validation layers, but we dont have any :(
        
        // Instancing the logical device.
        fixed (VkDevice* devicePtr = &_device) _result = Vk.CreateDevice(_physicalDevice, &deviceCreateInfo, null, devicePtr);
        if (_result != VkResult.Success) GameLogger.ThrowError($"The device couldn't be created with message {_result.ToString()}");
        GameLogger.DebugLog("Created the logical device.");
        
        fixed (VkQueue* graphicsQueuePtr = &_graphicsQueue) Vk.GetDeviceQueue(_device, _graphicsFamilyIndex.Value, 0, graphicsQueuePtr);
        fixed (VkQueue* presentQueuePtr = &_presentQueue) Vk.GetDeviceQueue(_device, _presentFamilyIndex.Value, 0, presentQueuePtr);
        
        // Get details of swapchain support
        VkSurfaceCapabilitiesKHR swapchainCapabilities = new VkSurfaceCapabilitiesKHR();
        List<VkSurfaceFormatKHR> swapchainSupportedFormats = new();
        List<VkPresentModeKHR> swapchainPresentModesSupported = new();
        
        // Get values pertaining to swapchain support.
        // Console.WriteLine("hi");
        Vk.GetPhysicalDeviceSurfaceCapabilitiesKHR(_physicalDevice, _surface, &swapchainCapabilities);

        uint formatCount = 0;
        Vk.GetPhysicalDeviceSurfaceFormatsKHR(_physicalDevice, _surface, &formatCount, null);
        if (formatCount != 0)
        {
            VkSurfaceFormatKHR* surfaceSupportedFormatsPtr = stackalloc VkSurfaceFormatKHR[(int)formatCount];
            Vk.GetPhysicalDeviceSurfaceFormatsKHR(_physicalDevice, _surface, &formatCount, surfaceSupportedFormatsPtr);
            for (int i = 0; i < formatCount; i++)
            {
                swapchainSupportedFormats.Add(surfaceSupportedFormatsPtr[i]);
            }
        }

        uint presentModeCount = 0;
        Vk.GetPhysicalDeviceSurfacePresentModesKHR(_physicalDevice, _surface, &presentModeCount, null);
        if (presentModeCount != 0)
        {
            VkPresentModeKHR* surfaceSupportedPresentModesPtr = stackalloc VkPresentModeKHR[(int)presentModeCount];
            Vk.GetPhysicalDeviceSurfacePresentModesKHR(_physicalDevice, _surface, &presentModeCount, surfaceSupportedPresentModesPtr);
            for (int i = 0; i < presentModeCount; i++)
            {
                swapchainPresentModesSupported.Add(surfaceSupportedPresentModesPtr[i]);
            }
        }
        
        if (swapchainPresentModesSupported.Count == 0 && swapchainSupportedFormats.Count == 0) GameLogger.ThrowError("Swapchain extensions are not supported.");
        
        // Choose necessary swapchain surface formats and capabilities.
        VkSurfaceFormatKHR swapchainSurfaceFormat = swapchainSupportedFormats[0];
        foreach (VkSurfaceFormatKHR format in swapchainSupportedFormats)
        {
            if (format.format == VkFormat.FormatB8g8r8a8Srgb && format.colorSpace == VkColorSpaceKHR.ColorspaceSrgbNonlinearKhr)
            {
                swapchainSurfaceFormat = format;
                break;
            }
        }

        // we can enumerate like we did surface format if we wanted, but PresentModeFifoKhr is always available.
        VkPresentModeKHR swapchainSurfacePresentMode = VkPresentModeKHR.PresentModeFifoKhr;

        Toolkit.Window.GetFramebufferSize(Window, out Vector2i framebufferSize);
        _swapchainSurfaceExtent.width = (uint) Math.Clamp(framebufferSize.X, swapchainCapabilities.minImageExtent.width, swapchainCapabilities.maxImageExtent.width);
        _swapchainSurfaceExtent.height = (uint) Math.Clamp(framebufferSize.Y, swapchainCapabilities.minImageExtent.height, swapchainCapabilities.maxImageExtent.height);

        uint imageCount = swapchainCapabilities.minImageCount + 1;
        if (swapchainCapabilities.maxImageCount > 0 && imageCount > swapchainCapabilities.maxImageCount) imageCount = swapchainCapabilities.maxImageCount;

        VkSwapchainCreateInfoKHR swapchainCreateInfo = new VkSwapchainCreateInfoKHR();
        swapchainCreateInfo.sType = VkStructureType.StructureTypeSwapchainCreateInfoKhr;
        swapchainCreateInfo.surface = _surface;
        swapchainCreateInfo.minImageCount = imageCount;
        swapchainCreateInfo.imageFormat = swapchainSurfaceFormat.format;
        swapchainCreateInfo.imageColorSpace = swapchainSurfaceFormat.colorSpace;
        swapchainCreateInfo.imageExtent = _swapchainSurfaceExtent;
        swapchainCreateInfo.imageArrayLayers = 1;
        swapchainCreateInfo.imageUsage = VkImageUsageFlagBits.ImageUsageColorAttachmentBit;
        
        uint* queueFamilyIndices = stackalloc uint[] { _graphicsFamilyIndex.Value, _presentFamilyIndex.Value };
        if (_graphicsFamilyIndex.Value != _presentFamilyIndex.Value)
        {
            swapchainCreateInfo.imageSharingMode = VkSharingMode.SharingModeConcurrent;
            swapchainCreateInfo.queueFamilyIndexCount = 2;
            swapchainCreateInfo.pQueueFamilyIndices = queueFamilyIndices;
        }
        else
        {
            swapchainCreateInfo.imageSharingMode = VkSharingMode.SharingModeExclusive;
            swapchainCreateInfo.queueFamilyIndexCount = 0;
            swapchainCreateInfo.pQueueFamilyIndices = null;
        }

        swapchainCreateInfo.preTransform = swapchainCapabilities.currentTransform;
        swapchainCreateInfo.compositeAlpha = VkCompositeAlphaFlagBitsKHR.CompositeAlphaOpaqueBitKhr;
        swapchainCreateInfo.presentMode = swapchainSurfacePresentMode;
        swapchainCreateInfo.clipped = 1;
        swapchainCreateInfo.oldSwapchain = VkSwapchainKHR.Zero;
        
        fixed (VkSwapchainKHR* swapchainPtr = &_swapchain) _result = Vk.CreateSwapchainKHR(_device, &swapchainCreateInfo, null, swapchainPtr);
        if (_result != VkResult.Success) GameLogger.ThrowError($"Failed to create swapchain with message {_result.ToString()}");
        GameLogger.DebugLog("Successfully created the swapchain.");
        
        uint swapchainImageCount = 0;
        Vk.GetSwapchainImagesKHR(_device, _swapchain, &swapchainImageCount, null);
        VkImage* swapchainImagesPtr = stackalloc VkImage[(int)swapchainImageCount];
        Vk.GetSwapchainImagesKHR(_device, _swapchain, &swapchainImageCount, swapchainImagesPtr);
        for (int i = 0; i < swapchainImageCount; i++)
        {
            _swapchainImages.Add(swapchainImagesPtr[i]);
        }

        // Create swapchain image views.
        VkImageView* swapchainImageViewsPtr = stackalloc VkImageView[_swapchainImages.Count];
        for (int i = 0; i < _swapchainImages.Count; i++)
        {
            
            VkImageViewCreateInfo imageViewCreateInfo = new VkImageViewCreateInfo();
            imageViewCreateInfo.sType = VkStructureType.StructureTypeImageViewCreateInfo;
            imageViewCreateInfo.image = _swapchainImages[i];
            imageViewCreateInfo.viewType = VkImageViewType.ImageViewType2d;
            imageViewCreateInfo.format = swapchainSurfaceFormat.format;
            imageViewCreateInfo.components.r = VkComponentSwizzle.ComponentSwizzleIdentity;
            imageViewCreateInfo.components.g = VkComponentSwizzle.ComponentSwizzleIdentity;
            imageViewCreateInfo.components.b = VkComponentSwizzle.ComponentSwizzleIdentity;
            imageViewCreateInfo.components.a = VkComponentSwizzle.ComponentSwizzleIdentity;
            imageViewCreateInfo.subresourceRange.aspectMask = VkImageAspectFlagBits.ImageAspectColorBit;
            imageViewCreateInfo.subresourceRange.baseMipLevel = 0;
            imageViewCreateInfo.subresourceRange.levelCount = 1;
            imageViewCreateInfo.subresourceRange.baseArrayLayer = 0;
            imageViewCreateInfo.subresourceRange.layerCount = 1;
            
            _result = Vk.CreateImageView(_device, &imageViewCreateInfo, null, &swapchainImageViewsPtr[i]);    
            if (_result != VkResult.Success) GameLogger.ThrowError($"Failed to create image view with message {_result.ToString()}");
            GameLogger.DebugLog($"Successfully created image view.");
            
        }
        
        for (int i = 0; i < _swapchainImages.Count; i++)
        {
            _swapchainImageViews.Add(swapchainImageViewsPtr[i]);
        }
        
        // Create the render pass.
        VkAttachmentDescription colorAttachment = new VkAttachmentDescription();
        colorAttachment.format = swapchainSurfaceFormat.format;
        colorAttachment.samples = VkSampleCountFlagBits.SampleCount1Bit;
        colorAttachment.loadOp = VkAttachmentLoadOp.AttachmentLoadOpClear;
        colorAttachment.storeOp = VkAttachmentStoreOp.AttachmentStoreOpStore;
        colorAttachment.stencilLoadOp = VkAttachmentLoadOp.AttachmentLoadOpDontCare;
        colorAttachment.stencilStoreOp = VkAttachmentStoreOp.AttachmentStoreOpDontCare;
        colorAttachment.initialLayout = VkImageLayout.ImageLayoutUndefined;
        colorAttachment.finalLayout = VkImageLayout.ImageLayoutPresentSrcKhr;
        
        VkAttachmentReference colorAttachmentReference = new VkAttachmentReference();
        colorAttachmentReference.attachment = 0;
        colorAttachmentReference.layout = VkImageLayout.ImageLayoutColorAttachmentOptimal;
        
        VkSubpassDescription subpassDescription = new VkSubpassDescription();
        subpassDescription.pipelineBindPoint = VkPipelineBindPoint.PipelineBindPointGraphics;
        subpassDescription.colorAttachmentCount = 1;
        subpassDescription.pColorAttachments = &colorAttachmentReference;
        
        VkSubpassDependency dependency = new VkSubpassDependency();
        dependency.srcSubpass = Vk.SubpassExternal;
        dependency.dstSubpass = 0;
        dependency.srcStageMask = VkPipelineStageFlagBits.PipelineStageColorAttachmentOutputBit;
        dependency.srcAccessMask = 0;
        dependency.dstStageMask = VkPipelineStageFlagBits.PipelineStageColorAttachmentOutputBit;
        dependency.dstAccessMask = VkAccessFlagBits.AccessColorAttachmentWriteBit;
        
        VkRenderPassCreateInfo renderPassCreateInfo = new VkRenderPassCreateInfo();
        renderPassCreateInfo.sType = VkStructureType.StructureTypeRenderPassCreateInfo;
        renderPassCreateInfo.attachmentCount = 1;
        renderPassCreateInfo.pAttachments = &colorAttachment;
        renderPassCreateInfo.subpassCount = 1;
        renderPassCreateInfo.pSubpasses = &subpassDescription;
        renderPassCreateInfo.dependencyCount = 1;
        renderPassCreateInfo.pDependencies = &dependency;
        
        fixed (VkRenderPass* renderPassPtr = &_renderPass) _result = Vk.CreateRenderPass(_device, &renderPassCreateInfo, null, renderPassPtr);
        if (_result != VkResult.Success) GameLogger.ThrowError($"Failed to create render pass with message {_result.ToString()}");
        GameLogger.DebugLog($"Successfully created render pass.");
        
        // Create the framebuffers.
        for (int i = 0; i < _swapchainImageViews.Count; i++)
        {
            
            VkFramebufferCreateInfo framebufferCreateInfo = new VkFramebufferCreateInfo();
            framebufferCreateInfo.sType = VkStructureType.StructureTypeFramebufferCreateInfo;
            framebufferCreateInfo.renderPass = _renderPass;
            framebufferCreateInfo.attachmentCount = 1;
            VkImageView attachments = _swapchainImageViews[i];
            framebufferCreateInfo.pAttachments = &attachments;
            framebufferCreateInfo.width = _swapchainSurfaceExtent.width;
            framebufferCreateInfo.height = _swapchainSurfaceExtent.height;
            framebufferCreateInfo.layers = 1;

            VkFramebuffer frameBuffer;
            _result = Vk.CreateFramebuffer(_device, &framebufferCreateInfo, null, &frameBuffer);
            if (_result != VkResult.Success) GameLogger.ThrowError($"Failed to create framebuffer with message {_result.ToString()}");
            _swapchainFramebuffers.Add(frameBuffer);
            GameLogger.DebugLog("Successfully created framebuffer.");
            
        }
        
        // Create the graphics pipeline.
        byte[] vertShader = File.ReadAllBytes("vert.spv");
        byte[] fragShader = File.ReadAllBytes("frag.spv");
        
        VkShaderModuleCreateInfo vertexShaderModuleCreateInfo = new VkShaderModuleCreateInfo();
        vertexShaderModuleCreateInfo.sType = VkStructureType.StructureTypeShaderModuleCreateInfo;
        vertexShaderModuleCreateInfo.codeSize = (UIntPtr) vertShader.Length;
        fixed (byte* vertShaderPtr = vertShader) vertexShaderModuleCreateInfo.pCode = (uint*)vertShaderPtr;

        VkShaderModule vertexShaderModule;
        _result = Vk.CreateShaderModule(_device, &vertexShaderModuleCreateInfo, null, &vertexShaderModule);
        if (_result != VkResult.Success) GameLogger.ThrowError($"Failed to create shader module with message {_result.ToString()}");
        
        VkShaderModuleCreateInfo fragmentShaderModuleCreateInfo = new VkShaderModuleCreateInfo();
        fragmentShaderModuleCreateInfo.sType = VkStructureType.StructureTypeShaderModuleCreateInfo;
        fragmentShaderModuleCreateInfo.codeSize = (UIntPtr) fragShader.Length;
        fixed (byte* fragShaderPtr = fragShader) fragmentShaderModuleCreateInfo.pCode = (uint*)fragShaderPtr;
        
        VkShaderModule fragmentShaderModule;
        _result = Vk.CreateShaderModule(_device, &fragmentShaderModuleCreateInfo, null, &fragmentShaderModule);
        if (_result != VkResult.Success) GameLogger.ThrowError($"Failed to create shader module with message {_result.ToString()}");
        
        VkPipelineShaderStageCreateInfo vertexShaderStageCreateInfo = new VkPipelineShaderStageCreateInfo();
        vertexShaderStageCreateInfo.sType = VkStructureType.StructureTypePipelineShaderStageCreateInfo;
        vertexShaderStageCreateInfo.stage = VkShaderStageFlagBits.ShaderStageVertexBit;
        vertexShaderStageCreateInfo.module = vertexShaderModule;
        vertexShaderStageCreateInfo.pName = (byte*) Marshal.StringToHGlobalAnsi("main");
        
        VkPipelineShaderStageCreateInfo fragmentShaderStageCreateInfo = new VkPipelineShaderStageCreateInfo();
        fragmentShaderStageCreateInfo.sType = VkStructureType.StructureTypePipelineShaderStageCreateInfo;
        fragmentShaderStageCreateInfo.stage = VkShaderStageFlagBits.ShaderStageFragmentBit;
        fragmentShaderStageCreateInfo.module = fragmentShaderModule;
        fragmentShaderStageCreateInfo.pName = (byte*) Marshal.StringToHGlobalAnsi("main");

        // VkPipelineShaderStageCreateInfo[] shaderStages = [ vertexShaderStageCreateInfo, fragmentShaderStageCreateInfo ];
        VkPipelineShaderStageCreateInfo* shaderStages = stackalloc VkPipelineShaderStageCreateInfo[] { vertexShaderStageCreateInfo, fragmentShaderStageCreateInfo };
        
        VkDynamicState* dynamicStates = stackalloc VkDynamicState[] { VkDynamicState.DynamicStateViewport, VkDynamicState.DynamicStateScissor };
        VkPipelineDynamicStateCreateInfo dynamicStateCreateInfo = new VkPipelineDynamicStateCreateInfo();
        dynamicStateCreateInfo.sType = VkStructureType.StructureTypePipelineDynamicStateCreateInfo;
        dynamicStateCreateInfo.dynamicStateCount = 2;
        dynamicStateCreateInfo.pDynamicStates = dynamicStates;
        
        VkPipelineVertexInputStateCreateInfo vertexInputInfo = new VkPipelineVertexInputStateCreateInfo();
        vertexInputInfo.sType = VkStructureType.StructureTypePipelineVertexInputStateCreateInfo;
        vertexInputInfo.vertexBindingDescriptionCount = 0;
        vertexInputInfo.pVertexAttributeDescriptions = null;
        vertexInputInfo.vertexAttributeDescriptionCount = 0;
        vertexInputInfo.pVertexAttributeDescriptions = null;
        
        VkPipelineInputAssemblyStateCreateInfo inputAssemblyInfo = new VkPipelineInputAssemblyStateCreateInfo();
        inputAssemblyInfo.sType = VkStructureType.StructureTypePipelineInputAssemblyStateCreateInfo;
        inputAssemblyInfo.topology = VkPrimitiveTopology.PrimitiveTopologyTriangleList;
        inputAssemblyInfo.primitiveRestartEnable = 0;
        
        _viewport.x = 0.0f;
        _viewport.y = 0.0f;
        _viewport.width = _swapchainSurfaceExtent.width;
        _viewport.height = _swapchainSurfaceExtent.height;
        _viewport.minDepth = 0.0f;
        _viewport.maxDepth = 1.0f;
        
        _scissorRect.offset = new VkOffset2D(0, 0);
        _scissorRect.extent = _swapchainSurfaceExtent;
        
        VkPipelineViewportStateCreateInfo viewportStateCreateInfo = new VkPipelineViewportStateCreateInfo();
        viewportStateCreateInfo.sType = VkStructureType.StructureTypePipelineViewportStateCreateInfo;
        viewportStateCreateInfo.viewportCount = 1;
        viewportStateCreateInfo.scissorCount = 1;
        
        VkPipelineRasterizationStateCreateInfo rasterizationStateCreateInfo = new VkPipelineRasterizationStateCreateInfo();
        rasterizationStateCreateInfo.sType = VkStructureType.StructureTypePipelineRasterizationStateCreateInfo;
        rasterizationStateCreateInfo.depthClampEnable = 0;
        rasterizationStateCreateInfo.polygonMode = VkPolygonMode.PolygonModeFill;
        rasterizationStateCreateInfo.cullMode = VkCullModeFlagBits.CullModeBackBit;
        rasterizationStateCreateInfo.frontFace = VkFrontFace.FrontFaceClockwise;
        rasterizationStateCreateInfo.depthClampEnable = 0;
        rasterizationStateCreateInfo.depthBiasConstantFactor = 0.0f;
        rasterizationStateCreateInfo.depthBiasClamp = 0.0f;
        rasterizationStateCreateInfo.depthBiasSlopeFactor = 0.0f;
        rasterizationStateCreateInfo.lineWidth = 1.0f;
        
        VkPipelineMultisampleStateCreateInfo multisampleStateCreateInfo = new VkPipelineMultisampleStateCreateInfo();
        multisampleStateCreateInfo.sType = VkStructureType.StructureTypePipelineMultisampleStateCreateInfo;
        multisampleStateCreateInfo.sampleShadingEnable = 0;
        multisampleStateCreateInfo.rasterizationSamples = VkSampleCountFlagBits.SampleCount1Bit;
        multisampleStateCreateInfo.minSampleShading = 1.0f;
        multisampleStateCreateInfo.pSampleMask = null;
        multisampleStateCreateInfo.alphaToCoverageEnable = 0;
        multisampleStateCreateInfo.alphaToOneEnable = 0;
        
        VkPipelineColorBlendAttachmentState colorBlendAttachmentState = new VkPipelineColorBlendAttachmentState();
        colorBlendAttachmentState.colorWriteMask = VkColorComponentFlagBits.ColorComponentRBit |
                                                   VkColorComponentFlagBits.ColorComponentGBit |
                                                   VkColorComponentFlagBits.ColorComponentBBit |
                                                   VkColorComponentFlagBits.ColorComponentABit;
        colorBlendAttachmentState.blendEnable = 1;
        colorBlendAttachmentState.srcColorBlendFactor = VkBlendFactor.BlendFactorSrcAlpha;
        colorBlendAttachmentState.dstColorBlendFactor = VkBlendFactor.BlendFactorOneMinusSrcAlpha;
        colorBlendAttachmentState.colorBlendOp = VkBlendOp.BlendOpAdd;
        colorBlendAttachmentState.srcAlphaBlendFactor = VkBlendFactor.BlendFactorOne;
        colorBlendAttachmentState.dstAlphaBlendFactor = VkBlendFactor.BlendFactorZero;
        colorBlendAttachmentState.alphaBlendOp = VkBlendOp.BlendOpAdd;
        
        VkPipelineColorBlendStateCreateInfo colorblendStateCreateInfo = new VkPipelineColorBlendStateCreateInfo();
        colorblendStateCreateInfo.sType = VkStructureType.StructureTypePipelineColorBlendStateCreateInfo;
        colorblendStateCreateInfo.logicOpEnable = 0;
        colorblendStateCreateInfo.logicOp = VkLogicOp.LogicOpCopy;
        colorblendStateCreateInfo.attachmentCount = 1;
        colorblendStateCreateInfo.pAttachments = &colorBlendAttachmentState;
        colorblendStateCreateInfo.blendConstants[0] = 0.0f;
        colorblendStateCreateInfo.blendConstants[1] = 0.0f;
        colorblendStateCreateInfo.blendConstants[2] = 0.0f;
        colorblendStateCreateInfo.blendConstants[3] = 0.0f;
        
        VkPipelineLayoutCreateInfo layoutCreateInfo = new VkPipelineLayoutCreateInfo();
        layoutCreateInfo.sType = VkStructureType.StructureTypePipelineLayoutCreateInfo;
        layoutCreateInfo.setLayoutCount = 0;
        layoutCreateInfo.pSetLayouts = null;
        layoutCreateInfo.pushConstantRangeCount = 0;
        layoutCreateInfo.pPushConstantRanges = null;
        
        fixed (VkPipelineLayout* pipelineLayoutPtr = &_pipelineLayout) _result = Vk.CreatePipelineLayout(_device, &layoutCreateInfo, null, pipelineLayoutPtr);
        if (_result != VkResult.Success) GameLogger.ThrowError($"Failed to create pipeline layout with message {_result.ToString()}");
        GameLogger.DebugLog("Successfully created pipeline layout.");
        
        VkGraphicsPipelineCreateInfo pipelineCreateInfo = new VkGraphicsPipelineCreateInfo();
        pipelineCreateInfo.sType = VkStructureType.StructureTypeGraphicsPipelineCreateInfo;
        pipelineCreateInfo.stageCount = 2;
        pipelineCreateInfo.pStages = shaderStages;
        pipelineCreateInfo.pVertexInputState = &vertexInputInfo;
        pipelineCreateInfo.pInputAssemblyState = &inputAssemblyInfo;
        pipelineCreateInfo.pViewportState = &viewportStateCreateInfo;
        pipelineCreateInfo.pRasterizationState = &rasterizationStateCreateInfo;
        pipelineCreateInfo.pMultisampleState = &multisampleStateCreateInfo;
        pipelineCreateInfo.pColorBlendState = &colorblendStateCreateInfo;
        pipelineCreateInfo.pDynamicState = &dynamicStateCreateInfo;

        pipelineCreateInfo.layout = _pipelineLayout;
        pipelineCreateInfo.renderPass = _renderPass;
        pipelineCreateInfo.subpass = 0;
        
        fixed (VkPipeline* graphicsPipelinePtr = &_graphicsPipeline) _result = Vk.CreateGraphicsPipelines(_device, VkPipelineCache.Zero, 1, &pipelineCreateInfo, null, graphicsPipelinePtr);
        if (_result != VkResult.Success) GameLogger.ThrowError($"Failed to create graphics pipeline with message {_result.ToString()}");
        GameLogger.DebugLog("Successfully created graphics pipeline.");
        
        // Create the framebuffers.
        /*
        for (int i = 0; i < _swapchainImageViews.Count; i++)
        {
            
            VkFramebufferCreateInfo framebufferCreateInfo = new VkFramebufferCreateInfo();
            framebufferCreateInfo.sType = VkStructureType.StructureTypeFramebufferCreateInfo;
            framebufferCreateInfo.renderPass = _renderPass;
            framebufferCreateInfo.attachmentCount = 1;
            VkImageView attachments = _swapchainImageViews[i];
            framebufferCreateInfo.pAttachments = &attachments;
            framebufferCreateInfo.width = _swapchainSurfaceExtent.width;
            framebufferCreateInfo.height = _swapchainSurfaceExtent.height;
            framebufferCreateInfo.layers = 1;

            VkFramebuffer frameBuffer;
            _result = Vk.CreateFramebuffer(_device, &framebufferCreateInfo, null, &frameBuffer);
            if (_result != VkResult.Success) GameLogger.ThrowError($"Failed to create framebuffer with message {_result.ToString()}");
            _swapchainFramebuffers.Add(frameBuffer);
            GameLogger.DebugLog("Successfully created framebuffer.");
            
        }
        */
        
        // Create the command pool.
        VkCommandPoolCreateInfo commandPoolCreateInfo = new VkCommandPoolCreateInfo();
        commandPoolCreateInfo.sType = VkStructureType.StructureTypeCommandPoolCreateInfo;
        commandPoolCreateInfo.flags = VkCommandPoolCreateFlagBits.CommandPoolCreateResetCommandBufferBit;
        commandPoolCreateInfo.queueFamilyIndex = _graphicsFamilyIndex.Value;
        
        fixed (VkCommandPool* commandPoolPtr = &_commandPool) _result = Vk.CreateCommandPool(_device, &commandPoolCreateInfo, null, commandPoolPtr);
        if (_result != VkResult.Success) GameLogger.ThrowError($"Failed to create the command pool with message {_result.ToString()}");
        GameLogger.DebugLog("Successfully created the command pool.");
        
        // Create the command buffer.
        VkCommandBufferAllocateInfo commandBufferAllocateInfo = new VkCommandBufferAllocateInfo();
        commandBufferAllocateInfo.sType = VkStructureType.StructureTypeCommandBufferAllocateInfo;
        commandBufferAllocateInfo.commandPool = _commandPool;
        commandBufferAllocateInfo.level = VkCommandBufferLevel.CommandBufferLevelPrimary;
        commandBufferAllocateInfo.commandBufferCount = 1;
        
        fixed (VkCommandBuffer* commandBufferPtr = &_commandBuffer) _result = Vk.AllocateCommandBuffers(_device, &commandBufferAllocateInfo, commandBufferPtr);
        if (_result != VkResult.Success) GameLogger.ThrowError($"Failed to create the command buffer with message {_result.ToString()}");
        GameLogger.DebugLog("Successfully created the command buffer.");
        
        // Create sync objects.
        VkSemaphoreCreateInfo semaphoreCreateInfo = new VkSemaphoreCreateInfo();
        semaphoreCreateInfo.sType = VkStructureType.StructureTypeSemaphoreCreateInfo;
        
        VkFenceCreateInfo fenceCreateInfo = new VkFenceCreateInfo();
        fenceCreateInfo.sType = VkStructureType.StructureTypeFenceCreateInfo;
        fenceCreateInfo.flags = VkFenceCreateFlagBits.FenceCreateSignaledBit;
        
        fixed (VkSemaphore* imageAvailableSemaphorePtr = &_imageAvailableSemaphore) _result = Vk.CreateSemaphore(_device, &semaphoreCreateInfo, null, imageAvailableSemaphorePtr);
        if (_result != VkResult.Success) GameLogger.ThrowError("Failed to create semaphore.");
        fixed (VkSemaphore* renderFinishedSemaphorePtr = &_renderFinishedSemaphore) _result = Vk.CreateSemaphore(_device, &semaphoreCreateInfo, null, renderFinishedSemaphorePtr);
        if (_result != VkResult.Success) GameLogger.ThrowError("Failed to create semaphore.");
        fixed (VkFence* inFlightFencePtr = &_inFlightFence) _result = Vk.CreateFence(_device, &fenceCreateInfo, null, inFlightFencePtr);
        if (_result != VkResult.Success) GameLogger.ThrowError("Failed to create semaphore.");
        
        GameLogger.DebugLog("Successfully created semaphores.");
        
    }
    
}