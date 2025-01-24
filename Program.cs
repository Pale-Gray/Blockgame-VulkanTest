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
    static unsafe void Main(string[] args)
    {
        
        VKLoader.Init();
        VkInstance instance;
        VkPhysicalDevice physicalDevice = VkPhysicalDevice.Zero;
        VkDevice device;
        VkResult result;
        
        VkQueue graphicsQueue;
        VkQueue presentQueue;

        VkSwapchainKHR swapchain;
        List<VkImage> swapchainImages = new();
        List<VkImageView> swapchainImageViews = new();
        
        VkRenderPass renderPass;
        VkPipelineLayout pipelineLayout;
        VkPipeline graphicsPipeline;
        
        VkCommandPool commandPool;
        VkCommandBuffer commandBuffer;
        
        VkSemaphore imageAvailableSemaphore;
        VkSemaphore renderFinishedSemaphore;
        VkFence inFlightFence;  
        
        ConsoleLogger consoleLogger = new ConsoleLogger();
        consoleLogger.Filter = LogLevel.Info;
        
        ToolkitOptions options = new ToolkitOptions();
        options.ApplicationName = "VulkanTest";
        options.Logger = null;
        
        Toolkit.Init(options);
        
        VulkanGraphicsApiHints contextSettings = new VulkanGraphicsApiHints();
        WindowHandle window = Toolkit.Window.Create(contextSettings);
        
        Toolkit.Window.SetTitle(window, "Vulkan Testing");
        Toolkit.Window.SetSize(window, (800, 600));
        Toolkit.Window.SetMode(window, WindowMode.Normal);

        ReadOnlySpan<byte> iconData = [255, 255, 255, 255, 0, 0, 0, 255, 0, 0, 0, 255, 255, 255, 255, 255];
        IconHandle icon = Toolkit.Icon.Create(2, 2, iconData);
        Toolkit.Window.SetIcon(window, icon);

        EventQueue.EventRaised += EventRaised;
        
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
        
        result = Vk.CreateInstance(&vkInstanceCreateInfo, null, &instance);
        if (result != VkResult.Success) GameLogger.ThrowError($"Failed to create a Vulkan instance with error {result.ToString()}");
        GameLogger.DebugLog("Created the Vulkan instance.");
        
        VKLoader.SetInstance(instance);
        
        // Create the window surface.
        result = Toolkit.Vulkan.CreateWindowSurface(instance, window, null, out VkSurfaceKHR surface);
        if (result != VkResult.Success) GameLogger.ThrowError($"Failed to create a window surface with error {result.ToString()}");
        
        // Create and select a physical device; your GPU.
        uint physicalDeviceCount = 0;
        Vk.EnumeratePhysicalDevices(instance, &physicalDeviceCount, null);
        if (physicalDeviceCount == 0) GameLogger.ThrowError("There are no devices that support Vulkan.");
        VkPhysicalDevice* physicalDevices = stackalloc VkPhysicalDevice[(int)physicalDeviceCount];
        Vk.EnumeratePhysicalDevices(instance, &physicalDeviceCount, physicalDevices);
        
        // Check if the devices are suitable for our use case.
        // We can have a more complex method of determining
        // what our physical device will be, based on parameters
        // like if it is integrated or not, but for now we will
        // just pick the very first one that supports our needs.
        physicalDevice = physicalDevices[0];

        uint extensionCount = 0;
        Vk.EnumerateDeviceExtensionProperties(physicalDevice, null, &extensionCount, null);
        VkExtensionProperties* availableExtensions = stackalloc VkExtensionProperties[(int)extensionCount];
        Vk.EnumerateDeviceExtensionProperties(physicalDevice, null, &extensionCount, availableExtensions);

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
        uint? graphicsFamilyIndex = null;
        uint? presentFamilyIndex = null;

        uint queueFamilyCount = 0;
        Vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, &queueFamilyCount, null);
        VkQueueFamilyProperties* queueFamilyProperties = stackalloc VkQueueFamilyProperties[(int)queueFamilyCount];
        Vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, &queueFamilyCount, queueFamilyProperties);
        for (uint i = 0; i < queueFamilyCount; i++)
        {

            int presentSupport = 0;
            Vk.GetPhysicalDeviceSurfaceSupportKHR(physicalDevice, i, surface, &presentSupport);
            if (presentSupport == 1) presentFamilyIndex = i;
            
            if (((uint)queueFamilyProperties[i].queueFlags & (uint)VkQueueFlagBits.QueueGraphicsBit) == 1)
            {
                graphicsFamilyIndex = i;
            }

        }
        
        if (graphicsFamilyIndex == null) GameLogger.ThrowError("No graphics family could be found.");
        
        // Stuff for creating the logical device.
        
        // Determine the amount of queues per queue family.
        List<uint> uniqueQueueFamilies = [ graphicsFamilyIndex.Value, presentFamilyIndex.Value ];

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
        result = Vk.CreateDevice(physicalDevice, &deviceCreateInfo, null, &device);
        if (result != VkResult.Success) GameLogger.ThrowError($"The device couldn't be created with message {result.ToString()}");
        GameLogger.DebugLog("Created the logical device.");
        
        Vk.GetDeviceQueue(device, graphicsFamilyIndex.Value, 0, &graphicsQueue);
        Vk.GetDeviceQueue(device, presentFamilyIndex.Value, 0, &presentQueue);
        
        // Get details of swapchain support
        VkSurfaceCapabilitiesKHR swapchainCapabilities = new VkSurfaceCapabilitiesKHR();
        List<VkSurfaceFormatKHR> swapchainSupportedFormats = new();
        List<VkPresentModeKHR> swapchainPresentModesSupported = new();
        
        // Get values pertaining to swapchain support.
        // Console.WriteLine("hi");
        Vk.GetPhysicalDeviceSurfaceCapabilitiesKHR(physicalDevice, surface, &swapchainCapabilities);

        uint formatCount = 0;
        Vk.GetPhysicalDeviceSurfaceFormatsKHR(physicalDevice, surface, &formatCount, null);
        if (formatCount != 0)
        {
            VkSurfaceFormatKHR* surfaceSupportedFormatsPtr = stackalloc VkSurfaceFormatKHR[(int)formatCount];
            Vk.GetPhysicalDeviceSurfaceFormatsKHR(physicalDevice, surface, &formatCount, surfaceSupportedFormatsPtr);
            for (int i = 0; i < formatCount; i++)
            {
                swapchainSupportedFormats.Add(surfaceSupportedFormatsPtr[i]);
            }
        }

        uint presentModeCount = 0;
        Vk.GetPhysicalDeviceSurfacePresentModesKHR(physicalDevice, surface, &presentModeCount, null);
        if (presentModeCount != 0)
        {
            VkPresentModeKHR* surfaceSupportedPresentModesPtr = stackalloc VkPresentModeKHR[(int)presentModeCount];
            Vk.GetPhysicalDeviceSurfacePresentModesKHR(physicalDevice, surface, &presentModeCount, surfaceSupportedPresentModesPtr);
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
        VkExtent2D swapchainSurfaceExtent;

        Toolkit.Window.GetFramebufferSize(window, out Vector2i framebufferSize);
        swapchainSurfaceExtent.width = (uint) Math.Clamp(framebufferSize.X, swapchainCapabilities.minImageExtent.width, swapchainCapabilities.maxImageExtent.width);
        swapchainSurfaceExtent.height = (uint) Math.Clamp(framebufferSize.Y, swapchainCapabilities.minImageExtent.height, swapchainCapabilities.maxImageExtent.height);

        uint imageCount = swapchainCapabilities.minImageCount + 1;
        if (swapchainCapabilities.maxImageCount > 0 && imageCount > swapchainCapabilities.maxImageCount) imageCount = swapchainCapabilities.maxImageCount;

        VkSwapchainCreateInfoKHR swapchainCreateInfo = new VkSwapchainCreateInfoKHR();
        swapchainCreateInfo.sType = VkStructureType.StructureTypeSwapchainCreateInfoKhr;
        swapchainCreateInfo.surface = surface;
        swapchainCreateInfo.minImageCount = imageCount;
        swapchainCreateInfo.imageFormat = swapchainSurfaceFormat.format;
        swapchainCreateInfo.imageColorSpace = swapchainSurfaceFormat.colorSpace;
        swapchainCreateInfo.imageExtent = swapchainSurfaceExtent;
        swapchainCreateInfo.imageArrayLayers = 1;
        swapchainCreateInfo.imageUsage = VkImageUsageFlagBits.ImageUsageColorAttachmentBit;
        
        uint* queueFamilyIndices = stackalloc uint[] { graphicsFamilyIndex.Value, presentFamilyIndex.Value };
        if (graphicsFamilyIndex.Value != presentFamilyIndex.Value)
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
        
        result = Vk.CreateSwapchainKHR(device, &swapchainCreateInfo, null, &swapchain);
        if (result != VkResult.Success) GameLogger.ThrowError($"Failed to create swapchain with message {result.ToString()}");
        GameLogger.DebugLog("Successfully created the swapchain.");
        
        uint swapchainImageCount = 0;
        Vk.GetSwapchainImagesKHR(device, swapchain, &swapchainImageCount, null);
        VkImage* swapchainImagesPtr = stackalloc VkImage[(int)swapchainImageCount];
        Vk.GetSwapchainImagesKHR(device, swapchain, &swapchainImageCount, swapchainImagesPtr);
        for (int i = 0; i < swapchainImageCount; i++)
        {
            swapchainImages.Add(swapchainImagesPtr[i]);
        }

        // Create swapchain image views.
        VkImageView* swapchainImageViewsPtr = stackalloc VkImageView[swapchainImages.Count];
        for (int i = 0; i < swapchainImages.Count; i++)
        {
            
            VkImageViewCreateInfo imageViewCreateInfo = new VkImageViewCreateInfo();
            imageViewCreateInfo.sType = VkStructureType.StructureTypeImageViewCreateInfo;
            imageViewCreateInfo.image = swapchainImages[i];
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
            
            result = Vk.CreateImageView(device, &imageViewCreateInfo, null, &swapchainImageViewsPtr[i]);    
            if (result != VkResult.Success) GameLogger.ThrowError($"Failed to create image view with message {result.ToString()}");
            GameLogger.DebugLog($"Successfully created image view.");
            
        }
        
        for (int i = 0; i < swapchainImages.Count; i++)
        {
            swapchainImageViews.Add(swapchainImageViewsPtr[i]);
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
        
        result = Vk.CreateRenderPass(device, &renderPassCreateInfo, null, &renderPass);
        if (result != VkResult.Success) GameLogger.ThrowError($"Failed to create render pass with message {result.ToString()}");
        GameLogger.DebugLog($"Successfully created render pass.");
        
        // Create the graphics pipeline.
        byte[] vertShader = File.ReadAllBytes("vert.spv");
        byte[] fragShader = File.ReadAllBytes("frag.spv");
        
        VkShaderModuleCreateInfo vertexShaderModuleCreateInfo = new VkShaderModuleCreateInfo();
        vertexShaderModuleCreateInfo.sType = VkStructureType.StructureTypeShaderModuleCreateInfo;
        vertexShaderModuleCreateInfo.codeSize = (UIntPtr) vertShader.Length;
        fixed (byte* vertShaderPtr = vertShader) vertexShaderModuleCreateInfo.pCode = (uint*)vertShaderPtr;

        VkShaderModule vertexShaderModule;
        result = Vk.CreateShaderModule(device, &vertexShaderModuleCreateInfo, null, &vertexShaderModule);
        if (result != VkResult.Success) GameLogger.ThrowError($"Failed to create shader module with message {result.ToString()}");
        
        VkShaderModuleCreateInfo fragmentShaderModuleCreateInfo = new VkShaderModuleCreateInfo();
        fragmentShaderModuleCreateInfo.sType = VkStructureType.StructureTypeShaderModuleCreateInfo;
        fragmentShaderModuleCreateInfo.codeSize = (UIntPtr) fragShader.Length;
        fixed (byte* fragShaderPtr = fragShader) fragmentShaderModuleCreateInfo.pCode = (uint*)fragShaderPtr;
        
        VkShaderModule fragmentShaderModule;
        result = Vk.CreateShaderModule(device, &fragmentShaderModuleCreateInfo, null, &fragmentShaderModule);
        if (result != VkResult.Success) GameLogger.ThrowError($"Failed to create shader module with message {result.ToString()}");
        
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
        
        VkViewport viewport = new VkViewport();
        viewport.x = 0.0f;
        viewport.y = 0.0f;
        viewport.width = swapchainSurfaceExtent.width;
        viewport.height = swapchainSurfaceExtent.height;
        viewport.minDepth = 0.0f;
        viewport.maxDepth = 1.0f;
        
        VkRect2D scissorRect = new VkRect2D();
        scissorRect.offset = new VkOffset2D(0, 0);
        scissorRect.extent = swapchainSurfaceExtent;
        
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
        
        result = Vk.CreatePipelineLayout(device, &layoutCreateInfo, null, &pipelineLayout);
        if (result != VkResult.Success) GameLogger.ThrowError($"Failed to create pipeline layout with message {result.ToString()}");
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

        pipelineCreateInfo.layout = pipelineLayout;
        pipelineCreateInfo.renderPass = renderPass;
        pipelineCreateInfo.subpass = 0;
        
        result = Vk.CreateGraphicsPipelines(device, VkPipelineCache.Zero, 1, &pipelineCreateInfo, null, &graphicsPipeline);
        if (result != VkResult.Success) GameLogger.ThrowError($"Failed to create graphics pipeline with message {result.ToString()}");
        GameLogger.DebugLog("Successfully created graphics pipeline.");
        
        // Create the framebuffers.
        List<VkFramebuffer> swapchainFramebuffers = new();
        for (int i = 0; i < swapchainImageViews.Count; i++)
        {
            
            VkFramebufferCreateInfo framebufferCreateInfo = new VkFramebufferCreateInfo();
            framebufferCreateInfo.sType = VkStructureType.StructureTypeFramebufferCreateInfo;
            framebufferCreateInfo.renderPass = renderPass;
            framebufferCreateInfo.attachmentCount = 1;
            VkImageView attachments = swapchainImageViews[i];
            framebufferCreateInfo.pAttachments = &attachments;
            framebufferCreateInfo.width = swapchainSurfaceExtent.width;
            framebufferCreateInfo.height = swapchainSurfaceExtent.height;
            framebufferCreateInfo.layers = 1;

            VkFramebuffer frameBuffer;
            result = Vk.CreateFramebuffer(device, &framebufferCreateInfo, null, &frameBuffer);
            if (result != VkResult.Success) GameLogger.ThrowError($"Failed to create framebuffer with message {result.ToString()}");
            swapchainFramebuffers.Add(frameBuffer);
            GameLogger.DebugLog("Successfully created framebuffer.");
            
        }
        
        // Create the command pool.
        VkCommandPoolCreateInfo commandPoolCreateInfo = new VkCommandPoolCreateInfo();
        commandPoolCreateInfo.sType = VkStructureType.StructureTypeCommandPoolCreateInfo;
        commandPoolCreateInfo.flags = VkCommandPoolCreateFlagBits.CommandPoolCreateResetCommandBufferBit;
        commandPoolCreateInfo.queueFamilyIndex = graphicsFamilyIndex.Value;
        
        result = Vk.CreateCommandPool(device, &commandPoolCreateInfo, null, &commandPool);
        if (result != VkResult.Success) GameLogger.ThrowError($"Failed to create the command pool with message {result.ToString()}");
        GameLogger.DebugLog("Successfully created the command pool.");
        
        // Create the command buffer.
        VkCommandBufferAllocateInfo commandBufferAllocateInfo = new VkCommandBufferAllocateInfo();
        commandBufferAllocateInfo.sType = VkStructureType.StructureTypeCommandBufferAllocateInfo;
        commandBufferAllocateInfo.commandPool = commandPool;
        commandBufferAllocateInfo.level = VkCommandBufferLevel.CommandBufferLevelPrimary;
        commandBufferAllocateInfo.commandBufferCount = 1;
        
        result = Vk.AllocateCommandBuffers(device, &commandBufferAllocateInfo, &commandBuffer);
        if (result != VkResult.Success) GameLogger.ThrowError($"Failed to create the command buffer with message {result.ToString()}");
        GameLogger.DebugLog("Successfully created the command buffer.");
        
        // Create sync objects.
        VkSemaphoreCreateInfo semaphoreCreateInfo = new VkSemaphoreCreateInfo();
        semaphoreCreateInfo.sType = VkStructureType.StructureTypeSemaphoreCreateInfo;
        
        VkFenceCreateInfo fenceCreateInfo = new VkFenceCreateInfo();
        fenceCreateInfo.sType = VkStructureType.StructureTypeFenceCreateInfo;
        fenceCreateInfo.flags = VkFenceCreateFlagBits.FenceCreateSignaledBit;
        
        result = Vk.CreateSemaphore(device, &semaphoreCreateInfo, null, &imageAvailableSemaphore);
        if (result != VkResult.Success) GameLogger.ThrowError("Failed to create semaphore.");
        result = Vk.CreateSemaphore(device, &semaphoreCreateInfo, null, &renderFinishedSemaphore);
        if (result != VkResult.Success) GameLogger.ThrowError("Failed to create semaphore.");
        result = Vk.CreateFence(device, &fenceCreateInfo, null, &inFlightFence);
        if (result != VkResult.Success) GameLogger.ThrowError("Failed to create semaphore.");
        
        GameLogger.DebugLog("Successfully created semaphores.");
        /*
        // Handle command buffer recording.
        VkCommandBufferBeginInfo commandBufferBeginInfo = new VkCommandBufferBeginInfo();
        commandBufferBeginInfo.sType = VkStructureType.StructureTypeCommandBufferBeginInfo;
        commandBufferBeginInfo.flags = 0;
        commandBufferBeginInfo.pInheritanceInfo = null;
        
        result = Vk.BeginCommandBuffer(commandBuffer, &commandBufferBeginInfo);
        if (result != VkResult.Success) GameLogger.ThrowError($"Failed to begin recoding command buffer with message {result.ToString()}");
        GameLogger.DebugLog("Successfully recording the command buffer.");
        
        VkRenderPassBeginInfo renderPassBeginInfo = new VkRenderPassBeginInfo();
        renderPassBeginInfo.sType = VkStructureType.StructureTypeRenderPassBeginInfo;
        renderPassBeginInfo.renderPass = renderPass;
        renderPassBeginInfo.framebuffer = swapchainFramebuffers[0];
        renderPassBeginInfo.renderArea.offset = new VkOffset2D(0, 0);
        renderPassBeginInfo.renderArea.extent = swapchainSurfaceExtent;

        VkClearValue clearColor = new VkClearValue();
        renderPassBeginInfo.clearValueCount = 1;
        renderPassBeginInfo.pClearValues = &clearColor;
        
        // Vk.CmdBeginRenderPass(commandBuffer, &renderPassBeginInfo, VkSubpassContents.SubpassContentsInline);
        // Vk.CmdBindPipeline(commandBuffer, VkPipelineBindPoint.PipelineBindPointGraphics, graphicsPipeline);
        // Vk.CmdDraw(commandBuffer, 3, 1, 0, 0);
        // Vk.CmdEndRenderPass(commandBuffer);
        */
        
        while (true)
        {

            Toolkit.Window.ProcessEvents(false);
            if (Toolkit.Window.IsWindowDestroyed(window))
            {
                Vk.DeviceWaitIdle(device);
                foreach (VkFramebuffer framebuffer in swapchainFramebuffers)
                {
                    Vk.DestroyFramebuffer(device, framebuffer, null);    
                }
                foreach (VkImageView imageView in swapchainImageViews)
                {
                    Vk.DestroyImageView(device, imageView, null);
                }
                Vk.DestroySwapchainKHR(device, swapchain, null);
                Vk.DestroySurfaceKHR(instance, surface, null);    
                Vk.DestroyInstance(instance, null);
                Vk.DestroyShaderModule(device, vertexShaderModule, null);
                Vk.DestroyShaderModule(device, fragmentShaderModule, null);
                Vk.DestroyPipeline(device, graphicsPipeline, null);
                Vk.DestroyPipelineLayout(device, pipelineLayout, null);
                Vk.DestroyRenderPass(device, renderPass, null);
                Vk.DestroyCommandPool(device, commandPool, null);
                Vk.DestroySemaphore(device, imageAvailableSemaphore, null);
                Vk.DestroySemaphore(device, renderFinishedSemaphore, null);
                Vk.DestroyFence(device, inFlightFence, null);
                Vk.DestroyDevice(device, null);
                break;

            }
            
            Vk.WaitForFences(device, 1, &inFlightFence, 1, ulong.MaxValue);
            Vk.ResetFences(device, 1, &inFlightFence);
            
            uint imageIndex;
            result = Vk.AcquireNextImageKHR(device, swapchain, ulong.MaxValue, imageAvailableSemaphore, VkFence.Zero, &imageIndex);
            if (result != VkResult.Success) GameLogger.ThrowError($"Failed to acquire next image with message {result.ToString()}");
        
            result = Vk.ResetCommandBuffer(commandBuffer, 0);
            if (result != VkResult.Success) GameLogger.ThrowError($"Failed to reset the command buffer with message {result.ToString()}");
    
            VkCommandBufferBeginInfo commandBufferBeginInfo = new VkCommandBufferBeginInfo();
            commandBufferBeginInfo.sType = VkStructureType.StructureTypeCommandBufferBeginInfo;
            commandBufferBeginInfo.pNext = null;
            commandBufferBeginInfo.flags = 0;
            commandBufferBeginInfo.pInheritanceInfo = null;
        
            result = Vk.BeginCommandBuffer(commandBuffer, &commandBufferBeginInfo);
            if (result != VkResult.Success) GameLogger.ThrowError("Failed to begin recording the command buffer.");

            VkRenderPassBeginInfo renderPassBeginInfo = new VkRenderPassBeginInfo();
            renderPassBeginInfo.sType = VkStructureType.StructureTypeRenderPassBeginInfo;
            renderPassBeginInfo.renderPass = renderPass;
            renderPassBeginInfo.framebuffer = swapchainFramebuffers[(int)imageIndex];
            renderPassBeginInfo.renderArea.offset = new VkOffset2D(0, 0);
            renderPassBeginInfo.renderArea.extent = swapchainSurfaceExtent;
            
            VkClearValue clearColor = default;
            clearColor.color.float32[0] = 0.0f;
            clearColor.color.float32[1] = 0.0f;
            clearColor.color.float32[2] = 0.0f;
            clearColor.color.float32[3] = 1.0f;
            renderPassBeginInfo.clearValueCount = 1;
            renderPassBeginInfo.pClearValues = &clearColor;
            
            Vk.CmdBeginRenderPass(commandBuffer, &renderPassBeginInfo, VkSubpassContents.SubpassContentsInline);
            Vk.CmdBindPipeline(commandBuffer, VkPipelineBindPoint.PipelineBindPointGraphics, graphicsPipeline);
            
            Vk.CmdSetViewport(commandBuffer, 0, 1, &viewport);
            Vk.CmdSetScissor(commandBuffer, 0, 1, &scissorRect);
            
            Vk.CmdDraw(commandBuffer, 6, 1, 0, 0);
            
            Vk.CmdEndRenderPass(commandBuffer);
            result = Vk.EndCommandBuffer(commandBuffer);
            if (result != VkResult.Success) GameLogger.ThrowError("Failed to record the command buffer.");
            
            VkSubmitInfo submitInfo = new VkSubmitInfo();
            submitInfo.sType = VkStructureType.StructureTypeSubmitInfo;

            submitInfo.waitSemaphoreCount = 1;
            submitInfo.pWaitSemaphores = &imageAvailableSemaphore;
            VkPipelineStageFlagBits bits = VkPipelineStageFlagBits.PipelineStageColorAttachmentOutputBit;
            submitInfo.pWaitDstStageMask = &bits;

            submitInfo.commandBufferCount = 1;
            submitInfo.pCommandBuffers = &commandBuffer;

            submitInfo.signalSemaphoreCount = 1;
            submitInfo.pSignalSemaphores = &renderFinishedSemaphore;
            
            result = Vk.QueueSubmit(graphicsQueue, 1, &submitInfo, inFlightFence);
            if (result != VkResult.Success) GameLogger.ThrowError($"Failed to submit the command buffer with message {result.ToString()}"); 
            
            VkPresentInfoKHR presentInfo = new VkPresentInfoKHR();
            presentInfo.sType = VkStructureType.StructureTypePresentInfoKhr;
            presentInfo.waitSemaphoreCount = 1;
            presentInfo.pWaitSemaphores = &renderFinishedSemaphore;
            
            presentInfo.swapchainCount = 1;
            presentInfo.pSwapchains = &swapchain;
            presentInfo.pImageIndices = &imageIndex;

            presentInfo.pResults = null;
            Vk.QueuePresentKHR(presentQueue, &presentInfo);
            
        }

        // Console.WriteLine("I should exit");
        Vk.DeviceWaitIdle(device);

    }

    static void EventRaised(PalHandle? handle, PlatformEventType type, EventArgs args)
    {

        switch (args)
        {
            
            case CloseEventArgs close:
                Toolkit.Window.Destroy(close.Window);
                break;
            
        }
        
    }
    
}